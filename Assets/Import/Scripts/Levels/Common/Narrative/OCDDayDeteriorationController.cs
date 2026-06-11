using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Управляет четырьмя Volume, которые нарастающе ухудшают картинку с каждым днём OCD-уровня.
///
///   День 1 — нейтральный, чуть тёплый и позитивный.
///   День 2 — мир слегка тускловат, едва заметная тревога.
///   День 3 — мир заметно мрачнеет, появляются первые навязчивые мысли.
///   День 4 — персонажу совсем плохо: тёмно, холодно, аберрация и дисторсия.
///
/// Вешается на любой GameObject в сцене OCD.
/// OCDMissionDayController уведомляет его при смене дня автоматически.
/// Volumes создаются программно — ничего настраивать в инспекторе не нужно,
/// но при желании можно назначить свои Volume в поля ниже.
/// </summary>
public class OCDDayDeteriorationController : MonoBehaviour
{
    public static OCDDayDeteriorationController Instance { get; private set; }

    [Header("Volumes (пусто → создаются автоматически)")]
    [SerializeField] private Volume day1Volume;
    [SerializeField] private Volume day2Volume;
    [SerializeField] private Volume day3Volume;
    [SerializeField] private Volume day4Volume;

    [Tooltip("Длительность плавного перехода между днями (секунды). " +
             "Смена дня происходит в темноте, поэтому обычно переход мгновенный.")]
    [SerializeField] private float blendDuration = 1.6f;

    private Volume[] _volumes;
    private int      _currentDay;
    private Coroutine _blendRoutine;

    // ─── Lifecycle ───────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        EnsureVolumes();
        _volumes = new[] { day1Volume, day2Volume, day3Volume, day4Volume };

        // Все выключены — Start() применит текущий день из OCDMissionDayController.
        foreach (var v in _volumes)
            SetWeight(v, 0f);
    }

    private void Start()
    {
        // OCDMissionDayController имеет ExecutionOrder -500, так что к Start() он уже готов.
        int startDay = OCDMissionDayController.Instance != null
            ? OCDMissionDayController.Instance.GetActiveDayIndex()
            : 1;
        SetDayImmediate(startDay);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Публичный API (вызывается из OCDMissionDayController) ───────────────

    /// <summary>Мгновенно применить день — используется пока экран чёрный.</summary>
    public void SetDayImmediate(int day)
    {
        if (_blendRoutine != null) { StopCoroutine(_blendRoutine); _blendRoutine = null; }
        _currentDay = Mathf.Clamp(day, 1, _volumes.Length);
        for (int i = 0; i < _volumes.Length; i++)
            SetWeight(_volumes[i], i == _currentDay - 1 ? 1f : 0f);
    }

    /// <summary>Плавный переход (если смена происходит без затемнения).</summary>
    public void SetDaySmooth(int day)
    {
        int clamped = Mathf.Clamp(day, 1, _volumes.Length);
        if (clamped == _currentDay) return;
        if (_blendRoutine != null) StopCoroutine(_blendRoutine);
        _blendRoutine = StartCoroutine(BlendRoutine(clamped));
    }

    // ─── Создание Volume если не заданы в инспекторе ─────────────────────────

    private void EnsureVolumes()
    {
        if (day1Volume == null) day1Volume = BuildVolume("Deterioration_Day1", 1);
        if (day2Volume == null) day2Volume = BuildVolume("Deterioration_Day2", 2);
        if (day3Volume == null) day3Volume = BuildVolume("Deterioration_Day3", 3);
        if (day4Volume == null) day4Volume = BuildVolume("Deterioration_Day4", 4);
    }

    private Volume BuildVolume(string goName, int day)
    {
        var go = new GameObject(goName);
        go.transform.SetParent(transform, false);

        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 9 + day;   // 10 / 11 / 12 — выше стандартных Volume сцены
        vol.weight   = 0f;
        vol.enabled  = false;
        vol.profile  = BuildProfile(day);
        return vol;
    }

    /// <summary>Формирует VolumeProfile с нужными URP-переопределениями для каждого дня.</summary>
    private static VolumeProfile BuildProfile(int day)
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        // ─── Общий блок: Color Adjustments + Vignette ─────────────────────
        var ca  = profile.Add<ColorAdjustments>(true);
        ca.active = true;

        var vig = profile.Add<Vignette>(true);
        vig.active = true;
        vig.color.Override(Color.black);

        switch (day)
        {
            // ──────────────────────────────────────────────────────────────
            // ДЕНЬ 1 — нейтральный, слегка тёплый и позитивный
            // ──────────────────────────────────────────────────────────────
            case 1:
                ca.postExposure.Override(0.07f);
                ca.contrast.Override(-3f);
                ca.colorFilter.Override(new Color(1.00f, 0.97f, 0.91f)); // тёплый оттенок
                ca.saturation.Override(6f);
                ca.hueShift.Override(0f);

                vig.intensity.Override(0.08f);
                vig.smoothness.Override(0.5f);
                break;

            // ──────────────────────────────────────────────────────────────
            // ДЕНЬ 2 — мир слегка тускловат, тонкая усталость
            // ──────────────────────────────────────────────────────────────
            case 2:
                ca.postExposure.Override(-0.05f);
                ca.contrast.Override(4f);
                ca.colorFilter.Override(new Color(0.97f, 0.96f, 1.00f)); // чуть холоднее
                ca.saturation.Override(-10f);

                vig.intensity.Override(0.18f);
                vig.smoothness.Override(0.55f);

                var grain2 = profile.Add<FilmGrain>(true);
                grain2.active = true;
                grain2.type.Override(FilmGrainLookup.Thin1);
                grain2.intensity.Override(0.08f);
                grain2.response.Override(0.75f);
                break;

            // ──────────────────────────────────────────────────────────────
            // ДЕНЬ 3 — мир заметно мрачнеет, тревога нарастает
            // ──────────────────────────────────────────────────────────────
            case 3:
                ca.postExposure.Override(-0.22f);
                ca.contrast.Override(14f);
                ca.colorFilter.Override(new Color(0.88f, 0.91f, 1.00f)); // холодно
                ca.saturation.Override(-38f);

                vig.intensity.Override(0.30f);   // аккуратнее, чем было (0.46)
                vig.smoothness.Override(0.72f);  // мягче край
                vig.rounded.Override(true);

                var chroma3 = profile.Add<ChromaticAberration>(true);
                chroma3.active = true;
                chroma3.intensity.Override(0.12f);

                var grain3 = profile.Add<FilmGrain>(true);
                grain3.active = true;
                grain3.type.Override(FilmGrainLookup.Thin1);
                grain3.intensity.Override(0.20f);
                grain3.response.Override(0.82f);
                break;

            // ──────────────────────────────────────────────────────────────
            // ДЕНЬ 4 — персонажу совсем плохо, мир разваливается
            // ──────────────────────────────────────────────────────────────
            default:
                ca.postExposure.Override(-0.40f);
                ca.contrast.Override(25f);
                ca.colorFilter.Override(new Color(0.75f, 0.82f, 1.00f)); // ледяной
                ca.saturation.Override(-68f);

                vig.intensity.Override(0.38f);   // аккуратнее (было 0.50/0.70); всё ещё чуть сильнее Дня 3 (0.30)
                vig.smoothness.Override(0.74f);  // мягче край
                vig.rounded.Override(true);

                var chroma4 = profile.Add<ChromaticAberration>(true);
                chroma4.active = true;
                chroma4.intensity.Override(0.35f);

                var grain4 = profile.Add<FilmGrain>(true);
                grain4.active = true;
                grain4.type.Override(FilmGrainLookup.Medium1);
                grain4.intensity.Override(0.40f);
                grain4.response.Override(0.88f);

                var lens4 = profile.Add<LensDistortion>(true);
                lens4.active = true;
                lens4.intensity.Override(-0.12f);
                lens4.xMultiplier.Override(1f);
                lens4.yMultiplier.Override(1f);
                lens4.scale.Override(1.05f);
                break;
        }

        return profile;
    }

    // ─── Blend coroutine ─────────────────────────────────────────────────────

    private IEnumerator BlendRoutine(int targetDay)
    {
        float[] fromW = new float[_volumes.Length];
        for (int i = 0; i < _volumes.Length; i++)
            fromW[i] = _volumes[i] != null ? _volumes[i].weight : 0f;

        _currentDay  = targetDay;
        int targetIdx = targetDay - 1;

        float elapsed = 0f;
        while (elapsed < blendDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / blendDuration));
            for (int i = 0; i < _volumes.Length; i++)
                SetWeight(_volumes[i], Mathf.Lerp(fromW[i], i == targetIdx ? 1f : 0f, t));
            yield return null;
        }

        SetDayImmediate(targetDay);
        _blendRoutine = null;
    }

    private static void SetWeight(Volume vol, float w)
    {
        if (vol == null) return;
        vol.weight  = Mathf.Clamp01(w);
        vol.enabled = vol.weight > 0.001f;
    }
}
