using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Sandbox.UI
{
/// <summary>
/// アイリスアウト / アイリスインによる画面遷移。
/// DontDestroyOnLoad シングルトン。シーンに置かずコードから自動生成される。
///
/// 使い方:
///   シーン遷移: IrisTransition.Instance.LoadScene("Mountain01");
///   現シーンリロード: IrisTransition.Instance.ReloadScene();
///   演出のみ(リスポーン等): IrisTransition.Instance.IrisOut() / IrisIn()
/// </summary>
public sealed class IrisTransition : MonoBehaviour, ISceneFadeService
{
    // ── Singleton ──────────────────────────────────────────────────────────

    private static IrisTransition _instance;

    public static IrisTransition Instance
    {
        get
        {
            if (_instance == null)
            {
                var go = new GameObject("[IrisTransition]");
                _instance = go.AddComponent<IrisTransition>();
                DontDestroyOnLoad(go);
            }

            return _instance;
        }
    }

    // ── Settings ───────────────────────────────────────────────────────────

    [Header("Duration")]
    [SerializeField] private float irisOutDuration = 0.4f;
    [SerializeField] private float irisInDuration  = 0.85f;

    [Header("Loading")]
    [Tooltip("GDD — ローディング中 TIPS の最低表示秒数。")]
    [SerializeField] private float minLoadingDisplaySeconds = 3f;

    [Header("Appearance")]
    [SerializeField] private Color irisColor = Color.black;
    [SerializeField] private Color irisLoadingTint = new(0.02f, 0.08f, 0.09f, 1f);

    // ── Private fields ─────────────────────────────────────────────────────

    private Material  _material;
    private RawImage  _irisImage;
    private bool      _isSceneTransitioning;
    private Coroutine _activeIrisCoroutine;

    private static readonly int RadiusProp      = Shader.PropertyToID("_Radius");
    private static readonly int ColorProp       = Shader.PropertyToID("_Color");
    private static readonly int AspectRatioProp = Shader.PropertyToID("_AspectRatio");

    // ── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        GameServices.Register((ISceneFadeService)this);

        SetupCanvas();
        SetupMaterial();
        SetupImage();

        // 起動時は完全に開いた状態（透明）
        SetRadius(GetMaxRadius());
    }

    // ── Public API ─────────────────────────────────────────────────────────

    /// <summary>
    /// アイリスアウト → シーンロード → アイリスイン を一括実行する。
    /// sceneName = null で現在のシーンをリロード。
    /// </summary>
    public void LoadScene(string sceneName)
    {
        if (_isSceneTransitioning) return;
        StartCoroutine(LoadSceneCoroutine(sceneName));
    }

    /// <summary>現在のシーンをリロードする。</summary>
    public void ReloadScene() => LoadScene(null);

    // ── ISceneFadeService（明示実装：引数なしオーバーロードへ委譲）─────────────
    void ISceneFadeService.IrisOut() => IrisOut();
    void ISceneFadeService.IrisIn()  => IrisIn();

    /// <summary>
    /// アイリスアウトのみ（画面を閉じる）。
    /// リスポーン演出など、シーン遷移を伴わない場合に使う。
    /// </summary>
    public void IrisOut(float duration = -1f, System.Action onComplete = null)
    {
        StopActiveIris();
        _activeIrisCoroutine = StartCoroutine(
            IrisOutCoroutine(duration < 0f ? irisOutDuration : duration, onComplete));
    }

    /// <summary>
    /// アイリスインのみ（画面を開く）。
    /// </summary>
    public void IrisIn(float duration = -1f, System.Action onComplete = null)
    {
        StopActiveIris();
        _activeIrisCoroutine = StartCoroutine(
            IrisInCoroutine(duration < 0f ? irisInDuration : duration, onComplete));
    }

    // ── Coroutines ─────────────────────────────────────────────────────────

    private IEnumerator LoadSceneCoroutine(string sceneName)
    {
        _isSceneTransitioning = true;

        string tip = LoadingTipsCatalog.PickRandom();
        LoadingTipsOverlay.Instance?.Show(tip);

        yield return IrisOutCoroutine(irisOutDuration, null);

        float loadStartedAt = Time.unscaledTime;

        AsyncOperation op = sceneName != null
            ? SceneManager.LoadSceneAsync(sceneName)
            : SceneManager.LoadSceneAsync(SceneManager.GetActiveScene().buildIndex);

        if (op != null)
        {
            op.allowSceneActivation = false;
            while (op.progress < 0.9f)
            {
                LoadingTipsOverlay.Instance?.SetProgress(Mathf.Clamp01(op.progress / 0.9f));
                yield return null;
            }
            LoadingTipsOverlay.Instance?.SetProgress(1f);
            op.allowSceneActivation = true;
            yield return op;
        }

        yield return null;

        float elapsed = Time.unscaledTime - loadStartedAt;
        float remain  = minLoadingDisplaySeconds - elapsed;
        if (remain > 0f)
        {
            float t = 0f;
            while (t < remain)
            {
                t += Time.unscaledDeltaTime;
                LoadingTipsOverlay.Instance?.SetProgress(Mathf.Lerp(0.85f, 1f, t / remain));
                yield return null;
            }
        }

        LoadingTipsOverlay.Instance?.Hide();

        yield return IrisInCoroutine(irisInDuration, null);

        _isSceneTransitioning = false;
    }

    private IEnumerator IrisOutCoroutine(float duration, System.Action onComplete)
    {
        float startRadius = _material != null ? _material.GetFloat(RadiusProp) : GetMaxRadius();
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / duration;
            SetRadius(Mathf.Lerp(startRadius, 0f, p));
            SetIrisTint(Color.Lerp(irisColor, irisLoadingTint, p));
            yield return null;
        }

        SetRadius(0f);
        SetIrisTint(irisLoadingTint);
        onComplete?.Invoke();
    }

    private IEnumerator IrisInCoroutine(float duration, System.Action onComplete)
    {
        UpdateAspectRatio();
        float maxRadius = GetMaxRadius();
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float p = t / duration;
            SetRadius(Mathf.Lerp(0f, maxRadius, p));
            SetIrisTint(Color.Lerp(irisLoadingTint, irisColor, p));
            yield return null;
        }

        SetRadius(maxRadius);
        SetIrisTint(irisColor);
        onComplete?.Invoke();
    }

    private void SetIrisTint(Color color)
    {
        _material?.SetColor(ColorProp, color);
    }

    // ── Helpers ────────────────────────────────────────────────────────────

    private void StopActiveIris()
    {
        if (_activeIrisCoroutine != null)
        {
            StopCoroutine(_activeIrisCoroutine);
            _activeIrisCoroutine = null;
        }
    }

    private void SetRadius(float radius)
    {
        _material?.SetFloat(RadiusProp, radius);
    }

    private void UpdateAspectRatio()
    {
        _material?.SetFloat(AspectRatioProp, (float)Screen.width / Screen.height);
    }

    /// <summary>
    /// 画面の四隅まで完全にカバーするための半径（アスペクト補正後の距離）。
    /// </summary>
    private float GetMaxRadius()
    {
        float aspect = (float)Screen.width / Screen.height;
        // 補正後の UV 空間でのコーナー距離 + 余白
        return Mathf.Sqrt(aspect * aspect * 0.25f + 0.25f) + 0.05f;
    }

    // ── Setup ──────────────────────────────────────────────────────────────

    private void SetupCanvas()
    {
        var canvasGo = new GameObject("Canvas");
        canvasGo.transform.SetParent(transform);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 32767; // 常に最前面

        canvasGo.AddComponent<CanvasScaler>();
    }

    private void SetupMaterial()
    {
        var shader = Shader.Find("Custom/IrisTransition");
        if (shader == null)
        {
            Debug.LogError("[IrisTransition] Shader 'Custom/IrisTransition' not found. " +
                           "Ensure Assets/Shaders/IrisTransition.shader exists.");
            return;
        }

        _material = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
        _material.SetColor(ColorProp, irisColor);
        _material.SetFloat(AspectRatioProp, (float)Screen.width / Screen.height);
    }

    private void SetupImage()
    {
        var canvas = GetComponentInChildren<Canvas>();
        if (canvas == null) return;

        var imageGo = new GameObject("IrisImage");
        imageGo.transform.SetParent(canvas.transform, false);

        _irisImage              = imageGo.AddComponent<RawImage>();
        _irisImage.texture      = Texture2D.whiteTexture;
        _irisImage.material     = _material;
        _irisImage.raycastTarget = false;

        var rt = _irisImage.rectTransform;
        rt.anchorMin       = Vector2.zero;
        rt.anchorMax       = Vector2.one;
        rt.sizeDelta       = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }
}
}
