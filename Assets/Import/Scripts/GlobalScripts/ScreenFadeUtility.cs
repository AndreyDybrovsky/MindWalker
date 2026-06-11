using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полноэкранное затемнение (FadeCanvas) для выхода из игры и переходов между сценами.
/// </summary>
public static class ScreenFadeUtility
{
    /// <summary>Выше OCD UI (10040–10050), паузы и подсказок.</summary>
    private const int FadeSortOrder = 32700;
    private const string FadeImageChildName = "FadeImage";

    public static CanvasGroup EnsureFadeCanvasGroup()
    {
        GameObject fadeObject = GameObject.Find("FadeCanvas");
        if (!IsAlive(fadeObject))
            fadeObject = null;

        if (fadeObject == null)
            fadeObject = new GameObject("FadeCanvas");

        ConfigureFadeCanvasRoot(fadeObject);

        if (!fadeObject.TryGetComponent(out CanvasGroup group))
            group = fadeObject.AddComponent<CanvasGroup>();

        if (group == null)
        {
            Debug.LogError("ScreenFadeUtility: не удалось создать CanvasGroup для FadeCanvas.");
            return null;
        }

        if (group.alpha < 0.01f)
        {
            group.blocksRaycasts = false;
            group.interactable = false;
        }

        return group;
    }

    private static bool IsAlive(Object obj) => obj != null;

    /// <summary>Перед затемнением: активировать, поднять sort order, вынести на передний план.</summary>
    public static void PrepareForFade(CanvasGroup fade)
    {
        if (fade == null)
            return;

        ConfigureFadeCanvasRoot(fade.gameObject);
        fade.transform.SetAsLastSibling();
    }

    private static void ConfigureFadeCanvasRoot(GameObject fadeCanvas)
    {
        if (!IsAlive(fadeCanvas))
            return;

        if (!fadeCanvas.TryGetComponent(out Canvas canvas))
            canvas = fadeCanvas.AddComponent<Canvas>();

        if (canvas != null)
        {
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.overrideSorting = true;
            canvas.sortingOrder = FadeSortOrder;
        }

        if (!fadeCanvas.TryGetComponent(out CanvasScaler scaler))
            scaler = fadeCanvas.AddComponent<CanvasScaler>();

        if (scaler != null)
        {
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }

        if (!fadeCanvas.TryGetComponent(out GraphicRaycaster _))
            fadeCanvas.AddComponent<GraphicRaycaster>();

        EnsureFadeImage(fadeCanvas.transform);
    }

    private static void EnsureFadeImage(Transform fadeRoot)
    {
        if (!IsAlive(fadeRoot))
            return;

        Transform imageTransform = fadeRoot.Find(FadeImageChildName);
        GameObject imageObject;

        if (imageTransform == null)
        {
            imageObject = new GameObject(FadeImageChildName);
            imageObject.transform.SetParent(fadeRoot, false);
        }
        else
        {
            imageObject = imageTransform.gameObject;
        }

        if (!imageObject.TryGetComponent(out Image fadeImage))
            fadeImage = imageObject.AddComponent<Image>();

        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
        rectTransform.pivot = new Vector2(0.5f, 0.5f);
        imageObject.transform.SetAsLastSibling();
    }

    public static async Awaitable FadeToBlackAsync(float duration)
    {
        CanvasGroup fade = EnsureFadeCanvasGroup();
        if (fade == null)
            return;

        fade.gameObject.SetActive(true);
        PrepareForFade(fade);
        fade.blocksRaycasts = true;
        float from = fade.alpha >= 0.99f ? 0f : fade.alpha;
        fade.alpha = from;
        await UiMenuTransitions.AnimateCanvasGroupAlpha(fade, from, 1f, duration);
        fade.alpha = 1f;
    }
}
