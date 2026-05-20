using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Создаёт SettingsData и передаёт AudioMixer в <see cref="SettingsManager"/>.
/// </summary>
public class SoundManager : MonoBehaviour
{
    [Header("Audio Mixer")]
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private string masterVolumeParam = "MasterVolume";
    [SerializeField] private string musicVolumeParam = "MusicVolume";
    [SerializeField] private string sfxVolumeParam = "SfxVolume";

    public static SettingsManager Instance => SettingsManager.Instance;

    private void Awake()
    {
        SettingsManager data = GetComponentInChildren<SettingsManager>(true);
        if (data == null)
        {
            GameObject go = new GameObject("SettingsData");
            go.transform.SetParent(transform, false);
            data = go.AddComponent<SettingsManager>();
        }

        data.ConfigureAudioMixer(audioMixer, masterVolumeParam, musicVolumeParam, sfxVolumeParam);
    }

    public void SaveSettings() => SettingsManager.Instance.SaveSettings();
    public void LoadSettings() => SettingsManager.Instance.LoadSettings();
    public GameSettings GetCurrentSettings() => SettingsManager.Instance.GetCurrentSettings();
    public void UpdateSetting<T>(string settingName, T value) => SettingsManager.Instance.UpdateSetting(settingName, value);
    public void OnSettingsChanged() => SettingsManager.Instance.OnSettingsChanged();
    public void ApplyAudioSettingsImmediate() => SettingsManager.Instance.ApplyAudioSettingsImmediate();
    public void SavePreviousSettings() => SettingsManager.Instance.SavePreviousSettings();
    public void RestorePreviousSettings() => SettingsManager.Instance.RestorePreviousSettings();
    public GameSettings GetPreviousSettings() => SettingsManager.Instance.GetPreviousSettings();
}
