using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Скрытый документ на уровне: пульсирующий свет, вращение, подсказка E, звуки, сохранение прогресса.
/// </summary>
[DisallowMultipleComponent]
public class CollectableDocument : PlayerInteractionZone
{
    [Header("Идентификатор")]
    [Tooltip("Уникальный ID (по умолчанию — имя активной сцены).")]
    [SerializeField] private string documentId;

    [Header("Визуал")]
    [SerializeField] private Transform rotateTarget;
    [SerializeField] private float rotateSpeed = 28f;
    [SerializeField] private Light guideLight;
    [SerializeField] private bool pulseGuideLight = true;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip pickupSound;
    [SerializeField] private AudioClip proximitySound;
    [SerializeField] private float proximityDistance = 2.2f;
    [SerializeField, Range(0f, 1f)] private float proximityVolume = 0.22f;

    private bool _proximityActive;

    public string DocumentId => ResolveDocumentId();

    public string ResolveDocumentId()
    {
        return string.IsNullOrWhiteSpace(documentId)
            ? SceneManager.GetActiveScene().name
            : documentId;
    }

    protected override void Awake()
    {
        base.Awake();

        if (rotateTarget == null)
            rotateTarget = transform;

        if (audioSource == null)
            TryGetComponent(out audioSource);
    }

    protected override void Start()
    {
        CollectableDocumentProgress.EnsureLoadedFromActiveSave();
        if (CollectableDocumentProgress.IsCollected(ResolveDocumentId()))
        {
            gameObject.SetActive(false);
            return;
        }

        SetupGuideLight();
        base.Start();
    }

    protected override void Update()
    {
        base.Update();

        if (!isActiveAndEnabled)
            return;

        RotateVisual();
        UpdateProximitySound();
    }

    protected override void OnInteractPressed()
    {
        string id = ResolveDocumentId();
        if (CollectableDocumentProgress.IsCollected(id))
            return;

        CollectableDocumentProgress.MarkCollected(id);

        if (pickupSound != null)
        {
            if (audioSource != null)
                audioSource.PlayOneShot(pickupSound);
            else
                AudioSource.PlayClipAtPoint(pickupSound, transform.position);
        }

        StopProximitySound();
        gameObject.SetActive(false);
        PressEPromptCoordinator.Refresh();
    }

    private void SetupGuideLight()
    {
        if (guideLight == null)
            guideLight = GetComponentInChildren<Light>(true);

        if (guideLight == null)
            return;

        guideLight.enabled = true;

        if (pulseGuideLight && !guideLight.TryGetComponent(out OCDGuideLightPulse _))
            guideLight.gameObject.AddComponent<OCDGuideLightPulse>();
    }

    private void RotateVisual()
    {
        if (rotateTarget == null || rotateSpeed <= 0f)
            return;

        rotateTarget.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    private void UpdateProximitySound()
    {
        if (proximitySound == null || proximityDistance <= 0f)
            return;

        Transform body = GetPlayerBodyTransform();
        bool shouldPlay = body != null
            && Vector3.Distance(body.position, transform.position) <= proximityDistance;

        if (shouldPlay && !_proximityActive)
        {
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.spatialBlend = 1f;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
            }

            audioSource.clip = proximitySound;
            audioSource.volume = proximityVolume;
            audioSource.loop = true;
            audioSource.Play();
            _proximityActive = true;
        }
        else if (!shouldPlay && _proximityActive)
        {
            StopProximitySound();
        }
    }

    private void StopProximitySound()
    {
        if (audioSource != null && audioSource.isPlaying && audioSource.clip == proximitySound)
            audioSource.Stop();

        _proximityActive = false;
    }

    private void OnDisable()
    {
        StopProximitySound();
    }
}
