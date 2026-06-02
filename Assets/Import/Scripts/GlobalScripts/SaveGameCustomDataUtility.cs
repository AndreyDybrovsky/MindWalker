using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// JSON-значения в <see cref="GameSaveData.customData"/> (ключ → строка).
/// </summary>
public static class SaveGameCustomDataUtility
{
    public static bool TryRead<T>(GameSaveData saveData, string key, out T value) where T : class
    {
        value = null;
        if (saveData == null || string.IsNullOrEmpty(key))
            return false;

        string json = FindValue(saveData, key);
        if (string.IsNullOrEmpty(json))
            return false;

        try
        {
            value = JsonUtility.FromJson<T>(json);
            return value != null;
        }
        catch
        {
            return false;
        }
    }

    public static void Write<T>(GameSaveData saveData, string key, T value) where T : class
    {
        if (saveData == null || string.IsNullOrEmpty(key) || value == null)
            return;

        if (saveData.customData == null)
            saveData.customData = new List<GameSaveData.CustomDataPair>();

        string json = JsonUtility.ToJson(value);
        for (int i = 0; i < saveData.customData.Count; i++)
        {
            if (saveData.customData[i].key != key)
                continue;

            saveData.customData[i].value = json;
            return;
        }

        saveData.customData.Add(new GameSaveData.CustomDataPair { key = key, value = json });
    }

    private static string FindValue(GameSaveData saveData, string key)
    {
        if (saveData.customData == null)
            return null;

        for (int i = 0; i < saveData.customData.Count; i++)
        {
            GameSaveData.CustomDataPair pair = saveData.customData[i];
            if (pair != null && pair.key == key)
                return pair.value;
        }

        return null;
    }
}
