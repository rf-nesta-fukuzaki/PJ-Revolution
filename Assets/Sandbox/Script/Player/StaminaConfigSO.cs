using UnityEngine;

/// <summary>
/// GDD §3.1 — スタミナパラメータを ScriptableObject で管理する。
/// </summary>
[CreateAssetMenu(fileName = "StaminaConfig", menuName = "PeakPlunder/Player/Stamina Config")]
public sealed class StaminaConfigSO : ScriptableObject
{
    [Header("スタミナ")]
    [SerializeField] private float _maxStamina = 100f;
    [SerializeField] private float _regenRateBase = 12f;
    [SerializeField] private float _regenRateMoving = 5f;
    [SerializeField] private float _sprintDrain = 15f;
    [SerializeField] private float _climbDrain = 10f;

    [Header("高山病")]
    [SerializeField] private float _highAltitude = 2000f;
    [SerializeField] private float _altitudeDrainBonus = 5f;

    [Header("セーフゾーン")]
    [SerializeField] private float _shelterRegenMultiplier = 2f;
    [SerializeField] private float _exhaustRecoverThreshold = 25f;

    public float MaxStamina => _maxStamina;
    public float RegenRateBase => _regenRateBase;
    public float RegenRateMoving => _regenRateMoving;
    public float SprintDrain => _sprintDrain;
    public float ClimbDrain => _climbDrain;
    public float HighAltitude => _highAltitude;
    public float AltitudeDrainBonus => _altitudeDrainBonus;
    public float ShelterRegenMultiplier => _shelterRegenMultiplier;
    public float ExhaustRecoverThreshold => _exhaustRecoverThreshold;

    private void OnValidate()
    {
        _maxStamina = Mathf.Max(1f, _maxStamina);
        _exhaustRecoverThreshold = Mathf.Clamp(_exhaustRecoverThreshold, 0f, _maxStamina);
    }
}
