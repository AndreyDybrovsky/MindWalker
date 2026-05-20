using System.Collections;
using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

/// <summary>
/// Применяет чувствительность мыши и FOV из SettingsManager к игроку в сцене.
/// </summary>
public class PlayerGameplaySettingsApplier : MonoBehaviour
{
    [SerializeField] private int maxResolveFrames = 120;

    private PlayerController _player;

    private void OnEnable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsApplied += ApplyFromSettings;

        StartCoroutine(ApplyWhenReady());
    }

    private void OnDisable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsApplied -= ApplyFromSettings;
    }

    private IEnumerator ApplyWhenReady()
    {
        GameSettings settings = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings()
            : null;

        for (int i = 0; i < maxResolveFrames; i++)
        {
            if (TryResolvePlayer() && settings != null)
            {
                GameplaySettingsUtility.ApplyToPlayer(_player, settings);
                yield break;
            }

            if (settings == null && SettingsManager.Instance != null)
                settings = SettingsManager.Instance.GetCurrentSettings();

            yield return null;
        }
    }

    private void ApplyFromSettings()
    {
        if (!TryResolvePlayer())
            return;

        GameSettings settings = SettingsManager.Instance.GetCurrentSettings();
        if (settings == null)
            return;

        GameplaySettingsUtility.ApplyToPlayer(_player, settings);
    }

    private bool TryResolvePlayer()
    {
        if (_player != null)
            return true;

        _player = GetComponent<PlayerController>();
        if (_player == null)
            _player = FindFirstObjectByType<PlayerController>();

        return _player != null;
    }
}
