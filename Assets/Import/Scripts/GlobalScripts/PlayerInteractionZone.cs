using UnityEngine;

/// <summary>
/// Базовая зона взаимодействия игрока: общая подсказка PressEText и проверка дистанции.
/// </summary>
[RequireComponent(typeof(Collider))]
public abstract class PlayerInteractionZone : MonoBehaviour, IPressEPromptContributor
{
    [Header("Подсказка")]
    [SerializeField] protected string promptLocalizationKey = "scene.hint_press_e";
    [SerializeField] protected string promptFallbackText = "Нажми E";
    [SerializeField] private float revealSpeed = 4f;

    protected bool PlayerInZone;
    protected bool InteractionBusy;
    protected Collider ZoneCollider;
    protected PressEPromptView PromptView;
    protected float Reveal;

    private bool _usesSharedPrompt;

    public float PressPromptReveal => Reveal;
    public bool IsPressPromptVisible => PlayerInZone && !InteractionBusy && PromptView != null;

    public void ApplySharedPressPromptText(PressEPromptView view)
    {
        if (view == null)
            return;

        view.SetText(PressEPromptUtility.ResolveLocalizedText(promptLocalizationKey, promptFallbackText));
    }

    protected virtual void Awake()
    {
        ZoneCollider = GetComponent<Collider>();
        if (ZoneCollider != null)
            ZoneCollider.isTrigger = true;

        EnsureTriggerRigidbody();
        PressEPromptCoordinator.Register(this);
        // Промпт создаётся в Start() — к тому моменту Player/PressEText уже инициализированы
    }

    protected virtual void Start()
    {
        PromptView = PressEPromptUtility.CreatePrompt();
        _usesSharedPrompt = PressEPromptUtility.IsSharedView(PromptView);

        if (PromptView != null)
        {
            if (!_usesSharedPrompt)
                PromptView.gameObject.SetActive(true);

            PromptView.SetReveal(0f);
        }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

        ApplyLocalizedPrompt();
    }

    protected virtual void OnDisable()
    {
        PlayerInZone = false;
        PressEPromptCoordinator.Refresh();
    }

    protected virtual void OnDestroy()
    {
        PressEPromptCoordinator.Unregister(this);
        PressEPromptCoordinator.Refresh();

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;

        if (PromptView != null && !_usesSharedPrompt)
            Destroy(PromptView.transform.root.gameObject);
    }

    protected virtual void Update()
    {
        UpdateZoneStateByDistance();
        UpdatePromptReveal();

        if (PlayerInZone && !InteractionBusy && Input.GetKeyDown(KeyCode.E))
            OnInteractPressed();
    }

    protected abstract void OnInteractPressed();

    protected virtual void OnLanguageChanged(GameLanguage _)
    {
        ApplyLocalizedPrompt();
    }

    protected virtual void ApplyLocalizedPrompt()
    {
        if (PromptView == null)
            return;

        string text = PressEPromptUtility.ResolveLocalizedText(promptLocalizationKey, promptFallbackText);
        PromptView.SetText(text);
    }

    protected void UpdatePromptReveal()
    {
        if (PromptView == null)
            return;

        float target = PlayerInZone && !InteractionBusy ? 1f : 0f;
        Reveal = Mathf.MoveTowards(Reveal, target, revealSpeed * Time.deltaTime);

        if (_usesSharedPrompt)
            PressEPromptCoordinator.Refresh();
        else
            PromptView.SetReveal(Reveal);
    }

    protected void UpdateZoneStateByDistance()
    {
        if (InteractionBusy || ZoneCollider == null)
            return;

        Transform body = GetPlayerBodyTransform();
        if (body == null)
        {
            PlayerInZone = false;
            return;
        }

        Vector3 closest = ZoneCollider.ClosestPoint(body.position);
        PlayerInZone = (closest - body.position).sqrMagnitude < 0.25f;
    }

    public static Transform GetPlayerBodyTransform()
    {
        return PlayerTeleportUtility.TryGetPlayerBody(out Transform body) ? body : null;
    }

    public static void SetPlayerControlLocked(bool locked)
    {
        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return;

        if (body.TryGetComponent(out CharacterController characterController))
            characterController.enabled = !locked;

        MonoBehaviour[] behaviours = body.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour == null)
                continue;

            string typeName = behaviour.GetType().Name;
            if (typeName is "PlayerController" or "WeaponHandler")
                behaviour.enabled = !locked;
        }
    }

    private void EnsureTriggerRigidbody()
    {
        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerCollider(other))
            PlayerInZone = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (IsPlayerCollider(other))
            PlayerInZone = false;
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
