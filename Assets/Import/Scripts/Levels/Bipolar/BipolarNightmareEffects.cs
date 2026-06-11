using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

/// <summary>
/// Реалистичные эффекты мрачного мира Bipolar (Body Camera).
/// Автоматически подписывается на BipolarMindscapeController.OnModeChanged.
///
/// Камера НЕ перемещается — только аддитивный поворот поверх движения игрока.
/// </summary>
public class BipolarNightmareEffects : MonoBehaviour
{
    [Header("Body Cam — шатание (Perlin, аддитивно к обычному повороту)")]
    [SerializeField] private float swayAmplitude = 0.45f;
    [SerializeField] private float swaySpeed     = 0.9f;

    [Header("Пульсация FOV — тревога")]
    [SerializeField] private float fovPulseAmount = 0.35f;
    [SerializeField] private float fovPulseSpeed  = 0.18f;

    [Header("Глитч-спазмы")]
    [SerializeField] private float glitchIntervalMin = 3.5f;
    [SerializeField] private float glitchIntervalMax = 10f;
    [SerializeField] private float glitchDuration    = 0.07f;
    [SerializeField] private float glitchSnapAngle   = 1.1f;

    [Header("URP Volume (BadPost) — для хроматической аберрации")]
    [SerializeField] private Volume nightmareVolume;

    // ─── Приватные ─────────────────────────────────────────────────────────

    private Camera _cam;
    private float  _baseFov;

    private float _nx, _ny, _nz;   // случайные сдвиги Perlin
    private float _fovPhase;

    // Последнее «чистое» вращение от контроллера игрока (до нашего сдвига)
    private Quaternion _cleanLocalRot;

    // Текущий аддитивный сдвиг от сдвига/спазма — применяется в LateUpdate
    private Quaternion _swayOffset = Quaternion.identity;
    private bool _glitchActive;

    // URP overrides
    private ChromaticAberration _chroma;
    private float               _chromaBase;

    private LensDistortion _lens;
    private FilmGrain      _grain;

    // VHS-оверлей
    private Canvas  _overlayCanvas;
    private Image[] _strips;

    private BipolarMindscapeController _controller;

    // ─── Lifecycle ─────────────────────────────────────────────────────────

    private void Awake()
    {
        _cam = Camera.main;
        if (_cam == null) _cam = FindFirstObjectByType<Camera>();

        BuildVhsOverlay();

        // Подписываемся в Awake, иначе Start() не вызовется (компонент disabled).
        _controller = FindFirstObjectByType<BipolarMindscapeController>();
        if (_controller != null)
            _controller.OnModeChanged += OnModeChanged;

        // Выключаем компонент (не GameObject!) — Volume на объекте продолжает работать.
        bool startAsNightmare = _controller != null &&
                                _controller.CurrentMode == BipolarMindscapeMode.Nightmare;
        this.enabled = startAsNightmare;
    }

    private void OnDestroy()
    {
        if (_controller != null)
            _controller.OnModeChanged -= OnModeChanged;
    }

    private void OnModeChanged(BipolarMindscapeMode mode)
    {
        this.enabled = (mode == BipolarMindscapeMode.Nightmare);
    }

    private void OnEnable()
    {
        if (_cam == null) return;

        _baseFov = _cam.fieldOfView;
        _fovPhase = 0f;
        _swayOffset = Quaternion.identity;

        _nx = Random.Range(0f, 500f);
        _ny = Random.Range(0f, 500f);
        _nz = Random.Range(0f, 500f);

        // Клонируем profile, чтобы не менять ассет напрямую
        _chroma = null; _lens = null; _grain = null;
        if (nightmareVolume != null)
        {
            if (!nightmareVolume.HasInstantiatedProfile())
                nightmareVolume.profile = Instantiate(nightmareVolume.sharedProfile);

            var profile = nightmareVolume.profile;

            // Chromatic Aberration
            if (profile.TryGet(out ChromaticAberration ca))
            {
                _chroma = ca;
                _chromaBase = ca.intensity.value;
            }

            // Lens Distortion — добавляем если нет
            if (!profile.TryGet(out LensDistortion ld))
                ld = profile.Add<LensDistortion>(true);
            _lens = ld;
            _lens.active = true;
            _lens.intensity.overrideState = true;
            _lens.intensity.value = -0.14f;   // лёгкая бочка — ощущение нестабильности

            // Film Grain — добавляем если нет
            if (!profile.TryGet(out FilmGrain fg))
                fg = profile.Add<FilmGrain>(true);
            _grain = fg;
            _grain.active = true;
            _grain.type.overrideState   = true;
            _grain.type.value           = FilmGrainLookup.Medium1;
            _grain.intensity.overrideState = true;
            _grain.intensity.value      = 0.22f;
            _grain.response.overrideState = true;
            _grain.response.value       = 0.85f;
        }

        if (_overlayCanvas != null) _overlayCanvas.gameObject.SetActive(true);
        StartCoroutine(GlitchLoop());
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        _glitchActive = false;

        // Восстанавливаем FOV — камеру НЕ двигаем, позиция никогда не менялась
        if (_cam != null)
        {
            _cam.fieldOfView = _baseFov;
            // Убираем наш аддитивный сдвиг: ставим то что было до LateUpdate
            _cam.transform.localRotation = _cleanLocalRot;
        }

        if (_chroma != null) _chroma.intensity.Override(_chromaBase);
        if (_lens   != null) { _lens.intensity.Override(0f);  _lens.active  = false; }
        if (_grain  != null) { _grain.intensity.Override(0f); _grain.active = false; }

        HideStrips();
        if (_overlayCanvas != null) _overlayCanvas.gameObject.SetActive(false);
    }

    // ─── Update / LateUpdate ───────────────────────────────────────────────

    private void Update()
    {
        ApplyFovPulse();
        UpdateSwayOffset();
    }

    // LateUpdate — применяем сдвиг ПОСЛЕ того как контроллер игрока выставил поворот
    private void LateUpdate()
    {
        if (_cam == null) return;

        // Запоминаем «чистый» поворот от игрока
        _cleanLocalRot = _cam.transform.localRotation;

        // Применяем наш сдвиг аддитивно
        _cam.transform.localRotation = _cleanLocalRot * _swayOffset;
    }

    private void UpdateSwayOffset()
    {
        if (_glitchActive) return;  // во время спазма сдвиг управляется корутиной

        float t = Time.unscaledTime * swaySpeed;
        float rx = (Mathf.PerlinNoise(_nx + t,        _ny)        - 0.5f) * 2f * swayAmplitude;
        float ry = (Mathf.PerlinNoise(_ny,             _nz + t)    - 0.5f) * 2f * swayAmplitude;
        float rz = (Mathf.PerlinNoise(_nz + t * 0.4f, _nx + 37f)  - 0.5f) * 2f * swayAmplitude * 0.2f;
        _swayOffset = Quaternion.Euler(rx, ry, rz);
    }

    private void ApplyFovPulse()
    {
        if (_cam == null) return;
        _fovPhase += Time.unscaledDeltaTime * fovPulseSpeed * Mathf.PI * 2f;
        _cam.fieldOfView = _baseFov + Mathf.Sin(_fovPhase) * fovPulseAmount;
    }

    // ─── Глитч-спазмы ──────────────────────────────────────────────────────

    private IEnumerator GlitchLoop()
    {
        while (true)
        {
            yield return new WaitForSecondsRealtime(
                Random.Range(glitchIntervalMin, glitchIntervalMax));
            yield return DoGlitch();
        }
    }

    private IEnumerator DoGlitch()
    {
        _glitchActive = true;

        float sx = Random.Range(-glitchSnapAngle, glitchSnapAngle);
        float sy = Random.Range(-glitchSnapAngle, glitchSnapAngle);
        _swayOffset = Quaternion.Euler(sx, sy, 0f);

        if (_chroma != null) _chroma.intensity.Override(Mathf.Min(1f, _chromaBase + 0.7f));
        if (_lens   != null) _lens.intensity.Override(-0.38f);

        ShowStrips();

        yield return new WaitForSecondsRealtime(glitchDuration);

        HideStrips();
        if (_chroma != null) _chroma.intensity.Override(_chromaBase);
        if (_lens   != null) _lens.intensity.Override(-0.14f);

        _glitchActive = false;
    }

    // ─── VHS-оверлей ───────────────────────────────────────────────────────

    private void BuildVhsOverlay()
    {
        var go = new GameObject("VHS_Overlay");
        go.transform.SetParent(transform, false);

        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 220;
        go.AddComponent<CanvasScaler>();
        _overlayCanvas = canvas;

        _strips = new Image[8];
        for (int i = 0; i < _strips.Length; i++)
        {
            var strip = new GameObject($"Strip{i}");
            strip.transform.SetParent(go.transform, false);
            var img = strip.AddComponent<Image>();
            img.color = i % 2 == 0
                ? new Color(0f, 0f, 0f, 0.88f)
                : new Color(0.85f, 0.9f, 1f, 0.14f);

            float yAnchor = Random.Range(0.05f, 0.88f);
            float height  = Random.Range(0.007f, 0.045f);
            float xShift  = Random.Range(-0.06f, 0.06f);
            var rt = img.rectTransform;
            rt.anchorMin = new Vector2(xShift,      yAnchor);
            rt.anchorMax = new Vector2(1f + xShift,  yAnchor + height);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            strip.SetActive(false);
            _strips[i] = img;
        }

        go.SetActive(false);
    }

    private void ShowStrips()
    {
        if (_strips == null) return;
        foreach (var s in _strips)
        {
            float y = Random.Range(0.05f, 0.88f);
            float h = Random.Range(0.006f, 0.04f);
            var rt  = s.rectTransform;
            rt.anchorMin = new Vector2(rt.anchorMin.x, y);
            rt.anchorMax = new Vector2(rt.anchorMax.x, y + h);
            s.gameObject.SetActive(Random.value > 0.4f);
        }
    }

    private void HideStrips()
    {
        if (_strips == null) return;
        foreach (var s in _strips) s.gameObject.SetActive(false);
    }
}
