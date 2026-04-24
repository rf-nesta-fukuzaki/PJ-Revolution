using System.Collections.Generic;
using UnityEngine;
using PeakPlunder.Audio;
using PPAudioManager = PeakPlunder.Audio.AudioManager;

/// <summary>
/// GDD §14.9 — 遺物発見トリガー。
/// 遺物オブジェクトに半径 20m の SphereCollider (isTrigger) を持たせ、
/// プレイヤーが初めて範囲内に入った時にだけ発見通知を送る。
/// 視線判定は行わない（GDD: 霧の中でも検出する）。
/// </summary>
[DisallowMultipleComponent]
public class RelicDiscoveryTrigger : MonoBehaviour
{
    // GDD §14.9: 検出半径 20m。
    private const float DETECTION_RADIUS = 20f;

    [SerializeField] private string _playerTag = "Player";

    private RelicBase           _relic;
    private SphereCollider      _detector;
    // 「初めて」判定のため、通知を送ったプレイヤーの InstanceID を記録する。
    private readonly HashSet<int> _seenByPlayerInstanceIds = new();

    private void Awake()
    {
        _relic = GetComponent<RelicBase>();
        EnsureDetector();
    }

    /// <summary>
    /// トリガー用の子 SphereCollider を用意する。遺物本体の物理コライダーと区別するため
    /// 子 GameObject に isTrigger=true のコライダーを付ける。
    /// </summary>
    private void EnsureDetector()
    {
        if (_detector != null) return;

        var child = new GameObject("RelicDiscoveryDetector");
        child.transform.SetParent(transform, false);
        child.layer = gameObject.layer;

        _detector = child.AddComponent<SphereCollider>();
        _detector.isTrigger = true;
        _detector.radius    = DETECTION_RADIUS;

        // 子 GameObject 経由で OnTriggerEnter を受け取るためのリレー。
        var relay = child.AddComponent<TriggerRelay>();
        relay.Host = this;
    }

    internal void HandleTriggerEnter(Collider other)
    {
        if (_relic == null || _relic.IsDestroyed) return;
        if (other == null || !other.CompareTag(_playerTag)) return;

        // プレイヤーオブジェクト（親）の InstanceID を一意キーにする。
        int playerId = (other.attachedRigidbody != null ? other.attachedRigidbody.gameObject : other.gameObject)
            .GetInstanceID();

        if (!_seenByPlayerInstanceIds.Add(playerId)) return;   // 既に通知済み

        // GDD §15.2 — relic_discover（初発見時のチャイム）
        PPAudioManager.Instance?.PlaySE(SoundId.RelicDiscover, transform.position);

        RelicDiscoveryNotifier.Instance?.NotifyDiscovered(playerId, _relic.RelicName);
    }

    /// <summary>
    /// 子 Collider の OnTriggerEnter を親 RelicDiscoveryTrigger に中継する薄いコンポーネント。
    /// </summary>
    private sealed class TriggerRelay : MonoBehaviour
    {
        public RelicDiscoveryTrigger Host;

        private void OnTriggerEnter(Collider other)
        {
            if (Host != null) Host.HandleTriggerEnter(other);
        }
    }
}
