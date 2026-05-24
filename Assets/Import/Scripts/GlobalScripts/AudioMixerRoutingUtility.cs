using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Привязка AudioSource к группе SFX в MainMixer.
/// </summary>
public static class AudioMixerRoutingUtility
{
    private static AudioMixerGroup _sfxGroup;

    public static AudioMixerGroup SfxGroup
    {
        get
        {
            if (_sfxGroup != null)
                return _sfxGroup;

            AudioMixer mixer = Resources.Load<AudioMixer>("Sounds/MainMixer");
            if (mixer != null)
            {
                AudioMixerGroup[] bySfx = mixer.FindMatchingGroups("Sfx");
                if (bySfx != null && bySfx.Length > 0)
                {
                    _sfxGroup = bySfx[0];
                    return _sfxGroup;
                }

                AudioMixerGroup[] byName = mixer.FindMatchingGroups("SFX");
                if (byName != null && byName.Length > 0)
                {
                    _sfxGroup = byName[0];
                    return _sfxGroup;
                }
            }

            AudioSource[] sources = Object.FindObjectsByType<AudioSource>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);

            for (int i = 0; i < sources.Length; i++)
            {
                AudioMixerGroup group = sources[i] != null ? sources[i].outputAudioMixerGroup : null;
                if (group == null)
                    continue;

                if (group.name.IndexOf("sfx", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    _sfxGroup = group;
                    return _sfxGroup;
                }
            }

            return null;
        }
    }

    public static void BindSourceToSfx(AudioSource source)
    {
        if (source == null)
            return;

        AudioMixerGroup sfx = SfxGroup;
        if (sfx != null)
            source.outputAudioMixerGroup = sfx;
    }
}
