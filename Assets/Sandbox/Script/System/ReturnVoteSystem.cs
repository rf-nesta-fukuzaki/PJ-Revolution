using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using TMPro;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §14.8 — 帰還投票システム。
/// F5 で提案、F5 承認 / F6 拒否。30秒タイムアウト（棄権扱い）。
/// 可決条件: 棄権者を除く投票者の過半数が承認。
/// 再提案クールダウン: 否決後、同じプレイヤーは 60 秒間再提案不可。
/// 幽霊にも投票権あり。
/// </summary>
public class ReturnVoteSystem : NetworkBehaviour
{
    public static ReturnVoteSystem Instance { get; private set; }

    // ── GDD §14.8 定数 ───────────────────────────────────────
    private const float   VOTE_TIMEOUT      = 30f;
    private const float   REPROPOSE_COOLDOWN = 60f;
    private const float   RESULT_DISPLAY_TIME = 3f;
    private const float   APPROVED_BANNER_TIME = 10f;
    private const KeyCode KEY_PROPOSE  = KeyCode.F5;
    private const KeyCode KEY_APPROVE  = KeyCode.F5;
    private const KeyCode KEY_REJECT   = KeyCode.F6;

    [Header("投票 UI")]
    [SerializeField] private GameObject       _votePanel;
    [SerializeField] private TextMeshProUGUI  _voteText;
    [SerializeField] private TextMeshProUGUI  _timerText;

    // ── ネットワーク状態 ─────────────────────────────────────
    private readonly NetworkVariable<int> _approveCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<int> _rejectCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<bool> _voteActive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly NetworkVariable<ulong> _proposerClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── サーバ専用状態 ───────────────────────────────────────
    private float _voteTimer;
    // GDD §14.8: 再提案クールダウンは否決されたプロポーザー単位で管理する。
    private readonly Dictionary<ulong, float> _cooldownUntil = new();
    // 投票済みクライアント集合（二重投票防止。タイムアウト時は棄権扱い）。
    private readonly HashSet<ulong> _voted = new();

    // ── クライアントローカル状態 ─────────────────────────────
    private bool _localVoted;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        HidePanel();
    }

    public override void OnDestroy()
    {
        if (Instance == this) Instance = null;
        base.OnDestroy();
    }

    public override void OnNetworkSpawn()
    {
        _voteActive.OnValueChanged    += OnVoteActiveChanged;
        _approveCount.OnValueChanged  += (_, _) => RefreshVoteUI();
        _rejectCount.OnValueChanged   += (_, _) => RefreshVoteUI();
    }

    public override void OnNetworkDespawn()
    {
        _voteActive.OnValueChanged    -= OnVoteActiveChanged;
    }

    private void Update()
    {
        // F5: 非投票中なら提案、投票中なら承認。F6: 拒否のみ。
        if (!_voteActive.Value)
        {
            if (Input.GetKeyDown(KEY_PROPOSE))
                RequestStartVoteServerRpc(NetworkManager.Singleton?.LocalClientId ?? 0UL);
        }
        else if (!_localVoted)
        {
            if (Input.GetKeyDown(KEY_APPROVE)) CastVote(true);
            if (Input.GetKeyDown(KEY_REJECT))  CastVote(false);
        }

        // タイマーはサーバ権威。タイムアウト = 棄権として resolve。
        if (_voteActive.Value && IsServer)
        {
            _voteTimer -= Time.deltaTime;
            UpdateTimerClientRpc(_voteTimer);
            if (_voteTimer <= 0f)
                ResolveVote(true);
        }
    }

    // ── 投票開始 ─────────────────────────────────────────────
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestStartVoteServerRpc(ulong proposer)
    {
        if (_voteActive.Value) return;
        if (GameServices.Expedition?.Phase != ExpeditionPhase.Climbing) return;

        // GDD §14.8 再提案クールダウン: 否決後の60秒間は同じプレイヤーから再提案不可。
        if (_cooldownUntil.TryGetValue(proposer, out float untilTime) && Time.time < untilTime)
        {
            float remaining = untilTime - Time.time;
            DenyProposalClientRpc(remaining,
                new ClientRpcParams { Send = new ClientRpcSendParams { TargetClientIds = new[] { proposer } } });
            return;
        }

        _approveCount.Value     = 0;
        _rejectCount.Value      = 0;
        _voteActive.Value       = true;
        _proposerClientId.Value = proposer;
        _voteTimer              = VOTE_TIMEOUT;
        _voted.Clear();

        AnnounceVoteStartClientRpc(proposer);
        Debug.Log($"[ReturnVote] client {proposer} が帰還提案を開始");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceVoteStartClientRpc(ulong proposer)
    {
        _localVoted = false;
        ShowPanel();
        if (_voteText != null)
            _voteText.text = $"プレイヤー {proposer} が帰還を提案しています。\n承認: F5　拒否: F6";

        // GDD §15.2 — ui_vote_start
        PPAudioManager.Instance?.PlaySE2D(SoundId.UiVoteStart);

        Debug.Log($"[ReturnVote] 帰還投票開始！F5=承認 / F6=拒否");
    }

    [ClientRpc]
    private void DenyProposalClientRpc(float remaining, ClientRpcParams rpcParams = default)
    {
        Debug.Log($"[ReturnVote] 再提案はあと {remaining:F0} 秒待つ必要があります。");
    }

    // ── 投票 ─────────────────────────────────────────────────
    private void CastVote(bool approve)
    {
        _localVoted = true;
        CastVoteServerRpc(approve);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CastVoteServerRpc(bool approve, RpcParams rpc = default)
    {
        if (!_voteActive.Value) return;

        ulong sender = rpc.Receive.SenderClientId;
        if (!_voted.Add(sender)) return;   // 二重投票防止

        if (approve) _approveCount.Value++;
        else         _rejectCount.Value++;

        int total = GetEligibleVoterCount();
        int voted = _approveCount.Value + _rejectCount.Value;

        Debug.Log($"[ReturnVote] 投票状況: 承認{_approveCount.Value} / 拒否{_rejectCount.Value} / 投票済み{voted}/{total}");

        // 全員投票済みで即解決（タイムアウト前でも）
        if (voted >= total)
            ResolveVote(false);
    }

    // ── 結果確定 ─────────────────────────────────────────────
    /// <summary>
    /// GDD §14.8 可決条件: 棄権者（タイムアウト未投票）を除く投票者の過半数が承認。
    /// </summary>
    private void ResolveVote(bool timedOut)
    {
        if (!_voteActive.Value) return;

        int approve = _approveCount.Value;
        int reject  = _rejectCount.Value;
        int cast    = approve + reject;

        // 棄権のみの場合は否決扱い（GDD §14.8「タイムアウト時は棄権扱い（投票数にカウントしない）」）。
        bool approved = cast > 0 && approve * 2 > cast;

        _voteActive.Value = false;

        // 否決されたら提案者にクールダウンを設定（§14.8）
        if (!approved)
        {
            ulong proposer = _proposerClientId.Value;
            if (proposer != ulong.MaxValue)
                _cooldownUntil[proposer] = Time.time + REPROPOSE_COOLDOWN;
        }

        AnnounceVoteResultClientRpc(approved, approve, reject, timedOut);

        if (approved)
        {
            Debug.Log("[ReturnVote] 帰還承認多数。帰還開始！");
            GameServices.Expedition?.ReturnToBase(true);
        }
        else
        {
            Debug.Log(timedOut
                ? "[ReturnVote] 投票タイムアウト。帰還提案は否決されました。"
                : "[ReturnVote] 帰還否決。遠征続行。");
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceVoteResultClientRpc(bool approved, int approve, int reject, bool timedOut)
    {
        if (_voteText != null)
        {
            if (approved)
                _voteText.text = $"帰還開始！ベースキャンプを目指しましょう\n承認 {approve} / 拒否 {reject}";
            else if (timedOut)
                _voteText.text = "帰還提案は否決されました（タイムアウト）";
            else
                _voteText.text = $"帰還提案は否決されました\n承認 {approve} / 拒否 {reject}";
        }

        // GDD §15.2 — ui_vote_approve / ui_vote_deny
        PPAudioManager.Instance?.PlaySE2D(approved ? SoundId.UiVoteApprove : SoundId.UiVoteDeny);

        StartCoroutine(HidePanelAfterDelay(approved ? APPROVED_BANNER_TIME : RESULT_DISPLAY_TIME));
    }

    // ── タイマー UI ───────────────────────────────────────────
    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateTimerClientRpc(float remaining)
    {
        if (_timerText == null) return;
        int s = Mathf.CeilToInt(remaining);
        _timerText.text = $"残り: {s} 秒";
        _timerText.color = s <= 10 ? Color.red : Color.white;
    }

    // ── NetworkVariable コールバック ──────────────────────────
    private void OnVoteActiveChanged(bool _, bool active)
    {
        if (!active) return;
        ShowPanel();
        RefreshVoteUI();
    }

    private void RefreshVoteUI()
    {
        if (!_voteActive.Value || _voteText == null) return;
        int total = GetEligibleVoterCount();
        _voteText.text = $"帰還投票中　承認: {_approveCount.Value} / 拒否: {_rejectCount.Value} / 全{total}人\n[F5] 承認　[F6] 拒否";
    }

    // ── UI ヘルパー ───────────────────────────────────────────
    private void ShowPanel() { if (_votePanel != null) _votePanel.SetActive(true); }
    private void HidePanel() { if (_votePanel != null) _votePanel.SetActive(false); }

    private IEnumerator HidePanelAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HidePanel();
    }

    // ── ヘルパー ─────────────────────────────────────────────
    /// <summary>
    /// 投票権を持つ人数。GDD §14.8「幽霊の投票権: あり」に従い、
    /// 登録済みプレイヤー全員（死亡/生存問わず）をカウントする。
    /// </summary>
    private static int GetEligibleVoterCount()
    {
        var players = PlayerHealthSystem.RegisteredPlayers;
        if (players == null || players.Count == 0) return 1;
        return Mathf.Max(1, players.Count);
    }
}
