using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.SceneManagement;

public enum GameLanguage
{
    Ru = 0,
    En = 1,
    Es = 2
}

public class LocalizationManager : MonoBehaviour
{
    private static LocalizationManager instance;
    public static LocalizationManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<LocalizationManager>();
                if (instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("LocalizationManager");
                    instance = go.AddComponent<LocalizationManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    public event Action<GameLanguage> OnLanguageChanged;

    private const string PlayerPrefsKey = "Language";
    // Имя String Table Collection из Assets/Localization/Tables
    public const string StringTableCollectionName = "LocalizationBase";

    /// <summary>Unity Localization (Addressables/.asset) успешно инициализирована.</summary>
    public static bool IsUnityLocalizationAvailable { get; private set; }

    private GameLanguage currentLanguage;
    private readonly Dictionary<GameLanguage, Dictionary<string, string>> tables =
        new Dictionary<GameLanguage, Dictionary<string, string>>();

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        if (transform.parent != null)
            transform.SetParent(null, true);
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);

        currentLanguage = LoadInitialLanguage();
        ApplyLocale(currentLanguage);
    }

    private void Start()
    {
        StartCoroutine(InitializeLocalizationWhenReady());
    }

    private IEnumerator InitializeLocalizationWhenReady()
    {
        var initOperation = LocalizationSettings.InitializationOperation;
        if (!initOperation.IsDone)
            yield return initOperation;

        if (initOperation.Status != UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            IsUnityLocalizationAvailable = false;
            Debug.LogWarning(
                "LocalizationManager: Unity Localization не инициализировалась (часто Git LFS: таблицы .asset не скачаны). " +
                "Используем только strings_*.json из Resources.");
            yield break;
        }

        IsUnityLocalizationAvailable = true;
        ApplyLocale(currentLanguage);
        SettingsLocalizedText.MigrateAllLocalizeStringEvents();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!IsUnityLocalizationAvailable)
            return;

        SettingsLocalizedText.MigrateAllLocalizeStringEvents();
    }

    private void OnSelectedLocaleChanged(Locale locale)
    {
        // Если локаль поменяли не через этот менеджер (например, через стандартный Locale dropdown),
        // всё равно уведомим UI.
        currentLanguage = LocaleToLanguage(locale);
        PlayerPrefs.SetInt(PlayerPrefsKey, (int)currentLanguage);
        PlayerPrefs.Save();
        OnLanguageChanged?.Invoke(currentLanguage);
    }

    private GameLanguage LoadInitialLanguage()
    {
        if (PlayerPrefs.HasKey(PlayerPrefsKey))
        {
            return (GameLanguage)PlayerPrefs.GetInt(PlayerPrefsKey, (int)GameLanguage.Ru);
        }

        switch (Application.systemLanguage)
        {
            case SystemLanguage.Russian:
            case SystemLanguage.Ukrainian:
            case SystemLanguage.Belarusian:
                return GameLanguage.Ru;
            case SystemLanguage.Spanish:
                return GameLanguage.Es;
            default:
                return GameLanguage.En;
        }
    }

    public GameLanguage GetLanguage()
    {
        return currentLanguage;
    }

    public void SetLanguage(GameLanguage language)
    {
        bool languageChanged = currentLanguage != language;
        if (!languageChanged)
            return;

        currentLanguage = language;
        PlayerPrefs.SetInt(PlayerPrefsKey, (int)language);
        PlayerPrefs.Save();

        ApplyLocale(language);
        OnLanguageChanged?.Invoke(language);
    }

    private void ApplyLocale(GameLanguage language)
    {
        // Важно: LocalizationSettings может быть еще в процессе инициализации
        // (например, при старте сцены). Мы применяем локаль синхронно, если доступно.
        if (!IsUnityLocalizationAvailable &&
            LocalizationSettings.InitializationOperation is { IsDone: true } &&
            LocalizationSettings.InitializationOperation.Status !=
            UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationStatus.Succeeded)
        {
            EnsureTableLoaded(language);
            return;
        }

        if (LocalizationSettings.AvailableLocales == null ||
            LocalizationSettings.AvailableLocales.Locales == null ||
            LocalizationSettings.AvailableLocales.Locales.Count == 0)
        {
            EnsureTableLoaded(language);
            return;
        }

        string code = LanguageToLocaleCode(language);
        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(code);
        if (locale == null)
        {
            // На случай, если локаль зарегистрирована под другим кодом
            foreach (var l in LocalizationSettings.AvailableLocales.Locales)
            {
                if (l != null && l.Identifier.Code == code)
                {
                    locale = l;
                    break;
                }
            }
        }

        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
        }

        // Подгружаем таблицу ключ->строка для выбранного языка из Resources
        EnsureTableLoaded(language);
    }

    public string T(string key)
    {
        if (string.IsNullOrEmpty(key))
            return "";

        // Сначала пытаемся взять из наших JSON-таблиц в Resources
        EnsureTableLoaded(currentLanguage);
        if (tables.TryGetValue(currentLanguage, out var dict) &&
            dict != null &&
            dict.TryGetValue(key, out var value) &&
            !string.IsNullOrEmpty(value))
        {
            return value;
        }

        // Фолбэк — Unity Localization только если локали доступны
        if (IsUnityLocalizationAvailable && LocalizationSettings.SelectedLocale != null)
        {
            try
            {
                string locValue = LocalizationSettings.StringDatabase.GetLocalizedString(StringTableCollectionName, key);
                if (LocalizationStringUtility.IsValidTranslation(locValue, key))
                    return locValue;
            }
            catch { }
        }

        return key;
    }

    public string T(string key, params object[] args)
    {
        if (string.IsNullOrEmpty(key))
            return "";

        try
        {
            // Сначала пробуем форматирование поверх нашей строки
            string format = T(key);
            if (args == null || args.Length == 0)
                return format;
            return string.Format(format, args);
        }
        catch
        {
            // fallback к ручному format поверх ключа/строки
            string format = T(key);
            if (args == null || args.Length == 0)
                return format;
            try { return string.Format(format, args); } catch { return format; }
        }
    }

    private string LanguageToLocaleCode(GameLanguage language)
    {
        switch (language)
        {
            case GameLanguage.Ru:
                return "ru";
            case GameLanguage.Es:
                return "es-ES";
            default:
                return "en-US";
        }
    }

    private GameLanguage LocaleToLanguage(Locale locale)
    {
        if (locale == null)
            return GameLanguage.En;

        string code = locale.Identifier.Code;
        if (code == "ru")
            return GameLanguage.Ru;
        if (code == "es-ES" || code == "es")
            return GameLanguage.Es;
        return GameLanguage.En;
    }

    [Serializable]
    private class LocalizationTableData
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public string value;
        }

        public List<Entry> entries = new List<Entry>();
    }

    private void EnsureTableLoaded(GameLanguage language)
    {
        if (tables.ContainsKey(language) && tables[language] != null)
            return;

        string code;
        switch (language)
        {
            case GameLanguage.Ru:
                code = "ru";
                break;
            case GameLanguage.Es:
                code = "es";
                break;
            default:
                code = "en";
                break;
        }

        TextAsset asset = Resources.Load<TextAsset>($"Localization/strings_{code}");
        if (asset == null)
        {
            tables[language] = new Dictionary<string, string>();
            return;
        }

        try
        {
            LocalizationTableData data = JsonUtility.FromJson<LocalizationTableData>(asset.text);
            var dict = new Dictionary<string, string>();
            if (data?.entries != null)
            {
                foreach (var e in data.entries)
                {
                    if (e == null || string.IsNullOrEmpty(e.key))
                        continue;
                    dict[e.key] = e.value ?? "";
                }
            }
            tables[language] = dict;
        }
        catch
        {
            tables[language] = new Dictionary<string, string>();
        }
    }
}

