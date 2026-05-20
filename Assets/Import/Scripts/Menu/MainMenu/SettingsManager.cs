using System;
using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using ElmanGameDevTools.PlayerSystem;

[Serializable]
public class GameSettings
{
    public float mouseSensitivity = 2f;
    public int language;

    public int qualityLevel;
    public int resolutionWidth;
    public int resolutionHeight;
    public bool fullscreen;
    public float fieldOfView = 60f;
    public bool vSync = true;

    public float masterVolume = 1f;
    public float musicVolume = 1f;
    public float sfxVolume = 1f;
}

public enum SettingsTab
{
    Game = 0,
    Graphics = 1,
    Audio = 2
}

/// <summary>
/// UI настроек (реализация: <see cref="SettingsPanelUi"/>).
/// </summary>
public interface ISettingsMenuUi
{
    void NotifySettingsMenuOpened();
    void NotifySettingsMenuClosed();
}

/// <summary>
/// Сохранение и применение настроек (синглтон на объекте SettingsData / SoundManager).
/// </summary>
public class SettingsManager : MonoBehaviour
{
    private static SettingsManager _dataHost;

    public static SettingsManager Instance
    {
        get
        {
            if (_dataHost != null)
                return _dataHost;

            SettingsManager[] managers = FindObjectsByType<SettingsManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < managers.Length; i++)
            {
                if (managers[i] != null && managers[i].IsDataHost)
                {
                    _dataHost = managers[i];
                    return _dataHost;
                }
            }

            if (!Application.isPlaying)
                return null;

            GameObject go = new GameObject("SettingsManager");
            _dataHost = go.AddComponent<SettingsManager>();
            _dataHost._isDataHost = true;
            DontDestroyOnLoad(go);
            _dataHost.InitPersistence();
            return _dataHost;
        }
    }

    public bool IsDataHost => _isDataHost;

    public event Action OnSettingsApplied;
    public event Action OnSettingsSaved;

    private GameSettings _currentSettings;
    private GameSettings _previousSettings;
    private string _settingsPath;
    private bool _isDataHost;

    [Header("Audio")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam = "MusicVolume";
    [SerializeField] private string sfxVolumeParam = "SfxVolume";

    private void Awake()
    {
        if (HasSiblingSettingsMenuUi())
            return;

        if (_dataHost != null && _dataHost != this)
        {
            Destroy(gameObject);
            return;
        }

        _dataHost = this;
        _isDataHost = true;

        if (transform.parent != null)
            transform.SetParent(null, true);
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
        InitPersistence();
    }

    private void OnDestroy()
    {
        if (_dataHost == this)
            _dataHost = null;
    }

    private void OnEnable()
    {
        if (_isDataHost)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (_isDataHost)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (_currentSettings == null)
            return;

        StartCoroutine(ApplyGameplayAfterSceneLoad());
    }

    private IEnumerator ApplyGameplayAfterSceneLoad()
    {
        GameSettings settings = GetCurrentSettings();

        for (int i = 0; i < 120; i++)
        {
            if (GameplaySettingsUtility.TryApplyToPlayerInScene(settings))
            {
                OnSettingsApplied?.Invoke();
                yield break;
            }

            yield return null;
        }

        EnsurePlayerApplier();
        OnSettingsApplied?.Invoke();
    }

    private void OnApplicationQuit()
    {
        if (_isDataHost)
            SaveSettings();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && _isDataHost)
            SaveSettings();
    }

    public void ConfigureAudioMixer(AudioMixer mixer, string masterParam, string musicParam, string sfxParam)
    {
        if (mixer != null)
            audioMixer = mixer;
        if (!string.IsNullOrEmpty(masterParam))
            masterVolumeParam = masterParam;
        if (!string.IsNullOrEmpty(musicParam))
            musicVolumeParam = musicParam;
        if (!string.IsNullOrEmpty(sfxParam))
            sfxVolumeParam = sfxParam;

        if (!_isDataHost)
        {
            _isDataHost = true;
            _dataHost = this;
            if (_currentSettings == null)
                InitPersistence();
        }

        if (_currentSettings != null)
            ApplyAudioSettings();
    }

    private bool HasSiblingSettingsMenuUi()
    {
        MonoBehaviour[] behaviours = GetComponents<MonoBehaviour>();
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] != null && behaviours[i] is ISettingsMenuUi)
                return true;
        }

        return false;
    }

    private void InitPersistence()
    {
        _settingsPath = Path.Combine(Application.persistentDataPath, "gameSettings.json");
        LoadSettings();
    }

    public void SaveSettings()
    {
        if (_currentSettings == null)
            _currentSettings = CreateDefaultSettings();

        File.WriteAllText(_settingsPath, JsonUtility.ToJson(_currentSettings, true));
        OnSettingsSaved?.Invoke();
    }

    public void LoadSettings()
    {
        _settingsPath ??= Path.Combine(Application.persistentDataPath, "gameSettings.json");

        try
        {
            if (File.Exists(_settingsPath))
            {
                _currentSettings = JsonUtility.FromJson<GameSettings>(File.ReadAllText(_settingsPath));
                if (_currentSettings == null)
                    _currentSettings = CreateDefaultSettings();
                else
                    MigrateLegacySettings();
            }
            else
            {
                _currentSettings = CreateDefaultSettings();
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning($"SettingsManager: не удалось прочитать настройки, используем значения по умолчанию. {e.Message}");
            _currentSettings = CreateDefaultSettings();
        }

        ApplySettings();
    }

    private static GameSettings CreateDefaultSettings()
    {
        return new GameSettings
        {
            mouseSensitivity = 2f,
            language = LocalizationManager.Instance != null
                ? (int)LocalizationManager.Instance.GetLanguage()
                : (int)GameLanguage.Ru,
            qualityLevel = QualitySettings.GetQualityLevel(),
            resolutionWidth = Screen.width,
            resolutionHeight = Screen.height,
            fullscreen = Screen.fullScreen,
            fieldOfView = 60f,
            vSync = QualitySettings.vSyncCount > 0,
            masterVolume = 1f,
            musicVolume = 1f,
            sfxVolume = 1f
        };
    }

    private void MigrateLegacySettings()
    {
        if (_currentSettings.fieldOfView <= 0f)
            _currentSettings.fieldOfView = 60f;
        if (_currentSettings.mouseSensitivity <= 0f)
            _currentSettings.mouseSensitivity = 2f;

        if (_currentSettings.resolutionWidth <= 0 || _currentSettings.resolutionHeight <= 0)
        {
            _currentSettings.resolutionWidth = Mathf.Max(320, Screen.width);
            _currentSettings.resolutionHeight = Mathf.Max(240, Screen.height);
        }
    }

    public GameSettings GetCurrentSettings()
    {
        if (_currentSettings == null)
            _currentSettings = CreateDefaultSettings();
        return _currentSettings;
    }

    public void UpdateSetting<T>(string settingName, T value)
    {
        if (_currentSettings == null)
            _currentSettings = CreateDefaultSettings();

        var field = typeof(GameSettings).GetField(settingName);
        if (field != null && field.FieldType == typeof(T))
            field.SetValue(_currentSettings, value);
    }

    public void OnSettingsChanged() => SaveSettings();

    public void ApplyAudioSettingsImmediate() => ApplyAudioSettings();

    public void ApplyGameplayImmediate() => ApplyGameplay();

    public void SavePreviousSettings()
    {
        if (_currentSettings == null)
            return;
        _previousSettings = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(_currentSettings));
    }

    public void RestorePreviousSettings()
    {
        if (_previousSettings == null)
            return;
        _currentSettings = JsonUtility.FromJson<GameSettings>(JsonUtility.ToJson(_previousSettings));
        ApplySettings();
    }

    public GameSettings GetPreviousSettings() => _previousSettings;

    public void ApplySettings()
    {
        if (_currentSettings == null)
            return;

        ApplyLanguage();
        ApplyGraphics();
        ApplyAudioSettings();
        ApplyGameplay();
    }

    private void ApplyLanguage()
    {
        if (LocalizationManager.Instance == null)
            return;

        var lang = (GameLanguage)Mathf.Clamp(_currentSettings.language, 0, 2);
        if (LocalizationManager.Instance.GetLanguage() != lang)
            LocalizationManager.Instance.SetLanguage(lang);
    }

    private void ApplyGraphics()
    {
        int quality = Mathf.Clamp(_currentSettings.qualityLevel, 0, Mathf.Max(0, QualitySettings.names.Length - 1));
        QualitySettings.SetQualityLevel(quality);
        QualitySettings.vSyncCount = _currentSettings.vSync ? 1 : 0;

        int width = Mathf.Max(320, _currentSettings.resolutionWidth);
        int height = Mathf.Max(240, _currentSettings.resolutionHeight);
        _currentSettings.resolutionWidth = width;
        _currentSettings.resolutionHeight = height;

        Screen.SetResolution(width, height, _currentSettings.fullscreen);
    }

    private void ApplyAudioSettings()
    {
        AudioListener.volume = _currentSettings.masterVolume;
        ApplyMixerVolume(masterVolumeParam, _currentSettings.masterVolume);
        ApplyMixerVolume(musicVolumeParam, _currentSettings.musicVolume);
        ApplyMixerVolume(sfxVolumeParam, _currentSettings.sfxVolume);
    }

    private void ApplyGameplay()
    {
        EnsurePlayerApplier();
        GameplaySettingsUtility.TryApplyToPlayerInScene(GetCurrentSettings());
        OnSettingsApplied?.Invoke();
    }

    private static void EnsurePlayerApplier()
    {
        PlayerController player = FindFirstObjectByType<PlayerController>();
        if (player == null)
            return;

        if (player.GetComponent<PlayerGameplaySettingsApplier>() == null)
            player.gameObject.AddComponent<PlayerGameplaySettingsApplier>();
    }

    private void ApplyMixerVolume(string parameterName, float linearVolume)
    {
        if (audioMixer == null || string.IsNullOrEmpty(parameterName))
            return;

        float db = Mathf.Log10(Mathf.Clamp(linearVolume, 0.0001f, 1f)) * 20f;
        audioMixer.SetFloat(parameterName, db);
    }
}
