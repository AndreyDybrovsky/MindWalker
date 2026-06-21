using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Концовка без Cinemachine-катсцены.
/// Показывает локализованный заголовок и тело концовки со стилизацией под тип,
/// затем статистику, потом возвращает в главное меню.
/// </summary>
public class OtherEndingCutscene : MonoBehaviour
{
    [Header("Текст")]
    [Tooltip("TMP объект из сцены — заголовок концовки.")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("TMP объект из сцены — тело концовки.")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private float textFadeInDuration  = 1.2f;
    [SerializeField] private float delayBetweenTexts   = 0.8f;

    [Header("Тайминги")]
    [SerializeField] private float pauseBeforeText     = 1.0f;
    [SerializeField] private float holdTextDuration    = 4.0f;
    [SerializeField] private float textFadeOutDuration = 1.5f;

    [Header("Статистика")]
    [SerializeField] private EndingStatsPanel statsPanel;

    [Header("Сцена")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Тип концовки (для локализации и стиля)")]
    [SerializeField] private EndingType endingType = EndingType.FalseEnding;

    // ─── Жизненный цикл ────────────────────────────────────────────────────

    private void Start()
    {
        ApplyLocalizedText();
        BuildUI();
        StartCoroutine(Play());

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += _ => ApplyLocalizedText();
    }

    // ─── Локализация ───────────────────────────────────────────────────────

    private void ApplyLocalizedText()
    {
        string key = EndingTypeToKey(endingType);
        if (string.IsNullOrEmpty(key)) return;

        LocalizationManager loc = LocalizationManager.Instance;
        if (loc == null) return;

        if (titleText != null)
        {
            string t = loc.T($"ending.{key}.title");
            if (!string.IsNullOrEmpty(t) && t != $"ending.{key}.title")
                titleText.text = t;
        }

        if (subtitleText != null)
        {
            string b = loc.T($"ending.{key}.body");
            if (!string.IsNullOrEmpty(b) && b != $"ending.{key}.body")
                subtitleText.text = b;
        }
    }

    private static string EndingTypeToKey(EndingType type)
    {
        switch (type)
        {
            case EndingType.TrueEnding:  return "true";
            case EndingType.FalseEnding: return "false";
            case EndingType.BadEnding:   return "bad";
            case EndingType.Failure:     return "failure";
            default:                     return null;
        }
    }

    // ─── Построение UI ─────────────────────────────────────────────────────

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("OtherEndingCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        MigrateTextToCanvas(titleText,    canvasGo.transform);
        MigrateTextToCanvas(subtitleText, canvasGo.transform);

        ApplyEndingStyle();

        SetTextAlpha(titleText,    0f);
        SetTextAlpha(subtitleText, 0f);
    }

    private static void MigrateTextToCanvas(TextMeshProUGUI text, Transform canvasTransform)
    {
        if (text == null) return;
        RectTransform rt    = text.rectTransform;
        Vector2 anchorMin   = rt.anchorMin;
        Vector2 anchorMax   = rt.anchorMax;
        Vector2 pivot       = rt.pivot;
        Vector2 anchoredPos = rt.anchoredPosition;
        Vector2 sizeDelta   = rt.sizeDelta;
        rt.SetParent(canvasTransform, false);
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
    }

    // Цвета и стиль по типу концовки
    private void ApplyEndingStyle()
    {
        switch (endingType)
        {
            case EndingType.TrueEnding:
                // Золотой + тёплый белый
                SetColor(titleText,    new Color(0.94f, 0.75f, 0.35f));
                SetColor(subtitleText, new Color(0.95f, 0.93f, 0.85f));
                break;

            case EndingType.FalseEnding:
                // Корпоративный холодный: синевато-белый + холодно-серый
                SetColor(titleText,    new Color(0.72f, 0.88f, 1.00f));
                SetColor(subtitleText, new Color(0.80f, 0.85f, 0.90f));
                break;

            case EndingType.BadEnding:
                // Тёмно-красный + серый
                SetColor(titleText,    new Color(0.80f, 0.12f, 0.12f));
                SetColor(subtitleText, new Color(0.72f, 0.72f, 0.72f));
                break;

            case EndingType.Failure:
                // Жёсткий красный + холодный серый
                SetColor(titleText,    new Color(0.90f, 0.18f, 0.08f));
                SetColor(subtitleText, new Color(0.75f, 0.75f, 0.75f));
                break;
        }
    }

    // ─── Главная последовательность ────────────────────────────────────────

    private IEnumerator Play()
    {
        yield return new WaitForSeconds(pauseBeforeText);

        // Заголовок — анимация зависит от типа концовки
        if (endingType == EndingType.Failure)
            yield return StampIn(titleText, textFadeInDuration);
        else
            yield return FadeText(titleText, 0f, 1f, textFadeInDuration);

        yield return new WaitForSeconds(delayBetweenTexts);

        // Тело — typewriter для всех, глитч для BadEnding
        if (endingType == EndingType.BadEnding)
            yield return GlitchReveal(subtitleText, Mathf.Max(textFadeInDuration, 2.5f));
        else
            yield return TypewriterReveal(subtitleText, Mathf.Max(textFadeInDuration, 2.0f));

        // Статистика
        GameStatsTracker.Instance?.Save();
        if (statsPanel != null)
            yield return statsPanel.PlayEntrance();

        yield return new WaitForSeconds(holdTextDuration);

        yield return FadeAllOut(textFadeOutDuration);

        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ─── Анимации текста ───────────────────────────────────────────────────

    // Посимвольное появление через maxVisibleCharacters
    private static IEnumerator TypewriterReveal(TextMeshProUGUI text, float duration)
    {
        if (text == null) yield break;
        text.alpha = 1f;
        text.ForceMeshUpdate();
        int total = text.textInfo.characterCount;
        text.maxVisibleCharacters = 0;

        float charsPerSec = total / Mathf.Max(0.01f, duration);
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            text.maxVisibleCharacters = Mathf.RoundToInt(charsPerSec * elapsed);
            yield return null;
        }
        text.maxVisibleCharacters = int.MaxValue;
    }

    // Глитч: текст нарастает с random-символами на границе
    private static IEnumerator GlitchReveal(TextMeshProUGUI text, float duration)
    {
        if (text == null) yield break;
        string final = text.text;
        text.alpha = 1f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            int revealed = Mathf.RoundToInt(t * final.Length);

            var sb = new System.Text.StringBuilder(final.Length);
            for (int i = 0; i < final.Length; i++)
            {
                char c = final[i];
                if (i < revealed)
                    sb.Append(c);
                else if (i < revealed + 4 && c != '\n' && c != ' ')
                    sb.Append((char)Random.Range(0x0410, 0x042F)); // случайная кирилица
                else
                    sb.Append(c == '\n' ? '\n' : ' ');
            }
            text.text = sb.ToString();
            yield return null;
        }
        text.text = final;
        text.maxVisibleCharacters = int.MaxValue;
    }

    // Штамп: заголовок влетает через масштаб 1.8→1.0 с одновременным появлением
    private static IEnumerator StampIn(TextMeshProUGUI text, float duration)
    {
        if (text == null) yield break;
        RectTransform rt = text.rectTransform;
        text.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            float scale = Mathf.Lerp(1.85f, 1.0f, t);
            rt.localScale = Vector3.one * scale;
            text.alpha = t;
            yield return null;
        }
        rt.localScale = Vector3.one;
        text.alpha = 1f;
    }

    // ─── Затухание ─────────────────────────────────────────────────────────

    private IEnumerator FadeText(TextMeshProUGUI text, float from, float to, float duration)
    {
        if (text == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            text.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        text.alpha = to;
    }

    private IEnumerator FadeAllOut(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, elapsed / duration);
            SetTextAlpha(titleText,    a);
            SetTextAlpha(subtitleText, a);
            statsPanel?.SetRootAlpha(a);
            yield return null;
        }
        SetTextAlpha(titleText,    0f);
        SetTextAlpha(subtitleText, 0f);
        statsPanel?.SetRootAlpha(0f);
    }

    // ─── Утилиты ───────────────────────────────────────────────────────────

    private static void SetTextAlpha(TextMeshProUGUI text, float alpha)
    {
        if (text != null) text.alpha = alpha;
    }

    private static void SetColor(TextMeshProUGUI text, Color color)
    {
        if (text != null) text.color = color;
    }
}
