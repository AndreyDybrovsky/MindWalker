using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using UnityEngine.Rendering;

/// <summary>
/// Система здоровья врага с плавной смертью и particle system
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Настройки здоровья")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float currentHealth;

    [Header("Эффекты смерти")]
    [SerializeField] private ParticleSystem deathParticleSystem;
    [Tooltip("Длительность плавного исчезновения модели и затухания звука смерти.")]
    [SerializeField] private float fadeOutDuration = 1.35f;
    [Tooltip("Пропустить плавное исчезновение — враг деактивируется мгновенно (частицы всё равно воспроизводятся).")]
    [SerializeField] private bool skipFade = false;

    [Header("Эффекты смерти (процедурные партиклы)")]
    [Tooltip("Если включено — при смерти спавним красный «круговой взрыв» частиц (даже если deathParticleSystem не задан).")]
    [SerializeField] private bool spawnProceduralDeathParticles = true;
    [Tooltip("Материал для процедурных партиклов. Если не задан — создаётся из подходящего шейдера в рантайме.")]
    [SerializeField] private Material proceduralParticleMaterial;
    [SerializeField] private Color proceduralParticleColor = new Color(0.85f, 0.1f, 0.12f, 1f);
    [SerializeField, Range(8, 160)] private int proceduralBurstCount = 48;
    [SerializeField] private float proceduralRadius = 0.65f;
    [SerializeField] private float proceduralStartSpeed = 2.2f;
    [SerializeField] private float proceduralLifetime = 0.65f;
    [SerializeField] private float proceduralSize = 0.08f;

    [Header("Звук попадания")]
    [Tooltip("Звук когда враг получает урон. Без файла — молчит.")]
    [SerializeField] private AudioClip hitSound;
    [SerializeField, Range(0f, 1f)] private float hitSoundVolume = 0.55f;

    [Header("Дроп хилки")]
    [Tooltip("Префаб хилки, спавнящийся при смерти с шансом 33%.")]
    [SerializeField] private GameObject healthPickupPrefab;
    [SerializeField, Range(0f, 1f)] private float healthDropChance = 0.33f;

    [Header("События")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnEnemyDeath;

    /// <summary>Вызывается при получении урона (живым врагом). Для hit-реакции в EnemyAnimatorDriver.</summary>
    public event System.Action OnDamaged;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private bool isDead;
    private FadeRendererEntry[] _fadeRenderers;
    private MaterialPropertyBlock _fadePropertyBlock;
    private AudioSource _deathAudioSource;
    private AudioSource _hitAudioSource;
    private Collider[] _colliders;
    private Coroutine _deathRoutine;

    private struct FadeRendererEntry
    {
        public Renderer Renderer;
        public Color BaseColor;
        public int ColorPropertyId;
        public bool HasColorProperty;
    }

    private void Start()
    {
        currentHealth = maxHealth;
        CacheFadeRenderers();
        EnsureDeathAudioSource();
        EnsureHitAudioSource();
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        OnHealthChanged?.Invoke(currentHealth);

        // Оповещаем ИИ о попадании — враг начинает преследование
        AlertEnemyControllerFromDamage();
        PlayHitSound();

        if (currentHealth <= 0f && !isDead)
        {
            Die();
        }
        else
        {
            OnDamaged?.Invoke();
        }
    }

    private void EnsureHitAudioSource()
    {
        Transform hitNode = transform.Find("HitAudio");
        if (hitNode == null)
        {
            GameObject go = new GameObject("HitAudio");
            go.transform.SetParent(transform, false);
            hitNode = go.transform;
        }

        if (!hitNode.TryGetComponent(out _hitAudioSource))
            _hitAudioSource = hitNode.gameObject.AddComponent<AudioSource>();

        _hitAudioSource.playOnAwake = false;
        _hitAudioSource.loop = false;
        _hitAudioSource.spatialBlend = 1f;
        _hitAudioSource.minDistance = 2f;
        _hitAudioSource.maxDistance = 18f;
        AudioMixerRoutingUtility.BindSourceToSfx(_hitAudioSource);
    }

    private void PlayHitSound()
    {
        if (hitSound == null || _hitAudioSource == null) return;
        float sfx = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().sfxVolume : 1f;
        _hitAudioSource.PlayOneShot(hitSound, hitSoundVolume * Mathf.Clamp01(sfx));
    }

    private void AlertEnemyControllerFromDamage()
    {
        // Стационарные враги не реагируют на удар движением
        if (GetComponent<StationaryEnemyController>() != null) return;

        EnemyController controller = GetComponent<EnemyController>();
        if (controller != null && controller.enabled)
            controller.ForceStartChase();
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(maxHealth, currentHealth);

        OnHealthChanged?.Invoke(currentHealth);
    }

    private void Die()
    {
        if (isDead) return;

        isDead = true;
        GameStatsTracker.Instance?.RecordEnemyKilled();

        // «Сок» убийства: микро-заморозка времени + тряска камеры.
        HitStopUtility.Do(0.05f);
        CameraShaker.Shake(0.4f);

        OnEnemyDeath?.Invoke();

        DisableEnemyBehaviour();
        DisableNavMeshAgents();
        DisableColliders();

        TrySpawnHealthPickup();

        if (deathParticleSystem != null)
            deathParticleSystem.Play();
        else if (spawnProceduralDeathParticles)
            SpawnProceduralDeathParticles();

        if (skipFade)
        {
            gameObject.SetActive(false);
            return;
        }

        if (_deathRoutine != null)
            StopCoroutine(_deathRoutine);

        _deathRoutine = StartCoroutine(DeathFadeRoutine());
    }

    private void TrySpawnHealthPickup()
    {
        if (healthPickupPrefab == null) return;
        if (Random.value > healthDropChance) return;

        Vector3 spawnPos = transform.position + Vector3.up * 0.5f;
        Instantiate(healthPickupPrefab, spawnPos, Quaternion.identity);
    }

    private void DisableEnemyBehaviour()
    {
        EnemyController enemyController = GetComponent<EnemyController>();
        if (enemyController != null)
            enemyController.enabled = false;

        StationaryEnemyController stationaryController = GetComponent<StationaryEnemyController>();
        if (stationaryController != null)
            stationaryController.enabled = false;

        EnemyShooting enemyShooting = GetComponent<EnemyShooting>();
        if (enemyShooting != null)
            enemyShooting.enabled = false;

        EnemyVision enemyVision = GetComponentInChildren<EnemyVision>();
        if (enemyVision != null)
            enemyVision.enabled = false;

        PatrolConeGuardEnemy patrolGuard = GetComponent<PatrolConeGuardEnemy>();
        if (patrolGuard != null)
            patrolGuard.enabled = false;
    }

    private IEnumerator DeathFadeRoutine()
    {
        float duration = Mathf.Max(0.05f, fadeOutDuration);
        PrepareMaterialsForFade();

        AudioClip deathClip = ResolveDeathClip();
        float deathPeakVolume = ResolveDeathPeakVolume();
        BeginDeathSound(deathClip, deathPeakVolume);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float fade = 1f - Mathf.SmoothStep(0f, 1f, t);

            ApplyFadeAlpha(fade);
            UpdateDeathSoundVolume(deathPeakVolume, fade);

            yield return null;
        }

        ApplyFadeAlpha(0f);
        UpdateDeathSoundVolume(deathPeakVolume, 0f);

        if (_deathAudioSource != null)
            _deathAudioSource.Stop();

        gameObject.SetActive(false);
        _deathRoutine = null;
    }

    private void CacheFadeRenderers()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        var entries = new System.Collections.Generic.List<FadeRendererEntry>(renderers.Length);

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            Material mat = r.sharedMaterial;
            if (mat == null)
                continue;

            FadeRendererEntry entry = new FadeRendererEntry
            {
                Renderer = r,
                HasColorProperty = false
            };

            if (mat.HasProperty(BaseColorId))
            {
                entry.ColorPropertyId = BaseColorId;
                entry.BaseColor = mat.GetColor(BaseColorId);
                entry.HasColorProperty = true;
            }
            else if (mat.HasProperty(ColorId))
            {
                entry.ColorPropertyId = ColorId;
                entry.BaseColor = mat.GetColor(ColorId);
                entry.HasColorProperty = true;
            }

            if (entry.HasColorProperty)
                entries.Add(entry);
        }

        _fadeRenderers = entries.ToArray();
        _fadePropertyBlock = new MaterialPropertyBlock();
    }

    private void PrepareMaterialsForFade()
    {
        if (_fadeRenderers == null)
            return;

        for (int i = 0; i < _fadeRenderers.Length; i++)
        {
            Renderer r = _fadeRenderers[i].Renderer;
            if (r == null)
                continue;

            Material instance = r.material;
            ConfigureMaterialForAlphaFade(instance);
        }
    }

    private void ApplyFadeAlpha(float alpha01)
    {
        if (_fadeRenderers == null || _fadePropertyBlock == null)
            return;

        alpha01 = Mathf.Clamp01(alpha01);

        for (int i = 0; i < _fadeRenderers.Length; i++)
        {
            FadeRendererEntry entry = _fadeRenderers[i];
            if (!entry.HasColorProperty || entry.Renderer == null)
                continue;

            Color c = entry.BaseColor;
            c.a *= alpha01;

            entry.Renderer.GetPropertyBlock(_fadePropertyBlock);
            _fadePropertyBlock.SetColor(entry.ColorPropertyId, c);
            entry.Renderer.SetPropertyBlock(_fadePropertyBlock);
        }
    }

    private static void ConfigureMaterialForAlphaFade(Material mat)
    {
        if (mat == null)
            return;

        if (!mat.HasProperty("_Surface"))
            return;

        mat.SetFloat("_Surface", 1f);
        mat.SetFloat("_Blend", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.renderQueue = (int)RenderQueue.Transparent;
    }

    private void EnsureDeathAudioSource()
    {
        Transform root = transform.Find("DeathAudio");
        if (root == null)
        {
            GameObject go = new GameObject("DeathAudio");
            go.transform.SetParent(transform, false);
            root = go.transform;
        }

        if (!root.TryGetComponent(out _deathAudioSource))
            _deathAudioSource = root.gameObject.AddComponent<AudioSource>();

        _deathAudioSource.playOnAwake = false;
        _deathAudioSource.loop = false;
        _deathAudioSource.spatialBlend = 1f;
        _deathAudioSource.minDistance = 2f;
        _deathAudioSource.maxDistance = 24f;
        AudioMixerRoutingUtility.BindSourceToSfx(_deathAudioSource);
    }

    private AudioClip ResolveDeathClip()
    {
        EnemyController controller = GetComponent<EnemyController>();
        if (controller != null && controller.DeathSound != null)
            return controller.DeathSound;

        return Resources.Load<AudioClip>("Sounds/Ludomania/fail");
    }

    private float ResolveDeathPeakVolume()
    {
        EnemyController controller = GetComponent<EnemyController>();
        float volume = controller != null ? controller.DeathSoundVolume : 0.85f;

        float sfx = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().sfxVolume
            : 1f;

        return volume * Mathf.Clamp01(sfx);
    }

    private void BeginDeathSound(AudioClip clip, float peakVolume)
    {
        if (clip == null || _deathAudioSource == null || peakVolume <= 0.001f)
            return;

        EnsureDeathAudioSource();
        _deathAudioSource.clip = clip;
        _deathAudioSource.volume = peakVolume;
        _deathAudioSource.mute = false;
        _deathAudioSource.Play();
    }

    private void UpdateDeathSoundVolume(float peakVolume, float fade01)
    {
        if (_deathAudioSource == null || !_deathAudioSource.isPlaying)
            return;

        _deathAudioSource.volume = peakVolume * Mathf.Clamp01(fade01);
    }

    private void DisableColliders()
    {
        _colliders = GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < _colliders.Length; i++)
        {
            if (_colliders[i] != null)
                _colliders[i].enabled = false;
        }
    }

    private void SpawnProceduralDeathParticles()
    {
        GameObject go = new GameObject("EnemyDeathVFX");
        go.transform.position = transform.position + Vector3.up * 0.9f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = ResolveProceduralParticleMaterial();
        }

        ParticleSystem.MainModule main = ps.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.duration = 0.25f;
        float lifetime = Mathf.Max(0.05f, proceduralLifetime);
        main.startLifetime = lifetime;
        main.startSpeed = Mathf.Max(0.01f, proceduralStartSpeed);
        main.startSize = Mathf.Max(0.01f, proceduralSize);
        main.startColor = proceduralParticleColor;
        main.maxParticles = Mathf.Max(8, proceduralBurstCount);

        ParticleSystem.EmissionModule emission = ps.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[]
        {
            new ParticleSystem.Burst(0f, (short)Mathf.Clamp(proceduralBurstCount, 1, 300))
        });

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = Mathf.Max(0f, proceduralRadius);
        shape.arcMode = ParticleSystemShapeMultiModeValue.Random;
        shape.alignToDirection = false;
        shape.rotation = new Vector3(90f, 0f, 0f);

        ParticleSystem.VelocityOverLifetimeModule vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space = ParticleSystemSimulationSpace.Local;
        vel.radial = 1f;

        ParticleSystem.ColorOverLifetimeModule col = ps.colorOverLifetime;
        col.enabled = true;
        Gradient g = new Gradient();
        g.SetKeys(
            new[] { new GradientColorKey(proceduralParticleColor, 0f), new GradientColorKey(proceduralParticleColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = g;

        ps.Play(true);
        Destroy(go, lifetime + main.duration + 0.35f);
    }

    private Material ResolveProceduralParticleMaterial()
    {
        if (proceduralParticleMaterial != null)
            return proceduralParticleMaterial;

        Shader shader =
            Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
            Shader.Find("Universal Render Pipeline/Particles/Lit") ??
            Shader.Find("Particles/Standard Unlit") ??
            Shader.Find("Legacy Shaders/Particles/Alpha Blended");

        if (shader == null)
            return null;

        Material mat = new Material(shader);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", proceduralParticleColor);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", proceduralParticleColor);

        proceduralParticleMaterial = mat;
        return proceduralParticleMaterial;
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;

        if (_deathRoutine != null)
        {
            StopCoroutine(_deathRoutine);
            _deathRoutine = null;
        }

        RestoreFadeVisuals();

        if (_colliders != null)
        {
            for (int i = 0; i < _colliders.Length; i++)
            {
                if (_colliders[i] != null)
                    _colliders[i].enabled = true;
            }
        }

        OnHealthChanged?.Invoke(currentHealth);
    }

    private void RestoreFadeVisuals()
    {
        if (_fadeRenderers == null)
            return;

        for (int i = 0; i < _fadeRenderers.Length; i++)
        {
            FadeRendererEntry entry = _fadeRenderers[i];
            if (entry.Renderer == null)
                continue;

            entry.Renderer.SetPropertyBlock(null);
        }
    }

    private void DisableNavMeshAgents()
    {
        UnityEngine.AI.NavMeshAgent[] agents = GetComponentsInChildren<UnityEngine.AI.NavMeshAgent>(true);
        for (int i = 0; i < agents.Length; i++)
        {
            if (agents[i] != null)
                agents[i].enabled = false;
        }
    }

    public void SetHealth(float health, float maxHealthValue)
    {
        maxHealth = maxHealthValue;
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);

        if (currentHealth <= 0f)
        {
            isDead = true;
            DisableEnemyBehaviour();
            DisableNavMeshAgents();
            DisableColliders();
        }
        else
        {
            isDead = false;
            RestoreFadeVisuals();
        }

        OnHealthChanged?.Invoke(currentHealth);
    }
}
