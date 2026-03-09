using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ゲーム全体の状態（探索中 / 深度遷移中 / 脱出成功 / 全員ダウン）を管理する Singleton。
/// EscapeGate から NotifyEscape() が呼ばれるか、全プレイヤーのダウンを検出して状態遷移する。
///
/// [深度遷移]
///   CurrentDepth &lt; 3 のとき NotifyEscape() は DepthTransition 状態に移行し、
///   _depthTransitionDelay 秒後に DepthManager.AdvanceDepth() → CaveGenerator.Generate() を
///   実行して次の深度の洞窟を生成する。
///   CurrentDepth == 3 のとき EscapeSuccess（最終クリア）に遷移する。
///
/// [ソロ前提実装]
///   プレイヤーは 1 人として動作確認する。
///   NGO 対応は後フェーズで行う。
/// </summary>
public class GameManager : MonoBehaviour
{
    // ─── Singleton ───────────────────────────────────────────────────────

    /// <summary>シーン全体からアクセスできる GameManager インスタンス。</summary>
    public static GameManager Instance { get; private set; }

    // ─── Inspector ───────────────────────────────────────────────────────

    [Header("ゲーム設定")]
    [Tooltip("全員ダウン判定の遅延秒数（誤判定防止）")]
    [SerializeField] private float ダウン判定遅延 = 1.5f;

    [Tooltip("深度遷移演出の待機時間（秒）")]
    [SerializeField] private float _depthTransitionDelay = 3f;

    [Header("参照")]
    [SerializeField] private CaveGenerator _caveGenerator;

    // ─── 公開プロパティ ──────────────────────────────────────────────────

    /// <summary>現在のゲーム状態。</summary>
    public GameState CurrentState { get; private set; } = GameState.Exploring;

    /// <summary>探索中の経過時間（秒）。Exploring 中のみ加算される。</summary>
    public float ElapsedTime { get; private set; }

    /// <summary>プレイヤーが収集した宝石の合計数。</summary>
    public int CollectedGems { get; private set; }

    // ─── イベント ────────────────────────────────────────────────────────

    /// <summary>
    /// ゲーム状態が変化したときにブロードキャストされる。
    /// ResultUI など各システムが Subscribe して状態変化に応答する。
    /// </summary>
    public static event Action<GameState> OnGameStateChanged;

    // ─── 内部状態 ────────────────────────────────────────────────────────

    private List<PlayerStateManager> _playerManagers = new List<PlayerStateManager>();

    // 全員ダウン判定用タイマー（連続で閾値を超えたときのみ遷移）
    private float _downedTimer;
    private bool  _downedTimerActive;

    // ─── Unity Lifecycle ─────────────────────────────────────────────────

    private void Awake()
    {
        // Singleton 登録（重複があれば自身を破棄）
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_caveGenerator == null)
            _caveGenerator = FindFirstObjectByType<CaveGenerator>();
    }

    private void Start()
    {
        // ゲーム開始時にプレイヤーを一度キャッシュする
        // （Start より後にスポーンする場合は RefreshPlayerCache() を外部から呼ぶ）
        RefreshPlayerCache();
    }

    private void Update()
    {
        if (CurrentState != GameState.Exploring) return;

        ElapsedTime += Time.deltaTime;
        CheckAllDowned();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── 公開 API ────────────────────────────────────────────────────────

    /// <summary>
    /// EscapeGate からプレイヤーが脱出ゲートに触れたときに呼ぶ。
    /// - CurrentDepth &lt; 3: DepthTransition 状態に遷移し、演出後に次の深度へ進む。
    /// - CurrentDepth == 3: 最終クリアとして EscapeSuccess へ遷移する。
    /// </summary>
    /// <param name="player">脱出したプレイヤーの GameObject（null も許容）</param>
    public void NotifyEscape(GameObject player)
    {
        if (CurrentState != GameState.Exploring) return;

        Debug.Log($"[GameManager] プレイヤーが脱出しました: {player?.name}");

        var depthMgr = DepthManager.Instance;
        if (depthMgr != null && depthMgr.CurrentDepth < 3)
        {
            // 最終深度未達: 次の深度へ遷移
            ChangeState(GameState.DepthTransition);
            StartCoroutine(DepthTransitionCoroutine());
        }
        else
        {
            // 最終深度（3）または DepthManager なし: 最終クリア
            ChangeState(GameState.EscapeSuccess);
        }
    }

    /// <summary>
    /// 宝石収集時に呼ぶ。CollectedGems に amount を加算する。
    /// </summary>
    public void AddGem(int amount)
    {
        CollectedGems += amount;
        Debug.Log($"[GameManager] 宝石 +{amount} → 合計 {CollectedGems}個");
    }

    /// <summary>
    /// プレイヤーキャッシュを再構築する。
    /// スポーンタイミングが遅い場合や NGO でプレイヤーが追加された場合に外部から呼ぶ。
    /// </summary>
    public void RefreshPlayerCache()
    {
        _playerManagers.Clear();
        _playerManagers.AddRange(
            FindObjectsByType<PlayerStateManager>(FindObjectsSortMode.None));

        Debug.Log($"[GameManager] プレイヤーキャッシュ更新: {_playerManagers.Count} 人");
    }

    // ─── 深度遷移コルーチン ───────────────────────────────────────────────

    /// <summary>
    /// 演出時間待機 → DepthManager.AdvanceDepth() でパラメータ更新
    /// → CaveGenerator.Generate() で洞窟再生成 → Exploring 状態に復帰する。
    /// </summary>
    private IEnumerator DepthTransitionCoroutine()
    {
        Debug.Log($"[GameManager] 深度遷移開始。{_depthTransitionDelay} 秒後に次の洞窟を生成します。");

        yield return new WaitForSeconds(_depthTransitionDelay);

        // パラメータを次の深度に更新（チャンク数・スポーン数を変更）
        DepthManager.Instance?.AdvanceDepth();

        // 洞窟を再生成（OnCaveGenerated で BatSpawner 等も再スポーンされる）
        if (_caveGenerator != null)
        {
            _caveGenerator.Generate();
            Debug.Log($"[GameManager] Depth {DepthManager.Instance?.CurrentDepth} の洞窟を再生成しました。");
        }
        else
        {
            Debug.LogWarning("[GameManager] CaveGenerator が見つかりません。洞窟を再生成できませんでした。");
        }

        // プレイヤーキャッシュを更新して探索再開
        RefreshPlayerCache();
        ChangeState(GameState.Exploring);
    }

    // ─── 内部処理 ────────────────────────────────────────────────────────

    /// <summary>
    /// 全プレイヤーがダウン状態かどうかを確認し、
    /// 一定時間継続した場合は AllDowned 状態へ遷移する。
    /// Update() から毎フレーム呼ばれる。
    /// </summary>
    private void CheckAllDowned()
    {
        if (_playerManagers.Count == 0) return;

        bool allDowned = true;
        foreach (var psm in _playerManagers)
        {
            if (psm == null) continue;
            if (psm.CurrentState != PlayerState.Downed)
            {
                allDowned = false;
                break;
            }
        }

        if (allDowned)
        {
            // 誤判定防止: ダウン状態が ダウン判定遅延 秒間継続したら遷移
            if (!_downedTimerActive)
            {
                _downedTimerActive = true;
                _downedTimer       = 0f;
            }

            _downedTimer += Time.deltaTime;

            if (_downedTimer >= ダウン判定遅延)
                ChangeState(GameState.AllDowned);
        }
        else
        {
            // 誰かが復活したらタイマーをリセット
            _downedTimerActive = false;
            _downedTimer       = 0f;
        }
    }

    private void ChangeState(GameState newState)
    {
        if (CurrentState == newState) return;

        Debug.Log($"[GameManager] 状態遷移: {CurrentState} → {newState}");
        CurrentState = newState;

        // ゲーム終了時（AllDowned / EscapeSuccess）はタイムスケールを 0 にして停止する。
        // DepthTransition はゲーム継続中なのでタイムスケールを変更しない。
        if (newState == GameState.AllDowned || newState == GameState.EscapeSuccess)
            Time.timeScale = 0f;

        // Exploring 開始時はアップグレードを全コンポーネントに反映する。
        if (newState == GameState.Exploring)
            UpgradeSystem.Instance?.ApplyAllUpgrades();

        OnGameStateChanged?.Invoke(newState);
    }
}

// ─── GameState 定義 ───────────────────────────────────────────────────────

/// <summary>
/// ゲーム全体の状態。GameManager が管理し OnGameStateChanged でブロードキャストする。
/// </summary>
public enum GameState
{
    /// <summary>洞窟を探索中（通常プレイ状態）</summary>
    Exploring,

    /// <summary>脱出ゲートを通過して脱出成功</summary>
    EscapeSuccess,

    /// <summary>全プレイヤーがダウンして敗北</summary>
    AllDowned,

    /// <summary>中間深度の脱出後、次の深度洞窟を生成中の遷移状態</summary>
    DepthTransition,
}
