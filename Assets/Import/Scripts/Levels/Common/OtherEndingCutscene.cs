using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Концовка без Cinemachine-катсцены.
/// Повторяет финальный экран TrueEndingCutscene: те же TMP-объекты из сцены,
/// тот же порядок — появление текста → статистика → затухание → переход.
/// </summary>
public class OtherEndingCutscene : MonoBehaviour
{
    [Header("Текст")]
    [Tooltip("TMP объект из сцены — заголовок концовки (например, 'Истинная концовка').")]
    [SerializeField] private TextMeshProUGUI titleText;
    [Tooltip("TMP объект из сцены — подзаголовок / краткая подпись.")]
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private float textFadeInDuration  = 1.2f;
    [SerializeField] private float delayBetweenTexts   = 0.6f;

    [Header("Тайминги")]
    [SerializeField] private float pauseBeforeText     = 1.0f;
    [SerializeField] private float holdTextDuration    = 3.0f;
    [SerializeField] private float textFadeOutDuration = 1.5f;

    [Header("Статистика")]
    [SerializeField] private EndingStatsPanel statsPanel;

    [Header("Сцена")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    // ─── Жизненный цикл ────────────────────────────────────────────────────

    private void Start()
    {
        BuildUI();
        StartCoroutine(Play());
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
        SetTextAlpha(titleText,    0f);
        SetTextAlpha(subtitleText, 0f);
    }

    // ─── Главная последовательность ────────────────────────────────────────

    private IEnumerator Play()
    {
        // 1. Пауза перед появлением текста
        yield return new WaitForSeconds(pauseBeforeText);

        // 2. Заголовок
        yield return FadeText(titleText, 0f, 1f, textFadeInDuration);
        yield return new WaitForSeconds(delayBetweenTexts);

        // 3. Подзаголовок
        yield return FadeText(subtitleText, 0f, 1f, textFadeInDuration);

        // 4. Статистика выезжает пункт за пунктом
        GameStatsTracker.Instance?.Save();
        if (statsPanel != null)
            yield return statsPanel.PlayEntrance();

        // 5. Держим экран
        yield return new WaitForSeconds(holdTextDuration);

        // 6. Всё гаснет одновременно
        yield return FadeAllOut(textFadeOutDuration);

        // 7. Переход
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ─── UI утилиты ────────────────────────────────────────────────────────

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

    private static void SetTextAlpha(TextMeshProUGUI text, float alpha)
    {
        if (text != null) text.alpha = alpha;
    }
}
