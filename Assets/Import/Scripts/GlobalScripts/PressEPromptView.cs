using TMPro;
using UnityEngine;

/// <summary>
/// Стилизованная подсказка «Нажмите E» (фон-кнопка + текст).
/// </summary>
public class PressEPromptView : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private TextMeshProUGUI label;

    public CanvasGroup CanvasGroup => canvasGroup;
    public TextMeshProUGUI Label => label;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);
    }

    public void SetReveal(float alpha)
    {
        if (canvasGroup != null)
            canvasGroup.alpha = Mathf.Clamp01(alpha);
    }

    public void SetText(string text)
    {
        if (label != null)
            label.text = text;
    }
}
