using UnityEngine;

/// <summary>
/// Вспомогательные переходы для UI при паузе (Time.timeScale = 0): только unscaledDeltaTime.
/// </summary>
public static class UiMenuTransitions
{
    public static CanvasGroup EnsureCanvasGroup(GameObject go)
    {
        if (go == null)
            return null;

        if (!go.TryGetComponent(out CanvasGroup cg))
            cg = go.AddComponent<CanvasGroup>();

        return cg;
    }

    public static async Awaitable AnimateCanvasGroupAlpha(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null)
            return;

        if (duration <= 0f)
        {
            cg.alpha = to;
            return;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            cg.alpha = Mathf.Lerp(from, to, k);
            await Awaitable.NextFrameAsync();
        }

        cg.alpha = to;
    }

    public static async Awaitable CrossFadePanels(
        CanvasGroup hideCg,
        CanvasGroup showCg,
        float duration)
    {
        if (showCg != null)
            showCg.alpha = 0f;

        float startHide = hideCg != null ? hideCg.alpha : 1f;

        if (duration <= 0f)
        {
            if (hideCg != null)
                hideCg.alpha = 0f;
            if (showCg != null)
                showCg.alpha = 1f;
            return;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            if (hideCg != null)
                hideCg.alpha = Mathf.Lerp(startHide, 0f, k);
            if (showCg != null)
                showCg.alpha = Mathf.Lerp(0f, 1f, k);
            await Awaitable.NextFrameAsync();
        }

        if (hideCg != null)
            hideCg.alpha = 0f;
        if (showCg != null)
            showCg.alpha = 1f;
    }
}
