using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Полноэкранное затемнение (FadeCanvas) для выхода из игры и переходов между сценами.
/// </summary>
public static class ScreenFadeUtility
{
    private const int FadeSortOrder = 9999;

    public static CanvasGroup EnsureFadeCanvasGroup()
    {
        GameObject fadeObject = GameObject.Find("FadeCanvas");
        if (fadeObject != null)
        {
            if (fadeObject.TryGetComponent(out CanvasGroup existing))
                return existing;

            return fadeObject.AddComponent<CanvasGroup>();
        }

        GameObject fadeCanvas = new GameObject("FadeCanvas");
        Canvas canvas = fadeCanvas.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = FadeSortOrder;

        CanvasScaler scaler = fadeCanvas.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;

        fadeCanvas.AddComponent<GraphicRaycaster>();

        GameObject imageObject = new GameObject("FadeImage");
        imageObject.transform.SetParent(fadeCanvas.transform, false);

        Image fadeImage = imageObject.AddComponent<Image>();
        fadeImage.color = Color.black;
        fadeImage.raycastTarget = false;

        RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.sizeDelta = Vector2.zero;

        CanvasGroup group = fadeCanvas.AddComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false;
        group.interactable = false;
        return group;
    }

    public static async Awaitable FadeToBlackAsync(float duration)
    {
        CanvasGroup fade = EnsureFadeCanvasGroup();
        if (fade == null)
            return;

        fade.gameObject.SetActive(true);
        fade.blocksRaycasts = true;
        float from = fade.alpha;
        await UiMenuTransitions.AnimateCanvasGroupAlpha(fade, from, 1f, duration);
        fade.alpha = 1f;
    }
}
