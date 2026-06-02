using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Цели слева сверху для уровня ОКР. Стиль — с назначенного TMP-образца, текст — из ключей локализации.
/// </summary>
public class OCDObjectiveUI : MonoBehaviour
{
    private const int SortOrder = 10040;

    private static OCDObjectiveUI s_shared;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text previousObjectiveText;
    [SerializeField] private TMP_Text currentObjectiveText;

    [Header("Стиль")]
    [Tooltip("TMP-образец (например QuestText). Содержимое не копируется.")]
    [SerializeField] private TMP_Text styleReference;

    [Header("Позиция (левый верх)")]
    [SerializeField] private float leftPadding = 16f;
    [SerializeField] private float topPadding = 16f;
    [SerializeField] private float panelWidth = 640f;
    [SerializeField] private float lineSpacing = 8f;

    private string _completedKey;
    private string[] _completedFormatArgs;
    private string _completedFallback;
    private string _currentKey;
    private string[] _currentFormatArgs;
    private string _currentFallback;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_shared = null;
    }

    static OCDObjectiveUI()
    {
        SceneManager.sceneLoaded += (_, _) => s_shared = null;
    }

    public static void SetSharedInstance(OCDObjectiveUI instance)
    {
        if (instance == null)
            return;

        instance.ResolveReferences();
        s_shared = instance;
    }

    public static OCDObjectiveUI GetShared()
    {
        if (s_shared != null)
            return s_shared;

        OCDObjectiveUI existing = FindFirstObjectByType<OCDObjectiveUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.ResolveReferences();
            s_shared = existing;
            return s_shared;
        }

        GameObject root = new GameObject("OCDObjectiveUI_Auto");
        s_shared = root.AddComponent<OCDObjectiveUI>();
        s_shared.BuildUi();
        return s_shared;
    }

    /// <summary>TMP-образец с панели (если не задан на триггере).</summary>
    public TMP_Text ResolveStyleReference()
    {
        if (styleReference != null)
            return styleReference;

        if (currentObjectiveText != null)
            return currentObjectiveText;

        return previousObjectiveText;
    }

    /// <summary>Задать TMP-образец стиля (обычно с <see cref="OCDMomentTrigger"/>).</summary>
    public void ApplyStyleFrom(TMP_Text reference)
    {
        if (reference != null)
            styleReference = reference;

        ResolveReferences();
        ApplyStyleToLines();
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyPanelLayout();
        ApplyStyleToLines();
        HidePreviousLine();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (Application.isPlaying)
            return;

        ResolveReferences();
        ApplyPanelLayout();
    }
#endif

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        RefreshFromStoredKeys();
    }

    public void ShowCurrent(string localizationKey, string[] formatArgs, string fallback)
    {
        _completedKey = null;
        _completedFormatArgs = null;
        _completedFallback = null;
        _currentKey = localizationKey;
        _currentFormatArgs = formatArgs;
        _currentFallback = fallback;

        ResolveReferences();
        ApplyPanelLayout();
        ApplyStyleToLines();
        HidePreviousLine();
        ApplyCurrentLine(ResolveLocalized(_currentKey, _currentFormatArgs, _currentFallback));
        SetRootVisible(true);
    }

    public void Advance(string completedKey, string[] completedArgs, string completedFallback,
        string nextKey, string[] nextArgs, string nextFallback)
    {
        _completedKey = completedKey;
        _completedFormatArgs = completedArgs;
        _completedFallback = completedFallback;
        _currentKey = nextKey;
        _currentFormatArgs = nextArgs;
        _currentFallback = nextFallback;

        ResolveReferences();
        ApplyPanelLayout();
        ApplyStyleToLines();
        RefreshFromStoredKeys();
        SetRootVisible(true);
    }

    private void RefreshFromStoredKeys()
    {
        ResolveReferences();
        ApplyPanelLayout();
        ApplyStyleToLines();

        if (!string.IsNullOrEmpty(_completedKey) || !string.IsNullOrEmpty(_completedFallback))
        {
            string completed = ResolveLocalized(_completedKey, _completedFormatArgs, _completedFallback);
            ApplyPreviousLine(completed);
        }
        else
        {
            HidePreviousLine();
        }

        if (!string.IsNullOrEmpty(_currentKey) || !string.IsNullOrEmpty(_currentFallback))
        {
            ApplyCurrentLine(ResolveLocalized(_currentKey, _currentFormatArgs, _currentFallback));
        }
        else if (currentObjectiveText != null)
        {
            currentObjectiveText.text = string.Empty;
        }
    }

    private void ApplyStyleToLines()
    {
        if (styleReference == null)
            return;

        if (currentObjectiveText != null)
        {
            TmpTextStyleUtility.CopyStyle(
                currentObjectiveText,
                styleReference,
                TmpTextStyleUtility.CopyMode.MatchReferenceSize);
        }

        if (previousObjectiveText != null)
        {
            TmpTextStyleUtility.CopyStyle(
                previousObjectiveText,
                styleReference,
                TmpTextStyleUtility.CopyMode.MatchReferenceSize,
                strikethrough: true);
        }

        ApplyTextAlignment();
        ReapplyTextAfterStyleChange();
    }

    private void ApplyTextAlignment()
    {
        ApplyLineTextAlignment(previousObjectiveText);
        ApplyLineTextAlignment(currentObjectiveText);
    }

    private static void ApplyLineTextAlignment(TMP_Text text)
    {
        if (text == null)
            return;

        text.alignment = TextAlignmentOptions.TopLeft;
        text.horizontalAlignment = HorizontalAlignmentOptions.Left;
        text.verticalAlignment = VerticalAlignmentOptions.Top;
    }

    private void ReapplyTextAfterStyleChange()
    {
        if (!string.IsNullOrEmpty(_completedKey) || !string.IsNullOrEmpty(_completedFallback))
        {
            ApplyPreviousLine(ResolveLocalized(_completedKey, _completedFormatArgs, _completedFallback));
        }

        if (!string.IsNullOrEmpty(_currentKey) || !string.IsNullOrEmpty(_currentFallback))
        {
            ApplyCurrentLine(ResolveLocalized(_currentKey, _currentFormatArgs, _currentFallback));
        }
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (previousObjectiveText == null || currentObjectiveText == null)
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                if (texts[i] == null)
                    continue;

                if (texts[i].gameObject.name == "PreviousObjective")
                    previousObjectiveText = texts[i];
                else if (texts[i].gameObject.name == "CurrentObjective")
                    currentObjectiveText = texts[i];
            }
        }
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
        canvasGroup.alpha = 1f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject panel = new GameObject("ObjectivePanel");
        panel.transform.SetParent(transform, false);

        RectTransform panelRt = panel.AddComponent<RectTransform>();
        previousObjectiveText = CreateLine(panel.transform, "PreviousObjective", 0f);
        currentObjectiveText = CreateLine(panel.transform, "CurrentObjective", -(GetLineHeight() + lineSpacing));

        ApplyPanelLayout();
        ApplyStyleToLines();
    }

    private void ApplyPanelLayout()
    {
        RectTransform panelRt = ResolvePanelRect();
        if (panelRt == null)
            return;

        panelRt.anchorMin = new Vector2(0f, 1f);
        panelRt.anchorMax = new Vector2(0f, 1f);
        panelRt.pivot = new Vector2(0f, 1f);
        panelRt.anchoredPosition = new Vector2(leftPadding, -topPadding);
        panelRt.sizeDelta = new Vector2(panelWidth, GetLineHeight() * 2f + lineSpacing);

        ApplyLineLayout(previousObjectiveText, 0f);
        ApplyLineLayout(currentObjectiveText, -(GetLineHeight() + lineSpacing));
        ApplyTextAlignment();
    }

    private RectTransform ResolvePanelRect()
    {
        if (previousObjectiveText != null)
            return previousObjectiveText.transform.parent as RectTransform;

        if (currentObjectiveText != null)
            return currentObjectiveText.transform.parent as RectTransform;

        Transform panel = transform.Find("ObjectivePanel");
        return panel != null ? panel as RectTransform : transform as RectTransform;
    }

    private void ApplyLineLayout(TMP_Text line, float yOffset)
    {
        if (line == null)
            return;

        RectTransform rt = line.rectTransform;
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(panelWidth, GetLineHeight());
    }

    private TMP_Text CreateLine(Transform parent, string objectName, float yOffset)
    {
        GameObject lineGo = new GameObject(objectName);
        lineGo.transform.SetParent(parent, false);

        RectTransform rt = lineGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.anchoredPosition = new Vector2(0f, yOffset);
        rt.sizeDelta = new Vector2(panelWidth, GetLineHeight());

        TMP_Text text = lineGo.AddComponent<TextMeshProUGUI>();
        ApplyLineTextAlignment(text);
        text.enableWordWrapping = true;
        text.overflowMode = TextOverflowModes.Overflow;
        text.richText = true;
        text.raycastTarget = false;
        return text;
    }

    private float GetLineHeight() => 72f;

    private void ApplyPreviousLine(string text)
    {
        if (previousObjectiveText == null)
            return;

        previousObjectiveText.gameObject.SetActive(!string.IsNullOrEmpty(text));
        previousObjectiveText.text = string.IsNullOrEmpty(text) ? string.Empty : $"<s>{text}</s>";
        previousObjectiveText.ForceMeshUpdate();
    }

    private void ApplyCurrentLine(string text)
    {
        if (currentObjectiveText == null)
            return;

        currentObjectiveText.text = text ?? string.Empty;
        currentObjectiveText.ForceMeshUpdate();
    }

    private void HidePreviousLine()
    {
        if (previousObjectiveText == null)
            return;

        previousObjectiveText.text = string.Empty;
        previousObjectiveText.gameObject.SetActive(false);
    }

    private void SetRootVisible(bool visible)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;

        gameObject.SetActive(visible);
    }

    private static string ResolveLocalized(string key, string[] formatArgs, string fallback)
    {
        return LocalizedTextResolver.Resolve(key, formatArgs, fallback);
    }
}
