using TMPro;
using UnityEngine;
using UnityEngine.Localization.Components;

/// <summary>
/// Подпись UI с ключом из LocalizationManager (JSON + Unity Localization).
/// Заменяет <see cref="LocalizeStringEvent"/> на том же объекте.
/// </summary>
[DisallowMultipleComponent]
public class SettingsLocalizedText : MonoBehaviour
{
    [SerializeField] private TMP_Text label;
    [SerializeField] private string localizationKey;

    private void Awake()
    {
        LocalizeStringEvent legacy = GetComponent<LocalizeStringEvent>();
        if (legacy != null)
            MigrateFrom(legacy);
    }

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

        Refresh();
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    public void SetKey(string key)
    {
        localizationKey = key;
        Refresh();
    }

    public void MigrateFrom(LocalizeStringEvent localizeEvent)
    {
        if (localizeEvent == null)
            return;

        if (string.IsNullOrEmpty(localizationKey))
            localizationKey = LocalizedTextKeyResolver.FromLocalizeStringEvent(localizeEvent);

        if (label == null)
            label = ResolveTextTarget(localizeEvent);

        localizeEvent.enabled = false;
        Refresh();
    }

    public static void MigrateAllLocalizeStringEvents()
    {
        LocalizeStringEvent[] events = UnityEngine.Object.FindObjectsByType<LocalizeStringEvent>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (LocalizeStringEvent localizeEvent in events)
        {
            if (localizeEvent == null)
                continue;

            SettingsLocalizedText localized = localizeEvent.GetComponent<SettingsLocalizedText>();
            if (localized == null)
                localized = localizeEvent.gameObject.AddComponent<SettingsLocalizedText>();

            localized.MigrateFrom(localizeEvent);
        }
    }

    public void Refresh()
    {
        if (label == null || string.IsNullOrEmpty(localizationKey))
            return;

        label.text = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.T(localizationKey)
            : localizationKey;
    }

    private static TMP_Text ResolveTextTarget(LocalizeStringEvent localizeEvent)
    {
        if (localizeEvent.TryGetComponent(out TMP_Text onSelf))
            return onSelf;

        return localizeEvent.GetComponentInChildren<TMP_Text>(true);
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        Refresh();
    }
}
