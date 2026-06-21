using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Сенсорный HUD-индикатор текущего цвета оружия (правый нижний угол).
/// Мягкий круг с glow, подпись цвета, scale-pop при смене, fade при появлении/скрытии.
/// </summary>
public class ColorHUDIndicator : MonoBehaviour
{
    [Header("Размеры")]
    [SerializeField] private float circleSize   = 80f;
    [SerializeField] private float glowSize     = 112f;
    [SerializeField] private float margin       = 32f;
    [SerializeField] private float verticalLift = 50f;

    [Header("Звук переключения")]
    [SerializeField] private AudioClip switchSound;
    [SerializeField, Range(0f, 1f)] private float switchVolume = 0.6f;

    // ── Runtime refs ──────────────────────────────────────────────────────────
    private GameObject _root;
    private CanvasGroup _rootGroup;
    private Image _glow;
    private Image _fill;
    private TextMeshProUGUI _label;
    private RectTransform _fillRt;
    private RectTransform _glowRt;

    private Coroutine _fadeRoutine;
    private Coroutine _popRoutine;
    private bool _built;
    private ElementColor _lastColor;

    // ── Unity ────────────────────────────────────────────────────────────────

    private void Start()
    {
        BuildUI();
        if (ColorManager.ColoringUnlocked)
            ShowImmediate(ColorManager.CurrentColor);
        else
            _root.SetActive(false);
    }

    private void OnEnable()
    {
        ColorManager.OnColorChanged += OnColorChanged;
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        ColorManager.OnColorChanged -= OnColorChanged;
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnDestroy() { if (_root) Destroy(_root); }

    // ── Events ────────────────────────────────────────────────────────────────

    private void OnColorChanged(ElementColor color)
    {
        if (!_built) return;

        if (!_root.activeSelf)
        {
            _root.SetActive(true);
            _rootGroup.alpha = 0f;
        }

        SetColors(color);
        PlaySwitchSound();

        if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
        _fadeRoutine = StartCoroutine(FadeTo(1f, 0.22f));

        if (_popRoutine != null) StopCoroutine(_popRoutine);
        _popRoutine = StartCoroutine(ScalePop());
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        if (_built && _root != null && _root.activeSelf)
            UpdateLabel(_lastColor);
    }

    // ── Build ────────────────────────────────────────────────────────────────

    private void BuildUI()
    {
        _root = new GameObject("ColorHUD_Root");

        var canvas = _root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 500;

        var scaler = _root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _root.AddComponent<GraphicRaycaster>();
        _rootGroup = _root.AddComponent<CanvasGroup>();
        _rootGroup.blocksRaycasts = false;
        _rootGroup.interactable   = false;

        float posY = margin + verticalLift;

        // Glow (outer soft circle)
        var glowGo = MakeRt("Glow", _root.transform);
        _glow    = glowGo.AddComponent<Image>();
        _glow.type = Image.Type.Simple;
        _glow.color = new Color(1f, 1f, 1f, 0f);
        _glowRt = glowGo.GetComponent<RectTransform>();
        Anchor(_glowRt, new Vector2(1f, 0f), new Vector2(1f, 0f),
               new Vector2(-margin, posY), new Vector2(glowSize, glowSize));

        // Fill (main circle)
        var fillGo = MakeRt("Fill", _root.transform);
        _fill    = fillGo.AddComponent<Image>();
        _fill.type = Image.Type.Simple;
        _fillRt  = fillGo.GetComponent<RectTransform>();
        Anchor(_fillRt, new Vector2(1f, 0f), new Vector2(1f, 0f),
               new Vector2(-margin, posY), new Vector2(circleSize, circleSize));

        // Label (color name) — below the circle
        var labelGo = MakeRt("Label", _root.transform);
        _label = labelGo.AddComponent<TextMeshProUGUI>();
        _label.fontSize    = 20f;
        _label.fontStyle   = FontStyles.Bold;
        _label.color       = new Color(1f, 1f, 1f, 0.9f);
        _label.alignment   = TextAlignmentOptions.Center;
        _label.raycastTarget = false;
        var lrt = labelGo.GetComponent<RectTransform>();
        Anchor(lrt, new Vector2(1f, 0f), new Vector2(1f, 0f),
               new Vector2(-margin, posY - 34f), new Vector2(circleSize + 32f, 28f));

        _built = true;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void ShowImmediate(ElementColor color)
    {
        _root.SetActive(true);
        _rootGroup.alpha = 1f;
        SetColors(color);
    }

    private void SetColors(ElementColor color)
    {
        _lastColor = color;
        Color c = ColorManager.ToColor(color);

        _fill.color = c;

        Color glow = c;
        glow.a = 0.28f;
        _glow.color = glow;

        UpdateLabel(color);
        _label.color = new Color(c.r * 0.9f + 0.1f, c.g * 0.9f + 0.1f, c.b * 0.9f + 0.1f, 0.95f);
    }

    private void UpdateLabel(ElementColor color)
    {
        _label.text = GetLocalizedColorName(color);
    }

    private string GetLocalizedColorName(ElementColor color)
    {
        string key = color switch
        {
            ElementColor.Red   => "autism.color.red",
            ElementColor.Blue  => "autism.color.blue",
            ElementColor.Green => "autism.color.green",
            _                  => null
        };

        if (key != null && LocalizationManager.Instance != null)
        {
            string localized = LocalizationManager.Instance.T(key);
            if (!string.IsNullOrEmpty(localized) && localized != key)
                return localized.ToUpper();
        }

        return ColorManager.ToRu(color).ToUpper();
    }

    private void PlaySwitchSound()
    {
        if (switchSound == null) return;
        float sfx = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().sfxVolume : 1f;
        AudioSource.PlayClipAtPoint(switchSound, Camera.main ? Camera.main.transform.position : Vector3.zero,
            switchVolume * Mathf.Clamp01(sfx));
    }

    // ── Coroutines ────────────────────────────────────────────────────────────

    private IEnumerator FadeTo(float target, float duration)
    {
        float start = _rootGroup.alpha;
        float t = 0f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / duration;
            _rootGroup.alpha = Mathf.Lerp(start, target, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        _rootGroup.alpha = target;
    }

    private IEnumerator ScalePop()
    {
        // 0 → 1.18 → 1 over ~0.28s
        float[] keys   = { 0f, 0.12f, 0.28f };
        float[] scales = { 0.75f, 1.18f, 1f };

        for (int i = 0; i < keys.Length - 1; i++)
        {
            float elapsed = 0f;
            float dur = keys[i + 1] - keys[i];
            float from = scales[i], to = scales[i + 1];

            while (elapsed < dur)
            {
                elapsed += Time.unscaledDeltaTime;
                float s = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / dur));
                _fillRt.localScale = Vector3.one * s;
                _glowRt.localScale = Vector3.one * (s * 1.08f);
                yield return null;
            }
        }

        _fillRt.localScale = Vector3.one;
        _glowRt.localScale = Vector3.one;
    }

    // ── Static helpers ────────────────────────────────────────────────────────

    private static GameObject MakeRt(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void Anchor(RectTransform rt, Vector2 anchorMin, Vector2 anchorMax,
                                Vector2 pos, Vector2 size)
    {
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = anchorMin;
        rt.anchoredPosition = pos;
        rt.sizeDelta        = size;
    }
}
