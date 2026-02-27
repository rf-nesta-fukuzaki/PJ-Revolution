using System.Collections;
using UnityEngine;

/// <summary>
/// プレイヤーが E キーで洞窟から脱出できるゴールオブジェクト。
///
/// [動作フロー]
///   1. Start() で CaveGenerator.GoalCenterPosition へ自動配置（Inspector で無効化可）
///   2. 0.3 秒ごとのコルーチンでプレイヤーとの距離を計測し、
///      感知半径以内に入ったら UIManager にプロンプトを送信しライト色を変更
///   3. プレイヤーが E キーを押すと PlayerInteractor → IInteractable.Interact() が呼ばれる
///   4. Interact() 内で PlayerStateManager を確認し、Downed でなければ GameManager に通知
///
///
/// [UIManager との連携]
///   UIManager は static event を持たないため SetInteractPrompt() を直接呼ぶ。
///   PlayerInteractor も SetInteractPrompt() を呼ぶが、同じ文字列のため競合しない。
/// </summary>
public class EscapeGate : MonoBehaviour, IInteractable
{
    // ─── Inspector: 脱出ゲート設定 ───────────────────────────────────────

    [Header("脱出ゲート設定")]
    [Tooltip("プレイヤーが感知できる距離（m）。この範囲に入るとプロンプトが表示される")]
    [Range(2f, 6f)]
    [SerializeField] private float 感知半径 = 3f;

    // ─── Inspector: 演出 ─────────────────────────────────────────────────

    [Header("演出")]
    [Tooltip("ゲートを照らすポイントライト。未アサインの場合は子オブジェクトから自動取得する")]
    [SerializeField] private Light ゲートライト;

    [Tooltip("ライトの明滅速度（Hz）。0.5 でゆっくり明滅する")]
    [Range(0.1f, 2f)]
    [SerializeField] private float 明滅速度 = 0.5f;

    [Tooltip("待機中のライトカラー（緑白）")]
    [SerializeField] private Color 通常カラー = new Color(0.2f, 1f, 0.5f, 1f);

    [Tooltip("プレイヤーが接近したときのライトカラー（黄白）")]
    [SerializeField] private Color 接近カラー = new Color(1f, 1f, 0.5f, 1f);

    // ─── Inspector: 自動配置 ─────────────────────────────────────────────

    [Header("自動配置")]
    [Tooltip("true にすると Start() で CaveGenerator のゴール座標に自動移動する。" +
             "false にすると Inspector / 手動配置を使用する")]
    [SerializeField] private bool 自動配置 = true;

    // ─── 内部状態 ────────────────────────────────────────────────────────

    private UIManager  _ui;
    private Transform  _playerTransform;
    private bool       _playerInRange;
    private float      _baseLightIntensity;
    private bool       _isActivated = true; // インタラクト後に false になる

    // ─── Unity Lifecycle ─────────────────────────────────────────────────

    private void Start()
    {
        // UIManager を取得（SetInteractPrompt を呼ぶために使用）
        _ui = FindFirstObjectByType<UIManager>();

        // ゲートライトが未アサインの場合は子オブジェクトから自動取得
        if (ゲートライト == null)
            ゲートライト = GetComponentInChildren<Light>();

        if (ゲートライト != null)
        {
            _baseLightIntensity = ゲートライト.intensity;
            ゲートライト.color  = 通常カラー;
        }

        // ソロ前提: PlayerStateManager からプレイヤーの Transform を取得
        var psm = FindFirstObjectByType<PlayerStateManager>();
        if (psm != null)
            _playerTransform = psm.transform;

        // 自動配置: CaveGenerator のゴール座標へ移動
        if (自動配置)
            AutoPlace();

        // 近接プロンプト管理コルーチンを開始
        StartCoroutine(ProximityRoutine());
    }

    private void Update()
    {
        if (!_isActivated || ゲートライト == null) return;

        // サイン波でライト強度を明滅させる（0.5Hz = 2 秒周期でゆっくり）
        float phase = Mathf.Sin(Time.time * 明滅速度 * 2f * Mathf.PI);
        ゲートライト.intensity = _baseLightIntensity * (0.7f + 0.3f * phase);
    }

    // ─── IInteractable 実装 ──────────────────────────────────────────────

    /// <summary>
    /// PlayerInteractor が Raycast でこのオブジェクトを検出したときに表示するプロンプト文字列。
    /// </summary>
    public string GetPromptText() => "[ E ] 脱出する";

    /// <summary>
    /// プレイヤーが E キーを押したときに PlayerInteractor から呼ばれる。
    /// ダウン中でなければ GameManager に脱出を通知し、ゲートを無効化する。
    /// </summary>
    public void Interact(UnityEngine.GameObject interactor)
    {
        if (!_isActivated) return;

        // PlayerStateManager を取得してダウン中かどうか確認する
        PlayerStateManager psm = interactor != null
            ? interactor.GetComponent<PlayerStateManager>()
            : FindFirstObjectByType<PlayerStateManager>();

        if (psm != null && psm.CurrentState == PlayerState.Downed)
        {
            Debug.Log("[EscapeGate] ダウン中のため脱出不可");
            return;
        }

        // GameManager に脱出を通知する
        GameManager.Instance?.NotifyEscape(interactor);

        // プロンプトを消去してゲートを無効化（再インタラクト防止）
        _ui?.SetInteractPrompt(null);
        _isActivated = false;
        gameObject.SetActive(false);
    }

    // ─── 近接プロンプト（コルーチン） ───────────────────────────────────
    //
    // Physics.OverlapSphere を使わず 0.3 秒ごとのポーリングで軽量化する。
    // Raycast ベースの PlayerInteractor とは独立して動作するが、
    // 表示文字列が同じため視覚的な競合は発生しない。

    private IEnumerator ProximityRoutine()
    {
        var wait = new WaitForSeconds(0.3f);

        while (_isActivated)
        {
            yield return wait;

            // プレイヤー Transform が未取得の場合は再検索（遅延スポーン対応）
            if (_playerTransform == null)
            {
                var psm = FindFirstObjectByType<PlayerStateManager>();
                if (psm != null) _playerTransform = psm.transform;
                continue;
            }

            float dist    = Vector3.Distance(transform.position, _playerTransform.position);
            bool  inRange = dist <= 感知半径;

            // 前フレームから状態が変わった場合のみ UI を更新する
            if (inRange == _playerInRange) continue;

            _playerInRange = inRange;

            if (inRange)
            {
                // 接近: プロンプトを表示し、ライトを接近カラーに変更
                _ui?.SetInteractPrompt(GetPromptText());
                if (ゲートライト != null) ゲートライト.color = 接近カラー;
            }
            else
            {
                // 離脱: プロンプトを消去し、ライトを通常カラーに戻す
                _ui?.SetInteractPrompt(null);
                if (ゲートライト != null) ゲートライト.color = 通常カラー;
            }
        }
    }

    // ─── 自動配置 ────────────────────────────────────────────────────────

    /// <summary>
    /// CaveGenerator を取得し、GoalCenterPosition が確定していれば即時配置、
    /// まだ (0,0,0) なら OnCaveGenerated イベントを購読して生成完了後に配置する。
    /// Start() の実行順が CaveGenerator.Start() より前になる場合の対策。
    /// </summary>
    private void AutoPlace()
    {
        var gen = FindFirstObjectByType<CaveGenerator>();
        if (gen == null)
        {
            Debug.LogWarning("[EscapeGate] CaveGenerator が見つかりません。自動配置をスキップします。" +
                             "手動でシーンに配置するか、Inspector で 自動配置 を false にしてください");
            return;
        }

        if (gen.GoalCenterPosition != Vector3.zero)
        {
            // 既に洞窟生成済み → 即時配置
            ApplyGoalPosition(gen.GoalCenterPosition);
        }
        else
        {
            // Start() 実行順の問題で GoalCenterPosition が未確定 → 生成完了後に配置
            gen.OnCaveGenerated += () => ApplyGoalPosition(gen.GoalCenterPosition);
        }
    }

    /// <summary>
    /// 指定座標にゲートを移動する。(0,0,0) の場合は警告を出す。
    /// </summary>
    private void ApplyGoalPosition(Vector3 goal)
    {
        if (goal == Vector3.zero)
        {
            Debug.LogWarning("[EscapeGate] GoalCenterPosition が (0, 0, 0) です。\n" +
                             "原因: CaveGenerator が GoalCenterPosition を計算していない可能性があります。\n" +
                             "対処: CaveGenerator.cs の Generate2D()/Generate3D() に GoalCenterPosition の計算があるか確認してください。");
        }

        transform.position = goal;
        Debug.Log($"[EscapeGate] ゴール座標 {goal} に自動配置しました\n" +
                  $"  GameObject アクティブ: {gameObject.activeSelf}\n" +
                  $"  localScale: {transform.localScale}\n" +
                  $"  ゲートライト: {(ゲートライト != null ? ゲートライト.name : "未アサイン")}");
    }
}
