using System.Collections;
using UnityEngine;

/// <summary>
/// Управляет ambient-музыкой сцены Bipolar.
/// Один экземпляр на сцену. Поддерживает плавный crossfade между клипами.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BipolarAmbientController : MonoBehaviour
{
    public static BipolarAmbientController Instance { get; private set; }

    [Header("Клипы по локациям")]
    [SerializeField] private AudioClip defaultClip;   // поверхность / GoodWorld
    [SerializeField, Range(0f, 1f)] private float volume = 0.6f;

    private AudioSource _source;
    private Coroutine _fadeRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        _source = GetComponent<AudioSource>();
        _source.loop = true;
        _source.playOnAwake = false;
        _source.spatialBlend = 0f;
        AudioMixerRoutingUtility.BindSourceToMusic(_source);
    }

    private void Start()
    {
        if (defaultClip != null)
            PlayImmediate(defaultClip);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void CrossfadeTo(AudioClip clip, float duration)
    {
        if (clip == null) return;
        if (_source.clip == clip && _source.isPlaying) return;
        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeRoutine(clip, duration));
    }

    public void CrossfadeToDefault(float duration)
    {
        CrossfadeTo(defaultClip, duration);
    }

    private void PlayImmediate(AudioClip clip)
    {
        _source.clip = clip;
        _source.volume = volume;
        _source.Play();
    }

    private IEnumerator FadeRoutine(AudioClip newClip, float duration)
    {
        float half = Mathf.Max(0.05f, duration * 0.5f);

        // Fade out
        float start = _source.volume;
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            _source.volume = Mathf.Lerp(start, 0f, t / half);
            yield return null;
        }
        _source.volume = 0f;

        _source.clip = newClip;
        _source.Play();

        // Fade in
        for (float t = 0; t < half; t += Time.unscaledDeltaTime)
        {
            _source.volume = Mathf.Lerp(0f, volume, t / half);
            yield return null;
        }
        _source.volume = volume;
        _fadeRoutine = null;
    }
}
