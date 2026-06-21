using System.Collections;
using UnityEngine;

/// <summary>
/// Ambient-звук ОКР: тиканье часов, усиливающееся с каждым днём.
/// Вешается на GlobalDay (рядом с OCDDayDeteriorationController).
/// Назначьте один AudioClip тиканья — громкость и питч меняются по дням.
/// </summary>
public class OCDAmbientDaySound : MonoBehaviour
{
    public static OCDAmbientDaySound Instance { get; private set; }

    [Header("Клипы")]
    [Tooltip("Один клип для всех дней (громкость/питч варьируются). Например: clock_tick.wav")]
    [SerializeField] private AudioClip ambientClip;

    [Header("Громкость по дням (День 1–4)")]
    [SerializeField] private float[] dayVolumes  = { 0.06f, 0.14f, 0.26f, 0.44f };

    [Header("Питч по дням (Day 1–4)")]
    [SerializeField] private float[] dayPitches  = { 0.92f, 0.97f, 1.03f, 1.10f };

    [Tooltip("Длительность плавного перехода при смене дня (секунды).")]
    [SerializeField] private float crossfadeDuration = 1.8f;

    private AudioSource  _source;
    private int          _currentDay;
    private Coroutine    _crossfade;

    // ─── Lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        _source               = gameObject.AddComponent<AudioSource>();
        _source.loop          = true;
        _source.playOnAwake   = false;
        _source.spatialBlend  = 0f;
        _source.volume        = 0f;
        _source.priority      = 200;
        AudioMixerRoutingUtility.BindSourceToSfx(_source);
    }

    private void Start()
    {
        int startDay = OCDMissionDayController.Instance != null
            ? OCDMissionDayController.Instance.GetActiveDayIndex()
            : 1;
        SetDay(startDay);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Публичный API ────────────────────────────────────────────────────

    public void SetDay(int day)
    {
        int clamped = Mathf.Clamp(day, 1, 4);
        if (_currentDay == clamped && _source.isPlaying)
            return;

        _currentDay = clamped;

        if (ambientClip == null)
            return;

        if (_crossfade != null)
            StopCoroutine(_crossfade);

        _crossfade = StartCoroutine(CrossfadeToDay(clamped));
    }

    // ─── Корутина перехода ────────────────────────────────────────────────

    private IEnumerator CrossfadeToDay(int day)
    {
        int idx = day - 1;
        float targetVol   = idx < dayVolumes.Length  ? dayVolumes[idx]  : 0f;
        float targetPitch = idx < dayPitches.Length  ? dayPitches[idx]  : 1f;

        if (!_source.isPlaying || _source.clip != ambientClip)
        {
            _source.clip = ambientClip;
            _source.volume = 0f;
            _source.Play();
        }

        float fromVol   = _source.volume;
        float fromPitch = _source.pitch;
        float elapsed   = 0f;

        while (elapsed < crossfadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / crossfadeDuration));
            _source.volume = Mathf.Lerp(fromVol,   targetVol,   t);
            _source.pitch  = Mathf.Lerp(fromPitch, targetPitch, t);
            yield return null;
        }

        _source.volume = targetVol;
        _source.pitch  = targetPitch;
        _crossfade     = null;
    }
}
