using ElmanGameDevTools.PlayerAudio;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Назначает шаги (PlayerMusic) на группу SFX микшера.
/// </summary>
public static class FootstepAudioBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        BindFootstepSources();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        BindFootstepSources();
    }

    private static void BindFootstepSources()
    {
        PlayerMusic[] players = Object.FindObjectsByType<PlayerMusic>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null)
                continue;

            if (players[i].audioSource == null)
                players[i].audioSource = players[i].GetComponent<AudioSource>();

            AudioMixerRoutingUtility.BindSourceToSfx(players[i].audioSource);
        }
    }
}
