using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Привязка AudioSource к группе SFX в MainMixer.
/// </summary>
public static class AudioMixerRoutingUtility
{
    private static AudioMixerGroup _sfxGroup;
<<<<<<< HEAD
=======
    private static AudioMixerGroup _musicGroup;

    public static AudioMixerGroup MusicGroup
    {
        get
        {
            if (_musicGroup != null)
                return _musicGroup;

            AudioMixer mixer = Resources.Load<AudioMixer>("Sounds/MainMixer");
            if (mixer != null)
            {
                AudioMixerGroup[] byMusic = mixer.FindMatchingGroups("MusicVolume");
                if (byMusic != null && byMusic.Length > 0)
                {
                    _musicGroup = byMusic[0];
                    return _musicGroup;
                }

                AudioMixerGroup[] byName = mixer.FindMatchingGroups("Music");
                if (byName != null && byName.Length > 0)
                {
                    _musicGroup = byName[0];
                    return _musicGroup;
                }
            }

            return null;
        }
    }
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)

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
<<<<<<< HEAD
=======

    public static void BindSourceToMusic(AudioSource source)
    {
        if (source == null)
            return;

        AudioMixerGroup music = MusicGroup;
        if (music != null)
            source.outputAudioMixerGroup = music;
    }

    public static void ApplyMusicVolumeFallback(float linearVolume)
    {
        bool mute = linearVolume <= 0.001f;
        AudioMixerGroup musicGroup = MusicGroup;

        AudioSource[] sources = Object.FindObjectsByType<AudioSource>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < sources.Length; i++)
        {
            AudioSource source = sources[i];
            if (source == null)
                continue;

            AudioMixerGroup group = source.outputAudioMixerGroup;
            if (musicGroup != null && group == musicGroup)
                source.mute = mute;
            else if (group != null && group.name.IndexOf("Music", System.StringComparison.OrdinalIgnoreCase) >= 0)
                source.mute = mute;
        }
    }
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
}
