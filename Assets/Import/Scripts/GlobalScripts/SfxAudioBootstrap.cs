using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// После загрузки сцены повторно применяет громкость SFX (микшер может быть не назначен вне MainMenu).
/// </summary>
public static class SfxAudioBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        ApplySfxSettings();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => ApplySfxSettings();

    private static void ApplySfxSettings()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.ApplyAudioSettingsImmediate();
    }
}
