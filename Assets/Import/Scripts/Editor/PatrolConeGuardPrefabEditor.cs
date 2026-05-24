#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

public static class PatrolConeGuardPrefabEditor
{
    private const string PrefabPath = "Assets/Import/Prefabs/Enemy/PatrolConeGuard.prefab";
    private const int DefaultPointCount = 3;

    [MenuItem("Tools/PTSD/Создать префаб PatrolConeGuard")]
    private static void CreatePatrolConeGuardPrefab()
    {
        GameObject root = new GameObject("PatrolConeGuard");
        Undo.RegisterCreatedObjectUndo(root, "Create PatrolConeGuard");

        root.tag = "Enemy";
        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = 2f;
        capsule.radius = 0.4f;
        capsule.center = new Vector3(0f, 1f, 0f);

        Rigidbody rb = root.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        Transform[] points = new Transform[DefaultPointCount];
        for (int i = 0; i < DefaultPointCount; i++)
        {
            GameObject point = new GameObject($"PatrolPoint_{i}");
            point.transform.SetParent(root.transform, false);
            float x = Mathf.Lerp(-4f, 4f, i / (float)(DefaultPointCount - 1));
            point.transform.localPosition = new Vector3(x, 0f, 0f);
            points[i] = point.transform;
        }

        GameObject visionGo = new GameObject("VisionCone");
        visionGo.transform.SetParent(root.transform, false);
        visionGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);

        ConeVisionVisualizer visualizer = visionGo.AddComponent<ConeVisionVisualizer>();

        PatrolConeGuardEnemy guard = root.AddComponent<PatrolConeGuardEnemy>();
        SerializedObject so = new SerializedObject(guard);
        so.FindProperty("patrolPoints").arraySize = points.Length;
        for (int i = 0; i < points.Length; i++)
            so.FindProperty("patrolPoints").GetArrayElementAtIndex(i).objectReferenceValue = points[i];

        so.FindProperty("coneVisualizer").objectReferenceValue = visualizer;
        so.FindProperty("visionOrigin").objectReferenceValue = visionGo.transform;
        so.FindProperty("obstacleLayer").intValue = 1;
        so.ApplyModifiedPropertiesWithoutUndo();

        visualizer.Configure(70f, 14f, visionGo.transform, 1, true, 2.75f);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        body.name = "Body";
        body.transform.SetParent(root.transform, false);
        body.transform.localPosition = new Vector3(0f, 1f, 0f);
        Object.DestroyImmediate(body.GetComponent<Collider>());

        if (!AssetDatabase.IsValidFolder("Assets/Import/Prefabs/Enemy"))
            AssetDatabase.CreateFolder("Assets/Import/Prefabs", "Enemy");

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);

        Selection.activeObject = prefab;
        EditorUtility.DisplayDialog(
            "PatrolConeGuard",
            $"Префаб создан:\n{PrefabPath}\n\nВ массиве Patrol Points — точки маршрута (минимум 2).",
            "OK");
    }
}
#endif
