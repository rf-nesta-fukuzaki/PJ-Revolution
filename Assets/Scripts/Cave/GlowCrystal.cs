using UnityEngine;

/// <summary>
/// 光るクリスタルの見た目と光源パルスエフェクトを制御するコンポーネント。
/// 子オブジェクトに Point Light を自動生成（または既存を使用）し、
/// sin カーブで輝度を「呼吸するように」明滅させる。
///
/// [セットアップ]
///   1. クリスタル本体 Mesh の GameObject にアタッチする。
///   2. このスクリプトが Point Light 子オブジェクトを自動生成する。
///      既存の Light があればそれを使用する。
///   3. Inspector でカラー・輝度・パルス速度を調整する。
///
/// [カラーバリエーション]
///   crystalColor で青・紫・橙などの色を設定できる。
///   Mesh の Material に Emission をセットしている場合、
///   EmissionColor を crystalColor と連動させることも可能（下記オプション参照）。
/// </summary>
public class GlowCrystal : MonoBehaviour
{
    // ─── クリスタル設定 ──────────────────────────────────────────────────

    [Header("クリスタル設定")]
    [Tooltip("クリスタルの発光色（青=神秘的 / 紫=魔力 / 橙=溶岩）")]
    [SerializeField] private Color crystalColor = new Color(0.3f, 0.6f, 1.0f);

    [Tooltip("光の最小輝度（sin 谷の値）")]
    [SerializeField] private float minIntensity = 0.4f;

    [Tooltip("光の最大輝度（sin 山の値）")]
    [SerializeField] private float maxIntensity = 1.8f;

    [Tooltip("パルス周期（Hz）— 大きいほど速く明滅する")]
    [SerializeField] private float pulseSpeed = 1.2f;

    [Tooltip("位相オフセット（0〜2π）— 複数クリスタルが同時に明滅するのを防ぐ")]
    [SerializeField] private float phaseOffset = 0f;

    [Header("マテリアル Emission 連動")]
    [Tooltip("true にすると MeshRenderer の Emission Color を crystalColor と同期する")]
    [SerializeField] private bool syncMaterialEmission = true;

    [Tooltip("Emission 強度の倍率（輝度に掛け合わせる）")]
    [SerializeField] private float emissionMultiplier = 0.5f;

    // ─── 内部状態 ────────────────────────────────────────────────────────

    private Light         _light;
    private MeshRenderer  _renderer;
    private Material      _material;           // インスタンス化済み Material
    private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

    // ─── 初期化 ─────────────────────────────────────────────────────────

    private void Awake()
    {
        // 既存の Point Light を探す。なければ子に生成する。
        _light = GetComponentInChildren<Light>();
        if (_light == null)
        {
            var lightGO = new GameObject("CrystalLight");
            lightGO.transform.SetParent(transform, false);
            _light = lightGO.AddComponent<Light>();
            _light.type  = LightType.Point;
            _light.range = 6f;
        }

        _light.color = crystalColor;
        _light.intensity = minIntensity;

        // Material 準備（Emission を動的変更するためインスタンス化）
        _renderer = GetComponentInChildren<MeshRenderer>();
        if (_renderer != null && syncMaterialEmission)
        {
            _material = _renderer.material; // Instance を作成
            _material.EnableKeyword("_EMISSION");
        }

        // 位相オフセットが設定されていない場合は位置ベースのハッシュで分散させる
        if (Mathf.Approximately(phaseOffset, 0f))
        {
            phaseOffset = (transform.position.x * 1.3f
                         + transform.position.z * 0.9f) % (Mathf.PI * 2f);
        }
    }

    private void OnDestroy()
    {
        // インスタンス化した Material を解放
        if (_material != null)
            Destroy(_material);
    }

    // ─── Update（毎フレームパルス更新）────────────────────────────────────

    private void Update()
    {
        if (_light == null) return;

        // sin カーブで 0〜1 の補間値を計算
        float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f + phaseOffset) + 1f) * 0.5f;
        float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

        _light.intensity = intensity;

        // Material Emission の同期
        if (_material != null && syncMaterialEmission)
        {
            Color emissionColor = crystalColor * (intensity * emissionMultiplier);
            _material.SetColor(EmissionColorID, emissionColor);
        }
    }

    // ─── Editor からも色変更を反映 ──────────────────────────────────────

    private void OnValidate()
    {
        // Inspector で値を変えたとき即座に Light 色に反映
        var existingLight = GetComponentInChildren<Light>();
        if (existingLight != null)
            existingLight.color = crystalColor;
    }

    // ─── 公開 API ──────────────────────────────────────────────────────────

    /// <summary>
    /// 外部からクリスタルカラーを変更する（バリエーション生成時に使用）。
    /// </summary>
    public void SetColor(Color color)
    {
        crystalColor  = color;
        if (_light != null) _light.color = color;
    }
}
