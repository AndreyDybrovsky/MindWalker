using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Экранная подсказка: буква для нажатия и прогресс последовательности.
/// </summary>
public class SlotMachineLetterUI : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text letterText;
    [SerializeField] private TMP_Text progressText;
    [SerializeField] private TMP_Text hintText;
    [SerializeField] private Image backgroundPanel;

    private const int SortOrder = 9200;
    private static SlotMachineLetterUI s_shared;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_shared = null;
    }

    static SlotMachineLetterUI()
    {
        SceneManager.sceneLoaded += (_, _) => s_shared = null;
    }

    /// <summary>Всегда экранный оверлей; префабный UI на автомате не используем.</summary>
    public static SlotMachineLetterUI GetSharedOverlay()
    {
        if (s_shared != null)
            return s_shared;

        GameObject root = new GameObject("SlotMachineLetterUI");
        DontDestroyOnLoad(root);
        s_shared = root.AddComponent<SlotMachineLetterUI>();
        s_shared.BuildUi();
        return s_shared;
    }

    private void BuildUi()
    {
        Canvas canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = SortOrder;

        CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        canvasGroup = gameObject.AddComponent<CanvasGroup>();
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;

        GameObject panelGo = new GameObject("Panel", typeof(RectTransform));
        panelGo.transform.SetParent(transform, false);

        RectTransform panelRt = panelGo.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0.5f, 0.5f);
        panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(520f, 320f);

        backgroundPanel = panelGo.AddComponent<Image>();
        backgroundPanel.color = new Color(0.05f, 0.05f, 0.08f, 0.82f);
        backgroundPanel.raycastTarget = false;

        letterText = CreateLabel(panelGo.transform, "Letter", 160f, new Vector2(0f, 40f));
        progressText = CreateLabel(panelGo.transform, "0/0", 36f, new Vector2(0f, -70f));
        hintText = CreateLabel(panelGo.transform, "", 28f, new Vector2(0f, -120f));

        TMP_Text fontSource = FindAnyObjectByType<TMP_Text>();
        if (fontSource != null)
        {
            letterText.font = fontSource.font;
            progressText.font = fontSource.font;
            hintText.font = fontSource.font;
        }

        gameObject.SetActive(true);
        Hide();
    }

    private static TMP_Text CreateLabel(Transform parent, string text, float fontSize, Vector2 anchoredPosition)
    {
        GameObject go = new GameObject("Label", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.5f);
        rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPosition;
        rt.sizeDelta = new Vector2(480f, 180f);

        TMP_Text tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.white;
        tmp.raycastTarget = false;
        return tmp;
    }

    public void ShowApproaching(string hint)
    {
        if (letterText == null || canvasGroup == null)
            BuildUi();

        letterText.text = "…";
        progressText.text = string.Empty;
        hintText.text = hint ?? string.Empty;

        letterText.color = Color.white;
        letterText.ForceMeshUpdate();
        hintText.ForceMeshUpdate();

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(true);
    }

    public void Show(char letter, int stepIndex, int sequenceLength, string hint)
    {
        if (letterText == null || canvasGroup == null)
            BuildUi();

        letterText.text = letter.ToString();
        progressText.text = FormatProgress(stepIndex, sequenceLength);
        hintText.text = hint ?? string.Empty;

        letterText.ForceMeshUpdate();
        progressText.ForceMeshUpdate();
        hintText.ForceMeshUpdate();

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(true);
    }

    public void Hide()
    {
        if (canvasGroup == null)
            return;

        canvasGroup.alpha = 0f;
    }

    public void FlashWrong()
    {
        if (letterText == null)
            return;

        letterText.color = new Color(1f, 0.35f, 0.35f);
    }

    public void ResetLetterColor()
    {
        if (letterText != null)
            letterText.color = Color.white;
    }

    private static string FormatProgress(int stepIndex, int sequenceLength)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.T("slotmachine.progress", stepIndex + 1, sequenceLength);

        return $"{stepIndex + 1}/{sequenceLength}";
    }
}
