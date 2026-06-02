using UnityEngine.Localization.Settings;

/// <summary>
/// Текст по ключу: сначала LocalizationBase (Unity), затем strings_*.json, затем fallback.
/// </summary>
public static class LocalizedTextResolver
{
    public static string Resolve(string key, string[] formatArgs, string fallback)
    {
        if (string.IsNullOrEmpty(key))
            return FormatFallback(fallback, formatArgs);

        if (!CanUseUnityLocalization())
            return ResolveFromJsonOrFallback(key, formatArgs, fallback);

        string localized = TryUnityTable(key);

        if (!LocalizationStringUtility.IsValidTranslation(localized, key))
            localized = ResolveFromJsonOrFallback(key, formatArgs, fallback);

        if (LocalizationStringUtility.IsValidTranslation(localized, key))
            return ApplyFormatArgs(localized, formatArgs);

        return FormatFallback(fallback, formatArgs);
    }

    private static string ResolveFromJsonOrFallback(string key, string[] formatArgs, string fallback)
    {
        if (LocalizationManager.Instance != null)
        {
            string json = LocalizationManager.Instance.T(key, formatArgs);
            if (LocalizationStringUtility.IsValidTranslation(json, key))
                return ApplyFormatArgs(json, formatArgs);
        }

        return FormatFallback(fallback, formatArgs);
    }

    private static bool CanUseUnityLocalization()
    {
        if (!LocalizationManager.IsUnityLocalizationAvailable)
            return false;

        try
        {
            if (LocalizationSettings.InitializationOperation is { IsDone: false })
                return false;

            if (LocalizationSettings.AvailableLocales == null ||
                LocalizationSettings.AvailableLocales.Locales == null ||
                LocalizationSettings.AvailableLocales.Locales.Count == 0)
                return false;

            if (LocalizationSettings.SelectedLocale == null)
                return false;

            return LocalizationSettings.StringDatabase != null;
        }
        catch
        {
            return false;
        }
    }

    private static string ApplyFormatArgs(string text, string[] formatArgs)
    {
        if (formatArgs == null || formatArgs.Length == 0)
            return text;

        try
        {
            return string.Format(text, formatArgs);
        }
        catch
        {
            return text;
        }
    }

    private static string TryUnityTable(string key)
    {
        if (!CanUseUnityLocalization())
            return key;

        try
        {
            string value = LocalizationSettings.StringDatabase.GetLocalizedString(
                LocalizationManager.StringTableCollectionName,
                key);

            if (LocalizationStringUtility.IsValidTranslation(value, key))
                return value;
        }
        catch
        {
            // ignored
        }

        return key;
    }

    private static string FormatFallback(string fallback, string[] formatArgs)
    {
        string text = fallback ?? string.Empty;
        if (formatArgs == null || formatArgs.Length == 0)
            return text;

        try
        {
            return string.Format(text, formatArgs);
        }
        catch
        {
            return text;
        }
    }
}
