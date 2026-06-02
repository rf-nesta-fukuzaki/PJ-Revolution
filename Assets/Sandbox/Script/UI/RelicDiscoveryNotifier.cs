using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Sandbox.UI;

/// <summary>
/// GDD §14.9 — 遺物発見通知 UI。
/// RelicDiscoveryTrigger から呼ばれ、「遺物を発見！— [name]」を 3 秒表示してフェードアウトする。
/// シーン内のシングルトン。未配置でも RelicDiscoveryTrigger が失敗しないよう
/// null チェック経由でアクセスされる（trigger 側で GameServices.RelicDiscovery?.NotifyDiscovered(...)）。
/// </summary>
[DisallowMultipleComponent]
public class RelicDiscoveryNotifier : MonoBehaviour, IRelicDiscoveryNotifier
{
    private static RelicDiscoveryNotifier _instance;

    [System.Obsolete("GameServices.RelicDiscovery を使用してください")]
    public static RelicDiscoveryNotifier Instance => _instance;

    // GDD §14.9: 表示 3 秒 + フェード 0.5 秒。
    private const float DISPLAY_SECONDS = 3f;
    private const float FADE_SECONDS    = 0.5f;

    [Header("UI 参照")]
    [SerializeField] private CanvasGroup      _group;
    [SerializeField] private TextMeshProUGUI  _label;
    [SerializeField] private GameObject       _iconRoot; // 遺物のミニアイコン表示枠（任意）

    [Header("ローカルプレイヤーフィルタ")]
    [Tooltip("空なら全てのプレイヤー発見を表示。設定時は指定プレイヤーの InstanceID のみ表示。")]
    [SerializeField] private int _localPlayerInstanceId = 0;

    private Coroutine _activeRoutine;
    // 連続発見に備えた簡易キュー（同時発見時の取りこぼし防止）。
    private readonly Queue<string> _pendingMessages = new();

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        GameServices.Register((IRelicDiscoveryNotifier)this);

        if (_group != null) _group.alpha = 0f;
        if (_iconRoot != null) _iconRoot.SetActive(false);

        RestyleAsPill();
    }

    /// <summary>
    /// 中央集約ミニマル: 素の黒バナー(上中央)を、画面下中央の角丸ピル型トーストへ
    /// 実行時に整形する（シーンアセットは書き換えない・非破壊）。
    /// 背景は 9-slice の角丸スプライト＋パレット色、ラベルはクリーム中央寄せ。
    /// </summary>
    private void RestyleAsPill()
    {
        if (_group != null)
        {
            var rt = _group.transform as RectTransform;
            if (rt != null)
            {
                rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(440f, 46f);
                rt.anchoredPosition = new Vector2(0f, 132f);
            }

            var bg = _group.GetComponent<Image>();
            if (bg != null)
            {
                bg.sprite = GetPillSprite();
                bg.type = Image.Type.Sliced;
                bg.pixelsPerUnitMultiplier = 1f;
                var c = UiPalette.Ink; c.a = 0.72f; // 読みやすさ重視でやや不透明
                bg.color = c;
            }
        }

        if (_label != null)
        {
            _label.color = UiPalette.Cream;
            _label.alignment = TextAlignmentOptions.Center;
            _label.fontSize = 20f;
        }

        if (_iconRoot != null) _iconRoot.SetActive(false);
    }

    private static Sprite s_pillSprite;

    /// <summary>角丸長方形（9-slice 用ボーダー付き）の柔らかいスプライトを手続き生成する。</summary>
    private static Sprite GetPillSprite()
    {
        if (s_pillSprite != null) return s_pillSprite;

        const int size = 48;
        const float radius = 16f;
        const float aa = 1.5f;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float half = (size - 1) * 0.5f;
        float b = half - radius;

        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float qx = Mathf.Max(Mathf.Abs(x - half) - b, 0f);
                float qy = Mathf.Max(Mathf.Abs(y - half) - b, 0f);
                float dist = Mathf.Sqrt(qx * qx + qy * qy) - radius; // <0 = 内側
                px[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(0.5f - dist / aa));
            }
        }
        tex.SetPixels(px);
        tex.Apply();

        s_pillSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
            100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        return s_pillSprite;
    }

    private void OnDestroy()
    {
        if (_instance == this) _instance = null;
    }

    /// <summary>
    /// プレイヤーが遺物の検出範囲に初めて入った際に呼ばれる。
    /// </summary>
    /// <param name="playerInstanceId">発見したプレイヤー GameObject の InstanceID</param>
    /// <param name="relicName">遺物名（Inspector 表示名）</param>
    public void NotifyDiscovered(int playerInstanceId, string relicName)
    {
        if (string.IsNullOrEmpty(relicName)) relicName = "???";

        // ローカルプレイヤー限定フィルタ（0 の場合は無効 = 全員の発見を表示）。
        if (_localPlayerInstanceId != 0 && _localPlayerInstanceId != playerInstanceId)
            return;

        string message = $"遺物を発見！— {relicName}";
        _pendingMessages.Enqueue(message);

        if (_activeRoutine == null)
            _activeRoutine = StartCoroutine(DrainQueue());
    }

    /// <summary>
    /// ローカルプレイヤーの InstanceID を登録する。設定後は本人の発見のみ表示される。
    /// </summary>
    public void SetLocalPlayer(GameObject playerRoot)
    {
        if (playerRoot == null) { _localPlayerInstanceId = 0; return; }
        _localPlayerInstanceId = playerRoot.GetInstanceID();
    }

    private IEnumerator DrainQueue()
    {
        while (_pendingMessages.Count > 0)
        {
            string msg = _pendingMessages.Dequeue();
            yield return ShowOne(msg);
        }
        _activeRoutine = null;
    }

    private IEnumerator ShowOne(string message)
    {
        if (_label != null) _label.text = message;
        if (_iconRoot != null) _iconRoot.SetActive(true);

        // フェードイン
        yield return FadeGroup(0f, 1f, FADE_SECONDS);

        // 表示キープ
        float elapsed = 0f;
        while (elapsed < DISPLAY_SECONDS)
        {
            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        // フェードアウト
        yield return FadeGroup(1f, 0f, FADE_SECONDS);

        if (_iconRoot != null) _iconRoot.SetActive(false);
    }

    private IEnumerator FadeGroup(float from, float to, float duration)
    {
        if (_group == null) yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
            yield return null;
        }
        _group.alpha = to;
    }
}
