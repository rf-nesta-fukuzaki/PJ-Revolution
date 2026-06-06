using System;
using UnityEngine;
using PeakPlunder.Audio;

/// <summary>
/// 抽出ノルマによる勝敗ループ（R.E.P.O. コア）。
/// 各遠征(=レベル)には必要価値ノルマがあり、帰還エリアに搬入した遺物の価値合計が
/// ノルマ以上なら成功→次レベルでノルマ上昇、未達なら失敗（ゲームオーバー判定）。
/// 遠征終了イベント時に帰還エリア内の遺物価値を集計して判定する。
/// </summary>
public class ExtractionQuotaSystem : MonoBehaviour
{
    public static ExtractionQuotaSystem Instance { get; private set; }

    [Header("ノルマ")]
    // GDD §9.4 の基準報酬額(300〜1500pt/個)に整合。レベル1は「良い遺物1個ぶん(~800pt)」が目安。
    [Tooltip("レベル1の必要価値")]
    [SerializeField] private int _baseQuota = 800;
    [Tooltip("レベルごとのノルマ増加率")]
    [SerializeField] private float _quotaGrowth = 1.5f;
    [SerializeField] private int _startLevel = 1;

    private int  _level;
    private int  _requiredQuota;
    private int  _lastExtractedValue;
    private bool _lastRunSucceeded;
    private bool _hasResult;

    // ヘリ空輸で抽出された遺物 ID。帰還ゾーン外でも「持ち帰り成立」として価値に算入する（GDD §2.4）。
    private readonly System.Collections.Generic.HashSet<int> _airliftedRelicIds = new();

    public int  Level              => _level;
    public int  RequiredQuota      => _requiredQuota;
    public int  LastExtractedValue => _lastExtractedValue;
    public bool LastRunSucceeded   => _lastRunSucceeded;
    public bool HasResult          => _hasResult;

    /// <summary>(成功か, 搬入価値, 必要ノルマ, レベル) を通知。HUD/リザルトが購読する。</summary>
    public event Action<bool, int, int, int> OnQuotaEvaluated;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        _level = Mathf.Max(1, _startLevel);
        RecomputeQuota();
    }

    private void OnEnable()
    {
        ExpeditionEvents.OnExpeditionStarted += HandleExpeditionStarted;
        ExpeditionEvents.OnExpeditionEnded   += HandleExpeditionEnded;
    }

    private void OnDisable()
    {
        ExpeditionEvents.OnExpeditionStarted -= HandleExpeditionStarted;
        ExpeditionEvents.OnExpeditionEnded   -= HandleExpeditionEnded;
        if (Instance == this) Instance = null;
    }

    private void HandleExpeditionStarted()
    {
        _hasResult = false;
        _lastExtractedValue = 0;
        _airliftedRelicIds.Clear();
        Debug.Log($"[Quota] レベル {_level} 開始。必要ノルマ {_requiredQuota}pt");
    }

    /// <summary>ヘリ空輸で持ち帰った遺物を抽出価値に算入する（HelicopterController から呼ぶ）。</summary>
    public void RegisterAirliftedRelic(RelicBase relic)
    {
        if (relic != null && !relic.IsDestroyed)
            _airliftedRelicIds.Add(relic.GetInstanceID());
    }

    private void HandleExpeditionEnded()
    {
        int extracted = ComputeExtractedValue();
        _lastExtractedValue = extracted;
        _lastRunSucceeded   = extracted >= _requiredQuota;
        _hasResult          = true;

        OnQuotaEvaluated?.Invoke(_lastRunSucceeded, extracted, _requiredQuota, _level);

        if (_lastRunSucceeded)
        {
            Debug.Log($"[Quota] ノルマ達成！ {extracted}/{_requiredQuota}pt → レベル {_level + 1} へ");
            GameServices.Audio?.PlaySE2D(SoundId.ResultTitle);

            // ノルマ達成お祝い：帰還ゾーン中心に金の大ポップ（集計イベントのため per-relic 位置は無い）。
            var _rz = UnityEngine.Object.FindFirstObjectByType<ReturnZone>();
            if (_rz != null && _rz.TryGetComponent<BoxCollider>(out var _rzBox))
            {
                Sandbox.World.Environment.StylizedImpactFx.CollectPop(
                    _rzBox.bounds.center + Vector3.up * 1.5f,
                    new Color(1f, 0.86f, 0.35f), 1.6f, 36);
            }

            // 抽出価値を所持金へ加算（恒久アップグレード購入の原資・R.E.P.O.）
            CurrencyWallet.Add(extracted);
            _level++;
            RecomputeQuota();
        }
        else
        {
            Debug.Log($"[Quota] ノルマ未達… {extracted}/{_requiredQuota}pt → ゲームオーバー");
            GameServices.Audio?.PlaySE2D(SoundId.WipeoutJingle);
        }
    }

    /// <summary>帰還エリア内に物理的に存在する（破壊されていない）遺物 + ヘリ空輸された遺物の価値合計。</summary>
    private int ComputeExtractedValue()
    {
        // 帰還ゾーンの AABB（無ければ位置判定はスキップし、ヘリ空輸分のみ算入）。
        bool hasZone = false;
        Bounds bounds = default;
        var zone = UnityEngine.Object.FindFirstObjectByType<ReturnZone>();
        if (zone != null && zone.TryGetComponent<BoxCollider>(out var box))
        {
            bounds = box.bounds;
            hasZone = true;
        }
        else if (_airliftedRelicIds.Count == 0)
        {
            Debug.LogWarning("[Quota] ReturnZone が見つからず空輸もありません。搬入価値 0 として扱います。");
            return 0;
        }

        int total = 0;
        var relics = UnityEngine.Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None);
        foreach (var relic in relics)
        {
            if (relic == null || relic.IsDestroyed) continue;
            // 帰還ゾーン内に置かれた遺物 or ヘリ空輸された遺物を持ち帰り成立として加算（重複は自然に1回）。
            bool inZone = hasZone && bounds.Contains(relic.transform.position);
            if (inZone || _airliftedRelicIds.Contains(relic.GetInstanceID()))
                total += relic.CurrentValue;
        }
        return total;
    }

    private void RecomputeQuota()
    {
        float q = _baseQuota * Mathf.Pow(_quotaGrowth, _level - 1);
        _requiredQuota = Mathf.RoundToInt(q);
    }

    /// <summary>デバッグ/テスト用：現在の搬入価値を即時集計して返す。</summary>
    public int PeekExtractedValue() => ComputeExtractedValue();
}
