using System;
using UnityEngine;

/// <summary>
/// プレイヤーのサバイバルステータス（HP / 酸素 / 空腹）を管理する。
/// シングルプレイ版（MonoBehaviour）。NetworkSurvivalStats.cs の移植版。
///
/// [設計メモ]
///   - Oxygen / Hunger は Update で毎秒 decayXxx ずつ自動減少する。
///   - Oxygen または Hunger が 0 になると毎秒 damageRate だけ Health を削る。
///   - Health <= 0 で IsDowned = true に遷移し OnDowned イベントを発火する。
///   - 回復は ApplyStatModification で行う。
///   - Y座標が depthThreshold を下回るごとに酸素消費が depthOxygenMultiplier 倍に増加する。
/// </summary>
public class SurvivalStats : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("減少速度 (毎秒)")]
    [Tooltip("酸素の毎秒減少量（基本値）")]
    [SerializeField] private float decayOxygen = 2f;

    [Tooltip("空腹度の毎秒減少量")]
    [SerializeField] private float decayHunger = 1f;

    [Tooltip("酸素または空腹度が 0 の時の毎秒 HP ダメージ")]
    [SerializeField] private float damageRate = 5f;

    [Header("深層酸素加速設定")]
    [Tooltip("深層酸素加速が発動する Y座標の閾値（この値より低いと加速する。例: -20 なら地下20m以下）")]
    [SerializeField] private float depthThreshold = -20f;

    [Tooltip("深層での酸素消費倍率（例: 1.5 なら通常の1.5倍速で酸素が減る）")]
    [Range(1f, 5f)]
    [SerializeField] private float depthOxygenMultiplier = 1.5f;

    // ─────────────── ステータス値 ───────────────

    /// <summary>現在 HP (0〜100)。</summary>
    public float Health { get; private set; } = 100f;

    /// <summary>現在 酸素量 (0〜100)。</summary>
    public float Oxygen { get; private set; } = 100f;

    /// <summary>現在 空腹値 (0〜100)。</summary>
    public float Hunger { get; private set; } = 100f;

    /// <summary>HP が 0 になったとき true に遷移する。</summary>
    public bool IsDowned { get; private set; } = false;

    // ─────────────── Events ───────────────

    /// <summary>HP が 0 になり IsDowned = true に変化したとき発火する。</summary>
    public event Action OnDowned;

    /// <summary>Health が変化したとき発火する。引数は (prev, current)。</summary>
    public event Action<float, float> OnHealthChanged;

    /// <summary>Oxygen が変化したとき発火する。引数は (prev, current)。</summary>
    public event Action<float, float> OnOxygenChanged;

    /// <summary>Hunger が変化したとき発火する。引数は (prev, current)。</summary>
    public event Action<float, float> OnHungerChanged;

    /// <summary>IsDowned が変化したとき発火する。引数は (prev, current)。</summary>
    public event Action<bool, bool> OnIsDownedChanged;

    // ─────────────── Update ───────────────

    private void Update()
    {
        if (IsDowned) return;

        float dt = Time.deltaTime;

        // 深層酸素加速: Y座標が depthThreshold を下回る場合は酸素消費を加速する
        float currentDecayOxygen = decayOxygen;
        if (transform.position.y < depthThreshold)
            currentDecayOxygen *= depthOxygenMultiplier;

        // Oxygen / Hunger を毎フレーム減少
        if (Oxygen > 0f)
            SetOxygen(Mathf.Max(0f, Oxygen - currentDecayOxygen * dt));

        if (Hunger > 0f)
            SetHunger(Mathf.Max(0f, Hunger - decayHunger * dt));

        // Oxygen または Hunger が尽きている場合は HP にダメージ
        if (Oxygen <= 0f || Hunger <= 0f)
        {
            SetHealth(Mathf.Max(0f, Health - damageRate * dt));

            if (Health <= 0f && !IsDowned)
                SetDowned(true);
        }
    }

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// ステータスを変更する。
    /// ResourceItem.Interact() など各インタラクション処理から呼ぶ。
    /// </summary>
    public void ApplyStatModification(StatType type, float amount)
    {
        switch (type)
        {
            case StatType.Health:
                SetHealth(Mathf.Clamp(Health + amount, 0f, 100f));
                break;
            case StatType.Oxygen:
                SetOxygen(Mathf.Clamp(Oxygen + amount, 0f, 100f));
                break;
            case StatType.Hunger:
                SetHunger(Mathf.Clamp(Hunger + amount, 0f, 100f));
                break;
        }

        // HP が回復してダウン状態が解除できる場合は復活
        if (IsDowned && Health > 0f)
            SetDowned(false);
    }

    // ─────────────── 内部セッター（イベント発火付き） ───────────────

    private void SetHealth(float value)
    {
        float prev = Health;
        Health = value;
        if (!Mathf.Approximately(prev, value))
            OnHealthChanged?.Invoke(prev, value);
    }

    private void SetOxygen(float value)
    {
        float prev = Oxygen;
        Oxygen = value;
        if (!Mathf.Approximately(prev, value))
            OnOxygenChanged?.Invoke(prev, value);
    }

    private void SetHunger(float value)
    {
        float prev = Hunger;
        Hunger = value;
        if (!Mathf.Approximately(prev, value))
            OnHungerChanged?.Invoke(prev, value);
    }

    private void SetDowned(bool value)
    {
        bool prev = IsDowned;
        IsDowned = value;
        OnIsDownedChanged?.Invoke(prev, value);
        if (value) OnDowned?.Invoke();
    }
}

/// <summary>ApplyStatModification で指定するステータス種別。</summary>
public enum StatType
{
    Health,
    Oxygen,
    Hunger,
}
