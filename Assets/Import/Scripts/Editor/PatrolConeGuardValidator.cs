#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PatrolConeGuardValidator
{
    [MenuItem("Tools/PTSD/Проверить выбранного PatrolConeGuard / EnemyPTSDInDanger")]
    private static void ValidateSelected()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            EditorUtility.DisplayDialog("PatrolConeGuard", "Выберите объект с PatrolConeGuardEnemy.", "OK");
            return;
        }

        PatrolConeGuardEnemy guard = selected.GetComponentInChildren<PatrolConeGuardEnemy>(true);
        if (guard == null)
        {
            EditorUtility.DisplayDialog("PatrolConeGuard", "На выбранном объекте нет PatrolConeGuardEnemy.", "OK");
            return;
        }

        ConeVisionVisualizer visualizer = selected.GetComponentInChildren<ConeVisionVisualizer>(true);
        if (visualizer == null)
        {
            EditorUtility.DisplayDialog(
                "PatrolConeGuard",
                "Нет ConeVisionVisualizer.\n\nДобавьте дочерний объект VisionCone с компонентом ConeVisionVisualizer\nили пересоздайте через Tools → PTSD → Создать префаб PatrolConeGuard.",
                "OK");
            return;
        }

        SerializedObject guardSo = new SerializedObject(guard);
        guardSo.FindProperty("coneVisualizer").objectReferenceValue = visualizer;
        guardSo.ApplyModifiedPropertiesWithoutUndo();

        visualizer.RebuildMeshNow();
        EditorUtility.DisplayDialog(
            "PatrolConeGuard",
            "Конус пересобран.\n\nПроверьте в Play Mode:\n• Patrol Points — минимум 2\n• View Distance / View Angle > 0\n• Obstacle Layer — слой стен",
            "OK");
    }
}
#endif
