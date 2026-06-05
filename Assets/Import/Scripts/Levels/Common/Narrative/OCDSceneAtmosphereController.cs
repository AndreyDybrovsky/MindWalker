using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Переключает пост-обработку, глобальный свет, ambient и фоновую музыку для уровня ОКР.
/// Вешается один раз на сцену; триггеры вызывают пресеты в затемнении.
/// </summary>
public class OCDSceneAtmosphereController : MonoBehaviour
{
    public static OCDSceneAtmosphereController Instance { get; private set; }

    [Serializable]
    public struct AtmosphereSnapshot
    {
        public Color lightColor;
        public float lightIntensity;
        public Vector3 lightEuler;
        public Color ambientSky;
        public Color ambientEquator;
        public Color ambientGround;
        public float ambientIntensity;
        public bool fogEnabled;
        public Color fogColor;
        public float fogDensity;
    }

    [Header("Свет")]
    [SerializeField] private Light directionalLight;

    [Header("Пост-обработка (URP Global Volume)")]
    [SerializeField] private Volume homeMorningVolume;
    [SerializeField] private Volume outdoorDayVolume;
    [SerializeField] private Volume homeEveningVolume;
    [SerializeField] private Volume outdoorNightVolume;

    [Header("Музыка (Music / SFX bus)")]
    [SerializeField] private AudioSource ambientMusicSource;
    [SerializeField] private AudioClip homeMorningMusic;
    [SerializeField] private AudioClip outdoorDayMusic;
    [SerializeField] private AudioClip homeEveningMusic;
    [SerializeField] private AudioClip outdoorNightMusic;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.42f;

    [Header("Снимки пресетов (если пусто — подставятся значения по умолчанию)")]
    [SerializeField] private AtmosphereSnapshot homeMorningSnapshot;
    [SerializeField] private AtmosphereSnapshot outdoorDaySnapshot;
    [SerializeField] private AtmosphereSnapshot homeEveningSnapshot;
    [SerializeField] private AtmosphereSnapshot outdoorNightSnapshot;
    [SerializeField] private bool captureSceneDefaultsOnAwake = true;

    [Header("Опционально: объекты сцены")]
    [SerializeField] private GameObject[] enableOnOutdoorDay;
    [SerializeField] private GameObject[] disableOnOutdoorDay;
    [SerializeField] private GameObject[] enableOnHomeEvening;
    [SerializeField] private GameObject[] disableOnHomeEvening;

    private AtmosphereSnapshot _currentSnapshot;
    private OCDAtmospherePreset _currentPreset = OCDAtmospherePreset.HomeMorning;
    private Coroutine _blendRoutine;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (directionalLight == null)
            directionalLight = FindDirectionalLight();

        EnsureAmbientMusicSource();
        ResolveDefaultMusicClips();
        ApplyDefaultSnapshotsIfNeeded();

        if (captureSceneDefaultsOnAwake)
            homeMorningSnapshot = CaptureFromScene();

        _currentSnapshot = homeMorningSnapshot;
        _currentPreset = OCDAtmospherePreset.HomeMorning;
        ApplySnapshotImmediate(_currentSnapshot, _currentPreset);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public void ApplyPresetDuringFade(OCDAtmospherePreset preset, float durationSeconds)
    {
        ApplyPresetDuringFade(preset, durationSeconds, default);
    }

    public void ApplyPresetDuringFade(
        OCDAtmospherePreset preset,
        float durationSeconds,
        OCDAtmosphereOverrides overrides)
    {
        if (preset == OCDAtmospherePreset.None)
            return;

        if (_blendRoutine != null)
            StopCoroutine(_blendRoutine);

        if (durationSeconds <= 0.01f)
        {
            ApplyPresetImmediate(preset, overrides);
            return;
        }

        _blendRoutine = StartCoroutine(BlendToPresetRoutine(preset, durationSeconds, overrides));
    }

    public void ApplyPresetImmediate(OCDAtmospherePreset preset)
    {
        ApplyPresetImmediate(preset, default);
    }

    public void ApplyPresetImmediate(OCDAtmospherePreset preset, OCDAtmosphereOverrides overrides)
    {
        if (preset == OCDAtmospherePreset.None)
            return;

        if (_blendRoutine != null)
        {
            StopCoroutine(_blendRoutine);
            _blendRoutine = null;
        }

        AtmosphereSnapshot target = GetSnapshotForPreset(preset);
        _currentPreset = preset;
        _currentSnapshot = target;
        ApplySnapshotImmediate(target, preset, overrides);
        ApplyPresetObjects(preset);
        PlayAmbientMusic(preset, overrides);
    }

    private IEnumerator BlendToPresetRoutine(
        OCDAtmospherePreset preset,
        float duration,
        OCDAtmosphereOverrides overrides)
    {
        AtmosphereSnapshot from = _currentSnapshot;
        AtmosphereSnapshot to = GetSnapshotForPreset(preset);
        Volume overrideVol = overrides.enabled ? overrides.postProcessVolume : null;
        float fromOverrideVol = overrideVol != null ? overrideVol.weight : 0f;
        CaptureVolumeWeights(out float fromHomeVol, out float fromOutdoorVol, out float fromEveningVol, out float fromNightVol);
        GetVolumeWeightsForPreset(preset, default, out float toHomeVol, out float toOutdoorVol, out float toEveningVol, out float toNightVol);
        float toOverrideVol = overrideVol != null ? 1f : 0f;

        ApplyPresetObjects(preset);
        CrossfadeAmbientMusic(preset, duration, overrides);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));

            AtmosphereSnapshot blended = LerpSnapshot(from, to, t);
            ApplyLightAndAmbient(blended);

            if (overrideVol != null)
            {
                SetVolumeWeight(homeMorningVolume, 0f);
                SetVolumeWeight(outdoorDayVolume, 0f);
                SetVolumeWeight(homeEveningVolume, 0f);
                SetVolumeWeight(outdoorNightVolume, 0f);
                SetVolumeWeight(overrideVol, Mathf.Lerp(fromOverrideVol, toOverrideVol, t));
            }
            else
            {
                LerpAllVolumeWeights(
                    fromHomeVol, fromOutdoorVol, fromEveningVol, fromNightVol,
                    toHomeVol, toOutdoorVol, toEveningVol, toNightVol, t);
            }

            yield return null;
        }

        _currentPreset = preset;
        _currentSnapshot = to;
        ApplySnapshotImmediate(to, preset, overrides);
        _blendRoutine = null;
    }

    private void ApplySnapshotImmediate(
        AtmosphereSnapshot snapshot,
        OCDAtmospherePreset preset,
        OCDAtmosphereOverrides overrides = default)
    {
        ApplyLightAndAmbient(snapshot);
        ApplyVolumePreset(preset, overrides);
    }

    private void ApplyLightAndAmbient(AtmosphereSnapshot snapshot)
    {
        if (directionalLight != null)
        {
            directionalLight.color = snapshot.lightColor;
            directionalLight.intensity = snapshot.lightIntensity;
            directionalLight.transform.rotation = Quaternion.Euler(snapshot.lightEuler);
        }

        RenderSettings.ambientSkyColor = snapshot.ambientSky;
        RenderSettings.ambientEquatorColor = snapshot.ambientEquator;
        RenderSettings.ambientGroundColor = snapshot.ambientGround;
        RenderSettings.ambientIntensity = snapshot.ambientIntensity;
        RenderSettings.fog = snapshot.fogEnabled;
        RenderSettings.fogColor = snapshot.fogColor;
        RenderSettings.fogDensity = snapshot.fogDensity;
        RenderSettings.fogMode = FogMode.Exponential;
    }

    private void ApplyVolumePreset(OCDAtmospherePreset preset, OCDAtmosphereOverrides overrides = default)
    {
        GetVolumeWeightsForPreset(preset, overrides, out float homeW, out float outdoorW, out float eveningW, out float nightW);

        SetVolumeWeight(homeMorningVolume, homeW);
        SetVolumeWeight(outdoorDayVolume, outdoorW);
        SetVolumeWeight(homeEveningVolume, eveningW);
        SetVolumeWeight(outdoorNightVolume, nightW);
    }

    private void CaptureVolumeWeights(
        out float home,
        out float outdoor,
        out float evening,
        out float night)
    {
        home = homeMorningVolume != null ? homeMorningVolume.weight : 0f;
        outdoor = outdoorDayVolume != null ? outdoorDayVolume.weight : 0f;
        evening = homeEveningVolume != null ? homeEveningVolume.weight : 0f;
        night = outdoorNightVolume != null ? outdoorNightVolume.weight : 0f;
    }

    private void LerpAllVolumeWeights(
        float fromHome, float fromOutdoor, float fromEvening, float fromNight,
        float toHome, float toOutdoor, float toEvening, float toNight,
        float t)
    {
        if (homeMorningVolume != null)
            homeMorningVolume.weight = Mathf.Lerp(fromHome, toHome, t);
        if (outdoorDayVolume != null)
            outdoorDayVolume.weight = Mathf.Lerp(fromOutdoor, toOutdoor, t);
        if (homeEveningVolume != null)
            homeEveningVolume.weight = Mathf.Lerp(fromEvening, toEvening, t);
        if (outdoorNightVolume != null)
            outdoorNightVolume.weight = Mathf.Lerp(fromNight, toNight, t);
    }

    private static void SetVolumeWeight(Volume volume, float weight)
    {
        if (volume == null)
            return;

        volume.enabled = weight > 0.001f;
        volume.weight = Mathf.Clamp01(weight);
    }

    private void GetVolumeWeightsForPreset(
        OCDAtmospherePreset preset,
        OCDAtmosphereOverrides overrides,
        out float home,
        out float outdoor,
        out float evening,
        out float night)
    {
        home = 0f;
        outdoor = 0f;
        evening = 0f;
        night = 0f;

        if (overrides.enabled && overrides.postProcessVolume != null)
        {
            SetVolumeWeight(overrides.postProcessVolume, 1f);
            SetVolumeWeight(homeMorningVolume, 0f);
            SetVolumeWeight(outdoorDayVolume, 0f);
            SetVolumeWeight(homeEveningVolume, 0f);
            SetVolumeWeight(outdoorNightVolume, 0f);
            return;
        }

        switch (preset)
        {
            case OCDAtmospherePreset.HomeMorning:
                home = 1f;
                break;
            case OCDAtmospherePreset.OutdoorDay:
                outdoor = 1f;
                break;
            case OCDAtmospherePreset.HomeEvening:
                evening = 1f;
                break;
            case OCDAtmospherePreset.OutdoorNight:
                night = 1f;
                break;
        }
    }

    private void ApplyPresetObjects(OCDAtmospherePreset preset)
    {
        if (preset == OCDAtmospherePreset.OutdoorDay)
        {
            SetObjectsActive(enableOnOutdoorDay, true);
            SetObjectsActive(disableOnOutdoorDay, false);
        }
        else if (preset == OCDAtmospherePreset.HomeEvening || preset == OCDAtmospherePreset.HomeMorning)
        {
            SetObjectsActive(enableOnOutdoorDay, false);
            SetObjectsActive(disableOnOutdoorDay, true);
        }

        if (preset == OCDAtmospherePreset.HomeEvening)
        {
            SetObjectsActive(enableOnHomeEvening, true);
            SetObjectsActive(disableOnHomeEvening, false);
        }
        else
        {
            SetObjectsActive(enableOnHomeEvening, false);
            SetObjectsActive(disableOnHomeEvening, true);
        }
    }

    private static void SetObjectsActive(GameObject[] objects, bool active)
    {
        if (objects == null)
            return;

        for (int i = 0; i < objects.Length; i++)
        {
            if (objects[i] != null)
                objects[i].SetActive(active);
        }
    }

    private AtmosphereSnapshot GetSnapshotForPreset(OCDAtmospherePreset preset)
    {
        return preset switch
        {
            OCDAtmospherePreset.OutdoorDay => outdoorDaySnapshot,
            OCDAtmospherePreset.HomeEvening => homeEveningSnapshot,
            OCDAtmospherePreset.OutdoorNight => outdoorNightSnapshot,
            _ => homeMorningSnapshot
        };
    }

    private static AtmosphereSnapshot LerpSnapshot(AtmosphereSnapshot a, AtmosphereSnapshot b, float t)
    {
        return new AtmosphereSnapshot
        {
            lightColor = Color.Lerp(a.lightColor, b.lightColor, t),
            lightIntensity = Mathf.Lerp(a.lightIntensity, b.lightIntensity, t),
            lightEuler = Vector3.Lerp(a.lightEuler, b.lightEuler, t),
            ambientSky = Color.Lerp(a.ambientSky, b.ambientSky, t),
            ambientEquator = Color.Lerp(a.ambientEquator, b.ambientEquator, t),
            ambientGround = Color.Lerp(a.ambientGround, b.ambientGround, t),
            ambientIntensity = Mathf.Lerp(a.ambientIntensity, b.ambientIntensity, t),
            fogEnabled = t < 0.5f ? a.fogEnabled : b.fogEnabled,
            fogColor = Color.Lerp(a.fogColor, b.fogColor, t),
            fogDensity = Mathf.Lerp(a.fogDensity, b.fogDensity, t)
        };
    }

    private AtmosphereSnapshot CaptureFromScene()
    {
        AtmosphereSnapshot snap = new AtmosphereSnapshot
        {
            ambientSky = RenderSettings.ambientSkyColor,
            ambientEquator = RenderSettings.ambientEquatorColor,
            ambientGround = RenderSettings.ambientGroundColor,
            ambientIntensity = RenderSettings.ambientIntensity,
            fogEnabled = RenderSettings.fog,
            fogColor = RenderSettings.fogColor,
            fogDensity = RenderSettings.fogDensity
        };

        if (directionalLight != null)
        {
            snap.lightColor = directionalLight.color;
            snap.lightIntensity = directionalLight.intensity;
            snap.lightEuler = directionalLight.transform.rotation.eulerAngles;
        }

        return snap;
    }

    private void ApplyDefaultSnapshotsIfNeeded()
    {
        if (IsSnapshotUnset(outdoorDaySnapshot))
        {
            outdoorDaySnapshot = homeMorningSnapshot;
            outdoorDaySnapshot.lightColor = new Color(1f, 0.96f, 0.88f);
            outdoorDaySnapshot.lightIntensity = 1.25f;
            outdoorDaySnapshot.lightEuler = new Vector3(58f, -32f, 0f);
            outdoorDaySnapshot.ambientSky = new Color(0.55f, 0.68f, 0.82f);
            outdoorDaySnapshot.ambientEquator = new Color(0.38f, 0.42f, 0.46f);
            outdoorDaySnapshot.ambientGround = new Color(0.22f, 0.24f, 0.2f);
            outdoorDaySnapshot.ambientIntensity = 1.15f;
            outdoorDaySnapshot.fogEnabled = false;
        }

        if (IsSnapshotUnset(homeEveningSnapshot))
        {
            homeEveningSnapshot = homeMorningSnapshot;
            homeEveningSnapshot.lightColor = new Color(1f, 0.72f, 0.48f);
            homeEveningSnapshot.lightIntensity = 0.48f;
            homeEveningSnapshot.lightEuler = new Vector3(14f, 118f, 0f);
            homeEveningSnapshot.ambientSky = new Color(0.12f, 0.11f, 0.18f);
            homeEveningSnapshot.ambientEquator = new Color(0.08f, 0.07f, 0.09f);
            homeEveningSnapshot.ambientGround = new Color(0.04f, 0.035f, 0.03f);
            homeEveningSnapshot.ambientIntensity = 0.75f;
            homeEveningSnapshot.fogEnabled = true;
            homeEveningSnapshot.fogColor = new Color(0.14f, 0.1f, 0.16f, 1f);
            homeEveningSnapshot.fogDensity = 0.012f;
        }

        if (IsSnapshotUnset(outdoorNightSnapshot))
        {
            outdoorNightSnapshot = outdoorDaySnapshot;
            outdoorNightSnapshot.lightColor = new Color(0.55f, 0.62f, 0.85f);
            outdoorNightSnapshot.lightIntensity = 0.22f;
            outdoorNightSnapshot.lightEuler = new Vector3(-8f, 200f, 0f);
            outdoorNightSnapshot.ambientSky = new Color(0.04f, 0.05f, 0.1f);
            outdoorNightSnapshot.ambientEquator = new Color(0.03f, 0.035f, 0.05f);
            outdoorNightSnapshot.ambientGround = new Color(0.015f, 0.02f, 0.025f);
            outdoorNightSnapshot.ambientIntensity = 0.55f;
            outdoorNightSnapshot.fogEnabled = true;
            outdoorNightSnapshot.fogColor = new Color(0.05f, 0.06f, 0.12f, 1f);
            outdoorNightSnapshot.fogDensity = 0.018f;
        }
    }

    private static bool IsSnapshotUnset(AtmosphereSnapshot snap)
    {
        return snap.lightIntensity <= 0.001f && snap.ambientIntensity <= 0.001f;
    }

    private void EnsureAmbientMusicSource()
    {
        if (ambientMusicSource == null)
            TryGetComponent(out ambientMusicSource);

        if (ambientMusicSource == null)
        {
            GameObject go = new GameObject("OCD_AmbientMusic");
            go.transform.SetParent(transform, false);
            ambientMusicSource = go.AddComponent<AudioSource>();
        }

        ambientMusicSource.playOnAwake = false;
        ambientMusicSource.loop = true;
        ambientMusicSource.spatialBlend = 0f;
        AudioMixerRoutingUtility.BindSourceToMusic(ambientMusicSource);
    }

    private void ResolveDefaultMusicClips()
    {
        if (homeMorningMusic == null)
            homeMorningMusic = Resources.Load<AudioClip>("Sounds/Other/AmbientHospital");
        if (outdoorDayMusic == null)
            outdoorDayMusic = Resources.Load<AudioClip>("Sounds/MainMenu/Ambient");
        if (homeEveningMusic == null)
            homeEveningMusic = Resources.Load<AudioClip>("Sounds/MainMenu/HospitalNoise");
        if (outdoorNightMusic == null)
            outdoorNightMusic = homeEveningMusic;
    }

    private void PlayAmbientMusic(OCDAtmospherePreset preset, OCDAtmosphereOverrides overrides = default)
    {
        AudioClip clip = ResolveAmbientClip(preset, overrides);

        if (clip == null || ambientMusicSource == null)
            return;

        float music = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().musicVolume
            : 1f;

        ambientMusicSource.clip = clip;
        ambientMusicSource.volume = musicVolume * Mathf.Clamp01(music);
        ambientMusicSource.mute = music <= 0.001f;
        if (!ambientMusicSource.isPlaying)
            ambientMusicSource.Play();
    }

    private void CrossfadeAmbientMusic(
        OCDAtmospherePreset preset,
        float duration,
        OCDAtmosphereOverrides overrides = default)
    {
        AudioClip clip = ResolveAmbientClip(preset, overrides);

        if (clip == null || ambientMusicSource == null)
            return;

        StartCoroutine(CrossfadeMusicRoutine(clip, duration));
    }

    private IEnumerator CrossfadeMusicRoutine(AudioClip nextClip, float duration)
    {
        float music = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().musicVolume
            : 1f;

        float targetVol = musicVolume * Mathf.Clamp01(music);
        float startVol = ambientMusicSource.volume;

        if (ambientMusicSource.clip != nextClip)
        {
            ambientMusicSource.volume = 0f;
            ambientMusicSource.clip = nextClip;
            ambientMusicSource.Play();
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            ambientMusicSource.volume = Mathf.Lerp(startVol, targetVol, t);
            yield return null;
        }

        ambientMusicSource.volume = targetVol;
    }

    private AudioClip ResolveAmbientClip(OCDAtmospherePreset preset, OCDAtmosphereOverrides overrides)
    {
        if (overrides.enabled && overrides.ambientMusicClip != null)
            return overrides.ambientMusicClip;

        return preset switch
        {
            OCDAtmospherePreset.OutdoorDay => outdoorDayMusic,
            OCDAtmospherePreset.HomeEvening => homeEveningMusic,
            OCDAtmospherePreset.OutdoorNight => outdoorNightMusic != null ? outdoorNightMusic : homeEveningMusic,
            _ => homeMorningMusic
        };
    }

    private static Light FindDirectionalLight()
    {
        Light[] lights = FindObjectsByType<Light>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < lights.Length; i++)
        {
            if (lights[i] != null && lights[i].type == LightType.Directional)
                return lights[i];
        }

        return null;
    }
}
