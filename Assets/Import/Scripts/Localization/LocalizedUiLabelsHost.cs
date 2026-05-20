using System;
using TMPro;
using UnityEngine;

/// <summary>
/// Локализует произвольный набор TMP-подписей по ключам LocalizationManager / strings_*.json.
/// </summary>
public class LocalizedUiLabelsHost : MonoBehaviour
{
    [Serializable]
    public struct Entry
    {
        public TMP_Text text;
        public string key;
        public string fallback;
    }

    [SerializeField] private Entry[] labels;

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

    private void OnLanguageChanged(GameLanguage _)
    {
        Refresh();
    }

    public void Refresh()
    {
        if (labels == null)
            return;

        foreach (Entry entry in labels)
        {
            if (entry.text == null || string.IsNullOrEmpty(entry.key))
                continue;

            entry.text.text = LocalizationManager.Instance != null
                ? LocalizedText(entry.key, entry.fallback)
                : entry.fallback;
        }
    }

    private static string LocalizedText(string key, string fallback)
    {
        string value = LocalizationManager.Instance.T(key);
        return string.IsNullOrEmpty(value) || value == key ? fallback : value;
    }
}
