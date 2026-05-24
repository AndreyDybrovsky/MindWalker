#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Настройка префаба EnemyPTSDInDanger (патруль + конус зрения).
/// </summary>
public static class EnemyPTSDInDangerPrefabEditor
{
    private const string PrefabPath = "Assets/Import/Prefabs/Enemy/EnemyPTSDInDanger.prefab";
    private const int DefaultPatrolPointCount = 3;

    [MenuItem("Tools/PTSD/Настроить префаб EnemyPTSDInDanger")]
    public static void SetupEnemyPTSDInDangerPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            EditorUtility.DisplayDialog("EnemyPTSDInDanger", $"Не найден префаб:\n{PrefabPath}", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Setup EnemyPTSDInDanger");

        PatrolConeGuardEnemy guard = root.GetComponent<PatrolConeGuardEnemy>();
        if (guard == null)
            guard = root.AddComponent<PatrolConeGuardEnemy>();

        CleanupBrokenConeOnRoot(root);

        Transform visionCone = EnsureVisionConeChild(root);
        ConeVisionVisualizer visualizer = visionCone.GetComponent<ConeVisionVisualizer>();
        if (visualizer == null)
            visualizer = visionCone.gameObject.AddComponent<ConeVisionVisualizer>();

        Transform[] patrolPoints = EnsurePatrolPoints(root);

        SerializedObject guardSo = new SerializedObject(guard);
        SerializedProperty pointsProp = guardSo.FindProperty("patrolPoints");
        pointsProp.arraySize = patrolPoints.Length;
        for (int i = 0; i < patrolPoints.Length; i++)
            pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = patrolPoints[i];

        guardSo.FindProperty("coneVisualizer").objectReferenceValue = visualizer;
        guardSo.FindProperty("visionOrigin").objectReferenceValue = visionCone;
        guardSo.FindProperty("obstacleLayer").intValue = 1;

        if (guardSo.FindProperty("viewDistance").floatValue < 1f)
            guardSo.FindProperty("viewDistance").floatValue = 14f;
        if (guardSo.FindProperty("viewAngle").floatValue < 1f)
            guardSo.FindProperty("viewAngle").floatValue = 70f;

        guardSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedProperty proximityProp = guardSo.FindProperty("proximityRadius");
        if (proximityProp != null && proximityProp.floatValue < 0.05f)
            proximityProp.floatValue = 2.75f;
        guardSo.ApplyModifiedPropertiesWithoutUndo();

        float angle = guardSo.FindProperty("viewAngle").floatValue;
        float distance = guardSo.FindProperty("viewDistance").floatValue;
        float proximity = proximityProp != null ? proximityProp.floatValue : 2.75f;
        visualizer.Configure(angle, distance, visionCone, 1, true, proximity);

        if (!root.TryGetComponent(out EnemyHealth _))
            root.AddComponent<EnemyHealth>();

        root.tag = "Enemy";

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        AssetDatabase.Refresh();
        EditorUtility.DisplayDialog(
            "EnemyPTSDInDanger",
            "Префаб настроен:\n" +
            "• VisionCone (конус)\n" +
            "• Patrol Points (3 точки, сдвиньте в сцене)\n" +
            "• Obstacle Layer = Default\n\n" +
            "Перетащите префаб в сцену или обновите существующие экземпляры.",
            "OK");

        Selection.activeObject = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
    }

    [MenuItem("Tools/PTSD/Настроить выбранный EnemyPTSDInDanger в сцене")]
    public static void SetupSelectedInScene()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
            return;

        PatrolConeGuardEnemy guard = selected.GetComponentInChildren<PatrolConeGuardEnemy>(true);
        if (guard == null)
        {
            EditorUtility.DisplayDialog("EnemyPTSDInDanger", "Выберите объект с PatrolConeGuardEnemy.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(guard.gameObject, "Setup EnemyPTSDInDanger Instance");

        GameObject root = guard.gameObject;
        CleanupBrokenConeOnRoot(root);

        Transform visionCone = EnsureVisionConeChild(root);
        ConeVisionVisualizer visualizer = visionCone.GetComponent<ConeVisionVisualizer>();
        if (visualizer == null)
            visualizer = visionCone.gameObject.AddComponent<ConeVisionVisualizer>();

        Transform[] patrolPoints = EnsurePatrolPoints(root);

        SerializedObject guardSo = new SerializedObject(guard);
        SerializedProperty pointsProp = guardSo.FindProperty("patrolPoints");
        pointsProp.arraySize = patrolPoints.Length;
        for (int i = 0; i < patrolPoints.Length; i++)
            pointsProp.GetArrayElementAtIndex(i).objectReferenceValue = patrolPoints[i];

        guardSo.FindProperty("coneVisualizer").objectReferenceValue = visualizer;
        guardSo.FindProperty("visionOrigin").objectReferenceValue = visionCone;
        guardSo.FindProperty("obstacleLayer").intValue = 1;
        guardSo.ApplyModifiedPropertiesWithoutUndo();

        float proximity = guardSo.FindProperty("proximityRadius")?.floatValue ?? 2.75f;
        visualizer.Configure(
            guardSo.FindProperty("viewAngle").floatValue,
            guardSo.FindProperty("viewDistance").floatValue,
            visionCone,
            1,
            true,
            proximity);

        visualizer.RebuildMeshNow();
        EditorUtility.SetDirty(root);
    }

    private static void CleanupBrokenConeOnRoot(GameObject root)
    {
        ConeVisionVisualizer onRoot = root.GetComponent<ConeVisionVisualizer>();
        if (onRoot != null)
            Object.DestroyImmediate(onRoot);

        MeshFilter meshFilter = root.GetComponent<MeshFilter>();
        if (meshFilter != null && meshFilter.sharedMesh == null)
            Object.DestroyImmediate(meshFilter);

        MeshRenderer meshRenderer = root.GetComponent<MeshRenderer>();
        if (meshRenderer != null && (meshRenderer.sharedMaterial == null || meshRenderer.sharedMaterials.Length == 0))
            Object.DestroyImmediate(meshRenderer);
    }

    private static Transform EnsureVisionConeChild(GameObject root)
    {
        Transform existing = root.transform.Find("VisionCone");
        if (existing != null)
            return existing;

        GameObject visionGo = new GameObject("VisionCone");
        visionGo.transform.SetParent(root.transform, false);
        visionGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        visionGo.transform.localRotation = Quaternion.identity;
        visionGo.transform.localRotation = Quaternion.identity;
        return visionGo.transform;
    }

    private static Transform[] EnsurePatrolPoints(GameObject root)
    {
        Transform patrolRoot = root.transform.Find("PatrolPoints");
        if (patrolRoot == null)
        {
            GameObject folder = new GameObject("PatrolPoints");
            folder.transform.SetParent(root.transform, false);
            folder.transform.localPosition = Vector3.zero;
            patrolRoot = folder.transform;
        }

        int targetCount = Mathf.Max(2, DefaultPatrolPointCount);
        int existing = patrolRoot.childCount;

        for (int i = existing; i < targetCount; i++)
        {
            GameObject point = new GameObject($"PatrolPoint_{i}");
            point.transform.SetParent(patrolRoot, false);
            float t = targetCount <= 1 ? 0.5f : i / (float)(targetCount - 1);
            point.transform.localPosition = new Vector3(Mathf.Lerp(-5f, 5f, t), 0f, 0f);
        }

        int count = patrolRoot.childCount;
        Transform[] result = new Transform[count];
        for (int i = 0; i < count; i++)
            result[i] = patrolRoot.GetChild(i);

        return result;
    }
}
#endif
