using System;

/// <summary>
/// Проверка ответа Unity Localization (в т.ч. «No translation found…»).
/// </summary>
public static class LocalizationStringUtility
{
    public static bool IsValidTranslation(string value, string key)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        if (value == key)
            return false;

        if (value.StartsWith("No translation found", StringComparison.OrdinalIgnoreCase))
            return false;

        return true;
    }
}
