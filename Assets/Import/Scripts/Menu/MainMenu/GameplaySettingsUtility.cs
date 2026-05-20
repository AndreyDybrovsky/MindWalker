using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

/// <summary>
/// Применение FOV и чувствительности мыши к <see cref="PlayerController"/>.
/// </summary>
public static class GameplaySettingsUtility
{
    public static bool TryApplyToPlayerInScene(GameSettings settings)
    {
        if (settings == null)
            return false;

        PlayerController player = Object.FindFirstObjectByType<PlayerController>();
        if (player == null)
            return false;

        ApplyToPlayer(player, settings);
        return true;
    }

    public static void ApplyToPlayer(PlayerController player, GameSettings settings)
    {
        if (player == null || settings == null)
            return;

        player.sensitivity = settings.mouseSensitivity;
        player.normalFov = settings.fieldOfView;

        if (player.playerCamera != null &&
            player.playerCamera.TryGetComponent(out Camera cam))
        {
            cam.fieldOfView = settings.fieldOfView;
        }
    }
}
