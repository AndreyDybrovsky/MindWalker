using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Включает volumetric fog (URP Renderer Feature) только в сценах с особой атмосферой.
/// Сам pass читает <see cref="VolumetricFogEnabled"/> — asset Renderer Feature не переключаем.
/// </summary>
public static class LevelAtmosphere
{
    private static readonly HashSet<string> VolumetricFogScenes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Depression",
    };

    public static bool VolumetricFogEnabled { get; private set; }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void RegisterRuntimeCallbacks()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        Apply(SceneManager.GetActiveScene().name);
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Apply(scene.name);
    }

    public static void Apply(string sceneName)
    {
        VolumetricFogEnabled = !string.IsNullOrEmpty(sceneName) && VolumetricFogScenes.Contains(sceneName);
    }
}
