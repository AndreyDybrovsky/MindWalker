using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Красная виньетка-вспышка при получении урона игроком.
/// Автоматически создаёт UI Canvas поверх экрана при первом использовании.
/// Добавляется на тот же объект что и PlayerHealth.
/// </summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDamageVignette : MonoBehaviour
{
    [Header("Виньетка")]
    [SerializeField] private Color vignetteColor = new Color(0.85f, 0.05f, 0.05f, 0f);
    [SerializeField] private float peakAlpha = 0.55f;
    [SerializeField] private float flashInDuration = 0.08f;
    [SerializeField] private float holdDuration = 0.05f;
    [SerializeField] private float fadeOutDuration = 0.55f;

    [Header("Пульс при критическом здоровье")]
    [Tooltip("Ниже какого % HP виньетка начинает медленно пульсировать.")]
    [SerializeField] private float criticalHealthThreshold = 0.25f;
    [SerializeField] private float pulseAlpha = 0.25f;
    [SerializeField] private float pulseSpeed = 1.2f;

    private PlayerHealth _health;
    private Image _vignetteImage;
    private CanvasGroup _canvasGroup;
    private Coroutine _flashRoutine;
    private float _prevHealth;
    private bool _criticalPulseActive;

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        BuildVignetteUI();
    }

    private void OnEnable()
    {
        _health.OnHealthChanged.AddListener(OnHealthChanged);
        _prevHealth = _health.CurrentHealth;
    }

    private void OnDisable()
    {
        _health.OnHealthChanged.RemoveListener(OnHealthChanged);
    }

    private void Update()
    {
        bool isCritical = _health.HealthPercentage <= criticalHealthThreshold && !_health.IsDead;

        if (isCritical && !_criticalPulseActive)
        {
            _criticalPulseActive = true;
        }
        else if (!isCritical && _criticalPulseActive)
        {
            _criticalPulseActive = false;
        }

        // Пульсация при критическом здоровье (только если не идёт flash от урона)
        if (_criticalPulseActive && _flashRoutine == null && _vignetteImage != null)
        {
            float pulse = (Mathf.Sin(Time.unscaledTime * pulseSpeed * Mathf.PI * 2f) + 1f) * 0.5f;
            Color c = vignetteColor;
            c.a = pulse * pulseAlpha;
            _vignetteImage.color = c;
        }
    }

    private void OnHealthChanged(float newHealth)
    {
        bool tookDamage = newHealth < _prevHealth - 0.01f;
        _prevHealth = newHealth;

        if (!tookDamage || _health.SuppressDamageFeedback) return;

        if (_flashRoutine != null)
            StopCoroutine(_flashRoutine);
        _flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Flash in
        for (float t = 0; t < flashInDuration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(0f, peakAlpha, t / flashInDuration));
            yield return null;
        }
        SetAlpha(peakAlpha);

        // Hold
        if (holdDuration > 0f)
            yield return new WaitForSecondsRealtime(holdDuration);

        // Fade out
        for (float t = 0; t < fadeOutDuration; t += Time.unscaledDeltaTime)
        {
            SetAlpha(Mathf.Lerp(peakAlpha, 0f, t / fadeOutDuration));
            yield return null;
        }
        SetAlpha(0f);
        _flashRoutine = null;
    }

    private void SetAlpha(float alpha)
    {
        if (_vignetteImage == null) return;
        Color c = vignetteColor;
        c.a = alpha;
        _vignetteImage.color = c;
    }

    private void BuildVignetteUI()
    {
        // Canvas поверх всего
        GameObject canvasGo = new GameObject("DamageVignetteCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasGo.AddComponent<CanvasScaler>();

        // Виньетка — borderless image на весь экран
        GameObject imgGo = new GameObject("Vignette");
        imgGo.transform.SetParent(canvasGo.transform, false);

        RectTransform rt = imgGo.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        _vignetteImage = imgGo.AddComponent<Image>();
        _vignetteImage.sprite = CreateVignetteSprite();
        _vignetteImage.color = new Color(vignetteColor.r, vignetteColor.g, vignetteColor.b, 0f);
        _vignetteImage.raycastTarget = false;
    }

    private static Sprite CreateVignetteSprite()
    {
        // Радиальный градиент: прозрачный центр, непрозрачные края
        int size = 128;
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2(size / 2f, size / 2f);
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - center.x) / center.x;
                float dy = (y - center.y) / center.y;
                float dist = Mathf.Sqrt(dx * dx + dy * dy); // 0 = center, 1+ = edge
                float alpha = Mathf.Clamp01(Mathf.Pow(dist, 1.8f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();

        return Sprite.Create(tex,
            new Rect(0, 0, size, size),
            new Vector2(0.5f, 0.5f),
            size);
    }
}
