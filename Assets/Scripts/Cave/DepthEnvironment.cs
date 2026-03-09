using UnityEngine;

/// <summary>
/// プレイヤーの Y 座標を監視し、深度レベル（Shallow / Mid / Deep）に応じて
/// アンビエントライトとフォグを滑らかに変化させる。
///
/// [深度区分]
///   Shallow : Y > 全体高さの 66%
///   Mid     : 33% ≤ Y ≤ 66%
///   Deep    : Y < 33%
///
/// [アタッチ先]
///   PlayerPrefab または任意の空 GameObject にアタッチする。
///   _playerTransform が未設定なら PlayerMovement を自動検索する。
/// </summary>
public class DepthEnvironment : MonoBehaviour
{
    // ─── Inspector 参照 ──────────────────────────────────────────────

    [Header("参照")]
    [Tooltip("監視するプレイヤーの Transform。null のとき PlayerMovement を自動検索する。")]
    [SerializeField] private Transform _playerTransform;

    [Tooltip("全体高さ取得のために参照する CaveGenerator。")]
    [SerializeField] private CaveGenerator _caveGenerator;

    // ─── Shallow 設定 ────────────────────────────────────────────────

    [Header("Shallow（上層）")]
    [Tooltip("Shallow 時のアンビエント色（暖色系）")]
    [SerializeField] private Color _shallowAmbient = new Color(0.15f, 0.12f, 0.08f);

    [Tooltip("Shallow 時のフォグ色")]
    [SerializeField] private Color _shallowFogColor = new Color(0.18f, 0.14f, 0.10f);

    [Tooltip("Shallow 時のフォグ密度")]
    [SerializeField] private float _shallowFogDensity = 0.01f;

    // ─── Mid 設定 ─────────────────────────────────────────────────────

    [Header("Mid（中層）")]
    [Tooltip("Mid 時のアンビエント色（灰色系）")]
    [SerializeField] private Color _midAmbient = new Color(0.08f, 0.08f, 0.08f);

    [Tooltip("Mid 時のフォグ色")]
    [SerializeField] private Color _midFogColor = new Color(0.05f, 0.05f, 0.06f);

    [Tooltip("Mid 時のフォグ密度")]
    [SerializeField] private float _midFogDensity = 0.02f;

    // ─── Deep 設定 ────────────────────────────────────────────────────

    [Header("Deep（深層）")]
    [Tooltip("Deep 時のアンビエント色（青黒系）")]
    [SerializeField] private Color _deepAmbient = new Color(0.02f, 0.02f, 0.03f);

    [Tooltip("Deep 時のフォグ色")]
    [SerializeField] private Color _deepFogColor = new Color(0.01f, 0.01f, 0.02f);

    [Tooltip("Deep 時のフォグ密度")]
    [SerializeField] private float _deepFogDensity = 0.04f;

    // ─── 遷移設定 ─────────────────────────────────────────────────────

    [Header("遷移")]
    [Tooltip("アンビエント・フォグの補間速度 (1/s)")]
    [SerializeField] private float _lerpSpeed = 2f;

    // ─── 深度区分 ─────────────────────────────────────────────────────

    private enum DepthLevel { Shallow, Mid, Deep }

    // ─── 内部状態 ─────────────────────────────────────────────────────

    private float      _totalHeight;
    private float      _caveOriginY;
    private DepthLevel _currentLevel = DepthLevel.Shallow;

    // 現在の補間値（RenderSettings に毎フレーム適用）
    private Color _currentAmbient;
    private Color _currentFogColor;
    private float _currentFogDensity;

    // ─── Unity ライフサイクル ─────────────────────────────────────────

    void Start()
    {
        RenderSettings.fog = true;

        // プレイヤー自動検索
        if (_playerTransform == null)
        {
            var pm = FindFirstObjectByType<PlayerMovement>();
            if (pm != null)
                _playerTransform = pm.transform;
            else
                Debug.LogWarning("[DepthEnvironment] PlayerMovement が見つかりません。_playerTransform を Inspector で設定してください。");
        }

        // CaveGenerator 自動検索
        if (_caveGenerator == null)
            _caveGenerator = FindFirstObjectByType<CaveGenerator>();

        RefreshCaveDimensions();

        // 初期値を即時設定（遷移なし）
        var (targetAmbient, targetFog, targetDensity) = GetTargetValues(DepthLevel.Shallow);
        _currentAmbient    = targetAmbient;
        _currentFogColor   = targetFog;
        _currentFogDensity = targetDensity;
        ApplyToRenderSettings();
    }

    void Update()
    {
        if (_playerTransform == null) return;

        // 全体高さが未確定（CaveGenerator が遅延生成する場合）なら再取得
        if (_totalHeight <= 0f)
            RefreshCaveDimensions();

        // 深度レベル判定
        float relativeY = _playerTransform.position.y - _caveOriginY;
        float ratio     = _totalHeight > 0f ? relativeY / _totalHeight : 0.5f;

        DepthLevel target = ratio > 0.66f ? DepthLevel.Shallow
                          : ratio > 0.33f ? DepthLevel.Mid
                                          : DepthLevel.Deep;

        _currentLevel = target;

        // ターゲット値を取得して Lerp
        var (tAmbient, tFog, tDensity) = GetTargetValues(target);
        float dt = Time.deltaTime * _lerpSpeed;
        _currentAmbient    = Color.Lerp(_currentAmbient,    tAmbient, dt);
        _currentFogColor   = Color.Lerp(_currentFogColor,   tFog,     dt);
        _currentFogDensity = Mathf.Lerp(_currentFogDensity, tDensity, dt);

        ApplyToRenderSettings();
    }

    // ─── 内部ヘルパー ─────────────────────────────────────────────────

    private void RefreshCaveDimensions()
    {
        if (_caveGenerator != null)
        {
            _totalHeight  = _caveGenerator.TotalWorldHeight;
            _caveOriginY  = _caveGenerator.transform.position.y;
        }
        else
        {
            _totalHeight = 0f;
            _caveOriginY = 0f;
        }
    }

    private (Color ambient, Color fog, float density) GetTargetValues(DepthLevel level)
    {
        return level switch
        {
            DepthLevel.Shallow => (_shallowAmbient, _shallowFogColor, _shallowFogDensity),
            DepthLevel.Mid     => (_midAmbient,     _midFogColor,     _midFogDensity),
            _                  => (_deepAmbient,    _deepFogColor,    _deepFogDensity),
        };
    }

    private void ApplyToRenderSettings()
    {
        RenderSettings.ambientLight = _currentAmbient;
        RenderSettings.fogColor     = _currentFogColor;
        RenderSettings.fogDensity   = _currentFogDensity;
    }
}
