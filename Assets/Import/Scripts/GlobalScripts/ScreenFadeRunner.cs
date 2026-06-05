using System.Collections;
using UnityEngine;

/// <summary>
/// Плавное затемнение / осветление экрана.
/// </summary>
public static class ScreenFadeRunner
{
    public static IEnumerator FadeToBlack(float duration, CanvasGroup fade = null)
    {
        fade ??= ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (fade == null)
            yield break;

        fade.gameObject.SetActive(true);
        ScreenFadeUtility.PrepareForFade(fade);
        fade.blocksRaycasts = true;
        float from = fade.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(from, 1f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        fade.alpha = 1f;
    }

    public static IEnumerator FadeFromBlack(float duration, CanvasGroup fade = null)
    {
        fade ??= ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (fade == null)
            yield break;

        fade.gameObject.SetActive(true);
        ScreenFadeUtility.PrepareForFade(fade);
        float from = fade.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fade.alpha = Mathf.Lerp(from, 0f, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        fade.alpha = 0f;
        fade.blocksRaycasts = false;
    }
}
