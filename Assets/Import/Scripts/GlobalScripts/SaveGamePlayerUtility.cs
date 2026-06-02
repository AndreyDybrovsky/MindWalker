using System.Collections;
using UnityEngine;

/// <summary>
/// Сбор и применение данных игрока для SaveManager (тело с CharacterController, не камера).
/// </summary>
public static class SaveGamePlayerUtility
{
<<<<<<< HEAD
    private const int MaxApplyAttempts = 120;

    public static bool TryCollectPlayerState(GameSaveData saveData)
=======
    public const string PtsdInDangerSceneName = "PTSD in Danger";

    private const int MaxApplyAttempts = 120;

    public static bool IsPtsdInDangerScene()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == PtsdInDangerSceneName;
    }

    public static bool TryCollectPlayerState(GameSaveData saveData, bool collectTransform = true)
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
    {
        if (saveData == null)
            return false;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

<<<<<<< HEAD
        saveData.playerPosition = body.position;
        saveData.playerRotation = body.rotation;
=======
        if (collectTransform)
        {
            saveData.playerPosition = body.position;
            saveData.playerRotation = body.rotation;
        }
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)

        if (TryGetPlayerHealth(body, out PlayerHealth health))
        {
            float hp = health.CurrentHealth;
            if (hp <= 0f && !health.IsDead)
                hp = health.MaxHealth;

            saveData.playerHealth = hp;
            saveData.playerMaxHealth = health.MaxHealth;
        }

        return true;
    }

<<<<<<< HEAD
    public static bool TryApplyPlayerState(GameSaveData saveData)
=======
    public static bool TryApplyPlayerState(GameSaveData saveData, bool applyTransform = true)
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
    {
        if (saveData == null)
            return false;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

<<<<<<< HEAD
        ApplyBodyTransform(body, saveData.playerPosition, saveData.playerRotation);
=======
        if (applyTransform)
            ApplyBodyTransform(body, saveData.playerPosition, saveData.playerRotation);
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)

        if (TryGetPlayerHealth(body, out PlayerHealth health))
        {
            float hp = saveData.playerHealth;
            float maxHp = saveData.playerMaxHealth > 0f ? saveData.playerMaxHealth : health.MaxHealth;
            if (hp <= 0f && maxHp > 0f)
                hp = maxHp;

            health.SetHealth(hp, maxHp);
        }

        SnapTopDownCameraIfPresent();
        return true;
    }

<<<<<<< HEAD
    public static IEnumerator ApplyPlayerStateWhenReady(GameSaveData saveData)
=======
    public static IEnumerator ApplyPlayerStateWhenReady(GameSaveData saveData, bool applyTransform = true)
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
    {
        if (saveData == null)
            yield break;

        for (int i = 0; i < MaxApplyAttempts; i++)
        {
<<<<<<< HEAD
            if (TryApplyPlayerState(saveData))
=======
            if (TryApplyPlayerState(saveData, applyTransform))
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
                yield break;

            yield return null;
        }

        Debug.LogWarning("SaveGamePlayerUtility: не удалось применить позицию/HP игрока после загрузки сцены.");
    }

    private static void ApplyBodyTransform(Transform body, Vector3 position, Quaternion rotation)
    {
        if (body.TryGetComponent(out CharacterController controller))
            controller.enabled = false;

        body.SetPositionAndRotation(position, rotation);

        if (controller != null)
        {
            controller.enabled = true;
            controller.Move(Vector3.zero);
        }

        Physics.SyncTransforms();
    }

    private static bool TryGetPlayerHealth(Transform body, out PlayerHealth health)
    {
        health = null;
        if (body == null)
            return false;

        if (body.TryGetComponent(out health))
            return true;

        health = body.GetComponentInParent<PlayerHealth>();
        if (health != null)
            return true;

        health = body.GetComponentInChildren<PlayerHealth>(true);
        return health != null;
    }

    private static void SnapTopDownCameraIfPresent()
    {
        TopDownCameraFollow follow = Object.FindFirstObjectByType<TopDownCameraFollow>();
        if (follow != null)
            follow.SnapToTarget();
    }
}
