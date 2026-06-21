using UnityEngine;

/// <summary>
/// Одна из точек «Исправь всё» (день 4): светится с начала, собирается по нажатию E
/// (подсказка «Нажми E» появляется при подходе). При сборе показывает свою нижнюю надпись.
/// </summary>
[RequireComponent(typeof(Collider))]
public class OCDFixAllCollectiblePoint : PlayerInteractionZone
{
    [SerializeField] private OCDFixAllCollectibleManager manager;
    [SerializeField] private GameObject guideLight;
    [SerializeField] private bool pulseGuideLight = true;
    [SerializeField] private AudioClip collectSound;
    [SerializeField, Range(0f, 1f)] private float collectSoundVolume = 1f;
    [SerializeField] private AudioSource audioSource;

    [Header("Надпись при сборе (низ экрана)")]
    [Tooltip("Если задано — используем этот UI (например MessageText из Player.prefab). Пусто — общий OCDCaptionUI на сцене.")]
    [SerializeField] private OCDCaptionUI captionUI;
    [Tooltip("Ключ в strings_*.json / LocalizationBase. Пусто — используется Caption Fallback Text.")]
    [SerializeField] private string captionLocalizationKey;
    [SerializeField, TextArea(2, 4)] private string captionFallbackText;
    [SerializeField] private float captionFadeInDuration = 0.45f;
    [SerializeField] private float captionHoldDuration = 3f;
    [SerializeField] private float captionFadeOutDuration = 0.35f;

    private bool _collected;

    public OCDFixAllCollectibleManager Manager => manager;

    public void BindManager(OCDFixAllCollectibleManager value)
    {
        manager = value;
    }

    protected override void Awake()
    {
        base.Awake(); // коллайдер-триггер, kinematic Rigidbody, общая подсказка «Нажми E»

        if (manager == null)
            manager = GetComponentInParent<OCDFixAllCollectibleManager>();
        if (manager == null)
            manager = Object.FindFirstObjectByType<OCDFixAllCollectibleManager>(FindObjectsInactive.Include);

        if (audioSource == null)
            TryGetComponent(out audioSource);

        SetGuideVisible(true);
    }

    protected override void OnInteractPressed()
    {
        if (_collected)
            return;

        _collected = true;
        InteractionBusy = true;
        PlayerInZone = false;
        Reveal = 0f;

        if (PromptView != null)
            PromptView.SetReveal(0f);

        PressEPromptCoordinator.Refresh();

        if (collectSound != null)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                audioSource.spatialBlend = 0f;
            }

            AudioMixerRoutingUtility.BindSourceToSfx(audioSource);
            audioSource.PlayOneShot(collectSound, collectSoundVolume);
        }

        if (ZoneCollider != null)
            ZoneCollider.enabled = false;

        SetGuideVisible(false);

        ShowCollectCaption();

        if (manager != null)
            manager.RegisterCollected(this);
    }

    /// <summary>Своя нижняя надпись точки при сборе (низ экрана), как у OCDMomentTrigger.</summary>
    private void ShowCollectCaption()
    {
        if (string.IsNullOrEmpty(captionLocalizationKey) && string.IsNullOrEmpty(captionFallbackText))
            return;

        OCDCaptionUI ui = captionUI != null ? captionUI : OCDCaptionUI.GetSharedOverlay();
        if (ui == null)
            return;

        string text = LocalizedTextResolver.Resolve(captionLocalizationKey, null, captionFallbackText);
        if (string.IsNullOrEmpty(text))
            return;

        // Запускаем на самом UI (он живёт дольше точки): появление → удержание → скрытие.
        ui.StartCoroutine(ui.ShowRoutine(text, captionFadeInDuration, captionHoldDuration, captionFadeOutDuration));
    }

    private void SetGuideVisible(bool visible)
    {
        if (guideLight == null)
            return;

        guideLight.SetActive(visible);

        if (!visible || !pulseGuideLight)
            return;

        if (!guideLight.TryGetComponent(out OCDGuideLightPulse pulse))
            pulse = guideLight.AddComponent<OCDGuideLightPulse>();
        pulse.enabled = true;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _collected
            ? new Color(0.4f, 0.4f, 0.4f, 0.25f)
            : new Color(0.9f, 0.75f, 0.2f, 0.45f);

        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawWireSphere(col.bounds.center, Mathf.Max(col.bounds.extents.magnitude * 0.35f, 0.25f));
    }
#endif
}
