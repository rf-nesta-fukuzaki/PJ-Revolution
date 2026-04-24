using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// GDD §14.9 — 遺物発見通知 UI。
/// RelicDiscoveryTrigger から呼ばれ、「遺物を発見！— [name]」を 3 秒表示してフェードアウトする。
/// シーン内のシングルトン。未配置でも RelicDiscoveryTrigger が失敗しないよう
/// null チェック経由でアクセスされる（trigger 側で Instance?.NotifyDiscovered(...)）。
/// </summary>
[DisallowMultipleComponent]
public class RelicDiscoveryNotifier : MonoBehaviour
{
    public static RelicDiscoveryNotifier Instance { get; private set; }

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
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (_group != null) _group.alpha = 0f;
        if (_iconRoot != null) _iconRoot.SetActive(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
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
