using System.Collections.Generic;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;
using UnityEngine.Localization.Tables;

/// <summary>
/// Извлекает ключ локализации из Unity <see cref="LocalizeStringEvent"/>.
/// </summary>
public static class LocalizedTextKeyResolver
{
    private static readonly Dictionary<long, string> KeyIdCache = new Dictionary<long, string>();

    public static string FromLocalizeStringEvent(LocalizeStringEvent localizeEvent)
    {
        if (localizeEvent == null)
            return string.Empty;

        var entryRef = localizeEvent.StringReference.TableEntryReference;
        if (!string.IsNullOrEmpty(entryRef.Key))
            return entryRef.Key;

        if (entryRef.KeyId == 0)
            return string.Empty;

        if (KeyIdCache.TryGetValue(entryRef.KeyId, out string cached))
            return cached;

        try
        {
            if (!LocalizationSettings.InitializationOperation.IsDone)
                return string.Empty;

            StringTable table = LocalizationSettings.StringDatabase.GetTable(LocalizationManager.StringTableCollectionName);
            if (table == null)
                return string.Empty;

            StringTableEntry entry = table.GetEntry(entryRef.KeyId);
            if (entry == null || string.IsNullOrEmpty(entry.Key))
                return string.Empty;

            KeyIdCache[entryRef.KeyId] = entry.Key;
            return entry.Key;
        }
        catch
        {
            return string.Empty;
        }
    }
}
