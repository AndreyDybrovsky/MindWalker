using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Упрощённая концовка без катсцены Cinemachine.
/// Повторяет финальный экран TrueEndingCutscene: слева большой заголовок + подзаголовок,
/// справа — панель статистики. Шрифт, размеры и позиции идентичны TrueVictory.
/// </summary>
public class SimpleEndingCutscene : MonoBehaviour
{
    [Header("Тексты")]
    [SerializeField] private string titleString    = "Концовка";
    [SerializeField] private string subtitleString = "Подзаголовок";

    [Header("Шрифт (horta SDF)")]
    [SerializeField] private TMP_FontAsset fontAsset;

    [Header("Тайминги")]
    [SerializeField] private float fadeInDuration      = 1.5f;
    [SerializeField] private float pauseBeforeText     = 1.0f;
    [SerializeField] private float textFadeInDuration  = 1.2f;
    [SerializeField] private float delayBetweenTexts   = 0.6f;
    [SerializeField] private float holdTextDuration    = 5.0f;
    [SerializeField] private float fadeOutDuration     = 1.5f;

    [Header("Статистика")]
    [SerializeField] private EndingStatsPanel statsPanel;

    [Header("Сцена")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ── Приватные ──────────────────────────────────────────────────────────

    private Image            _fadePanel;
    private TextMeshProUGUI  _titleText;
    private TextMeshProUGUI  _subtitleText;

    // ── Жизненный цикл ─────────────────────────────────────────────────────

    private void Start()
    {
        BuildUI();
        StartCoroutine(Play());
    }

    // ── Построение UI ──────────────────────────────────────────────────────

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("EndingCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        // Тёмная подложка
        _fadePanel = CreateFullscreenImage(canvasGo.transform, "FadePanel", Color.black);

        // Заголовок — слева (точь-в-точь как TrueVictory)
        _titleText = CreateText(canvasGo.transform, "TitleText",
            titleString,
            fontSize:    120f,
            alignment:   TextAlignmentOptions.Bottom,
            anchoredPos: new Vector2(-250f, 150f),
            sizeDelta:   new Vector2(200f,   50f));

        // Подзаголовок — ниже заголовка
        _subtitleText = CreateText(canvasGo.transform, "SubTitleText",
            subtitleString,
            fontSize:    100f,
            alignment:   TextAlignmentOptions.Top,
            anchoredPos: new Vector2(-250f, -50f),
            sizeDelta:   new Vector2(200f,   50f));

        SetAlpha(_titleText,    0f);
        SetAlpha(_subtitleText, 0f);
    }

    // ── Главная последовательность ─────────────────────────────────────────

    private IEnumerator Play()
    {
        // 1. Начинаем с чёрного экрана — fade in
        yield return FadePanel(1f, 0f, fadeInDuration);
        yield return new WaitForSeconds(pauseBeforeText);

        // 2. Заголовок
        yield return FadeText(_titleText, 0f, 1f, textFadeInDuration);
        yield return new WaitForSeconds(delayBetweenTexts);

        // 3. Подзаголовок
        yield return FadeText(_subtitleText, 0f, 1f, textFadeInDuration);

        // 4. Статистика
        GameStatsTracker.Instance?.Save();
        if (statsPanel != null)
        {
            statsPanel.SetTemplatesFromCode(_titleText, _subtitleText);
            yield return statsPanel.PlayEntrance();
        }

        // 5. Держим
        yield return new WaitForSeconds(holdTextDuration);

        // 6. Всё гаснет
        yield return FadeAllOut(fadeOutDuration);

        // 7. Главное меню
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ── Утилиты ────────────────────────────────────────────────────────────

    private TextMeshProUGUI CreateText(Transform parent, string goName, string text,
        float fontSize, TextAlignmentOptions alignment,
        Vector2 anchoredPos, Vector2 sizeDelta)
    {
        GameObject go  = new GameObject(goName);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();

        if (fontAsset != null)
            tmp.font = fontAsset;

        tmp.enableAutoSizing = false;
        tmp.fontSize    = fontSize;
        tmp.alignment   = alignment;
        tmp.color       = Color.white;
        tmp.text        = text;
        tmp.overflowMode = TextOverflowModes.Overflow;

        RectTransform rt = tmp.rectTransform;
        rt.anchorMin        = new Vector2(0.5f, 0.5f);
        rt.anchorMax        = new Vector2(0.5f, 0.5f);
        rt.pivot            = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;

        return tmp;
    }

    private static Image CreateFullscreenImage(Transform parent, string goName, Color color)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        RectTransform rt = img.rectTransform;
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
        return img;
    }

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = _fadePanel.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            _fadePanel.color = c;
            yield return null;
        }
        c.a = to;
        _fadePanel.color = c;
    }

    private IEnumerator FadeText(TextMeshProUGUI tmp, float from, float to, float duration)
    {
        if (tmp == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            tmp.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        tmp.alpha = to;
    }

    private IEnumerator FadeAllOut(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, elapsed / duration);
            SetAlpha(_titleText,    a);
            SetAlpha(_subtitleText, a);
            statsPanel?.SetRootAlpha(a);
            yield return null;
        }
        SetAlpha(_titleText,    0f);
        SetAlpha(_subtitleText, 0f);
        statsPanel?.SetRootAlpha(0f);
    }

    private static void SetAlpha(TextMeshProUGUI tmp, float alpha)
    {
        if (tmp != null) tmp.alpha = alpha;
    }
}
