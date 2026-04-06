using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// オプション画面の UI 制御。
/// </summary>
public class OptionsUI : MonoBehaviour
{
    private const string KeyVolume = "MasterVolume";
    private const string KeySensitivity = "MouseSensitivity";

    [Header("Sliders")]
    [SerializeField] private Slider volumeSlider;
    [SerializeField] private Slider sensitivitySlider;

    [Header("Buttons")]
    [SerializeField] private Button applyButton;
    [SerializeField] private Button backButton;

    private void Awake()
    {
        if (applyButton != null) applyButton.onClick.AddListener(OnApply);
        if (backButton != null) backButton.onClick.AddListener(OnBack);
    }

    private void OnEnable() => LoadSettings();

    private void LoadSettings()
    {
        if (volumeSlider != null)
        {
            volumeSlider.minValue = 0f;
            volumeSlider.maxValue = 1f;
            volumeSlider.value = PlayerPrefs.GetFloat(KeyVolume, AudioListener.volume);
        }
        if (sensitivitySlider != null)
        {
            sensitivitySlider.minValue = 0.1f;
            sensitivitySlider.maxValue = 5f;
            sensitivitySlider.value = PlayerPrefs.GetFloat(KeySensitivity, 2f);
        }
    }

    private void OnApply()
    {
        if (volumeSlider != null)
        {
            AudioListener.volume = volumeSlider.value;
            PlayerPrefs.SetFloat(KeyVolume, volumeSlider.value);
        }
        if (sensitivitySlider != null)
        {
            PlayerPrefs.SetFloat(KeySensitivity, sensitivitySlider.value);
            var look = FindFirstObjectByType<FirstPersonLook>();
            look?.SetSensitivity(sensitivitySlider.value);
        }
        PlayerPrefs.Save();
    }

    private void OnBack()
    {
        var pm = FindFirstObjectByType<PauseManager>();
        pm?.SetPaused(false);
        gameObject.SetActive(false);
    }
}
