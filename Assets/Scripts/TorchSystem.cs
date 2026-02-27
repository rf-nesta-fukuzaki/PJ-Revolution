using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// たいまつの燃料管理・光の制御・明滅エフェクトを担当する。
/// </summary>
public class TorchSystem : MonoBehaviour
{
    // ─────────────── Inspector ───────────────

    [Header("燃料設定")]
    [Tooltip("たいまつの最大燃料量 (デフォルト: 100)")]
    [SerializeField] private float maxFuel = 100f;

    [Tooltip("点灯中の毎秒燃料消費量")]
    [SerializeField] private float fuelConsumptionRate = 5f;

    [Header("光の設定")]
    [Tooltip("燃料満タン時の光強度 (Light.intensity の最大値)")]
    [SerializeField] private float maxIntensity = 3f;

    [Tooltip("燃料満タン時の光の到達範囲 (Light.range の最大値)")]
    [SerializeField] private float maxRange = 10f;

    [Header("明滅設定")]
    [Tooltip("明滅を開始する燃料の残量比率。0.2 = 残量20%以下で開始")]
    [Range(0f, 1f)]
    [SerializeField] private float flickerThreshold = 0.2f;

    [Tooltip("明滅の切り替わり間隔の最小値 (秒)")]
    [SerializeField] private float flickerIntervalMin = 0.05f;

    [Tooltip("明滅の切り替わり間隔の最大値 (秒)")]
    [SerializeField] private float flickerIntervalMax = 0.20f;

    [Tooltip("明滅時の光強度倍率の下限 (0.0～1.0)。小さいほど暗くなる")]
    [Range(0f, 1f)]
    [SerializeField] private float flickerIntensityMin = 0.5f;

    // ─────────────── 公開プロパティ ───────────────

    /// <summary>最大燃料量</summary>
    public float MaxFuel => maxFuel;

    /// <summary>燃料消費速度</summary>
    public float FuelConsumptionRate => fuelConsumptionRate;

    // ─────────────── Events ───────────────

    /// <summary>燃料残量が変化したときに発火する。引数は燃料割合 (0.0〜1.0)。</summary>
    public event Action<float> OnFuelChanged;

    /// <summary>たいまつの点灯/消灯状態が切り替わったときに発火する。引数は点灯中なら true。</summary>
    public event Action<bool> OnTorchToggled;

    // ─────────────── Properties ───────────────

    /// <summary>現在の燃料割合 (0.0〜1.0)</summary>
    public float FuelRatio => maxFuel > 0f ? currentFuel / maxFuel : 0f;

    /// <summary>現在たいまつが点灯しているか</summary>
    public bool IsLit => isLit;

    // ─────────────── Internal ───────────────

    private Light torchLight;
    private float currentFuel;
    private bool isLit = true;
    private bool isFlickering = false;
    private Coroutine flickerCoroutine;

    /// <summary>true のとき Update() 内の燃料消費ロジックをスキップする。</summary>
    private bool externalControl = false;

    // ─────────────── Unity Lifecycle ───────────────

    private void Awake()
    {
        torchLight = GetComponentInChildren<Light>();
        currentFuel = maxFuel;
    }

    private void Start()
    {
        ApplyLightParameters(FuelRatio);
        OnFuelChanged?.Invoke(FuelRatio);
    }

    private void Update()
    {
        if (externalControl) return;

        if (!isLit) return;

        currentFuel = Mathf.Max(0f, currentFuel - fuelConsumptionRate * Time.deltaTime);

        bool shouldFlicker = FuelRatio <= flickerThreshold && currentFuel > 0f;
        if (shouldFlicker && !isFlickering)
            flickerCoroutine = StartCoroutine(FlickerCoroutine());
        else if (!shouldFlicker && isFlickering)
            StopFlicker();

        if (!isFlickering)
            ApplyLightParameters(FuelRatio);

        OnFuelChanged?.Invoke(FuelRatio);

        if (currentFuel <= 0f)
            SetLit(false);
    }

    // ─────────────── Light 制御 ───────────────

    private void ApplyLightParameters(float ratio)
    {
        torchLight.intensity = maxIntensity * ratio;
        torchLight.range     = maxRange * ratio;
    }

    // ─────────────── 明滅コルーチン ───────────────

    private IEnumerator FlickerCoroutine()
    {
        isFlickering = true;

        while (isLit && FuelRatio <= flickerThreshold && currentFuel > 0f)
        {
            float baseIntensity = maxIntensity * FuelRatio;
            float multiplier    = UnityEngine.Random.Range(flickerIntensityMin, 1f);

            torchLight.intensity = baseIntensity * multiplier;
            torchLight.range     = maxRange * FuelRatio * multiplier;

            float wait = UnityEngine.Random.Range(flickerIntervalMin, flickerIntervalMax);
            yield return new WaitForSeconds(wait);
        }

        isFlickering = false;
    }

    private void StopFlicker()
    {
        if (flickerCoroutine != null)
        {
            StopCoroutine(flickerCoroutine);
            flickerCoroutine = null;
        }
        isFlickering = false;
    }

    // ─────────────── オフライン公開 API ───────────────

    /// <summary>たいまつの点灯/消灯を切り替える。</summary>
    public void ToggleTorch()
    {
        if (!isLit && currentFuel <= 0f) return;
        SetLit(!isLit);
    }

    private void SetLit(bool lit)
    {
        if (isLit == lit) return;
        isLit = lit;
        torchLight.enabled = lit;
        if (!lit) StopFlicker();
        OnTorchToggled?.Invoke(isLit);
    }

    /// <summary>燃料を補充する。</summary>
    public void RefillFuel(float amount)
    {
        if (amount <= 0f) return;
        currentFuel = Mathf.Min(maxFuel, currentFuel + amount);
        if (!isLit && currentFuel > 0f) SetLit(true);
        OnFuelChanged?.Invoke(FuelRatio);
        Debug.Log($"[TorchSystem] 燃料を補充しました。+{amount:F1} → 残量 {currentFuel:F1} / {maxFuel}");
    }

    // ─────────────── 外部状態適用 API ───────────────

    /// <summary>
    /// 外部から燃料と点灯状態を直接適用する。
    /// 初回呼び出し時に externalControl = true になり、
    /// 以降 Update() 内のローカル燃料消費ロジックは停止する。
    /// </summary>
    public void ApplyNetworkState(float fuel, bool litState)
    {
        externalControl = true;

        bool prevLit = isLit;
        currentFuel  = fuel;
        isLit        = litState;
        torchLight.enabled = litState;

        // 消灯時は明滅を即停止
        if (!litState && isFlickering)
            StopFlicker();

        // 明滅コルーチンの開始/停止管理
        bool shouldFlicker = FuelRatio <= flickerThreshold && currentFuel > 0f && litState;
        if (shouldFlicker && !isFlickering)
            flickerCoroutine = StartCoroutine(FlickerCoroutine());
        else if (!shouldFlicker && isFlickering)
            StopFlicker();

        // 明滅中でなければ通常の光パラメータを適用
        if (!isFlickering)
            ApplyLightParameters(FuelRatio);

        // UI 等が購読しているイベントを発火
        OnFuelChanged?.Invoke(FuelRatio);
        if (prevLit != litState)
            OnTorchToggled?.Invoke(isLit);
    }
}
