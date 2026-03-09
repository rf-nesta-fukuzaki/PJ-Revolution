using System;
using UnityEngine;

/// <summary>
/// 階層マップシステム。Depth 1-3 の進行を管理し、各深度のパラメータを保持する。
///
/// [設計]
///   Singleton。GameManager から AdvanceDepth() を呼ぶことで Depth を進め、
///   CaveGenerator / BatSpawner / LizardSpawner にパラメータを反映する。
///   SurvivalStats が参照する消耗倍率も GetCurrentConfig() 経由で取得する。
/// </summary>
public class DepthManager : MonoBehaviour
{
    // ─────────────── Singleton ───────────────

    public static DepthManager Instance { get; private set; }

    // ─────────────── 各 Depth パラメータ定義 ───────────────

    [Serializable]
    public struct DepthConfig
    {
        [Tooltip("X 方向チャンク数")]
        public int ChunkCountX;

        [Tooltip("Y 方向チャンク数（高さ）")]
        public int ChunkCountY;

        [Tooltip("Z 方向チャンク数")]
        public int ChunkCountZ;

        [Tooltip("コウモリ最大スポーン数")]
        public int MaxBats;

        [Tooltip("トカゲ最大スポーン数")]
        public int MaxLizards;

        [Tooltip("酸素消耗速度倍率")]
        public float OxygenDrainMultiplier;

        [Tooltip("空腹消耗速度倍率")]
        public float HungerDrainMultiplier;
    }

    // ─────────────── Inspector ───────────────

    [Header("📊 深度設定")]
    [Tooltip("Depth 1 / 2 / 3 の順にパラメータを設定する（配列サイズ 3 固定）")]
    [SerializeField] private DepthConfig[] _depthConfigs = new DepthConfig[3]
    {
        // Depth 1: 標準
        new DepthConfig
        {
            ChunkCountX          = 8,
            ChunkCountY          = 4,
            ChunkCountZ          = 8,
            MaxBats              = 5,
            MaxLizards           = 2,
            OxygenDrainMultiplier = 1.0f,
            HungerDrainMultiplier = 1.0f,
        },
        // Depth 2: 中規模・やや強化
        new DepthConfig
        {
            ChunkCountX          = 10,
            ChunkCountY          = 5,
            ChunkCountZ          = 10,
            MaxBats              = 8,
            MaxLizards           = 4,
            OxygenDrainMultiplier = 1.3f,
            HungerDrainMultiplier = 1.2f,
        },
        // Depth 3: 大規模・最強
        new DepthConfig
        {
            ChunkCountX          = 12,
            ChunkCountY          = 6,
            ChunkCountZ          = 12,
            MaxBats              = 12,
            MaxLizards           = 6,
            OxygenDrainMultiplier = 1.6f,
            HungerDrainMultiplier = 1.5f,
        },
    };

    [Header("🔗 参照")]
    [SerializeField] private CaveGenerator _caveGenerator;
    [SerializeField] private BatSpawner    _batSpawner;
    [SerializeField] private LizardSpawner _lizardSpawner;

    [Header("🔧 デバッグ")]
    [SerializeField] private int _debugCurrentDepth = 1;

    // ─────────────── 公開プロパティ ───────────────

    /// <summary>現在の深度（1 〜 3）。</summary>
    public int CurrentDepth { get; private set; } = 1;

    /// <summary>深度が変化したときに発火するイベント。引数は新しい深度。</summary>
    public event Action<int> OnDepthChanged;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 参照が Inspector で設定されていない場合は自動検索
        if (_caveGenerator == null)
            _caveGenerator = FindFirstObjectByType<CaveGenerator>();
        if (_batSpawner == null)
            _batSpawner = FindFirstObjectByType<BatSpawner>();
        if (_lizardSpawner == null)
            _lizardSpawner = FindFirstObjectByType<LizardSpawner>();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// 深度を 1 進める。最大 3 でクランプ。
    /// CaveGenerator / BatSpawner / LizardSpawner にパラメータを反映し、
    /// OnDepthChanged を発火する。
    /// </summary>
    public void AdvanceDepth()
    {
        if (CurrentDepth >= 3)
        {
            Debug.LogWarning("[DepthManager] 既に最深部 (Depth 3) です。");
            return;
        }

        CurrentDepth       = Mathf.Clamp(CurrentDepth + 1, 1, 3);
        _debugCurrentDepth = CurrentDepth;

        ApplyConfig(GetCurrentConfig());

        Debug.Log($"[DepthManager] 深度が {CurrentDepth} に進みました。");
        OnDepthChanged?.Invoke(CurrentDepth);
    }

    /// <summary>現在の深度設定を返す。</summary>
    public DepthConfig GetCurrentConfig() => _depthConfigs[CurrentDepth - 1];

    // ─────────────── 内部処理 ───────────────

    /// <summary>指定設定を各スポーナー・ジェネレータに反映する。</summary>
    private void ApplyConfig(DepthConfig cfg)
    {
        if (_caveGenerator != null)
            _caveGenerator.SetChunkCounts(cfg.ChunkCountX, cfg.ChunkCountY, cfg.ChunkCountZ);
        else
            Debug.LogWarning("[DepthManager] CaveGenerator が見つかりません。");

        if (_batSpawner != null)
            _batSpawner.SetMaxBats(cfg.MaxBats);
        else
            Debug.LogWarning("[DepthManager] BatSpawner が見つかりません。");

        if (_lizardSpawner != null)
            _lizardSpawner.SetMaxLizards(cfg.MaxLizards);
        else
            Debug.LogWarning("[DepthManager] LizardSpawner が見つかりません。");
    }
}
