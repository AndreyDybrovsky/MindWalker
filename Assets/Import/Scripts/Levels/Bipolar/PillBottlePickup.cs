using System.Collections;
using UnityEngine;

/// <summary>
/// Таблетка для сбора в Bipolar Location3.
/// Мигает точечным светом; подбирается через PillReticleController (TryCollect).
/// </summary>
[RequireComponent(typeof(Collider))]
public class PillBottlePickup : MonoBehaviour
{
    [Header("Пульсирующий свет")]
    [SerializeField] private Light pointLight;
    [SerializeField] private float pulseSpeed      = 1.4f;   // циклов в секунду
    [SerializeField] private float lightMinIntensity = 0.5f;
    [SerializeField] private float lightMaxIntensity = 2.0f;
    [SerializeField] private float lightRange      = 2.5f;   // радиус (world units)

    [Header("Звук")]
    [SerializeField] private AudioClip ambientClip;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.35f;
    [SerializeField] private AudioClip pickupClip;
    [SerializeField, Range(0f, 1f)] private float pickupVolume  = 0.85f;

    [Header("Fade при подборе")]
    [SerializeField] private float fadeOutDuration = 0.55f;

    private bool        _collected;
    private float       _pulsePhase;
    private AudioSource _ambientSource;

    public bool IsCollected => _collected;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        col.isTrigger = true;

        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity  = false;
    }

    private void Start()
    {
        if (pointLight == null)
            pointLight = GetComponentInChildren<Light>(true);

        if (pointLight != null)
        {
            pointLight.range     = lightRange;
            pointLight.intensity = lightMinIntensity;
            pointLight.enabled   = true;
        }

        if (ambientClip != null)
        {
            _ambientSource              = gameObject.AddComponent<AudioSource>();
            _ambientSource.clip         = ambientClip;
            _ambientSource.loop         = true;
            _ambientSource.volume       = ambientVolume;
            _ambientSource.spatialBlend = 1f;
            _ambientSource.rolloffMode  = AudioRolloffMode.Linear;
            _ambientSource.maxDistance  = 14f;
            _ambientSource.Play();
        }

        _pulsePhase = Random.Range(0f, Mathf.PI * 2f);
    }

    private void Update()
    {
        if (_collected || pointLight == null) return;

        // Плавный синусоидальный пульс: 0..1 → lightMin..lightMax
        float t = (Mathf.Sin(Time.time * pulseSpeed * Mathf.PI * 2f + _pulsePhase) + 1f) * 0.5f;
        pointLight.intensity = Mathf.Lerp(lightMinIntensity, lightMaxIntensity, t);
    }

    public bool TryCollect()
    {
        if (_collected) return false;
        _collected = true;
        StartCoroutine(CollectRoutine());
        return true;
    }

    private IEnumerator CollectRoutine()
    {
        PillCollectionManager.RegisterPickup();

        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);

        if (_ambientSource != null)
            _ambientSource.Stop();

        if (pointLight != null)
            pointLight.enabled = false;

        // Маленький «pop» вверх
        Vector3 scaleOrigin = transform.localScale;
        Vector3 scaleUp     = scaleOrigin * 1.25f;
        float elapsed = 0f;
        while (elapsed < 0.08f)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(scaleOrigin, scaleUp, elapsed / 0.08f);
            yield return null;
        }

        // Fade-out: уменьшаем scale к нулю
        elapsed = 0f;
        while (elapsed < fadeOutDuration)
        {
            elapsed += Time.deltaTime;
            transform.localScale = Vector3.Lerp(scaleUp, Vector3.zero, elapsed / fadeOutDuration);
            yield return null;
        }

        Destroy(gameObject);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(transform.position, 0.7f);
    }
#endif
}
