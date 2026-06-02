using System.Collections;
using UnityEngine;

/// <summary>
/// 3D-звук паники у пациентки (плач). Запускается зоной, при успокоении — плавное затухание.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class PanicPatientAmbientAudio : MonoBehaviour
{
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip panicAppearClip;
    [SerializeField] private bool loop = true;

    [Header("3D (направление и дистанция)")]
    [SerializeField] private float minDistance = 2f;
    [SerializeField] private float maxDistance = 24f;
    [SerializeField] private AudioRolloffMode rolloffMode = AudioRolloffMode.Linear;
    [SerializeField, Range(0f, 1f)] private float spatialBlend = 1f;

    private float _baseVolume = 1f;
    private Coroutine _fadeRoutine;

    public bool IsPlaying => audioSource != null && audioSource.isPlaying;
    public bool IsFading => _fadeRoutine != null;

    private void OnEnable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsApplied += OnSettingsApplied;
    }

    private void OnDisable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsApplied -= OnSettingsApplied;
    }

    private void Awake()
    {
        ResolveAudioSource();
        audioSource.playOnAwake = false;
        audioSource.loop = loop;
        ApplySpatialSettings();
        BindToSfxBus();
    }

    private void OnSettingsApplied() => ApplyEffectiveVolume();

    private void BindToSfxBus()
    {
        ResolveAudioSource();
        AudioMixerRoutingUtility.BindSourceToSfx(audioSource);
        ApplyEffectiveVolume();
    }

    public void ApplySpatialSettings(float? minDist = null, float? maxDist = null)
    {
        if (minDist.HasValue)
            minDistance = minDist.Value;
        if (maxDist.HasValue)
            maxDistance = maxDist.Value;

        ApplySpatialSettings();
    }

    public void Play(AudioClip clipOverride, float volume)
    {
        ResolveAudioSource();

        AudioClip clip = clipOverride != null ? clipOverride : panicAppearClip;
        if (clip == null)
            return;

        if (_fadeRoutine != null)
        {
            StopCoroutine(_fadeRoutine);
            _fadeRoutine = null;
        }

        BindToSfxBus();
        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = loop;
        _baseVolume = Mathf.Clamp01(volume);
        ApplyEffectiveVolume();
        audioSource.Play();
    }

    public void FadeOut(float duration)
    {
        if (audioSource == null)
            return;

        if (_fadeRoutine != null)
            return;

        if (!audioSource.isPlaying && audioSource.volume <= 0.001f)
            return;

        _fadeRoutine = StartCoroutine(FadeOutRoutine(duration));
    }

    private IEnumerator FadeOutRoutine(float duration)
    {
        float from = audioSource.volume;
        float d = Mathf.Max(0.01f, duration);
        float elapsed = 0f;

        while (elapsed < d)
        {
            elapsed += Time.deltaTime;
            audioSource.volume = Mathf.Lerp(from, 0f, elapsed / d);
            yield return null;
        }

        ApplyEffectiveVolume();
        audioSource.Stop();
        _fadeRoutine = null;
    }

    private void ApplyEffectiveVolume()
    {
        if (audioSource == null)
            return;

        float sfx = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().sfxVolume
            : 1f;

        audioSource.volume = _baseVolume * Mathf.Clamp01(sfx);
        audioSource.mute = sfx <= 0.001f;
    }

    private void ResolveAudioSource()
    {
        if (audioSource == null)
            TryGetComponent(out audioSource);

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();
    }

    private void ApplySpatialSettings()
    {
        if (audioSource == null)
            return;

        audioSource.spatialBlend = spatialBlend;
        audioSource.rolloffMode = rolloffMode;
        audioSource.minDistance = Mathf.Max(0.1f, minDistance);
        audioSource.maxDistance = Mathf.Max(audioSource.minDistance + 0.5f, maxDistance);
        audioSource.dopplerLevel = 0.12f;
        audioSource.spread = 40f;
    }
}
