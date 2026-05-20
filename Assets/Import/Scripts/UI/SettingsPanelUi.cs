using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI панели настроек (вкладки, слайдеры). Панель вкладок не скрывается при переключении.
/// </summary>
public class SettingsPanelUi : MonoBehaviour, ISettingsMenuUi
{
    [Header("Корень меню настроек")]
    [SerializeField] private GameObject settingsMenuRoot;
    [SerializeField] private Graphic settingsBackdropGraphic;

    [Header("Панель вкладок (всегда видна в Settings)")]
    [SerializeField] private GameObject tabBarRoot;
    [SerializeField] private float tabFadeDuration = 0.18f;
    [SerializeField] private Button tabGameButton;
    [SerializeField] private Button tabGraphicsButton;
    [SerializeField] private Button tabAudioButton;
    [Header("Заголовок секции над вкладками (виден только активная)")]
    [SerializeField] private TMP_Text tabGameLabel;
    [SerializeField] private TMP_Text tabGraphicsLabel;
    [SerializeField] private TMP_Text tabAudioLabel;

    [Header("Контент вкладок")]
    [SerializeField] private GameObject gameTabPanel;
    [SerializeField] private GameObject graphicsTabPanel;
    [SerializeField] private GameObject audioTabPanel;

    [Header("Игра")]
    [SerializeField] private Slider mouseSensitivitySlider;
    [SerializeField] private TMP_Dropdown languageDropdown;

    [Header("Графика")]
    [SerializeField] private TMP_Dropdown qualityDropdown;
    [SerializeField] private TMP_Dropdown resolutionDropdown;
    [SerializeField] private Toggle fullscreenToggle;
    [SerializeField] private Slider fovSlider;
    [SerializeField] private Toggle vSyncToggle;

    [Header("Звук")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;

    [Header("Диапазоны")]
    [SerializeField] private float minFov = 60f;
    [SerializeField] private float maxFov = 110f;
    [SerializeField] private float minMouseSensitivity = 0.5f;
    [SerializeField] private float maxMouseSensitivity = 8f;

    [Header("Доп. подписи (кнопки вкладок, пауз-меню)")]
    [SerializeField] private LocalizedUiLabel[] additionalLocalizedLabels;

    private readonly List<Resolution> _uniqueResolutions = new List<Resolution>();
    private SettingsTab _activeTab = SettingsTab.Game;
    private bool _tabTransitionBusy;
    private bool _uiBound;
    private bool _isMenuOpen;

    private void Awake()
    {
        if (settingsMenuRoot == null)
            settingsMenuRoot = gameObject;
    }

    private void Start()
    {
        BindUiOnce();
    }

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnExternalLanguageChanged;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnExternalLanguageChanged;
    }

    public void NotifySettingsMenuOpened()
    {
        if (_isMenuOpen)
            return;

        _isMenuOpen = true;
        EnsureBackdropPassthrough();

        if (tabBarRoot != null)
            tabBarRoot.SetActive(true);

        RefreshAllUi();
        RefreshSectionTitle();

        if (HasContentPanels())
            SelectTabInstant(_activeTab);
    }

    public void NotifySettingsMenuClosed()
    {
        _isMenuOpen = false;
        _tabTransitionBusy = false;
    }

    private void EnsureBackdropPassthrough()
    {
        if (settingsBackdropGraphic != null)
            settingsBackdropGraphic.raycastTarget = false;
    }

    private void BindUiOnce()
    {
        if (_uiBound)
            return;

        _uiBound = true;

        if (mouseSensitivitySlider != null)
        {
            mouseSensitivitySlider.minValue = minMouseSensitivity;
            mouseSensitivitySlider.maxValue = maxMouseSensitivity;
            mouseSensitivitySlider.onValueChanged.AddListener(OnMouseSensitivityChanged);
        }

        if (languageDropdown != null)
            languageDropdown.onValueChanged.AddListener(OnLanguageChanged);

        if (qualityDropdown != null)
            qualityDropdown.onValueChanged.AddListener(OnQualityChanged);

        if (resolutionDropdown != null)
            resolutionDropdown.onValueChanged.AddListener(OnResolutionChanged);

        if (fullscreenToggle != null)
            fullscreenToggle.onValueChanged.AddListener(OnFullscreenChanged);

        if (fovSlider != null)
        {
            fovSlider.minValue = minFov;
            fovSlider.maxValue = maxFov;
            fovSlider.onValueChanged.AddListener(OnFovChanged);
        }

        if (vSyncToggle != null)
            vSyncToggle.onValueChanged.AddListener(OnVSyncChanged);

        if (masterVolumeSlider != null)
            masterVolumeSlider.onValueChanged.AddListener(OnMasterVolumeChanged);

        if (musicVolumeSlider != null)
            musicVolumeSlider.onValueChanged.AddListener(OnMusicVolumeChanged);

        if (sfxVolumeSlider != null)
            sfxVolumeSlider.onValueChanged.AddListener(OnSfxVolumeChanged);

        if (tabGameButton != null)
            tabGameButton.onClick.AddListener(() => SelectTabAsync(SettingsTab.Game));
        if (tabGraphicsButton != null)
            tabGraphicsButton.onClick.AddListener(() => SelectTabAsync(SettingsTab.Graphics));
        if (tabAudioButton != null)
            tabAudioButton.onClick.AddListener(() => SelectTabAsync(SettingsTab.Audio));

        InitializeQualityDropdown();
        InitializeResolutionDropdown();

        GameLanguage lang = (GameLanguage)SettingsManager.Instance.GetCurrentSettings().language;
        InitializeLanguageDropdown(LanguageToDropdownIndex(lang));
    }

    private void OnExternalLanguageChanged(GameLanguage _)
    {
        if (!_isMenuOpen)
            return;

        RefreshAllUi();
        RefreshSectionTitle();
    }

    public void RefreshAllUi()
    {
        GameSettings settings = SettingsManager.Instance.GetCurrentSettings();
        if (settings == null)
            return;

        SetSliderWithoutNotify(mouseSensitivitySlider, settings.mouseSensitivity);
        SetSliderWithoutNotify(fovSlider, settings.fieldOfView);
        SetToggleWithoutNotify(fullscreenToggle, settings.fullscreen);
        SetToggleWithoutNotify(vSyncToggle, settings.vSync);
        SetSliderWithoutNotify(masterVolumeSlider, settings.masterVolume);
        SetSliderWithoutNotify(musicVolumeSlider, settings.musicVolume);
        SetSliderWithoutNotify(sfxVolumeSlider, settings.sfxVolume);

        if (qualityDropdown != null && settings.qualityLevel >= 0 && settings.qualityLevel < qualityDropdown.options.Count)
            qualityDropdown.SetValueWithoutNotify(settings.qualityLevel);

        if (languageDropdown != null)
            languageDropdown.SetValueWithoutNotify(LanguageToDropdownIndex((GameLanguage)settings.language));

        if (resolutionDropdown != null)
        {
            int resIndex = FindResolutionIndex(settings.resolutionWidth, settings.resolutionHeight);
            if (resIndex >= 0)
                resolutionDropdown.SetValueWithoutNotify(resIndex);
        }

        RefreshLocalizedTextsInScene();
    }

    private void InitializeQualityDropdown()
    {
        if (qualityDropdown == null)
            return;

        qualityDropdown.ClearOptions();
        for (int i = 0; i < QualitySettings.names.Length; i++)
        {
            string key = $"Settings.Quality.{i}";
            qualityDropdown.options.Add(new TMP_Dropdown.OptionData(T(key, QualitySettings.names[i])));
        }

        qualityDropdown.RefreshShownValue();
    }

    private void InitializeResolutionDropdown()
    {
        if (resolutionDropdown == null)
            return;

        resolutionDropdown.ClearOptions();
        _uniqueResolutions.Clear();

        HashSet<string> seen = new HashSet<string>();
        foreach (Resolution res in Screen.resolutions)
        {
            string key = $"{res.width}x{res.height}";
            if (!seen.Add(key))
                continue;

            _uniqueResolutions.Add(res);
            resolutionDropdown.options.Add(new TMP_Dropdown.OptionData($"{res.width} × {res.height}"));
        }

        resolutionDropdown.RefreshShownValue();
    }

    private void InitializeLanguageDropdown(int? selectedIndex = null)
    {
        if (languageDropdown == null)
            return;

        int index = selectedIndex ?? languageDropdown.value;
        if (index < 0 || index > 2)
            index = LanguageToDropdownIndex((GameLanguage)SettingsManager.Instance.GetCurrentSettings().language);

        languageDropdown.ClearOptions();
        languageDropdown.options.Add(new TMP_Dropdown.OptionData(T("Settings.Language.Ru", "Русский")));
        languageDropdown.options.Add(new TMP_Dropdown.OptionData(T("Settings.Language.En", "English")));
        languageDropdown.options.Add(new TMP_Dropdown.OptionData(T("Settings.Language.Es", "Español")));
        languageDropdown.SetValueWithoutNotify(index);
        languageDropdown.RefreshShownValue();
    }

    private static int LanguageToDropdownIndex(GameLanguage lang)
    {
        return lang switch
        {
            GameLanguage.Ru => 0,
            GameLanguage.Es => 2,
            _ => 1
        };
    }

    private void RefreshSectionTitle()
    {
        SetSectionTitleLabel(tabGameLabel, SettingsTab.Game, "Settings.Tab.Game", "Игра");
        SetSectionTitleLabel(tabGraphicsLabel, SettingsTab.Graphics, "Settings.Tab.Graphics", "Графика");
        SetSectionTitleLabel(tabAudioLabel, SettingsTab.Audio, "Settings.Tab.Audio", "Звук");
        RefreshTabButtonLabels();
        RefreshAdditionalLocalizedLabels();
    }

    private void RefreshTabButtonLabels()
    {
        SetButtonLabel(tabGameButton, "Settings.Tab.Game", "Игра");
        SetButtonLabel(tabGraphicsButton, "Settings.Tab.Graphics", "Графика");
        SetButtonLabel(tabAudioButton, "Settings.Tab.Audio", "Звук");
    }

    private static void SetButtonLabel(Button button, string key, string fallback)
    {
        if (button == null)
            return;

        if (!button.TryGetComponent(out TMP_Text label))
            label = button.GetComponentInChildren<TMP_Text>(true);

        if (label != null)
            label.text = T(key, fallback);
    }

    private void RefreshAdditionalLocalizedLabels()
    {
        if (additionalLocalizedLabels == null)
            return;

        foreach (LocalizedUiLabel entry in additionalLocalizedLabels)
        {
            if (entry.text == null || string.IsNullOrEmpty(entry.key))
                continue;

            entry.text.text = T(entry.key, entry.fallback);
        }
    }

    private void SetSectionTitleLabel(TMP_Text label, SettingsTab tab, string key, string fallback)
    {
        if (label == null)
            return;

        label.text = T(key, fallback);
        label.gameObject.SetActive(tab == _activeTab);
    }

    private int FindResolutionIndex(int width, int height)
    {
        for (int i = 0; i < _uniqueResolutions.Count; i++)
        {
            if (_uniqueResolutions[i].width == width && _uniqueResolutions[i].height == height)
                return i;
        }

        return -1;
    }

    private void OnMouseSensitivityChanged(float value)
    {
        SettingsManager.Instance.UpdateSetting("mouseSensitivity", value);
        ApplyGameplaySettingsImmediate();
        SettingsManager.Instance.OnSettingsChanged();
    }

    private void OnLanguageChanged(int index)
    {
        GameLanguage lang = index switch
        {
            0 => GameLanguage.Ru,
            2 => GameLanguage.Es,
            _ => GameLanguage.En
        };

        SettingsManager.Instance.UpdateSetting("language", (int)lang);
        LocalizationManager.Instance?.SetLanguage(lang);
        InitializeLanguageDropdown(index);
        InitializeQualityDropdown();
        RefreshSectionTitle();
        RefreshLocalizedTextsInScene();
        SettingsManager.Instance.OnSettingsChanged();
    }

    private void OnQualityChanged(int value)
    {
        SettingsManager.Instance.UpdateSetting("qualityLevel", value);
        QualitySettings.SetQualityLevel(value);
        SettingsManager.Instance.OnSettingsChanged();
    }

    private void OnResolutionChanged(int value)
    {
        if (value < 0 || value >= _uniqueResolutions.Count)
            return;

        Resolution selected = _uniqueResolutions[value];
        SettingsManager.Instance.UpdateSetting("resolutionWidth", selected.width);
        SettingsManager.Instance.UpdateSetting("resolutionHeight", selected.height);

        bool fullscreen = fullscreenToggle != null ? fullscreenToggle.isOn : Screen.fullScreen;
        Screen.SetResolution(selected.width, selected.height, fullscreen);
        SettingsManager.Instance.OnSettingsChanged();
    }

    private void OnFullscreenChanged(bool value)
    {
        SettingsManager.Instance.UpdateSetting("fullscreen", value);
        GameSettings s = SettingsManager.Instance.GetCurrentSettings();
        Screen.SetResolution(s.resolutionWidth, s.resolutionHeight, value);
        SettingsManager.Instance.OnSettingsChanged();
    }

    private void OnFovChanged(float value)
    {
        SettingsManager.Instance.UpdateSetting("fieldOfView", value);
        ApplyGameplaySettingsImmediate();
        SettingsManager.Instance.OnSettingsChanged();
    }

    private static void ApplyGameplaySettingsImmediate()
    {
        SettingsManager.Instance.ApplyGameplayImmediate();
    }

    private void OnVSyncChanged(bool value)
    {
        SettingsManager.Instance.UpdateSetting("vSync", value);
        QualitySettings.vSyncCount = value ? 1 : 0;
        SettingsManager.Instance.OnSettingsChanged();
    }

    private void OnMasterVolumeChanged(float value)
    {
        SettingsManager.Instance.UpdateSetting("masterVolume", value);
        SettingsManager.Instance.ApplyAudioSettingsImmediate();
        SettingsManager.Instance.OnSettingsChanged();
    }

    private void OnMusicVolumeChanged(float value)
    {
        SettingsManager.Instance.UpdateSetting("musicVolume", value);
        SettingsManager.Instance.ApplyAudioSettingsImmediate();
        SettingsManager.Instance.OnSettingsChanged();
    }

    private void OnSfxVolumeChanged(float value)
    {
        SettingsManager.Instance.UpdateSetting("sfxVolume", value);
        SettingsManager.Instance.ApplyAudioSettingsImmediate();
        SettingsManager.Instance.OnSettingsChanged();
    }

    public void SelectTabAsync(SettingsTab tab)
    {
        if (!HasContentPanels() || _tabTransitionBusy)
            return;

        if (tab == _activeTab)
        {
            SelectTabInstant(tab);
            return;
        }

        RunTabTransitionAsync(tab);
    }

    private async void RunTabTransitionAsync(SettingsTab tab)
    {
        _tabTransitionBusy = true;
        try
        {
            CanvasGroup hideCg = GetContentCanvasGroup(_activeTab);
            CanvasGroup showCg = GetContentCanvasGroup(tab);

            if (hideCg != null)
            {
                hideCg.blocksRaycasts = false;
                hideCg.interactable = false;
            }

            if (showCg != null)
            {
                showCg.gameObject.SetActive(true);
                showCg.blocksRaycasts = false;
                showCg.interactable = false;
            }

            await UiMenuTransitions.CrossFadePanels(hideCg, showCg, tabFadeDuration);

            SetContentPanelActive(_activeTab, false);
            _activeTab = tab;

            if (showCg != null)
            {
                showCg.alpha = 1f;
                showCg.blocksRaycasts = true;
                showCg.interactable = true;
            }

            UpdateTabButtonStates();
            RefreshSectionTitle();
        }
        finally
        {
            _tabTransitionBusy = false;
        }
    }

    private void SelectTabInstant(SettingsTab tab)
    {
        if (!HasContentPanels())
            return;

        SetContentPanelActive(SettingsTab.Game, false);
        SetContentPanelActive(SettingsTab.Graphics, false);
        SetContentPanelActive(SettingsTab.Audio, false);

        _activeTab = tab;
        GameObject panel = GetContentPanel(tab);
        if (panel == null)
            return;

        panel.SetActive(true);
        CanvasGroup cg = UiMenuTransitions.EnsureCanvasGroup(panel);
        cg.alpha = 1f;
        cg.blocksRaycasts = true;
        cg.interactable = true;
        UpdateTabButtonStates();
        RefreshSectionTitle();
    }

    private void SetContentPanelActive(SettingsTab tab, bool active)
    {
        GameObject panel = GetContentPanel(tab);
        if (panel == null || active)
            return;

        CanvasGroup cg = panel.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        panel.SetActive(false);
    }

    private bool HasContentPanels()
    {
        return gameTabPanel != null || graphicsTabPanel != null || audioTabPanel != null;
    }

    private GameObject GetContentPanel(SettingsTab tab)
    {
        return tab switch
        {
            SettingsTab.Game => gameTabPanel,
            SettingsTab.Graphics => graphicsTabPanel,
            SettingsTab.Audio => audioTabPanel,
            _ => null
        };
    }

    private CanvasGroup GetContentCanvasGroup(SettingsTab tab)
    {
        GameObject panel = GetContentPanel(tab);
        return panel != null ? UiMenuTransitions.EnsureCanvasGroup(panel) : null;
    }

    private void UpdateTabButtonStates()
    {
        SetTabButtonSelected(tabGameButton, _activeTab == SettingsTab.Game);
        SetTabButtonSelected(tabGraphicsButton, _activeTab == SettingsTab.Graphics);
        SetTabButtonSelected(tabAudioButton, _activeTab == SettingsTab.Audio);
    }

    private static void SetTabButtonSelected(Button button, bool selected)
    {
        if (button != null && button.TryGetComponent(out SettingsTabButtonStyle style))
            style.SetSelected(selected);
    }

    private static string T(string key, string fallback)
    {
        if (LocalizationManager.Instance == null)
            return fallback;

        string value = LocalizationManager.Instance.T(key);
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }

    private static void RefreshLocalizedTextsInScene()
    {
        SettingsLocalizedText[] texts = FindObjectsByType<SettingsLocalizedText>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (SettingsLocalizedText text in texts)
            text.Refresh();
    }

    private static void SetSliderWithoutNotify(Slider slider, float value)
    {
        if (slider != null)
            slider.SetValueWithoutNotify(value);
    }

    private static void SetToggleWithoutNotify(Toggle toggle, bool value)
    {
        if (toggle != null)
            toggle.SetIsOnWithoutNotify(value);
    }

    [Serializable]
    private struct LocalizedUiLabel
    {
        public TMP_Text text;
        public string key;
        public string fallback;
    }
}
