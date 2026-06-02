using UnityEngine;

/// <summary>
/// Простая пульсация "света-указателя" для OCDMomentTrigger.
/// Пульсирует интенсивность Light (если есть), иначе — масштаб объекта.
/// </summary>
public class OCDGuideLightPulse : MonoBehaviour
{
    [Header("Пульсация")]
    [SerializeField] private bool useUnscaledTime = true;
    [SerializeField] private float speed = 2.2f;
    [SerializeField] private float minMultiplier = 0.75f;
    [SerializeField] private float maxMultiplier = 1.25f;

    [Header("Цель (опционально)")]
    [SerializeField] private Light targetLight;
    [SerializeField] private bool includeInactive = true;

    private float _baseIntensity;
    private Vector3 _baseScale;
    private float _phase;

    private void Awake()
    {
        _baseScale = transform.localScale;

        if (targetLight == null)
        {
            targetLight = GetComponent<Light>();
            if (targetLight == null)
                targetLight = GetComponentInChildren<Light>(includeInactive);
        }

        if (targetLight != null)
            _baseIntensity = Mathf.Max(0f, targetLight.intensity);
    }

    private void OnEnable()
    {
        _phase = Random.value * 10f;
        if (targetLight != null)
            _baseIntensity = Mathf.Max(0f, targetLight.intensity);
        _baseScale = transform.localScale;
    }

    private void Update()
    {
        float dt = useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;
        _phase += dt * Mathf.Max(0.01f, speed);

        float sin01 = (Mathf.Sin(_phase) + 1f) * 0.5f;
        float mult = Mathf.Lerp(minMultiplier, maxMultiplier, sin01);

        if (targetLight != null)
        {
            targetLight.intensity = _baseIntensity * mult;
        }
        else
        {
            transform.localScale = _baseScale * mult;
        }
    }
}

