using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// GDD §2.2 — 帰還エリア管理。
/// BoxCollider (isTrigger=true, 10m×4m×10m), Tag: ReturnZone。
///
/// 帰還フロー：
///   1. 最初の生存者が入った瞬間 → 全プレイヤーに 120 秒カウントダウン表示開始
///   2. 全生存者がエリア内に入った → カウントダウン即終了 → Returning 遷移
///   3. 120 秒経過 → エリア外の生存者を除外したまま Returning 遷移
///
/// 遺物の持ち込み判定：
///   OnTriggerStay で遺物が帰還エリア内にあれば「持ち帰り成功」として ScoreTracker に記録。
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class ReturnZone : NetworkBehaviour
{
    // ── GDD パラメータ ────────────────────────────────────────
    private const float COUNTDOWN_SECONDS = 120f;
    private static readonly Vector3 ZONE_SIZE = new(10f, 4f, 10f);

    [Header("カウントダウン UI")]
    [SerializeField] private GameObject _countdownPanel;   // 全体 Canvas
    [SerializeField] private TextMeshProUGUI _countdownText;
    [SerializeField] private TextMeshProUGUI _statusText;

    // ── ネットワーク状態 ─────────────────────────────────────
    private readonly NetworkVariable<float> _countdownRemaining = new(
        -1f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    // ── ローカル状態 ─────────────────────────────────────────
    private readonly HashSet<int> _survivorsInZone   = new();
    private readonly HashSet<int> _relicsInZone      = new();
    private bool _countdownStarted;
    private bool _resolved;

    // ── ライフサイクル ────────────────────────────────────────
    private void Awake()
    {
        gameObject.tag = "ReturnZone";

        var col = GetComponent<BoxCollider>();
        col.isTrigger = true;
        col.size   = ZONE_SIZE;
        col.center = new Vector3(0f, ZONE_SIZE.y * 0.5f, 0f);

        if (_countdownPanel != null)
            _countdownPanel.SetActive(false);
    }

    public override void OnNetworkSpawn()
    {
        _countdownRemaining.OnValueChanged += OnCountdownChanged;
    }

    public override void OnNetworkDespawn()
    {
        _countdownRemaining.OnValueChanged -= OnCountdownChanged;
    }

    // ── トリガー判定 ─────────────────────────────────────────
    private void OnTriggerEnter(Collider other)
    {
        var health = other.GetComponentInParent<PlayerHealthSystem>();
        if (health != null && !health.IsDead)
        {
            var ghost = other.GetComponentInParent<GhostSystem>();
            if (ghost == null || !ghost.IsGhost)
            {
                _survivorsInZone.Add(health.GetInstanceID());
                TryStartCountdown();
                Debug.Log($"[ReturnZone] 生存者入場: {other.name} ({_survivorsInZone.Count}人)");
                return;
            }
        }

        var relic = other.GetComponentInParent<RelicBase>();
        if (relic != null)
        {
            _relicsInZone.Add(relic.GetInstanceID());
            Debug.Log($"[ReturnZone] 遺物がエリア内: {other.name}");
        }
    }

    private void OnTriggerExit(Collider other)
    {
        var health = other.GetComponentInParent<PlayerHealthSystem>();
        if (health != null)
            _survivorsInZone.Remove(health.GetInstanceID());

        var relic = other.GetComponentInParent<RelicBase>();
        if (relic != null)
            _relicsInZone.Remove(relic.GetInstanceID());
    }

    private void OnTriggerStay(Collider other)
    {
        // 遺物が引き続きエリア内にいることを追跡
        var relic = other.GetComponentInParent<RelicBase>();
        if (relic != null)
            _relicsInZone.Add(relic.GetInstanceID());
    }

    // ── カウントダウン開始 ────────────────────────────────────
    private void TryStartCountdown()
    {
        if (_countdownStarted || _resolved) return;

        // ホスト権威でカウントダウン開始
        if (!IsServer) { TryStartCountdownServerRpc(); return; }
        StartCountdownOnServer();
    }

    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void TryStartCountdownServerRpc() => StartCountdownOnServer();

    private void StartCountdownOnServer()
    {
        if (_countdownStarted || _resolved) return;
        _countdownStarted = true;
        _countdownRemaining.Value = COUNTDOWN_SECONDS;
        StartCoroutine(CountdownCoroutine());
        NotifyCountdownStartClientRpc();
        Debug.Log("[ReturnZone] 帰還カウントダウン開始！120秒");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void NotifyCountdownStartClientRpc()
    {
        if (_countdownPanel != null)
            _countdownPanel.SetActive(true);
        if (_statusText != null)
            _statusText.text = "帰還カウントダウン開始！全員エリア内へ！";
    }

    // ── カウントダウンコルーチン（サーバーのみ） ─────────────
    private IEnumerator CountdownCoroutine()
    {
        while (_countdownRemaining.Value > 0f && !_resolved)
        {
            yield return new WaitForSeconds(1f);
            _countdownRemaining.Value -= 1f;

            // 全生存者がエリア内に揃ったら即終了
            if (AllSurvivorsInZone())
            {
                _countdownRemaining.Value = 0f;
                Debug.Log("[ReturnZone] 全生存者集合！即時帰還");
                break;
            }
        }

        if (!_resolved)
            ResolveReturn();
    }

    // ── 帰還確定 ─────────────────────────────────────────────
    private void ResolveReturn()
    {
        if (_resolved) return;
        _resolved = true;

        // エリア内の遺物を ScoreTracker に記録
        RecordRelicsInZone();

        // 全員生還したかチェック
        bool allSurvived = AllSurvivorsInZone();

        ResolveClientRpc(allSurvived);
        GameServices.Expedition?.ReturnToBase(allSurvived);

        Debug.Log($"[ReturnZone] 帰還確定。全員生還={allSurvived}。遺物{_relicsInZone.Count}個持ち帰り");
    }

    [Rpc(SendTo.ClientsAndHost)]
    private void ResolveClientRpc(bool allSurvived)
    {
        if (_countdownPanel != null)
            _countdownPanel.SetActive(false);

        if (_statusText != null)
            _statusText.text = allSurvived ? "全員帰還！" : "帰還完了（一部未帰還）";
    }

    // ── 遺物記録 ─────────────────────────────────────────────
    private void RecordRelicsInZone()
    {
        var score = GameServices.Score;
        foreach (var relicId in _relicsInZone)
        {
            score?.RecordRelicReturned(relicId);
            Debug.Log($"[ReturnZone] 遺物 (InstanceID {relicId}) を持ち帰り成功として記録");
        }
    }

    // ── ヘルパー ─────────────────────────────────────────────
    private bool AllSurvivorsInZone()
    {
        if (PlayerHealthSystem.RegisteredPlayers == null) return true;

        int survivorCount = 0;
        foreach (var p in PlayerHealthSystem.RegisteredPlayers)
        {
            if (p.IsDead) continue;
            var ghost = p.GetComponent<GhostSystem>();
            if (ghost != null && ghost.IsGhost) continue;
            survivorCount++;
        }

        return survivorCount > 0 && _survivorsInZone.Count >= survivorCount;
    }

    // ── NetworkVariable コールバック（クライアント側 UI 更新） ─
    private void OnCountdownChanged(float _, float remaining)
    {
        if (_countdownText == null) return;
        if (remaining < 0f) { _countdownText.text = ""; return; }

        int seconds = Mathf.CeilToInt(remaining);
        _countdownText.text = $"帰還カウントダウン: {seconds}秒";
        _countdownText.color = seconds <= 30 ? Color.red : Color.white;
    }

    // ── Gizmos ───────────────────────────────────────────────
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.3f);
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(new Vector3(0f, ZONE_SIZE.y * 0.5f, 0f), ZONE_SIZE);
        Gizmos.color = new Color(0f, 1f, 0.5f, 0.8f);
        Gizmos.DrawWireCube(new Vector3(0f, ZONE_SIZE.y * 0.5f, 0f), ZONE_SIZE);
    }
}
