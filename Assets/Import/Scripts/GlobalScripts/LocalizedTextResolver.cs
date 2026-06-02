using UnityEngine.Localization.Settings;

/// <summary>
/// Текст по ключу: сначала LocalizationBase (Unity), затем strings_*.json, затем fallback.
/// </summary>
public static class LocalizedTextResolver
{
    public static string Resolve(string key, string[] formatArgs, string fallback)
    {
        if (!string.IsNullOrEmpty(key))
        {
            string localized = TryUnityTable(key);

            if (!LocalizationStringUtility.IsValidTranslation(localized, key))
            {
                if (LocalizationManager.Instance != null)
                    localized = LocalizationManager.Instance.T(key, formatArgs);
                else
                    localized = null;
            }

            if (LocalizationStringUtility.IsValidTranslation(localized, key))
                return ApplyFormatArgs(localized, formatArgs);
        }

        return FormatFallback(fallback, formatArgs);
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
        try
        {
            if (LocalizationSettings.StringDatabase == null)
                return key;

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
