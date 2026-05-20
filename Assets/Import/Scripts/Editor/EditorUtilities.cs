#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Общие проверки, чтобы editor bootstrap не падал во время компиляции и отрисовки IMGUI.
/// </summary>
static class EditorUtilities
{
    public static bool CanRunEditorMaintenance()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return false;

        if (EditorApplication.isCompiling)
            return false;

        if (EditorApplication.isUpdating)
            return false;

        return true;
    }
}
#endif
