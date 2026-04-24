using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// GDD §4.9 — ピン（マーカー）システム。
///
/// F キーで 3 種のピンを設置。生存者・幽霊ともに使用可能。
///   🔴 危険（赤）/ 🔵 遺物（青）/ 🟡 ルート（黄）
///
/// 生存者: 最大 3 個同時設置。幽霊: 最大 5 個（GhostSystem と連携）。
/// 60 秒で自動消滅。Fキー長押し（2 秒）で最古のピンを手動削除。
/// ネットワーク同期: ServerRpc → ClientsAndHost でワールド空間に 3D アイコンを生成。
///
/// 注意: GhostSystem の既存ピン機能と競合しないよう、
/// GhostSystem 側の PlacePin() は廃止し本システムに一本化することを推奨。
/// </summary>
public class PinSystem : NetworkBehaviour
{
    // ── Shader プロパティ ID キャッシュ（文字列ルックアップを排除）──
    private static readonly int BaseColorPropId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropId     = Shader.PropertyToID("_Color");

    // ── 共有マテリアル（Shader.Find はアプリ起動時に1回だけ）────
    private static Material s_pinSharedMaterial;

    // ── 全アクティブピンのクライアント側レジストリ（HUD ミニコンパス用）──
    // クライアントごとのローカル追跡。SpawnPinClientRpc で追加・寿命切れで除去。
    private static readonly List<(Transform transform, PinType type)> s_activePins = new();

    /// <summary>
    /// クライアント側で認識している全アクティブピン（HUD ミニコンパス等の表示用）。
    /// 外部コードは読み取り専用として扱うこと（変更禁止）。
    /// </summary>
    public static IReadOnlyList<(Transform transform, PinType type)> ActivePins => s_activePins;

    // ── GDD 定数 ─────────────────────────────────────────────
    private const float PIN_LIFETIME       = 60f;    // 秒
    private const float DELETE_HOLD_TIME   = 2f;     // 長押し削除
    private const float MAX_PLACE_DISTANCE = 100f;   // 最大設置距離（m）
    private const KeyCode PIN_KEY          = KeyCode.F;

    private static readonly int SURVIVOR_PIN_LIMIT = 3;
    private static readonly int GHOST_PIN_LIMIT    = 5;

    // ── ピン種別 ─────────────────────────────────────────────
    public enum PinType { Danger = 0, Relic = 1, Route = 2 }

    private static readonly Color[] PIN_COLORS = {
        Color.red,
        new Color(0.1f, 0.4f, 1f),  // 青
        new Color(1f, 0.85f, 0f)    // 黄
    };

    private static readonly string[] PIN_LABELS = { "危険", "遺物", "ルート" };

    // ── Inspector ─────────────────────────────────────────────
    [Header("参照")]
    [SerializeField] private Transform _cameraTransform;

    [Header("ホイール UI（任意）")]
    [SerializeField] private GameObject _wheelPanel;

    // ── コンポーネント ─────────────────────────────────────────
    private PlayerHealthSystem _health;
    private GhostSystem        _ghost;

    // ── ローカル状態 ─────────────────────────────────────────
    private PinType _selectedType   = PinType.Danger;
    private bool    _wheelOpen;
    private float   _holdTimer;
    // 長押し削除が発火したら、このホールド中は KeyUp で設置しないためのガード。
    private bool    _deleteFiredThisHold;
    private readonly List<PinRecord> _myPins = new();  // 自分が設置したピン

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _health = GetComponent<PlayerHealthSystem>();
        _ghost  = GetComponent<GhostSystem>();
        if (_cameraTransform == null)
            _cameraTransform = GetComponentInChildren<Camera>()?.transform ?? transform;
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (_health != null && _health.IsDead && (_ghost == null || !_ghost.IsGhost)) return;

        HandlePinInput();
    }

    // ── 入力処理 ─────────────────────────────────────────────
    private void HandlePinInput()
    {
        // 新たなホールド開始でガードをリセット
        if (Input.GetKeyDown(PIN_KEY))
            _deleteFiredThisHold = false;

        // F キー長押し → 最古のピンを削除（ホールド中に 1 回だけ）
        if (Input.GetKey(PIN_KEY))
        {
            _holdTimer += Time.deltaTime;
            if (!_deleteFiredThisHold && _holdTimer >= DELETE_HOLD_TIME)
            {
                DeleteOldestPin();
                _deleteFiredThisHold = true;
                _holdTimer           = 0f;
            }
        }

        if (Input.GetKeyUp(PIN_KEY))
        {
            // 短押し: 即時設置（現在の選択種で設置）。
            // ただしこのホールドで削除が既に発火していれば、リセット直後の _holdTimer が
            // 0.3s 未満の状態で拾われて誤設置してしまうのでガードする。
            if (!_deleteFiredThisHold && _holdTimer < 0.3f)
                TryPlacePin(_selectedType);
            _holdTimer           = 0f;
            _deleteFiredThisHold = false;
        }

        // 数字キー 1-3 でピン種別を切替
        if (Input.GetKeyDown(KeyCode.Alpha1)) _selectedType = PinType.Danger;
        if (Input.GetKeyDown(KeyCode.Alpha2)) _selectedType = PinType.Relic;
        if (Input.GetKeyDown(KeyCode.Alpha3)) _selectedType = PinType.Route;
    }

    // ── ピン設置 ─────────────────────────────────────────────
    public void TryPlacePin(PinType type)
    {
        int limit = (_ghost != null && _ghost.IsGhost) ? GHOST_PIN_LIMIT : SURVIVOR_PIN_LIMIT;
        CleanExpiredPins();

        if (_myPins.Count >= limit)
        {
            Debug.Log($"[PinSystem] 設置上限 ({limit} 個) に達しています");
            return;
        }

        // Raycast でワールド座標を決定
        Vector3 pinPos;
        if (_cameraTransform != null &&
            Physics.Raycast(_cameraTransform.position, _cameraTransform.forward,
                            out RaycastHit hit, MAX_PLACE_DISTANCE))
        {
            pinPos = hit.point + Vector3.up * 0.3f;
        }
        else
        {
            pinPos = transform.position + transform.forward * 5f;
        }

        // ServerRpc 経由で全クライアントにスポーン
        PlacePinServerRpc(pinPos, (byte)type, OwnerClientId);

        // スコア記録（GameServices 経由でシングルトン直結を回避）
        if (_ghost != null && _ghost.IsGhost)
            GameServices.Score?.RecordGhostPin((int)OwnerClientId);

        Debug.Log($"[PinSystem] ピン設置: {PIN_LABELS[(int)type]} @ {pinPos}");
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void PlacePinServerRpc(Vector3 position, byte typeIndex, ulong ownerClientId)
    {
        SpawnPinClientRpc(position, typeIndex, ownerClientId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void SpawnPinClientRpc(Vector3 position, byte typeIndex, ulong ownerClientId)
    {
        PinType type  = (PinType)typeIndex;
        Color   color = PIN_COLORS[typeIndex];
        string  label = PIN_LABELS[typeIndex];

        var go = CreatePinVisual(position, color, label);

        // 自分のピンとして記録（設置者のみ）
        if (IsOwner && OwnerClientId == ownerClientId)
            _myPins.Add(new PinRecord(go, Time.time));

        // グローバルレジストリに追加（ミニコンパス等の HUD 用）
        s_activePins.Add((go.transform, type));

        StartCoroutine(DestroyAfter(go, PIN_LIFETIME));
    }

    // ── ピン最古削除 ─────────────────────────────────────────
    private void DeleteOldestPin()
    {
        CleanExpiredPins();
        if (_myPins.Count == 0) return;

        // 最古（先頭）のピンを削除
        var oldest = _myPins[0].pinObject;
        if (oldest != null)
        {
            var t = oldest.transform;
            s_activePins.RemoveAll(p => p.transform == t);
            Destroy(oldest);
            Debug.Log("[PinSystem] 最古のピンを削除");
        }
        _myPins.RemoveAt(0);
    }

    // ── ピン生成 ─────────────────────────────────────────────
    private static GameObject CreatePinVisual(Vector3 position, Color color, string label)
    {
        var root = new GameObject($"Pin_{label}");
        root.transform.position = position;

        // 柱（Cylinder）
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.transform.SetParent(root.transform);
        cylinder.transform.localPosition = new Vector3(0f, 0.5f, 0f);
        cylinder.transform.localScale    = new Vector3(0.08f, 0.5f, 0.08f);
        Object.Destroy(cylinder.GetComponent<Collider>());
        SetColor(cylinder, color);

        // 頂点の球（ピンヘッド）
        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.SetParent(root.transform);
        head.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        head.transform.localScale    = Vector3.one * 0.25f;
        Object.Destroy(head.GetComponent<Collider>());
        SetColor(head, color);

        // GDD §4.9 — 距離テキスト（ローカルカメラからの距離を常時ビルボード表示）
        var labelGo = new GameObject("PinLabel");
        labelGo.transform.SetParent(root.transform);
        labelGo.transform.localPosition = new Vector3(0f, 1.55f, 0f);
        var tmp = labelGo.AddComponent<TextMeshPro>();
        tmp.text             = label;
        tmp.fontSize         = 3f;
        tmp.color            = color;
        tmp.alignment        = TextAlignmentOptions.Center;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        labelGo.transform.localScale = Vector3.one * 0.5f;
        labelGo.AddComponent<PinLabelBillboard>().Setup(tmp, label);

        return root;
    }

    // ── 距離表示 + ビルボード ─────────────────────────────────
    /// <summary>
    /// GDD §4.9 — ピンのラベル: ローカルカメラに向けて billboard し、
    /// 「{label} {distance}m」形式で残距離を表示する。遠距離では非表示化してコスト最適化。
    /// </summary>
    private sealed class PinLabelBillboard : MonoBehaviour
    {
        private const float MAX_DISPLAY_DISTANCE = 80f;  // これ以上の距離ではラベル非表示
        private const float UPDATE_INTERVAL      = 0.2f; // 距離テキストの更新頻度（秒）

        private TextMeshPro _tmp;
        private string      _baseLabel;
        private float       _updateTimer;

        internal void Setup(TextMeshPro tmp, string baseLabel)
        {
            _tmp       = tmp;
            _baseLabel = baseLabel;
        }

        private void LateUpdate()
        {
            if (_tmp == null) return;
            var cam = Camera.main;
            if (cam == null) return;

            // 距離チェック（遠距離は完全に非表示に）
            float dist = Vector3.Distance(cam.transform.position, transform.position);
            bool  show = dist <= MAX_DISPLAY_DISTANCE;
            if (_tmp.gameObject.activeSelf != show)
                _tmp.gameObject.SetActive(show);
            if (!show) return;

            // ビルボード（Y 軸のみ回転して常にカメラ側を向く）
            Vector3 toCam = cam.transform.position - transform.position;
            toCam.y = 0f;
            if (toCam.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(-toCam.normalized);

            // 距離テキスト更新（毎フレームは無駄なので間引き）
            _updateTimer -= Time.deltaTime;
            if (_updateTimer > 0f) return;
            _updateTimer = UPDATE_INTERVAL;
            _tmp.text = $"{_baseLabel} {Mathf.RoundToInt(dist)}m";
        }
    }

    /// <summary>
    /// ピンの色を設定する。
    /// SharedMaterial を使い回し、MaterialPropertyBlock でインスタンス色を指定することで
    /// 毎回の Shader.Find と new Material を完全に排除する（パフォーマンス最適化）。
    /// </summary>
    private static void SetColor(GameObject go, Color color)
    {
        var rend = go.GetComponent<Renderer>();
        if (rend == null) return;

        // 共有マテリアルを遅延初期化（Shader.Find はここで1回だけ）
        if (s_pinSharedMaterial == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null)
            {
                Debug.LogWarning("[PinSystem] URP/Lit シェーダーが見つかりません");
                return;
            }
            s_pinSharedMaterial = new Material(shader) { name = "PinSharedMaterial" };
        }

        rend.sharedMaterial = s_pinSharedMaterial;

        // インスタンス固有の色を MaterialPropertyBlock で設定（描画バッチを維持）
        var block = new MaterialPropertyBlock();
        rend.GetPropertyBlock(block);
        block.SetColor(BaseColorPropId, color);
        block.SetColor(ColorPropId,     color);
        rend.SetPropertyBlock(block);
    }

    // ── ユーティリティ ────────────────────────────────────────
    private void CleanExpiredPins()
    {
        _myPins.RemoveAll(p => p.pinObject == null);
    }

    private static IEnumerator DestroyAfter(GameObject go, float seconds)
    {
        yield return new WaitForSeconds(seconds);
        if (go != null)
        {
            // グローバルレジストリからも除去（HUD 用）
            var t = go.transform;
            s_activePins.RemoveAll(p => p.transform == t);
            Destroy(go);
        }
    }

    // ── 残りピン数プロパティ ──────────────────────────────────
    public int PinsRemaining
    {
        get
        {
            CleanExpiredPins();
            int limit = (_ghost != null && _ghost.IsGhost) ? GHOST_PIN_LIMIT : SURVIVOR_PIN_LIMIT;
            return Mathf.Max(0, limit - _myPins.Count);
        }
    }

    // ── レコード型 ────────────────────────────────────────────
    private sealed class PinRecord
    {
        public readonly GameObject pinObject;
        public readonly float placedAt;
        public PinRecord(GameObject obj, float time) { pinObject = obj; placedAt = time; }
    }
}
