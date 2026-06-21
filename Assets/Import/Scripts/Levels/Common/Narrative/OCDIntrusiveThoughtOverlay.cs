using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Навязчивые мысли персонажа с ОКР: короткие фразы появляются по краям экрана в День 3–4.
/// Вешается на любой постоянный GameObject на сцене OCD.
/// OCDMissionDayController уведомляет через SetDay() автоматически.
/// </summary>
public class OCDIntrusiveThoughtOverlay : MonoBehaviour
{
    public static OCDIntrusiveThoughtOverlay Instance { get; private set; }

    [Header("Навязчивые мысли")]
    [SerializeField, TextArea(1, 2)] private string[] thoughts =
    {
        "А вдруг я не выключил утюг?",
        "Надо проверить ещё раз",
        "Что если что-то случится?",
        "Мне надо вернуться",
        "Почему я не проверил?",
        "Это всё неправильно",
        "Всё равно ведь не то",
        "Нельзя так оставлять",
        "Надо переделать",
        "Что если я ошибся?",
        "Надо было иначе",
        "Что-то точно не так",
        "А вдруг я что-то забыл?",
        "Я должен был проверить",
        "А вдруг это важно?",
        "Нет, всё не так"
    };

    [Header("Интенсивность — День 3 (редко, 1 мысль)")]
    [SerializeField] private float day3IntervalMin = 8f;
    [SerializeField] private float day3IntervalMax = 14f;
    [SerializeField] private int   day3MaxParallel = 1;

    [Header("Интенсивность — День 4 (часто, несколько)")]
    [SerializeField] private float day4IntervalMin = 3f;
    [SerializeField] private float day4IntervalMax = 7f;
    [SerializeField] private int   day4MaxParallel = 3;

    [Header("Анимация")]
    [SerializeField] private float fadeInDuration  = 0.55f;
    [SerializeField] private float holdDuration    = 2.8f;
    [SerializeField] private float fadeOutDuration = 0.9f;
    [Tooltip("Максимальная прозрачность мысли (0-1). Чуть меньше 1 даёт призрачный эффект.")]
    [SerializeField] private float peakAlpha       = 0.72f;

    [Header("Стиль")]
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private float         fontSize    = 15f;
    [SerializeField] private Color         textColor   = new Color(0.18f, 0.10f, 0.12f, 1f);
    [Tooltip("Максимальный угол наклона текста (±°). До 25° текст не переворачивается.")]
    [SerializeField] private float         maxRotation = 20f;
    [Tooltip("Ширина краевой полосы, в которой появляются мысли (доля экрана 0–1).")]
    [SerializeField] private float         edgeStrip   = 0.13f;
    [SerializeField] private Vector2       textSize    = new Vector2(195f, 85f);

    private Canvas    _canvas;
    private int       _currentDay;
    private int       _activeCount;
    private Coroutine _loop;

    // ─── Lifecycle ────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        BuildCanvas();
    }

    private void Start()
    {
        // OCDMissionDayController.Awake() (order -500) вызывает SetDay до того, как наш Instance создан.
        // Повторяем вызов здесь — к Start() все Awake() уже выполнены.
        if (OCDMissionDayController.Instance != null)
            SetDay(OCDMissionDayController.Instance.GetActiveDayIndex());
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // ─── Публичный API ───────────────────────────────────────────────────

    /// <summary>Устанавливает текущий день. День ≥ 3 запускает мысли, День 1–2 останавливает.</summary>
    public void SetDay(int day)
    {
        _currentDay = day;

        if (_loop != null) { StopCoroutine(_loop); _loop = null; }

        if (day >= 3)
            _loop = StartCoroutine(SpawnLoop());
    }

    // ─── Корутины ────────────────────────────────────────────────────────

    private IEnumerator SpawnLoop()
    {
        while (true)
        {
            bool day4  = _currentDay >= 4;
            float iMin = day4 ? day4IntervalMin : day3IntervalMin;
            float iMax = day4 ? day4IntervalMax : day3IntervalMax;
            int   maxP = day4 ? day4MaxParallel : day3MaxParallel;

            yield return new WaitForSecondsRealtime(Random.Range(iMin, iMax));

            if (_activeCount < maxP && thoughts.Length > 0)
                StartCoroutine(ShowThought());
        }
    }

    private IEnumerator ShowThought()
    {
        _activeCount++;

        var go = new GameObject("IT");
        go.transform.SetParent(_canvas.transform, false);

        var tmp                = go.AddComponent<TextMeshProUGUI>();
        tmp.text               = thoughts[Random.Range(0, thoughts.Length)];
        tmp.fontSize           = fontSize;
        tmp.color              = new Color(textColor.r, textColor.g, textColor.b, 0f);
        tmp.alignment          = TextAlignmentOptions.Center;
        tmp.enableWordWrapping = true;
        if (font != null) tmp.font = font;

        var rt       = tmp.rectTransform;
        rt.pivot     = Vector2.one * 0.5f;
        rt.sizeDelta = textSize;

        Vector2 anchor = RandomEdgeAnchor();
        rt.anchorMin = anchor;
        rt.anchorMax = anchor;
        rt.anchoredPosition = Vector2.zero;

        // Наклон: случайный, но не более ±maxRotation (текст не перевёрнут)
        go.transform.localEulerAngles = new Vector3(0f, 0f, Random.Range(-maxRotation, maxRotation));

        yield return Fade(tmp, 0f, peakAlpha, fadeInDuration);
        yield return new WaitForSecondsRealtime(holdDuration);
        yield return Fade(tmp, peakAlpha, 0f, fadeOutDuration);

        Destroy(go);
        _activeCount--;
    }

    // ─── Позиционирование по краям ───────────────────────────────────────

    private Vector2 RandomEdgeAnchor()
    {
        float s = edgeStrip;

        // Безопасный отступ: половина размера текста в долях экрана (при ref 1920×1080)
        float px = textSize.x / 1920f * 0.5f;
        float py = textSize.y / 1080f * 0.5f;

        // Центр текста должен быть не ближе px/py к краю экрана
        float xMin = px + 0.01f;
        float xMax = 1f - px - 0.01f;
        float yMin = py + 0.01f;
        float yMax = 1f - py - 0.01f;

        int edge = Random.Range(0, 4);
        switch (edge)
        {
            case 0: // верхний край
                return new Vector2(
                    Random.Range(xMin, xMax),
                    Random.Range(Mathf.Clamp(1f - s, yMin, yMax), yMax));

            case 1: // нижний край
                return new Vector2(
                    Random.Range(xMin, xMax),
                    Random.Range(yMin, Mathf.Clamp(s, yMin, yMax)));

            case 2: // левый край
                return new Vector2(
                    Random.Range(xMin, Mathf.Clamp(s, xMin, xMax)),
                    Random.Range(yMin, yMax));

            default: // правый край
                return new Vector2(
                    Random.Range(Mathf.Clamp(1f - s, xMin, xMax), xMax),
                    Random.Range(yMin, yMax));
        }
    }

    // ─── Утилиты ─────────────────────────────────────────────────────────

    private IEnumerator Fade(TextMeshProUGUI tmp, float from, float to, float dur)
    {
        float elapsed = 0f;
        Color c = tmp.color;
        while (elapsed < dur)
        {
            if (tmp == null) yield break;
            elapsed += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / dur));
            tmp.color = c;
            yield return null;
        }
        if (tmp != null) { c.a = to; tmp.color = c; }
    }

    private void BuildCanvas()
    {
        var go = new GameObject("IntrusiveThoughtCanvas");
        go.transform.SetParent(transform, false);

        var c          = go.AddComponent<Canvas>();
        c.renderMode   = RenderMode.ScreenSpaceOverlay;
        c.sortingOrder = 150; // выше обычного UI, намного ниже FadeCanvas (32700)

        var scaler                  = go.AddComponent<CanvasScaler>();
        scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight   = 0.5f;

        _canvas = c;
    }
}
