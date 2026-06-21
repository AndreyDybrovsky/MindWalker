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

    [Header("Пульсация когда видна")]
    [SerializeField] private float pulseAmount = 0.04f;
    [SerializeField] private float pulseSpeed = 3.5f;

    private Vector3 _baseScale = Vector3.one;
    private bool _hasBaseScale;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();
        if (label == null)
            label = GetComponentInChildren<TextMeshProUGUI>(true);

        _baseScale = transform.localScale;
        _hasBaseScale = true;
    }

    private void Update()
    {
        if (!_hasBaseScale)
            return;

        float visible = canvasGroup != null ? canvasGroup.alpha : 1f;
        if (visible <= 0.05f)
        {
            transform.localScale = _baseScale;
            return;
        }

        float pulse = 1f + Mathf.Sin(Time.unscaledTime * pulseSpeed) * pulseAmount * visible;
        transform.localScale = _baseScale * pulse;
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
