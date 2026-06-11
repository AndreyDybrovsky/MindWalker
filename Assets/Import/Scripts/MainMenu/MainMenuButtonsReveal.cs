using UnityEngine;

public class MainMenuButtonsReveal : MonoBehaviour
{
    [SerializeField] private RectTransform[] buttons;
    [SerializeField] private float initialDelay = 0.35f;
    [SerializeField] private float fadeDuration = 0.45f;
    [SerializeField] private float staggerDelay = 0.12f;
    [SerializeField] private float slideOffsetY = -25f;

    private void Start()
    {
        if (buttons == null || buttons.Length == 0)
            CollectChildren();

        RunRevealAsync();
    }

    private void CollectChildren()
    {
        int count = transform.childCount;
        buttons = new RectTransform[count];
        for (int i = 0; i < count; i++)
            buttons[i] = transform.GetChild(i) as RectTransform;
    }

    private async void RunRevealAsync()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            CanvasGroup cg = UiMenuTransitions.EnsureCanvasGroup(buttons[i].gameObject);
            cg.alpha = 0f;
            cg.blocksRaycasts = false;
            cg.interactable = false;
        }

        float waited = 0f;
        while (waited < initialDelay)
        {
            waited += Time.unscaledDeltaTime;
            await Awaitable.NextFrameAsync();
        }

        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            RevealButtonAsync(buttons[i]);

            if (i < buttons.Length - 1)
            {
                float remaining = staggerDelay;
                while (remaining > 0f)
                {
                    remaining -= Time.unscaledDeltaTime;
                    await Awaitable.NextFrameAsync();
                }
            }
        }
    }

    private async void RevealButtonAsync(RectTransform btn)
    {
        CanvasGroup cg = UiMenuTransitions.EnsureCanvasGroup(btn.gameObject);
        Vector2 originalPos = btn.anchoredPosition;
        Vector2 startPos = originalPos + new Vector2(0f, slideOffsetY);
        btn.anchoredPosition = startPos;
        cg.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / fadeDuration));
            cg.alpha = t;
            btn.anchoredPosition = Vector2.Lerp(startPos, originalPos, t);
            await Awaitable.NextFrameAsync();
        }

        cg.alpha = 1f;
        btn.anchoredPosition = originalPos;
        cg.blocksRaycasts = true;
        cg.interactable = true;
    }
}
