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
    [Tooltip("レベル1の必要価値")]
    [SerializeField] private int _baseQuota = 120;
    [Tooltip("レベルごとのノルマ増加率")]
    [SerializeField] private float _quotaGrowth = 1.5f;
    [SerializeField] private int _startLevel = 1;

    private int  _level;
    private int  _requiredQuota;
    private int  _lastExtractedValue;
    private bool _lastRunSucceeded;
    private bool _hasResult;

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
        Debug.Log($"[Quota] レベル {_level} 開始。必要ノルマ {_requiredQuota}pt");
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

    /// <summary>帰還エリア内に物理的に存在する（破壊されていない）遺物の価値合計。</summary>
    private int ComputeExtractedValue()
    {
        var zone = UnityEngine.Object.FindFirstObjectByType<ReturnZone>();
        if (zone == null)
        {
            Debug.LogWarning("[Quota] ReturnZone が見つかりません。搬入価値 0 として扱います。");
            return 0;
        }

        var box = zone.GetComponent<BoxCollider>();
        if (box == null) return 0;
        Bounds bounds = box.bounds; // ワールド AABB

        int total = 0;
        var relics = UnityEngine.Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None);
        foreach (var relic in relics)
        {
            if (relic == null || relic.IsDestroyed) continue;
            if (bounds.Contains(relic.transform.position))
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
