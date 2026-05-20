#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
static class LevelAtmosphereEditor
{
    static LevelAtmosphereEditor()
    {
        EditorApplication.delayCall += SyncFromActiveScene;
        EditorSceneManager.sceneOpened += (_, _) => EditorApplication.delayCall += SyncFromActiveScene;
    }

    static void SyncFromActiveScene()
    {
        if (!EditorUtilities.CanRunEditorMaintenance())
            return;

        LevelAtmosphere.Apply(SceneManager.GetActiveScene().name);
    }
}
#endif
