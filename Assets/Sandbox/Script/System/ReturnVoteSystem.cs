using System.Collections;
using Unity.Netcode;
using UnityEngine;
using TMPro;

/// <summary>
/// GDD §14.8 — 帰還投票システム。
/// F5 キーで帰還提案を開始。全員の過半数（50%超）が賛成で帰還確定。
/// 投票 UI を全プレイヤーに同期表示する。
/// </summary>
public class ReturnVoteSystem : NetworkBehaviour
{
    public static ReturnVoteSystem Instance { get; private set; }

    // ── GDD 定数 ─────────────────────────────────────────────
    private const float VOTE_TIMEOUT     = 30f;  // 投票タイムアウト（秒）
    private const KeyCode VOTE_KEY       = KeyCode.F5;

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

    // ── ローカル状態 ─────────────────────────────────────────
    private bool _hasVotedThisRound;
    private float _voteTimer;

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
        // F5 で帰還提案（GDD §4.2）
        if (Input.GetKeyDown(VOTE_KEY) && !_voteActive.Value)
            RequestStartVoteServerRpc(NetworkManager.Singleton?.LocalClientId ?? 0UL);

        // 投票中: Y/N 入力処理
        if (_voteActive.Value && !_hasVotedThisRound)
        {
            if (Input.GetKeyDown(KeyCode.Y)) CastVote(true);
            if (Input.GetKeyDown(KeyCode.N)) CastVote(false);
        }

        // タイマー UI
        if (_voteActive.Value && IsServer)
        {
            _voteTimer -= Time.deltaTime;
            UpdateTimerClientRpc(_voteTimer);
            if (_voteTimer <= 0f)
                ResolveVote(false);
        }
    }

    // ── 投票開始 ─────────────────────────────────────────────
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void RequestStartVoteServerRpc(ulong proposer)
    {
        if (_voteActive.Value) return;
        if (GameServices.Expedition?.Phase != ExpeditionPhase.Climbing) return;

        _approveCount.Value   = 0;
        _rejectCount.Value    = 0;
        _voteActive.Value     = true;
        _proposerClientId.Value = proposer;
        _voteTimer            = VOTE_TIMEOUT;

        AnnounceVoteStartClientRpc(proposer);
        Debug.Log($"[ReturnVote] client {proposer} が帰還提案を開始");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceVoteStartClientRpc(ulong proposer)
    {
        _hasVotedThisRound = false;
        ShowPanel();
        if (_voteText != null)
            _voteText.text = $"プレイヤー {proposer} が帰還を提案！\n[Y] 賛成　[N] 反対";
        Debug.Log($"[ReturnVote] 帰還投票開始！Y/N で投票してください");
    }

    // ── 投票 ─────────────────────────────────────────────────
    private void CastVote(bool approve)
    {
        _hasVotedThisRound = true;
        CastVoteServerRpc(approve);
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void CastVoteServerRpc(bool approve)
    {
        if (!_voteActive.Value) return;

        if (approve) _approveCount.Value++;
        else         _rejectCount.Value++;

        int total = GetAliveSurvivorCount();
        int voted = _approveCount.Value + _rejectCount.Value;

        Debug.Log($"[ReturnVote] 投票状況: 賛成{_approveCount.Value} / 反対{_rejectCount.Value} / 投票済み{voted}/{total}");

        // 全員投票済みか過半数に達したら即決
        if (voted >= total || _approveCount.Value > total / 2f)
            ResolveVote(false);
    }

    // ── 結果確定 ─────────────────────────────────────────────
    private void ResolveVote(bool timeout)
    {
        if (!_voteActive.Value) return;

        bool approved = _approveCount.Value > _rejectCount.Value;
        _voteActive.Value = false;

        AnnounceVoteResultClientRpc(approved, _approveCount.Value, _rejectCount.Value);

        if (approved)
        {
            Debug.Log("[ReturnVote] 帰還賛成多数。帰還開始！");
            GameServices.Expedition?.ReturnToBase(true);
        }
        else
        {
            Debug.Log("[ReturnVote] 帰還否決。遠征続行。");
        }
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void AnnounceVoteResultClientRpc(bool approved, int approve, int reject)
    {
        if (_voteText != null)
            _voteText.text = approved
                ? $"帰還賛成！ ({approve} 対 {reject})"
                : $"帰還否決。 ({approve} 対 {reject})";

        StartCoroutine(HidePanelAfterDelay(3f));
    }

    // ── タイマー UI ───────────────────────────────────────────
    [Rpc(SendTo.ClientsAndHost)]
    private void UpdateTimerClientRpc(float remaining)
    {
        if (_timerText == null) return;
        int s = Mathf.CeilToInt(remaining);
        _timerText.text = $"投票終了まで: {s}秒";
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
        int total = GetAliveSurvivorCount();
        _voteText.text = $"帰還投票中　賛成: {_approveCount.Value} / 反対: {_rejectCount.Value} / 全{total}人\n[Y] 賛成　[N] 反対";
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
    private static int GetAliveSurvivorCount()
    {
        if (PlayerHealthSystem.RegisteredPlayers == null) return 1;
        int count = 0;
        foreach (var p in PlayerHealthSystem.RegisteredPlayers)
            if (!p.IsDead) count++;
        return Mathf.Max(1, count);
    }
}
