using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GDD §9.3 — 誰が遺物にダメージを与えたかを記録するトラッカー。
/// リザルト画面の「遺物を一番ぶつけた人」称号に使用する。
/// RelicBase と同じ GameObject にアタッチする。
/// </summary>
[RequireComponent(typeof(RelicBase))]
public class RelicDamageTracker : MonoBehaviour
{
    // playerId → 累積ダメージ
    private readonly Dictionary<int, float> _damageByPlayer = new();

    private RelicBase _relic;

    private void Awake()
    {
        _relic = GetComponent<RelicBase>();
    }

    private void OnEnable()
    {
        _relic.OnDamaged += HandleDamaged;
    }

    private void OnDisable()
    {
        _relic.OnDamaged -= HandleDamaged;
    }

    // ── ダメージ記録 ─────────────────────────────────────────
    private void HandleDamaged(float damage, float currentHp)
    {
        // 最後に触れたプレイヤーIDを取得（RelicCarrier 経由）
        int playerId = GetLastTouchPlayerId();
        if (playerId < 0) return;

        _damageByPlayer.TryGetValue(playerId, out float existing);
        _damageByPlayer[playerId] = existing + damage;
    }

    /// <summary>最後に遺物に触れたプレイヤーのIDを返す。-1 = 不明。</summary>
    private int GetLastTouchPlayerId()
    {
        var carrier = GetComponent<RelicCarrier>();
        return carrier != null ? carrier.LastCarrierPlayerId : -1;
    }

    // ── クエリ ───────────────────────────────────────────────
    /// <summary>指定プレイヤーが与えた累積ダメージ。</summary>
    public float GetDamageByPlayer(int playerId)
    {
        _damageByPlayer.TryGetValue(playerId, out float v);
        return v;
    }

    /// <summary>最もダメージを与えたプレイヤーのIDを返す。</summary>
    public int GetTopDamageDealer()
    {
        int   topId  = -1;
        float topVal = 0f;
        foreach (var kv in _damageByPlayer)
        {
            if (kv.Value > topVal)
            {
                topVal = kv.Value;
                topId  = kv.Key;
            }
        }
        return topId;
    }

    public IReadOnlyDictionary<int, float> AllDamages => _damageByPlayer;
}
