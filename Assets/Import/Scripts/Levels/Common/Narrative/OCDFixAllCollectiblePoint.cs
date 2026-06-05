using UnityEngine;

/// <summary>
/// Одна из точек «Исправь всё» (день 4): светится с начала, собирается при касании игрока.
/// </summary>
[RequireComponent(typeof(Collider))]
public class OCDFixAllCollectiblePoint : MonoBehaviour
{
    [SerializeField] private OCDFixAllCollectibleManager manager;
    [SerializeField] private GameObject guideLight;
    [SerializeField] private bool pulseGuideLight = true;
    [SerializeField] private AudioClip collectSound;
    [SerializeField, Range(0f, 1f)] private float collectSoundVolume = 1f;
    [SerializeField] private AudioSource audioSource;

    private bool _collected;

    public OCDFixAllCollectibleManager Manager => manager;

    public void BindManager(OCDFixAllCollectibleManager value)
    {
        manager = value;
    }

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        if (manager == null)
            manager = GetComponentInParent<OCDFixAllCollectibleManager>();

        if (audioSource == null)
            TryGetComponent(out audioSource);

        SetGuideVisible(true);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected || !other.CompareTag("Player"))
            return;

        _collected = true;

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

        Collider col = GetComponent<Collider>();
        if (col != null)
            col.enabled = false;

        SetGuideVisible(false);

        if (manager != null)
            manager.RegisterCollected(this);
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
