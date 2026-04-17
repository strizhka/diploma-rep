using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    [Header("Панель")]
    [SerializeField] private GameObject _settingsPanel;

    [Header("Разрешение")]
    [SerializeField] private TMP_Dropdown _resolutionDropdown;

    [Header("Полный экран")]
    [SerializeField] private Toggle _fullscreenToggle;

    [Header("Громкость")]
    [SerializeField] private Slider _volumeSlider;
    [SerializeField] private TextMeshProUGUI _volumeText;

    [Header("Яркость")]
    [SerializeField] private Slider _brightnessSlider;
    [SerializeField] private TextMeshProUGUI _brightnessText;

    [Header("Яркость (Post Processing)")]
    [Tooltip("Global Volume для управления яркостью. Если нет — яркость не регулируется.")]
    [SerializeField] private Volume _globalVolume;

    private Resolution[] _resolutions;
    private UnityEngine.Rendering.Universal.ColorAdjustments _colorAdjustments;

    private const string PrefVolume = "Settings_Volume";
    private const string PrefBrightness = "Settings_Brightness";
    private const string PrefResolution = "Settings_Resolution";
    private const string PrefFullscreen = "Settings_Fullscreen";

    private void Start()
    {
        SetupResolutions();
        SetupFullscreen();
        SetupVolume();
        SetupBrightness();

        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);
    }

    public void Open()
    {
        if (_settingsPanel != null)
            _settingsPanel.SetActive(true);
    }

    public void Close()
    {
        PlayerPrefs.Save();

        if (_settingsPanel != null)
            _settingsPanel.SetActive(false);
    }

    private void SetupResolutions()
    {
        if (_resolutionDropdown == null) return;

        _resolutions = Screen.resolutions;
        _resolutionDropdown.ClearOptions();

        var options = new List<string>();
        int current = 0;

        for (int i = 0; i < _resolutions.Length; i++)
        {
            var r = _resolutions[i];
            options.Add($"{r.width} x {r.height} @ {r.refreshRateRatio.value:F0}Hz");

            if (r.width == Screen.currentResolution.width &&
                r.height == Screen.currentResolution.height)
                current = i;
        }

        _resolutionDropdown.AddOptions(options);

        int saved = PlayerPrefs.GetInt(PrefResolution, current);
        _resolutionDropdown.value = saved;
        _resolutionDropdown.RefreshShownValue();

        _resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);
    }

    private void SetupFullscreen()
    {
        if (_fullscreenToggle == null) return;

        bool saved = PlayerPrefs.GetInt(PrefFullscreen, Screen.fullScreen ? 1 : 0) == 1;
        _fullscreenToggle.isOn = saved;
        _fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);
    }

    private void SetupVolume()
    {
        if (_volumeSlider == null) return;

        float saved = PlayerPrefs.GetFloat(PrefVolume, 1f);
        _volumeSlider.value = saved;
        AudioListener.volume = saved;
        UpdateVolumeText(saved);

        _volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
    }

    private void SetupBrightness()
    {
        if (_brightnessSlider == null) return;

        if (_globalVolume != null)
            _globalVolume.profile.TryGet(out _colorAdjustments);

        float saved = PlayerPrefs.GetFloat(PrefBrightness, 0f);
        _brightnessSlider.minValue = -1f;
        _brightnessSlider.maxValue = 1f;
        _brightnessSlider.value = saved;
        ApplyBrightness(saved);
        UpdateBrightnessText(saved);

        _brightnessSlider.onValueChanged.AddListener(OnBrightnessChanged);
    }

    public void OnResolutionChanged(int index)
    {
        if (index < 0 || index >= _resolutions.Length) return;

        var r = _resolutions[index];
        Screen.SetResolution(r.width, r.height, Screen.fullScreen);
        PlayerPrefs.SetInt(PrefResolution, index);
    }

    public void OnFullscreenChanged(bool isFullscreen)
    {
        Screen.fullScreen = isFullscreen;
        PlayerPrefs.SetInt(PrefFullscreen, isFullscreen ? 1 : 0);
    }

    public void OnVolumeChanged(float value)
    {
        AudioListener.volume = value;
        UpdateVolumeText(value);
        PlayerPrefs.SetFloat(PrefVolume, value);
    }

    public void OnBrightnessChanged(float value)
    {
        ApplyBrightness(value);
        UpdateBrightnessText(value);
        PlayerPrefs.SetFloat(PrefBrightness, value);
    }

    private void ApplyBrightness(float value)
    {
        if (_colorAdjustments != null)
        {
            _colorAdjustments.postExposure.Override(value);
        }
    }

    private void UpdateVolumeText(float value)
    {
        if (_volumeText != null)
            _volumeText.text = $"{Mathf.RoundToInt(value * 100)}%";
    }

    private void UpdateBrightnessText(float value)
    {
        if (_brightnessText != null)
            _brightnessText.text = $"{value:+0.0;-0.0;0}";
    }
}
