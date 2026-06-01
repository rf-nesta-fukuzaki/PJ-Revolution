using UnityEngine;

/// <summary>
/// GDD §4.1 — プレイヤー HP・落下ダメージのパラメータを ScriptableObject で管理する。
/// ロジック (PlayerHealthSystem) と数値設定を分離し、バランス調整を Inspector から行える。
/// </summary>
[CreateAssetMenu(fileName = "PlayerHealthConfig", menuName = "PeakPlunder/Player/Health Config")]
public sealed class PlayerHealthConfigSO : ScriptableObject
{
    [Header("HP")]
    [SerializeField] private float _maxHp = 100f;

    [Header("Fall Damage")]
    [SerializeField] private float _fallDamageMinSpeed = 8f;
    [SerializeField] private float _fallDamageScale = 3f;

    [Header("Death")]
    [SerializeField] private float _deathY = -30f;

    public float MaxHp => _maxHp;
    public float FallDamageMinSpeed => _fallDamageMinSpeed;
    public float FallDamageScale => _fallDamageScale;
    public float DeathY => _deathY;

    private void OnValidate()
    {
        _maxHp = Mathf.Max(1f, _maxHp);
        _fallDamageMinSpeed = Mathf.Max(0f, _fallDamageMinSpeed);
        _fallDamageScale = Mathf.Max(0f, _fallDamageScale);
    }

    public bool TryValidate(out string reason)
    {
        if (_maxHp <= 0f)
        {
            reason = "maxHp は 1 以上にしてください";
            return false;
        }

        if (_fallDamageMinSpeed < 0f)
        {
            reason = "fallDamageMinSpeed は 0 以上にしてください";
            return false;
        }

        if (_fallDamageScale < 0f)
        {
            reason = "fallDamageScale は 0 以上にしてください";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}
