using UnityEngine;

public class FadeStart : MonoBehaviour
{
    [SerializeField] private CanvasGroup fadeLayer;
    [SerializeField] private float fadeDuration = 1.5f;
    [SerializeField] private float holdTime = 0.5f;
    [SerializeField] private bool disableAfterFade = true;
    
    private bool isFading = false;
    public static bool IsAnyFadeActive { get; private set; } = false;

    private void Awake()
    {
        if (fadeLayer != null)
        {
            fadeLayer.alpha = 1f;
            fadeLayer.blocksRaycasts = true;
        }
    }

    private void Start()
    {
        if (fadeLayer != null)
        {
            isFading = true;
            IsAnyFadeActive = true;
            StartCoroutine(FadeOutRoutine());
        }
        else
        {
            Debug.LogWarning($"{nameof(FadeStart)}: CanvasGroup не назначен.");
        }
    }

    private System.Collections.IEnumerator FadeOutRoutine()
    {
        if (holdTime > 0f)
        {
            yield return new WaitForSecondsRealtime(holdTime);
        }

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            fadeLayer.alpha = Mathf.Lerp(1f, 0f, t);
            yield return null;
        }

        fadeLayer.alpha = 0f;
        fadeLayer.blocksRaycasts = false;
        if (disableAfterFade)
        {
            fadeLayer.gameObject.SetActive(false);
        }
        
        isFading = false;
        IsAnyFadeActive = false;
    }
    
    private void OnDestroy()
    {
        if (isFading)
        {
            IsAnyFadeActive = false;
        }
    }
}
