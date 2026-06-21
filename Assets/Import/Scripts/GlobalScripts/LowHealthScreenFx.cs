using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Усиливает виньетку и хроматическую аберрацию по мере падения HP игрока.
/// Создаёт глобальный URP Volume в рантайме — настройка сцен не нужна.
/// Ниже порога здоровья экран краснеет и «плывёт», подчёркивая опасность.
/// </summary>
public class LowHealthScreenFx : MonoBehaviour
{
    private const float Threshold = 0.5f;     // ниже этого HP эффект начинает проявляться
    private const float CriticalPct = 0.08f;  // здесь эффект максимален
    private const float MaxVignette = 0.5f;
    private const float MaxChromatic = 0.7f;

    private static LowHealthScreenFx _instance;

    private Volume _volume;
    private Vignette _vignette;
    private ChromaticAberration _chromatic;
    private PlayerHealth _player;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        GameObject go = new GameObject(nameof(LowHealthScreenFx));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<LowHealthScreenFx>();
        _instance.Setup();
        SceneManager.sceneLoaded += _instance.OnSceneLoaded;
    }

    private void Setup()
    {
        _volume = gameObject.AddComponent<Volume>();
        _volume.isGlobal = true;
        _volume.priority = 100f;
        _volume.weight = 0f;

        VolumeProfile profile = ScriptableObject.CreateInstance<VolumeProfile>();
        _volume.sharedProfile = profile;

        _vignette = profile.Add<Vignette>(true);
        _vignette.color.overrideState = true;
        _vignette.color.value = new Color(0.55f, 0f, 0f);
        _vignette.intensity.overrideState = true;
        _vignette.intensity.value = 0f;

        _chromatic = profile.Add<ChromaticAberration>(true);
        _chromatic.intensity.overrideState = true;
        _chromatic.intensity.value = 0f;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        _player = null;
    }

    private void Update()
    {
        if (_player == null)
            _player = FindFirstObjectByType<PlayerHealth>();

        if (_volume == null)
            return;

        if (_player == null || _player.IsDead)
        {
            _volume.weight = 0f;
            return;
        }

        float pct = _player.HealthPercentage;
        float t = pct >= Threshold ? 0f : Mathf.InverseLerp(Threshold, CriticalPct, pct);

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * 4f) * 0.08f * t;

        _volume.weight = t;
        if (_vignette != null)
            _vignette.intensity.value = MaxVignette * t * pulse;
        if (_chromatic != null)
            _chromatic.intensity.value = MaxChromatic * t;
    }
}
