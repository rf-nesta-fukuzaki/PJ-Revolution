using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §4.10 — エモートシステム。
///
/// T キーで 6 種のエモートホイールを表示。
/// エモート選択後、Animator Trigger "Emote_{N}" を発火 +
/// ServerRpc → ClientsAndHost RPC で全プレイヤーに同期再生する。
///
/// EA 版 6 種：
///   0: 手を振る / 1: 指差し / 2: 拍手 / 3: お辞儀 / 4: ガッツポーズ / 5: 頭を抱える
///
/// ステート管理は PlayerStateMachine に委譲。
/// クライミング中・運搬中はエモート不可（GDD §4.10）。
/// エモート再生中（2 秒）は移動不可。
/// </summary>
[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerStateMachine))]
public class EmoteSystem : NetworkBehaviour
{
    // ── GDD 定数 ─────────────────────────────────────────────
    private const float   EMOTE_DURATION = 2f;
    private const KeyCode EMOTE_KEY      = KeyCode.T;

    private static readonly string[] EMOTE_NAMES = {
        "手を振る", "指差し", "拍手", "お辞儀", "ガッツポーズ", "頭を抱える"
    };

    // Animator.StringToHash をキャッシュ — SetTrigger 呼び出し時の文字列アロケーションを回避
    private static readonly int[] EMOTE_TRIGGER_HASHES = BuildEmoteTriggerHashes();

    private static int[] BuildEmoteTriggerHashes()
    {
        var hashes = new int[EMOTE_NAMES.Length];
        for (int i = 0; i < hashes.Length; i++)
            hashes[i] = Animator.StringToHash($"Emote_{i}");
        return hashes;
    }

    // ── Inspector ─────────────────────────────────────────────
    [Header("エモートホイール UI")]
    [SerializeField] private GameObject    _wheelRoot;
    [SerializeField] private EmoteSlotUI[] _slots;

    [Header("エモートアイコン（省略可）")]
    [SerializeField] private Sprite[] _emoteIcons;

    // ── コンポーネント ─────────────────────────────────────────
    private Animator           _animator;
    private PlayerHealthSystem _health;
    private PlayerStateMachine _stateMachine;
    private ExplorerController _controller;

    // ── ローカル状態 ─────────────────────────────────────────
    private bool _wheelOpen;
    private int  _hoveredSlot = -1;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        _animator     = GetComponentInChildren<Animator>();
        _health       = GetComponent<PlayerHealthSystem>();
        _stateMachine = GetComponent<PlayerStateMachine>();
        _controller   = GetComponent<ExplorerController>();

        Debug.Assert(_stateMachine != null,
            "[EmoteSystem] PlayerStateMachine が同一 GameObject に見つかりません");

        _wheelRoot?.SetActive(false);
        InitSlots();
    }

    private void Update()
    {
        if (!IsOwner) return;
        if (_health != null && _health.IsDead) return;

        // 幽霊状態・ラグドール中はエモート不可
        if (_stateMachine != null && !_stateMachine.IsAlive) return;

        // T キーでホイール開閉（エモート再生中は不可）
        if (Input.GetKeyDown(EMOTE_KEY))
        {
            _wheelOpen = !_wheelOpen;
            _wheelRoot?.SetActive(_wheelOpen);
        }

        if (!_wheelOpen) return;

        UpdateHoveredSlot();

        if (Input.GetMouseButtonDown(0) && _hoveredSlot >= 0)
            SelectEmote(_hoveredSlot);

        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(EMOTE_KEY))
        {
            _wheelOpen = false;
            _wheelRoot?.SetActive(false);
        }
    }

    // ── スロット選択 ─────────────────────────────────────────
    private void UpdateHoveredSlot()
    {
        if (_slots == null) return;

        Vector2 center = new(Screen.width * 0.5f, Screen.height * 0.5f);
        Vector2 dir    = (Vector2)Input.mousePosition - center;

        if (dir.magnitude < 30f)
        {
            SetHighlight(-1);
            return;
        }

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        if (angle < 0f) angle += 360f;

        int slot = Mathf.FloorToInt(angle / 60f) % EMOTE_NAMES.Length;
        SetHighlight(slot);
    }

    private void SetHighlight(int slot)
    {
        _hoveredSlot = slot;
        if (_slots == null) return;
        for (int i = 0; i < _slots.Length; i++)
            _slots[i]?.SetHighlight(i == slot);
    }

    private void SelectEmote(int index)
    {
        _wheelOpen = false;
        _wheelRoot?.SetActive(false);

        // 前提条件: index は有効範囲内
        if ((uint)index >= (uint)EMOTE_NAMES.Length)
        {
            Debug.LogError($"[Contract] EmoteSystem.SelectEmote: index {index} が範囲外です");
            return;
        }

        SendEmoteServerRpc((byte)index);
    }

    // ── NetworkRPC ────────────────────────────────────────────
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SendEmoteServerRpc(byte emoteIndex)
    {
        BroadcastEmoteClientRpc(emoteIndex, OwnerClientId);
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void BroadcastEmoteClientRpc(byte emoteIndex, ulong senderClientId)
    {
        if (OwnerClientId == senderClientId)
            StartCoroutine(PlayEmoteLocal(emoteIndex));
    }

    // ── エモート再生 ─────────────────────────────────────────
    private IEnumerator PlayEmoteLocal(int index)
    {
        // 既にエモート中なら無視
        if (_stateMachine != null && !_stateMachine.IsAlive) yield break;

        _stateMachine?.Transition(PlayerState.Emoting);
        if (_controller != null) _controller.enabled = false;

        if (_animator != null)
            _animator.SetTrigger(EMOTE_TRIGGER_HASHES[index]);

        Debug.Log($"[Emote] {EMOTE_NAMES[index]}");
        yield return new WaitForSeconds(EMOTE_DURATION);

        if (_controller != null) _controller.enabled = true;

        // 死亡中でなければ Alive に戻す
        if (_stateMachine != null && _stateMachine.IsEmoting)
            _stateMachine.Transition(PlayerState.Alive);
    }

    // ── スロット初期化 ────────────────────────────────────────
    private void InitSlots()
    {
        if (_slots == null) return;
        for (int i = 0; i < _slots.Length && i < EMOTE_NAMES.Length; i++)
        {
            if (_slots[i] == null) continue;
            _slots[i].SetLabel(EMOTE_NAMES[i]);
            if (_emoteIcons != null && i < _emoteIcons.Length)
                _slots[i].SetIcon(_emoteIcons[i]);
        }
    }
}

// ── エモートスロット UI ───────────────────────────────────────
/// <summary>エモートホイールの1スロット分の UI ヘルパー。</summary>
[System.Serializable]
public class EmoteSlotUI
{
    [SerializeField] private Image           _background;
    [SerializeField] private Image           _icon;
    [SerializeField] private TextMeshProUGUI _label;

    private static readonly Color NORMAL    = new(0.2f, 0.2f, 0.2f, 0.8f);
    private static readonly Color HIGHLIGHT = new(0.9f, 0.7f, 0.1f, 0.95f);

    public void SetLabel(string text)  { if (_label      != null) _label.text      = text; }
    public void SetIcon(Sprite sprite) { if (_icon       != null) _icon.sprite      = sprite; }
    public void SetHighlight(bool on)  { if (_background != null) _background.color = on ? HIGHLIGHT : NORMAL; }
}
