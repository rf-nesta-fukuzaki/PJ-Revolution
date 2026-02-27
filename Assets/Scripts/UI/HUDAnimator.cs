using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD ゲージの視覚演出を担当するコンポーネント。
///
/// [責務]
///   UIManager とは別に、TorchSystem / SurvivalStats のイベントを購読して
///   コルーチンベースの演出（点滅・フラッシュ・ビネット）を再生する。
///
/// [演出一覧]
///   - HP 減少時        : HP バー赤点滅 + 画面端に赤ビネット（alpha 往復）
///   - 酸素 20% 以下   : 酸素バー点滅 + 画面全体うっすら青オーバーレイ
///   - 空腹 20% 以下   : 空腹バー点滅
///   - 燃料 20% 以下   : 燃料バーオレンジ点滅
///   - 被ダメージ時    : 画面全体を 0.2 秒で赤フラッシュ
///
/// [バインド]
///   BindToPlayer() をプレイヤー初期化時に呼ぶ。
/// </summary>
public class HUDAnimator : MonoBehaviour
{
    // ─────────────── Inspector: HUD バー参照 ───────────────

    [Header("HUD バー参照")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider oxygenSlider;
    [SerializeField] private Slider hungerSlider;
    [SerializeField] private Slider fuelSlider;

    // ─────────────── Inspector: 演出用 Image ───────────────

    [Header("演出用 Image (全画面オーバーレイ)")]
    [SerializeField] private Image damageVignetteImage;
    [SerializeField] private Image oxygenOverlayImage;

    // ─────────────── Inspector: 演出パラメータ ───────────────

    [Header("演出パラメータ")]
    [Range(0f, 0.5f)]
    [SerializeField] private float warningThreshold = 0.2f;
    [SerializeField] private float blinkFrequency = 3f;
    [Range(0f, 1f)]
    [SerializeField] private float overlayMaxAlpha = 0.35f;
    [SerializeField] private float vignetteFrequency = 2f;
    [SerializeField] private float damageFlashDuration = 0.2f;
    [SerializeField] private float damageDetectThreshold = 5f;

    // ─────────────── 内部状態 ───────────────

    private SurvivalStats _stats;
    private TorchSystem   _torchSystem;

    private float _lastHealth = 100f;

    private Coroutine _healthBlinkCoroutine;
    private Coroutine _oxygenCoroutine;
    private Coroutine _hungerBlinkCoroutine;
    private Coroutine _fuelBlinkCoroutine;
    private Coroutine _damageFlashCoroutine;

    private Color _healthBarColor = Color.red;
    private Color _oxygenBarColor = new Color(0.2f, 0.6f, 1f);
    private Color _hungerBarColor = new Color(0.8f, 0.5f, 0.1f);
    private Color _fuelBarColor   = new Color(1f, 0.6f, 0f);

    // ─────────────── 公開 API ───────────────

    /// <summary>
    /// プレイヤーの SurvivalStats と TorchSystem をバインドする。
    /// null を渡すと購読を解除する。
    /// </summary>
    public void BindToPlayer(SurvivalStats stats, TorchSystem torchSystem)
    {
        UnbindAll();

        _stats       = stats;
        _torchSystem = torchSystem;

        if (_stats != null)
        {
            _lastHealth = _stats.Health;
            _stats.OnHealthChanged += OnHealthChanged;
            _stats.OnOxygenChanged += OnOxygenChanged;
            _stats.OnHungerChanged += OnHungerChanged;

            EvaluateOxygen(_stats.Oxygen);
            EvaluateHunger(_stats.Hunger);
        }

        if (_torchSystem != null)
        {
            _torchSystem.OnFuelChanged += OnFuelChanged;
            EvaluateFuel(_torchSystem.FuelRatio);
        }
    }

    /// <summary>毎フレームポーリングして演出を評価する（イベントの補助）。</summary>
    public void PollStandalone()
    {
        if (_stats == null) return;

        float hp = _stats.Health;
        float ox = _stats.Oxygen;
        float hu = _stats.Hunger;

        if (_lastHealth - hp >= damageDetectThreshold)
            TriggerDamageFlash();

        _lastHealth = hp;

        EvaluateOxygen(ox);
        EvaluateHunger(hu);
    }

    // ─────────────── Unity Lifecycle ───────────────

    private void OnDisable()
    {
        UnbindAll();
    }

    // ─────────────── イベントハンドラ ───────────────

    private void OnHealthChanged(float prev, float current)
    {
        if (prev - current >= damageDetectThreshold)
            TriggerDamageFlash();

        _lastHealth = current;
    }

    private void OnOxygenChanged(float prev, float current) => EvaluateOxygen(current);
    private void OnHungerChanged(float prev, float current) => EvaluateHunger(current);

    private void OnFuelChanged(float fuelRatio) => EvaluateFuel(fuelRatio);

    // ─────────────── 演出評価 ───────────────

    private void EvaluateOxygen(float value)
    {
        bool isLow = (value / 100f) <= warningThreshold;

        if (isLow && _oxygenCoroutine == null)
        {
            _oxygenCoroutine = StartCoroutine(
                BlinkBarAndOverlay(oxygenSlider, _oxygenBarColor, oxygenOverlayImage,
                    () => ((_stats?.Oxygen ?? 100f) / 100f) > warningThreshold));
        }
        else if (!isLow && _oxygenCoroutine != null)
        {
            StopCoroutine(_oxygenCoroutine);
            _oxygenCoroutine = null;
            ResetBarColor(oxygenSlider, _oxygenBarColor);
            SetOverlayAlpha(oxygenOverlayImage, 0f);
        }
    }

    private void EvaluateHunger(float value)
    {
        bool isLow = (value / 100f) <= warningThreshold;

        if (isLow && _hungerBlinkCoroutine == null)
        {
            _hungerBlinkCoroutine = StartCoroutine(
                BlinkBar(hungerSlider, _hungerBarColor,
                    () => ((_stats?.Hunger ?? 100f) / 100f) > warningThreshold));
        }
        else if (!isLow && _hungerBlinkCoroutine != null)
        {
            StopCoroutine(_hungerBlinkCoroutine);
            _hungerBlinkCoroutine = null;
            ResetBarColor(hungerSlider, _hungerBarColor);
        }
    }

    private void EvaluateFuel(float fuelRatio)
    {
        bool isLow = fuelRatio <= warningThreshold;

        if (isLow && _fuelBlinkCoroutine == null)
        {
            _fuelBlinkCoroutine = StartCoroutine(
                BlinkBar(fuelSlider, _fuelBarColor,
                    () => (_torchSystem?.FuelRatio ?? 1f) > warningThreshold));
        }
        else if (!isLow && _fuelBlinkCoroutine != null)
        {
            StopCoroutine(_fuelBlinkCoroutine);
            _fuelBlinkCoroutine = null;
            ResetBarColor(fuelSlider, _fuelBarColor);
        }
    }

    // ─────────────── 演出コルーチン ───────────────

    private IEnumerator BlinkBar(Slider slider, Color baseColor, System.Func<bool> stopCondition)
    {
        if (slider == null) yield break;
        Image fill = slider.fillRect?.GetComponent<Image>();
        if (fill == null) yield break;

        while (true)
        {
            if (stopCondition != null && stopCondition()) break;

            float t = Mathf.PingPong(Time.time * blinkFrequency, 1f);
            fill.color = Color.Lerp(baseColor, Color.white, t);
            yield return null;
        }

        fill.color = baseColor;
    }

    private IEnumerator BlinkBarAndOverlay(Slider slider, Color baseColor, Image overlay, System.Func<bool> stopCondition)
    {
        Image fill = slider?.fillRect?.GetComponent<Image>();

        while (true)
        {
            if (stopCondition != null && stopCondition()) break;

            float t = Mathf.PingPong(Time.time * blinkFrequency, 1f);
            if (fill != null) fill.color = Color.Lerp(baseColor, Color.white, t);

            float vt = Mathf.PingPong(Time.time * vignetteFrequency, 1f);
            SetOverlayAlpha(overlay, vt * overlayMaxAlpha);

            yield return null;
        }

        if (fill != null) fill.color = baseColor;
        SetOverlayAlpha(overlay, 0f);
    }

    private void TriggerDamageFlash()
    {
        if (_damageFlashCoroutine != null)
            StopCoroutine(_damageFlashCoroutine);

        _damageFlashCoroutine = StartCoroutine(DamageFlashCoroutine());
    }

    private IEnumerator DamageFlashCoroutine()
    {
        if (damageVignetteImage == null) yield break;

        Color flashColor = new Color(1f, 0f, 0f, overlayMaxAlpha);
        damageVignetteImage.color = flashColor;

        float elapsed = 0f;
        while (elapsed < damageFlashDuration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / damageFlashDuration;
            float alpha = Mathf.Lerp(overlayMaxAlpha, 0f, t);
            SetOverlayAlpha(damageVignetteImage, alpha);
            yield return null;
        }

        SetOverlayAlpha(damageVignetteImage, 0f);
        _damageFlashCoroutine = null;
    }

    // ─────────────── 内部ユーティリティ ───────────────

    private void UnbindAll()
    {
        if (_stats != null)
        {
            _stats.OnHealthChanged -= OnHealthChanged;
            _stats.OnOxygenChanged -= OnOxygenChanged;
            _stats.OnHungerChanged -= OnHungerChanged;
            _stats = null;
        }

        if (_torchSystem != null)
        {
            _torchSystem.OnFuelChanged -= OnFuelChanged;
            _torchSystem = null;
        }

        StopAllCoroutines();

        ResetBarColor(healthSlider, _healthBarColor);
        ResetBarColor(oxygenSlider, _oxygenBarColor);
        ResetBarColor(hungerSlider, _hungerBarColor);
        ResetBarColor(fuelSlider,   _fuelBarColor);
        SetOverlayAlpha(damageVignetteImage, 0f);
        SetOverlayAlpha(oxygenOverlayImage,  0f);

        _healthBlinkCoroutine = null;
        _oxygenCoroutine      = null;
        _hungerBlinkCoroutine = null;
        _fuelBlinkCoroutine   = null;
        _damageFlashCoroutine = null;
    }

    private static void ResetBarColor(Slider slider, Color color)
    {
        if (slider == null) return;
        Image fill = slider.fillRect?.GetComponent<Image>();
        if (fill != null) fill.color = color;
    }

    private static void SetOverlayAlpha(Image image, float alpha)
    {
        if (image == null) return;
        Color c = image.color;
        c.a = alpha;
        image.color = c;
    }
}
