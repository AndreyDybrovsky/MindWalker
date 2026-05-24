using ElmanGameDevTools.PlayerAudio;
using UnityEngine;

/// <summary>
/// Блокирует игровой ввод (стрельба, камера, шаги) при паузе и сразу после закрытия меню.
/// </summary>
public static class GameplayInputBlocker
{
    private static bool _isBlocked;
    private static int _suppressMouseLookFrames;
    private static int _enforceLockedCursorFrames;

    public static bool IsBlocked => _isBlocked;
    public static bool ShouldSuppressMouseLook => _suppressMouseLookFrames > 0;

    public static void SetBlocked(bool blocked)
    {
        if (_isBlocked == blocked)
            return;

        _isBlocked = blocked;
        if (blocked)
            StopFootstepLoops();
    }

    public static void BeginMouseLookSuppress(int frames = 5)
    {
        _suppressMouseLookFrames = Mathf.Max(_suppressMouseLookFrames, frames);
    }

    public static void LockCursorForGameplay(int enforceFrames = 10)
    {
        _enforceLockedCursorFrames = Mathf.Max(_enforceLockedCursorFrames, enforceFrames);
        ApplyLockedCursor();
    }

    public static void UnlockCursorForMenu()
    {
        _enforceLockedCursorFrames = 0;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public static void TickFrame()
    {
        if (_suppressMouseLookFrames > 0)
            _suppressMouseLookFrames--;

        if (_enforceLockedCursorFrames <= 0)
            return;

        ApplyLockedCursor();
        _enforceLockedCursorFrames--;
    }

    private static void ApplyLockedCursor()
    {
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }

    private static void StopFootstepLoops()
    {
        PlayerMusic[] footstepPlayers = Object.FindObjectsByType<PlayerMusic>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (PlayerMusic footstepPlayer in footstepPlayers)
        {
            if (footstepPlayer == null || footstepPlayer.audioSource == null)
                continue;

            if (footstepPlayer.audioSource.isPlaying)
                footstepPlayer.audioSource.Stop();
        }
    }
}
