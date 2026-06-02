using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;

/// <summary>
/// Назначает фоновую музыку на группу MusicVolume микшера.
/// </summary>
public static class MusicAudioBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindMusicSources();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindMusicSources();
    }

    private static void BindMusicSources()
    {
        AudioSource[] sources = Object.FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null || source.clip == null)
                continue;

            AudioMixerGroup group = source.outputAudioMixerGroup;
            if (group != null && group.name.IndexOf("Music", System.StringComparison.OrdinalIgnoreCase) >= 0)
                continue;

            if (source.loop || source.gameObject.name.IndexOf("Music", System.StringComparison.OrdinalIgnoreCase) >= 0)
                AudioMixerRoutingUtility.BindSourceToMusic(source);
        }
    }
}
