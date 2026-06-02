using System.Collections;
using UnityEngine;

/// <summary>
/// Сбор и применение данных игрока для SaveManager (тело с CharacterController, не камера).
/// </summary>
public static class SaveGamePlayerUtility
{
    public const string PtsdInDangerSceneName = "PTSD in Danger";

    private const int MaxApplyAttempts = 120;

    public static bool IsPtsdInDangerScene()
    {
        return UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == PtsdInDangerSceneName;
    }

    public static bool TryCollectPlayerState(GameSaveData saveData, bool collectTransform = true)
    {
        if (saveData == null)
            return false;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

        if (collectTransform)
        {
            saveData.playerPosition = body.position;
            saveData.playerRotation = body.rotation;
        }

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

    public static bool TryApplyPlayerState(GameSaveData saveData, bool applyTransform = true)
    {
        if (saveData == null)
            return false;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

        if (applyTransform)
            ApplyBodyTransform(body, saveData.playerPosition, saveData.playerRotation);

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

    public static IEnumerator ApplyPlayerStateWhenReady(GameSaveData saveData, bool applyTransform = true)
    {
        if (saveData == null)
            yield break;

        for (int i = 0; i < MaxApplyAttempts; i++)
        {
            if (TryApplyPlayerState(saveData, applyTransform))
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
