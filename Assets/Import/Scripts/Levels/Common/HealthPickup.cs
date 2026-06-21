using UnityEngine;

/// <summary>
/// Хилка: при подходе игрока лечит его и уничтожается.
/// Визуальные эффекты: боббинг, вращение, шатание, пульсация масштаба, мигающее свечение.
/// </summary>
public class HealthPickup : MonoBehaviour
{
    [Header("Лечение")]
    [SerializeField] private float healAmount = 25f;

    [Header("Боббинг")]
    [SerializeField] private float bobHeight = 0.22f;
    [SerializeField] private float bobSpeed = 1.6f;

    [Header("Вращение")]
    [SerializeField] private float spinSpeed = 95f;

    [Header("Шатание (wobble)")]
    [SerializeField] private float wobbleAmplitudeX = 10f;
    [SerializeField] private float wobbleAmplitudeZ = 7f;
    [SerializeField] private float wobbleSpeedX = 2.1f;
    [SerializeField] private float wobbleSpeedZ = 1.7f;

    [Header("Пульсация масштаба")]
    [SerializeField] private float pulseMin = 0.80f;
    [SerializeField] private float pulseMax = 1.20f;
    [SerializeField] private float pulseSpeed = 2.4f;

    [Header("Свечение (Point Light)")]
    [SerializeField] private Color glowColor = new Color(0.15f, 1f, 0.25f, 1f);
    [SerializeField] private float lightMinIntensity = 0.5f;
    [SerializeField] private float lightMaxIntensity = 2.2f;
    [SerializeField] private float lightRange = 5f;
    [SerializeField] private float lightPulseSpeed = 3.0f;

    [Header("Частицы")]
    [SerializeField] private bool spawnParticles = true;
    [SerializeField] private Color particleColor = new Color(0.2f, 1f, 0.3f, 1f);

    private Vector3 _basePosition;
    private float _timeOffset;
    private Light _glowLight;
    private Transform _visual;

    private void Awake()
    {
        _basePosition = transform.position;
        _timeOffset = Random.Range(0f, Mathf.PI * 2f);

        SetupVisualChild();
        SetupLight();

        if (spawnParticles)
            SetupParticles();
    }

    private void SetupVisualChild()
    {
        // Ищем дочерний объект-визуал (первый не-Light child)
        for (int i = 0; i < transform.childCount; i++)
        {
            if (transform.GetChild(i).GetComponent<Light>() == null)
            {
                _visual = transform.GetChild(i);
                break;
            }
        }

        // Если нет — сам объект является визуалом
        if (_visual == null)
            _visual = transform;
    }

    private void SetupLight()
    {
        _glowLight = GetComponentInChildren<Light>();
        if (_glowLight != null)
        {
            _glowLight.color = glowColor;
            _glowLight.range = lightRange;
            _glowLight.shadows = LightShadows.None;
            return;
        }

        GameObject lightGo = new GameObject("GlowLight");
        lightGo.transform.SetParent(transform, false);
        _glowLight = lightGo.AddComponent<Light>();
        _glowLight.type = LightType.Point;
        _glowLight.color = glowColor;
        _glowLight.range = lightRange;
        _glowLight.intensity = lightMinIntensity;
        _glowLight.shadows = LightShadows.None;
    }

    private void SetupParticles()
    {
        GameObject psGo = new GameObject("HealParticles");
        psGo.transform.SetParent(transform, false);

        ParticleSystem ps = psGo.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;
        rend.material = BuildParticleMaterial();

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.duration = 1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.12f);
        main.startColor = particleColor;
        main.maxParticles = 40;
        main.gravityModifier = -0.15f;

        ParticleSystem.EmissionModule emit = ps.emission;
        emit.enabled = true;
        emit.rateOverTime = 12f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.25f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(particleColor, 0f), new GradientColorKey(particleColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g;

        ps.Play();
    }

    private static Material BuildParticleMaterial()
    {
        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Universal Render Pipeline/Particles/Lit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        if (shader == null) return null;

        Material mat = new Material(shader);
        Color c = new Color(0.2f, 1f, 0.3f, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", c);
        return mat;
    }

    private void Update()
    {
        float t = Time.time + _timeOffset;

        // Боббинг
        Vector3 pos = _basePosition;
        pos.y += Mathf.Sin(t * bobSpeed) * bobHeight;
        transform.position = pos;

        // Вращение вокруг Y
        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        // Шатание (wobble) — дочерний визуал качается
        if (_visual != null && _visual != transform)
        {
            float tiltX = Mathf.Sin(t * wobbleSpeedX) * wobbleAmplitudeX;
            float tiltZ = Mathf.Cos(t * wobbleSpeedZ) * wobbleAmplitudeZ;
            _visual.localRotation = Quaternion.Euler(tiltX, 0f, tiltZ);
        }

        // Пульсация масштаба
        float scale = Mathf.Lerp(pulseMin, pulseMax, (Mathf.Sin(t * pulseSpeed) + 1f) * 0.5f);
        transform.localScale = Vector3.one * scale;

        // Пульсация света
        if (_glowLight != null)
            _glowLight.intensity = Mathf.Lerp(lightMinIntensity, lightMaxIntensity,
                (Mathf.Sin(t * lightPulseSpeed) + 1f) * 0.5f);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player"))
            return;

        PlayerHealth health = other.GetComponent<PlayerHealth>()
            ?? other.GetComponentInParent<PlayerHealth>()
            ?? other.transform.root.GetComponentInChildren<PlayerHealth>();

        if (health == null || health.IsDead) return;
        if (health.CurrentHealth >= health.MaxHealth) return;

        health.Heal(healAmount);

        // Звук подбора (2D, чтобы всегда был слышен)
        AudioClip pickupClip = Resources.Load<AudioClip>("Sounds/Other/Heal");
        if (pickupClip != null)
        {
            float sfx = SettingsManager.Instance != null
                ? SettingsManager.Instance.GetCurrentSettings().sfxVolume : 1f;
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, 0.9f * sfx);
        }

        Destroy(gameObject);
    }
}
