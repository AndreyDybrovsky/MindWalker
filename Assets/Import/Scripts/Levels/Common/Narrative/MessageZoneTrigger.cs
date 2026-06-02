using System.Collections;
using UnityEngine;

/// <summary>
/// Триггер сообщения снизу экрана: показ при входе, скрытие при выходе. Может повторяться бесконечно.
/// Использует стиль/панель как у OCD Moment (OCDCaptionUI / MessageText).
/// </summary>
[RequireComponent(typeof(Collider))]
public class MessageZoneTrigger : MonoBehaviour
{
    [Header("Текст")]
    [SerializeField] private string localizationKey;
    [SerializeField, TextArea(2, 6)] private string fallbackText = "Текст…";

    [Header("Поведение")]
    [SerializeField] private OCDCaptionUI captionUI;
    [SerializeField] private float fadeInDuration = 0.35f;
    [SerializeField] private float fadeOutDuration = 0.25f;

    private Coroutine _routine;
    private bool _inside;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        EnsureTriggerRigidbody();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        _inside = true;
        Show();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        _inside = false;
        Hide();
    }

    private void Show()
    {
        OCDCaptionUI ui = captionUI != null ? captionUI : OCDCaptionUI.GetSharedOverlay();
        if (ui == null)
            return;

        string text = LocalizedTextResolver.Resolve(localizationKey, null, fallbackText);

        if (_routine != null)
            StopCoroutine(_routine);

        // Держим "hold" очень долго, а при выходе просто делаем HideSmooth().
        _routine = StartCoroutine(ui.ShowRoutine(text, fadeInDuration, holdDuration: 999999f, fadeOutDuration: 0.01f));
    }

    private void Hide()
    {
        OCDCaptionUI ui = captionUI != null ? captionUI : OCDCaptionUI.GetSharedOverlay();
        if (ui == null)
            return;

        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        StartCoroutine(ui.HideSmooth(fadeOutDuration));
    }

    private void EnsureTriggerRigidbody()
    {
        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        return other.transform.root.CompareTag("Player");
    }
}

