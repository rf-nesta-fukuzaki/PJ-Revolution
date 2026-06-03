using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// GDD §2.1 — 遠征（ラン）全体のフロー管理。
/// ベースキャンプ→登攀→帰還判断→リザルト の流れを制御する。
/// </summary>
public class ExpeditionManager : MonoBehaviour, IExpeditionService
{
    private static ExpeditionManager _instance;

    [System.Obsolete("GameServices.Expedition を使用してください")]
    public static ExpeditionManager Instance => _instance;

    [Header("参照")]
    [SerializeField] private ResultScreen _resultScreen;
    [SerializeField] private SpawnManager _spawnManager;
    [SerializeField] private BasecampShop _basecampShop;

    [Header("フェードUI")]
    [SerializeField] private CanvasGroup _fadeCanvas;
    [SerializeField] private float       _fadeDuration = 1.0f;

    [Header("ゲームオーバー")]
    [Tooltip("ソロ/オフライン時、GhostSystem を持たないプレイヤーが死亡した際の自動リスポーンまでの待機秒数。")]
    [SerializeField] private float       _respawnDelay  = 5f;
    [SerializeField] private Transform[] _checkpoints;

    // ── 状態 ─────────────────────────────────────────────────
    private readonly ExpeditionPhaseStateMachine _phaseMachine = new();
    private readonly ExpeditionTimer _timer = new();
    private int                    _currentCheckpointIdx;
    private bool                   _expeditionEnded;
    private bool                   _expeditionStarted;
    private readonly List<Transform> _dynamicCheckpoints = new();

    public ExpeditionPhase Phase => _phaseMachine.Current;
    public ExpeditionTimer Timer => _timer;

    private void Update()
    {
        if (_phaseMachine.Current == ExpeditionPhase.Climbing)
            _timer.Tick(Time.deltaTime);
    }

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        GameServices.Register((IExpeditionService)this);
    }

    private void Start()
    {
        _spawnManager?.RunAllLayers();

        // BasecampShop がある場合は OnDepart ボタンから StartExpedition() を呼ぶ。
        // ショップが存在しない場合（テストシーン等）は即時開始する。
        if (_basecampShop == null)
            _basecampShop = Object.FindFirstObjectByType<BasecampShop>();

        if (_basecampShop == null)
            StartExpedition();
    }

    // ── 遠征開始 ─────────────────────────────────────────────
    /// <summary>遠征を開始する。BasecampShop.OnDepart() または自動呼び出しから使用。</summary>
    public void StartExpedition()
    {
        if (_expeditionStarted) return;

        Contract.Invariant(_phaseMachine.Current == ExpeditionPhase.Basecamp,
            $"StartExpedition: invalid phase transition {_phaseMachine.Current}→Climbing");

        _expeditionStarted = true;
        _timer.Start();

        // GDD §5.2 — BivouacTent「1遠征1個」制限をここで解除。
        // 新しい遠征の開始＝前回までの設置記録をクリアする唯一の正当なタイミング。
        BivouacTentItem.ResetExpeditionFlag();

        TransitionPhase(ExpeditionPhase.Climbing);
        ExpeditionEvents.RaiseExpeditionStarted();

        // ネットワーク同期：ホスト以外のクライアントにもフェーズ変化を通知
        NetworkExpeditionSync.Instance?.StartExpeditionServerRpc();

        Debug.Log("[Expedition] 遠征開始！");
    }

    // ── 動的チェックポイント管理（BivouacTent 等）──────────────
    /// <summary>ランタイムで設置されたチェックポイントを登録する（BivouacCheckpoint から呼ぶ）。</summary>
    public void RegisterDynamicCheckpoint(Transform checkpoint)
    {
        _dynamicCheckpoints.Add(checkpoint);
        Debug.Log($"[Expedition] 動的チェックポイント登録: {checkpoint.position}");
    }

    /// <summary>最適なリスポーン地点を返す。動的 → 固定チェックポイントの優先順。</summary>
    public Transform GetRespawnPoint()
    {
        // 最後に設置された動的チェックポイント（ビバークテント）を優先
        for (int i = _dynamicCheckpoints.Count - 1; i >= 0; i--)
        {
            if (_dynamicCheckpoints[i] != null)
                return _dynamicCheckpoints[i];
        }

        // 固定チェックポイントにフォールバック
        if (_checkpoints != null && _currentCheckpointIdx < _checkpoints.Length)
            return _checkpoints[_currentCheckpointIdx];

        return null;
    }

    // ── チェックポイント到達 ──────────────────────────────────
    public void OnCheckpointReached(int checkpointIdx)
    {
        _currentCheckpointIdx = checkpointIdx;
        int total = _checkpoints?.Length ?? 4;
        ExpeditionEvents.RaiseCheckpointReached(checkpointIdx + 1, total);
    }

    // ── 帰還 ─────────────────────────────────────────────────
    public void ReturnToBase(bool allSurvived = true)
    {
        if (_expeditionEnded) return;

        // 帰還は登攀フェーズからのみ有効。遠征未開始（Basecamp）等で呼ばれた場合は
        // 不正なフェーズ遷移エラーを発生させず、安全に無視する（防御的ガード）。
        if (_phaseMachine.Current != ExpeditionPhase.Climbing)
        {
            Debug.LogWarning(
                $"[Expedition] ReturnToBase は Climbing フェーズでのみ有効です（現在: {_phaseMachine.Current}）。無視します。");
            return;
        }

        _expeditionEnded = true;
        _timer.Stop();

        TransitionPhase(ExpeditionPhase.Returning);
        ExpeditionEvents.RaiseExpeditionEnded();

        NetworkExpeditionSync.Instance?.ReturnToBaseServerRpc();

        float clearTime = _timer.ElapsedSeconds;

        var scoreService = GameServices.Score;
        if (scoreService == null)
        {
            Debug.LogWarning("[Expedition] IScoreService が見つかりません");
            return;
        }

        if (!Contract.TryRequires(clearTime >= 0f,
            $"ExpeditionManager.ReturnToBase: clearTime が負の値 ({clearTime})"))
            clearTime = 0f;

        var score = scoreService.BuildResultData(clearTime, allSurvived);
        StartCoroutine(ShowResultAfterFade(score));
    }

    private IEnumerator ShowResultAfterFade(ScoreData score)
    {
        yield return StartCoroutine(Fade(0f, 1f));
        yield return new WaitForSeconds(0.5f);
        yield return StartCoroutine(Fade(1f, 0f));

        TransitionPhase(ExpeditionPhase.Result);
        _resultScreen?.Show(score);
    }

    private void TransitionPhase(ExpeditionPhase next)
    {
        if (!_phaseMachine.TryTransition(next))
            return;

        Debug.Log($"[Expedition] フェーズ遷移: {next}");
    }

    // ── 死亡リスポーン ────────────────────────────────────────
    public void OnPlayerDied(PlayerHealthSystem player)
    {
        GameServices.Score?.RecordFall(PlayerScoreId.FromMember(player));

        var ghost = player.GetComponent<GhostSystem>();
        if (ghost == null)
        {
            // ソロ / オフライン: GhostSystem が無いので _respawnDelay 秒後に自動リスポーン
            StartCoroutine(SoloRespawnRoutine(player));
            return;
        }

        if (ghost.IsGhost)
        {
            // 既にゴースト状態 = 再死亡 → 全員死亡チェック
            CheckAllDead();
            return;
        }

        Debug.Log($"[Expedition] {player.name} が死亡 (ゴーストモード移行)");
    }

    /// <summary>
    /// GhostSystem を持たないプレイヤー向けのフォールバックリスポーン。
    /// _respawnDelay 秒待機後、最寄りのチェックポイントへ転送し HP を回復する。
    /// </summary>
    private IEnumerator SoloRespawnRoutine(PlayerHealthSystem player)
    {
        Debug.Log($"[Expedition] {player.name} を {_respawnDelay:F1} 秒後にリスポーンします");
        yield return new WaitForSeconds(_respawnDelay);

        if (player == null) yield break;

        var respawnPoint = GetRespawnPoint();
        if (respawnPoint != null)
        {
            var rb = player.GetComponent<Rigidbody>();
            if (rb != null) rb.linearVelocity = Vector3.zero;
            player.transform.position = respawnPoint.position;
            player.transform.rotation = respawnPoint.rotation;
        }
        else
        {
            Debug.LogWarning("[Expedition] リスポーン地点が見つかりません。現在地で復活します");
        }

        player.Revive(player.MaxHp);
    }

    private void CheckAllDead()
    {
        bool anyAlive = false;

        foreach (var p in PlayerHealthSystem.RegisteredPlayers)
        {
            if (!p.IsDead)
            {
                anyAlive = true;
                break;
            }
        }

        if (!anyAlive)
        {
            Debug.Log("[Expedition] 全員死亡。遺物ゼロで帰還。");
            StartCoroutine(PlayWipeoutSequence());
            ReturnToBase(false);
        }
    }

    /// <summary>
    /// GDD §15.1 — 全滅ジングル演出：BGM 2 秒フェードアウト → 1 秒無音 → 全滅ジングル再生。
    /// </summary>
    private IEnumerator PlayWipeoutSequence()
    {
        var audio = GameServices.Audio;
        if (audio == null) yield break;

        audio.StopBGM();                     // 2 秒クロスフェードで BGM がフェードアウト
        yield return new WaitForSeconds(3f); // BGM フェード 2s + 無音 1s
        audio.PlaySE2D(SoundId.WipeoutJingle);
    }

    // ── フェード ─────────────────────────────────────────────
    private IEnumerator Fade(float from, float to)
    {
        if (_fadeCanvas == null) yield break;

        float elapsed = 0f;
        _fadeCanvas.gameObject.SetActive(true);
        while (elapsed < _fadeDuration)
        {
            elapsed          += Time.deltaTime;
            _fadeCanvas.alpha = Mathf.Lerp(from, to, elapsed / _fadeDuration);
            yield return null;
        }
        _fadeCanvas.alpha = to;

        if (to <= 0f)
            _fadeCanvas.gameObject.SetActive(false);
    }
}

public enum ExpeditionPhase { Basecamp, Climbing, Returning, Result }
