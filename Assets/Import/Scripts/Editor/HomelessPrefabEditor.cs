#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Настройка префаба Homeless: тег Enemy, здоровье, зрение, периодический урон без стрельбы.
/// </summary>
public static class HomelessPrefabEditor
{
    private const string PrefabPath = "Assets/Import/Prefabs/Enemy/Ludomania/Homeless.prefab";

    [MenuItem("Tools/Gambling/Настроить префаб Homeless")]
    public static void SetupHomelessPrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            EditorUtility.DisplayDialog("Homeless", $"Не найден префаб:\n{PrefabPath}", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Setup Homeless");

        root.tag = "Enemy";

        CapsuleCollider capsule = root.GetComponent<CapsuleCollider>();
        if (capsule == null)
        {
            capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.5f;
            capsule.center = new Vector3(0f, 1f, 0f);
        }

        Rigidbody rb = root.GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        GetOrAdd<EnemyHealth>(root);
        GetOrAdd<PeriodicProximityDamageEnemy>(root);

        Transform visionRoot = root.transform.Find("Vision");
        if (visionRoot == null)
        {
            GameObject visionGo = new GameObject("Vision");
            visionGo.transform.SetParent(root.transform, false);
            visionGo.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            visionRoot = visionGo.transform;
        }

        EnemyVision vision = visionRoot.GetComponent<EnemyVision>();
        if (vision == null)
            vision = visionRoot.gameObject.AddComponent<EnemyVision>();

        PeriodicProximityDamageEnemy damage = root.GetComponent<PeriodicProximityDamageEnemy>();
        SerializedObject damageSo = new SerializedObject(damage);
        damageSo.FindProperty("vision").objectReferenceValue = vision;
        damageSo.ApplyModifiedPropertiesWithoutUndo();

        StationaryEnemyController stationary = root.GetComponent<StationaryEnemyController>();
        if (stationary != null)
            Object.DestroyImmediate(stationary);

        EnemyShooting shooting = root.GetComponent<EnemyShooting>();
        if (shooting != null)
            Object.DestroyImmediate(shooting);

        EnemyShooting[] childShooting = root.GetComponentsInChildren<EnemyShooting>(true);
        for (int i = 0; i < childShooting.Length; i++)
            Object.DestroyImmediate(childShooting[i]);

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        EditorUtility.DisplayDialog(
            "Homeless",
            "Готово: Enemy, EnemyHealth, EnemyVision, PeriodicProximityDamageEnemy.\n" +
            "Стрельба отключена. Урон — раз в ~1.25 с, пока игрок в зоне видимости.",
            "OK");
    }

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        if (!go.TryGetComponent(out T component))
            component = go.AddComponent<T>();
        return component;
    }
}
#endif
