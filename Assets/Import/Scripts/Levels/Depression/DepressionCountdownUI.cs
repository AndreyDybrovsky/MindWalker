using System;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Таймер слева сверху (как OCD Objective) для моментов на уровне Depression.
/// </summary>
public class DepressionCountdownUI : MonoBehaviour
{
    private const int SortOrder = 10041;

    private static DepressionCountdownUI s_shared;

    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TMP_Text timerText;

    [Header("Позиция (левый верх)")]
    [SerializeField] private float leftPadding = 16f;
    [SerializeField] private float topPadding = 16f;
    [SerializeField] private float panelWidth = 640f;

    [Header("Стиль")]
    [SerializeField] private TMP_Text styleReference;

    private bool _active;
    private float _remainingSeconds;
    private string _labelKey = "scene.depression.calm_timer";
    private string _labelFallback = "Успокойте: {0}";
    private Action _onExpired;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        s_shared = null;
    }

    static DepressionCountdownUI()
    {
        SceneManager.sceneLoaded += (_, _) => s_shared = null;
    }

    public static DepressionCountdownUI GetShared()
    {
        if (s_shared != null)
            return s_shared;

        DepressionCountdownUI existing = FindFirstObjectByType<DepressionCountdownUI>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.ResolveReferences();
            s_shared = existing;
            return s_shared;
        }

        GameObject root = new GameObject("DepressionCountdownUI_Auto");
        s_shared = root.AddComponent<DepressionCountdownUI>();
        s_shared.BuildUi();
        return s_shared;
    }

    public static void StartCountdown(
        float durationSeconds,
        string labelLocalizationKey,
        string labelFallback,
        TMP_Text styleReference,
        Action onExpired)
    {
        DepressionCountdownUI ui = GetShared();
        ui.Begin(durationSeconds, labelLocalizationKey, labelFallback, styleReference, onExpired);
    }

    public static void StopCountdown()
    {
        if (s_shared == null)
            return;

        s_shared.End();
    }

    public void ApplyStyleFrom(TMP_Text reference)
    {
        if (reference == null || timerText == null)
            return;

        styleReference = reference;
        TmpTextStyleUtility.CopyStyle(timerText, reference, TmpTextStyleUtility.CopyMode.MatchReferenceSize);
        ApplyTextAlignment();
        RefreshLabel();
    }

    private void Awake()
    {
        ResolveReferences();
        ApplyPanelLayout();
        SetVisible(false);
    }

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
        RefreshLabel();
    }

    private void Update()
    {
        if (!_active)
            return;

        _remainingSeconds -= Time.deltaTime;
        RefreshLabel();

        if (_remainingSeconds > 0f)
            return;

        _active = false;
        Action callback = _onExpired;
        _onExpired = null;
        SetVisible(false);
        callback?.Invoke();
    }

    private void Begin(
        float durationSeconds,
        string labelLocalizationKey,
        string labelFallback,
        TMP_Text reference,
        Action onExpired)
    {
        ResolveReferences();
        _remainingSeconds = Mathf.Max(0.01f, durationSeconds);
        _labelKey = labelLocalizationKey;
        _labelFallback = labelFallback;
        _onExpired = onExpired;
        _active = true;

        if (reference != null)
            ApplyStyleFrom(reference);
        else if (styleReference != null)
            ApplyStyleFrom(styleReference);

        ApplyPanelLayout();
        RefreshLabel();
        SetVisible(true);
    }

    private void End()
    {
        _active = false;
        _onExpired = null;
        SetVisible(false);
    }

    private void RefreshLabel()
    {
        if (timerText == null)
            return;

        string timeText = FormatTime(Mathf.Max(0f, _remainingSeconds));
        timerText.text = LocalizedTextResolver.Resolve(_labelKey, new[] { timeText }, FormatFallback(_labelFallback, timeText));
        timerText.ForceMeshUpdate();
    }

    private static string FormatFallback(string fallback, string timeText)
    {
        if (string.IsNullOrEmpty(fallback))
            return timeText;

        try
        {
            return string.Format(fallback, timeText);
        }
        catch
        {
            return fallback;
        }
    }

    private static string FormatTime(float seconds)
    {
        int total = Mathf.CeilToInt(seconds);
        int minutes = total / 60;
        int secs = total % 60;
        return minutes > 0 ? $"{minutes}:{secs:00}" : $"{secs}";
    }

    private void SetVisible(bool visible)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = visible ? 1f : 0f;

        gameObject.SetActive(visible);
    }

    private void ResolveReferences()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (timerText == null)
            timerText = GetComponentInChildren<TMP_Text>(true);
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
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;

        GameObject panel = new GameObject("TimerPanel");
        panel.transform.SetParent(transform, false);
        panel.AddComponent<RectTransform>();

        GameObject lineGo = new GameObject("TimerText");
        lineGo.transform.SetParent(panel.transform, false);

        RectTransform rt = lineGo.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(panelWidth, 72f);

        timerText = lineGo.AddComponent<TextMeshProUGUI>();
        ApplyTextAlignment();
        timerText.enableWordWrapping = true;
        timerText.overflowMode = TextOverflowModes.Overflow;
        timerText.raycastTarget = false;

        ApplyPanelLayout();
    }

    private void ApplyPanelLayout()
    {
        Transform panel = transform.Find("TimerPanel");
        if (panel == null && timerText != null)
            panel = timerText.transform.parent;

        if (panel is RectTransform panelRt)
        {
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(leftPadding, -topPadding);
            panelRt.sizeDelta = new Vector2(panelWidth, 72f);
        }

        if (timerText != null)
        {
            RectTransform rt = timerText.rectTransform;
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(panelWidth, 72f);
            ApplyTextAlignment();
        }
    }

    private void ApplyTextAlignment()
    {
        if (timerText == null)
            return;

        timerText.alignment = TextAlignmentOptions.TopLeft;
        timerText.horizontalAlignment = HorizontalAlignmentOptions.Left;
        timerText.verticalAlignment = VerticalAlignmentOptions.Top;
    }
}
