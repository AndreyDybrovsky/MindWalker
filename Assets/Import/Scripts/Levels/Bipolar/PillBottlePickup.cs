using System.Collections;
using UnityEngine;

/// <summary>
/// Бутылочка с таблетками: боббинг, вращение, пульсирующий свет, ambient SFX.
/// При подходе игрока — звук подбора, плавное исчезание, уведомление менеджера.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PillBottlePickup : MonoBehaviour
{
    [Header("Анимация")]
    [SerializeField] private float bobHeight = 0.10f;
    [SerializeField] private float bobSpeed   = 1.6f;
    [SerializeField] private float rotateSpeed = 50f;

    [Header("Пульсирующий свет")]
    [SerializeField] private Light pointLight;
    [SerializeField] private float lightMinIntensity = 0.3f;
    [SerializeField] private float lightMaxIntensity = 1.1f;

    [Header("Звук")]
    [SerializeField] private AudioClip ambientClip;
    [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.35f;
    [SerializeField] private AudioClip pickupClip;
    [SerializeField, Range(0f, 1f)] private float pickupVolume  = 0.85f;

    [Header("Fade при подборе")]
    [SerializeField] private float fadeOutDuration = 0.55f;

    private bool        _collected;
    private Vector3     _startLocalPos;
    private float       _bobPhase;
    private AudioSource _ambientSource;

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
        _startLocalPos = transform.localPosition;
        _bobPhase      = Random.Range(0f, Mathf.PI * 2f);

        if (pointLight == null)
            pointLight = GetComponentInChildren<Light>();

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
    }

    private void Update()
    {
        if (_collected) return;

        float t = Time.time * bobSpeed + _bobPhase;

        // Боббинг по локальной Y
        Vector3 pos = _startLocalPos;
        pos.y += Mathf.Sin(t) * bobHeight;
        transform.localPosition = pos;

        // Вращение вокруг мировой Y
        transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);

        // Пульсация света
        if (pointLight != null)
        {
            float pulse = (Mathf.Sin(t * 1.7f) + 1f) * 0.5f;
            pointLight.intensity = Mathf.Lerp(lightMinIntensity, lightMaxIntensity, pulse);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected) return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

        _collected = true;
        StartCoroutine(CollectRoutine());
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
            float p = elapsed / fadeOutDuration;
            transform.localScale = Vector3.Lerp(scaleUp, Vector3.zero, p);
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
