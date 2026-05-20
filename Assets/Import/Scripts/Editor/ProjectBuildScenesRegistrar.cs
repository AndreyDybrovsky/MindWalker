#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEngine;

/// <summary>
/// Синхронизирует игровые сцены с Editor Build Settings и Shared Build Profile (Unity 6).
/// </summary>
[InitializeOnLoad]
static class ProjectBuildScenesRegistrar
{
    private const string ScenesRoot = "Assets/Import/Scenes";

    private static readonly string[] SceneOrder =
    {
        $"{ScenesRoot}/MainMenu.unity",
        $"{ScenesRoot}/Main.unity",
        $"{ScenesRoot}/Victory.unity",
        $"{ScenesRoot}/Levels/Autism.unity",
        $"{ScenesRoot}/Levels/Depression.unity",
        $"{ScenesRoot}/Levels/Level4.unity",
        $"{ScenesRoot}/Levels/Level5.unity",
        $"{ScenesRoot}/Levels/PTSD.unity",
        $"{ScenesRoot}/Levels/PTSD in Danger.unity",
        $"{ScenesRoot}/Levels/Twice.unity",
    };

    static ProjectBuildScenesRegistrar()
    {
        EditorApplication.delayCall += RegisterScenesIfNeeded;
    }

    private static void RegisterScenesIfNeeded()
    {
        if (!EditorUtilities.CanRunEditorMaintenance())
            return;

        try
        {
            EditorBuildSettingsScene[] desired = CollectDesiredScenes();
            if (desired.Length == 0)
                return;

            if (!ScenesEqual(EditorBuildSettings.scenes, desired))
                EditorBuildSettings.scenes = desired;

            BuildProfile sharedProfile = AssetDatabase.LoadAssetAtPath<BuildProfile>(
                "Library/BuildProfiles/SharedProfile.asset");

            if (sharedProfile != null
                && !sharedProfile.overrideGlobalScenes
                && !ScenesEqual(sharedProfile.scenes, desired))
            {
                sharedProfile.scenes = desired;
                EditorUtility.SetDirty(sharedProfile);
            }
        }
        catch
        {
            // Package Manager / Build Profile может быть ещё не готов.
        }
    }

    private static EditorBuildSettingsScene[] CollectDesiredScenes()
    {
        var ordered = new List<EditorBuildSettingsScene>();

        foreach (string path in SceneOrder)
        {
            if (!System.IO.File.Exists(path))
                continue;

            ordered.Add(new EditorBuildSettingsScene(path, true));
        }

        string[] guids = AssetDatabase.FindAssets("t:Scene", new[] { ScenesRoot });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (ordered.Any(s => s.path == path))
                continue;

            ordered.Add(new EditorBuildSettingsScene(path, true));
        }

        return ordered.ToArray();
    }

    private static bool ScenesEqual(EditorBuildSettingsScene[] current, EditorBuildSettingsScene[] desired)
    {
        if (current == null || desired == null)
            return false;

        if (current.Length != desired.Length)
            return false;

        for (int i = 0; i < current.Length; i++)
        {
            if (current[i].path != desired[i].path || current[i].enabled != desired[i].enabled)
                return false;
        }

        return true;
    }
}
#endif
