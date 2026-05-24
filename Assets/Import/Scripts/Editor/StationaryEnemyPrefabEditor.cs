#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Быстрая настройка префаба стационарного врага (EnemyPTSD и др.).
/// </summary>
public static class StationaryEnemyPrefabEditor
{
    private const string EnemyTag = "Enemy";
    private const string BulletPrefabPath = "Assets/Import/Scripts/Main/Gun/EnemyBullet.prefab";

    [MenuItem("Tools/PTSD/Настроить выбранный объект как стационарного врага")]
    private static void SetupSelectedAsStationaryEnemy()
    {
        GameObject root = Selection.activeGameObject;
        if (root == null)
        {
            EditorUtility.DisplayDialog("Стационарный враг", "Выберите корневой объект врага в Hierarchy.", "OK");
            return;
        }

        Undo.RegisterFullObjectHierarchyUndo(root, "Setup Stationary Enemy");

        root.tag = EnemyTag;

        if (!root.TryGetComponent(out CapsuleCollider capsule))
        {
            capsule = root.AddComponent<CapsuleCollider>();
            capsule.height = 2f;
            capsule.radius = 0.45f;
            capsule.center = new Vector3(0f, 1f, 0f);
        }

        if (!root.TryGetComponent(out Rigidbody rb))
        {
            rb = root.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }
        else
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        EnemyHealth health = GetOrAdd<EnemyHealth>(root);
        EnemyShooting shooting = GetOrAdd<EnemyShooting>(root);
        StationaryEnemyController controller = GetOrAdd<StationaryEnemyController>(root);

        Transform firePoint = root.transform.Find("FirePoint");
        if (firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            Undo.RegisterCreatedObjectUndo(firePointObj, "Create FirePoint");
            firePoint = firePointObj.transform;
            firePoint.SetParent(root.transform, false);
            firePoint.localPosition = new Vector3(0f, 1.1f, 0.8f);
        }

        GameObject bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BulletPrefabPath);
        if (bulletPrefab == null)
        {
            string[] guids = AssetDatabase.FindAssets("EnemyBullet t:Prefab");
            if (guids.Length > 0)
                bulletPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[0]));
        }

        SerializedObject shootingSo = new SerializedObject(shooting);
        shootingSo.FindProperty("firePoint").objectReferenceValue = firePoint;
        if (bulletPrefab != null)
            shootingSo.FindProperty("bulletPrefab").objectReferenceValue = bulletPrefab;
        shootingSo.ApplyModifiedPropertiesWithoutUndo();

        Transform visionTransform = root.transform.Find("Vision");
        GameObject visionGo;
        if (visionTransform == null)
        {
            visionGo = new GameObject("Vision");
            Undo.RegisterCreatedObjectUndo(visionGo, "Create Vision");
            visionGo.transform.SetParent(root.transform, false);
            visionGo.transform.localPosition = Vector3.zero;
        }
        else
        {
            visionGo = visionTransform.gameObject;
        }

        EnemyVision vision = GetOrAdd<EnemyVision>(visionGo);

        SerializedObject visionSo = new SerializedObject(vision);
        visionSo.FindProperty("detectionRadius").floatValue = 18f;
        visionSo.FindProperty("playerLayer").intValue = 1;
        visionSo.FindProperty("obstacleLayer").intValue = 1;
        visionSo.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject controllerSo = new SerializedObject(controller);
        controllerSo.FindProperty("vision").objectReferenceValue = vision;
        controllerSo.FindProperty("shooting").objectReferenceValue = shooting;
        controllerSo.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(root);
        Debug.Log($"Стационарный враг настроен: {root.name}. Проверьте firePoint, префаб пули и слой препятствий у EnemyVision.", root);
    }

    [MenuItem("Tools/PTSD/Настроить выбранный объект как стационарного врага", true)]
    private static bool SetupSelectedAsStationaryEnemyValidate() => Selection.activeGameObject != null;

    private static T GetOrAdd<T>(GameObject go) where T : Component
    {
        if (!go.TryGetComponent(out T component))
            component = go.AddComponent<T>();
        return component;
    }
}
#endif
