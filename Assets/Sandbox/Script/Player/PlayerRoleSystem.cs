using UnityEngine;

/// <summary>
/// プレイヤーの役割（<see cref="PlayerRole"/>）とパッシブ特典を管理する。
/// 特典は恒久アップグレードとは別系統の直交フックで適用し、競合しない。
///  - Vanguard: 被ダメージ軽減（PlayerHealthSystem.SetDamageResistance）
///  - Medic:    味方蘇生の速度倍率（DownedSystem が蘇生者の本値を参照）
///  - Scout:    周囲の遺物をレーダー探知（RelicDiscovery 通知）
/// PlayerHealthSystem から自動付与される。
/// </summary>
[RequireComponent(typeof(PlayerHealthSystem))]
public class PlayerRoleSystem : MonoBehaviour
{
    [SerializeField] private PlayerRole _role = PlayerRole.Scout;

    [Header("Vanguard")]
    [SerializeField, Range(0f, 0.9f)] private float _vanguardDamageResistance = 0.4f;
    [Header("Medic")]
    [SerializeField] private float _medicReviveMultiplier = 2f;
    [Header("Scout")]
    [SerializeField] private float _scoutRadarRadius   = 35f;
    [SerializeField] private float _scoutRadarInterval = 2f;

    private PlayerHealthSystem _health;
    private float _radarTimer;
    private int   _scoutDetectedCount;

    public PlayerRole Role => _role;
    public int ScoutDetectedCount => _scoutDetectedCount;

    /// <summary>Medic が他者を蘇生する際の速度倍率（非Medicは1）。DownedSystem が参照する。</summary>
    public float ReviveSpeedMultiplier => _role == PlayerRole.Medic ? Mathf.Max(1f, _medicReviveMultiplier) : 1f;

    private void Awake()
    {
        _health = GetComponent<PlayerHealthSystem>();
        ApplyRole();
    }

    public void SetRole(PlayerRole role)
    {
        _role = role;
        ApplyRole();
    }

    private void ApplyRole()
    {
        _health.SetDamageResistance(_role == PlayerRole.Vanguard ? _vanguardDamageResistance : 0f);
    }

    private void Update()
    {
        if (_role != PlayerRole.Scout) return;

        _radarTimer -= Time.deltaTime;
        if (_radarTimer <= 0f)
        {
            ScanRelics();
            _radarTimer = _scoutRadarInterval;
        }
    }

    private void ScanRelics()
    {
        int count = 0;
        var relics = Object.FindObjectsByType<RelicBase>(FindObjectsSortMode.None);
        foreach (var r in relics)
        {
            if (r == null || r.IsDestroyed) continue;
            if (Vector3.Distance(r.transform.position, transform.position) > _scoutRadarRadius) continue;
            count++;
            GameServices.RelicDiscovery?.NotifyDiscovered(GetInstanceID(), r.RelicName);
        }
        _scoutDetectedCount = count;
    }
}
