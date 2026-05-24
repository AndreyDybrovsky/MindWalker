#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.Localization.Settings;

/// <summary>
/// Отключает устаревшие LocalizeStringEvent в редакторе (без Play Mode).
/// </summary>
[InitializeOnLoad]
static class LocalizationEditorBootstrap
{
    static LocalizationEditorBootstrap()
    {
        EditorApplication.delayCall += MigrateLegacyLocalizers;
        EditorSceneManager.sceneOpened += OnSceneOpened;
        EditorSceneManager.sceneClosing += OnSceneClosing;
    }

    static void OnSceneOpened(UnityEngine.SceneManagement.Scene scene, OpenSceneMode mode)
    {
        if (!EditorUtilities.CanRunEditorMaintenance())
            return;

        EditorApplication.delayCall += MigrateLegacyLocalizers;
    }

    static void OnSceneClosing(UnityEngine.SceneManagement.Scene scene, bool removingScene)
    {
        if (!EditorUtilities.CanRunEditorMaintenance())
            return;

        DestroyEditorSpawnedSingletons();
    }

    static void DestroyEditorSpawnedSingletons()
    {
        try
        {
            LocalizationManager[] localizationManagers = Object.FindObjectsByType<LocalizationManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (LocalizationManager manager in localizationManagers)
            {
                if (manager != null && !EditorUtility.IsPersistent(manager))
                    Object.DestroyImmediate(manager.gameObject);
            }

            SaveManager[] saveManagers = Object.FindObjectsByType<SaveManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (SaveManager manager in saveManagers)
            {
                if (manager != null && !EditorUtility.IsPersistent(manager))
                    Object.DestroyImmediate(manager.gameObject);
            }

            SettingsManager[] settingsManagers = Object.FindObjectsByType<SettingsManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (SettingsManager manager in settingsManagers)
            {
                if (manager != null
                    && manager.IsDataHost
                    && manager.gameObject.name == "SettingsManager"
                    && !EditorUtility.IsPersistent(manager))
                {
                    Object.DestroyImmediate(manager.gameObject);
                }
            }
        }
        catch
        {
            // Не прерываем repaint редактора (IMGUIContainer).
        }
    }

    static void MigrateLegacyLocalizers()
    {
        if (!EditorUtilities.CanRunEditorMaintenance())
            return;

        if (!LocalizationSettings.HasSettings)
            return;

        try
        {
            LocalizeStringEvent[] events = Object.FindObjectsByType<LocalizeStringEvent>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            foreach (LocalizeStringEvent localizeEvent in events)
            {
                if (localizeEvent == null || !localizeEvent.enabled)
                    continue;

                SettingsLocalizedText localized = localizeEvent.GetComponent<SettingsLocalizedText>();
                if (localized == null)
                    localized = localizeEvent.gameObject.AddComponent<SettingsLocalizedText>();

                localized.MigrateFrom(localizeEvent);

                if (!Application.isPlaying)
                    EditorUtility.SetDirty(localizeEvent.gameObject);
            }
        }
        catch
        {
            // LocalizeStringEvent может ещё не быть готов при старте редактора.
        }
    }
}
#endif
