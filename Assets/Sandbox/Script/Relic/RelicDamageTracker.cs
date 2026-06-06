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

    private RelicBase    _relic;
    private RelicCarrier _carrier;

    private void Awake()
    {
        _relic   = GetComponent<RelicBase>();
        _carrier = GetComponent<RelicCarrier>();
    }

    private void OnEnable()
    {
        _relic.OnDamaged     += HandleDamaged;
        _relic.OnRelicBroken += HandleBroken;
    }

    private void OnDisable()
    {
        _relic.OnDamaged     -= HandleDamaged;
        _relic.OnRelicBroken -= HandleBroken;
    }

    // ── 破壊の帰責（GDD §12.3 — 遺物破壊 -50pt/個） ──────────
    private void HandleBroken(RelicBase relic)
    {
        // 運搬中ダメージを最も与えたプレイヤーへ破壊ペナルティを帰責する。
        // 純粋な環境破壊（誰も運搬中にダメージを与えていない）なら帰責者なし＝ペナルティなし。
        int topId = GetTopDamageDealer();
        if (topId < 0) return;
        GameServices.Score?.RecordRelicDestroyed(topId);
    }

    // ── ダメージ記録 ─────────────────────────────────────────
    private void HandleDamaged(float damage, float currentHp)
    {
        // 帰責は「運搬中に受けたダメージ」に限定する。置いた後に転がって落下した／
        // 落石や環境ダメージを受けた分まで、最後の運搬者へ加算し続ける誤帰責を防ぐ。
        if (_carrier == null || !_carrier.IsBeingCarried) return;

        int playerId = _carrier.LastCarrierPlayerId;
        if (playerId < 0) return;

        _damageByPlayer.TryGetValue(playerId, out float existing);
        _damageByPlayer[playerId] = existing + damage;

        // 個人スコアの遺物ダメージ・ペナルティ／「遺物クラッシャー」称号へ転送する。
        // 従来この転送が無く、RelicDamageTracker のデータは死蔵されていた。
        GameServices.Score?.RecordRelicDamage(playerId, damage);
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
