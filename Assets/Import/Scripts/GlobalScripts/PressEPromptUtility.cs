using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Общая подсказка «Нажмите E» — сначала PressEText из PauseMenu / лобби, иначе Resources.
/// </summary>
public static class PressEPromptUtility
{
    private const string ResourcesPath = "UI/PressEPrompt";
    private const string SharedObjectName = "PressEText";

    private static GameObject _cachedPrefab;
    private static PressEPromptView _sharedPrompt;

    public static bool IsSharedView(PressEPromptView view) => view != null && view == _sharedPrompt;

    public static PressEPromptView AcquireSharedPrompt()
    {
        if (_sharedPrompt != null)
            return _sharedPrompt;

        TextMeshProUGUI label = FindPressETextLabel();
        if (label == null)
            return null;

        GameObject pressE = label.gameObject;

        if (!pressE.TryGetComponent(out CanvasGroup canvasGroup))
            canvasGroup = pressE.AddComponent<CanvasGroup>();

        if (!pressE.TryGetComponent(out PressEPromptView view))
            view = pressE.AddComponent<PressEPromptView>();

        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
        canvasGroup.interactable = false;
        pressE.SetActive(false);

        _sharedPrompt = view;
        return _sharedPrompt;
    }

    public static PressEPromptView CreatePrompt()
    {
        PressEPromptView shared = AcquireSharedPrompt();
        if (shared != null)
            return shared;

        GameObject canvasGo = new GameObject("PressEPromptCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9000;
        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject prefab = LoadPrefab();
        if (prefab != null)
        {
            GameObject instance = Object.Instantiate(prefab, canvasGo.transform, false);
            RectTransform rt = instance.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchorMin = new Vector2(0.5f, 0.12f);
                rt.anchorMax = new Vector2(0.5f, 0.12f);
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.anchoredPosition = Vector2.zero;
            }

            PressEPromptView view = instance.GetComponent<PressEPromptView>();
            if (view == null)
                view = instance.GetComponentInChildren<PressEPromptView>(true);
            if (view != null)
            {
                view.CanvasGroup.alpha = 0f;
                return view;
            }
        }

        PressEPromptView fallback = CreateFallbackPrompt(canvasGo.transform);
        fallback.CanvasGroup.alpha = 0f;
        return fallback;
    }

    public static string ResolveLocalizedText(string key, string fallback)
    {
        if (!string.IsNullOrEmpty(key) && LocalizationManager.Instance != null)
        {
            string localized = LocalizationManager.Instance.T(key);
            if (!string.IsNullOrEmpty(localized) && localized != key)
                return localized;
        }

        return fallback;
    }

    private static TextMeshProUGUI FindPressETextLabel()
    {
        GameObject pressE = GameObject.Find(SharedObjectName);
        if (pressE != null && pressE.TryGetComponent(out TextMeshProUGUI onRoot))
            return onRoot;

        TextMeshProUGUI[] all = Object.FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.name == SharedObjectName)
                return all[i];
        }

        return null;
    }

    private static GameObject LoadPrefab()
    {
        if (_cachedPrefab != null)
            return _cachedPrefab;

        _cachedPrefab = Resources.Load<GameObject>(ResourcesPath);
        return _cachedPrefab;
    }

    private static PressEPromptView CreateFallbackPrompt(Transform parent)
    {
        GameObject root = new GameObject("PressEPrompt");
        root.transform.SetParent(parent, false);

        CanvasGroup group = root.AddComponent<CanvasGroup>();
        PressEPromptView view = root.AddComponent<PressEPromptView>();

        RectTransform rootRt = root.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.12f);
        rootRt.anchorMax = new Vector2(0.5f, 0.12f);
        rootRt.sizeDelta = new Vector2(700f, 130f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);

        GameObject buttonGo = new GameObject("ButtonBackground");
        buttonGo.transform.SetParent(root.transform, false);
        Image image = buttonGo.AddComponent<Image>();
        image.color = new Color(0.15f, 0.15f, 0.15f, 0.85f);
        image.raycastTarget = false;

        RectTransform buttonRt = buttonGo.GetComponent<RectTransform>();
        buttonRt.anchorMin = Vector2.zero;
        buttonRt.anchorMax = Vector2.one;
        buttonRt.offsetMin = Vector2.zero;
        buttonRt.offsetMax = Vector2.zero;

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(buttonGo.transform, false);
        TextMeshProUGUI label = textGo.AddComponent<TextMeshProUGUI>();
        label.alignment = TextAlignmentOptions.Center;
        label.fontSize = 42f;
        label.color = Color.white;
        label.raycastTarget = false;

        TextMeshProUGUI fontSource = Object.FindAnyObjectByType<TextMeshProUGUI>();
        if (fontSource != null)
            label.font = fontSource.font;

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        return view;
    }
}
