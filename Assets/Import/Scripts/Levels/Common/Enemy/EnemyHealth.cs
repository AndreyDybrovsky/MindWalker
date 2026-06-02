using UnityEngine;
using UnityEngine.Events;
using System.Collections;

/// <summary>
/// Система здоровья врага с плавной смертью и particle system
/// </summary>
public class EnemyHealth : MonoBehaviour
{
    [Header("Настройки здоровья")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float currentHealth;

    [Header("Эффекты смерти")]
    [SerializeField] private ParticleSystem deathParticleSystem; // Particle System для эффекта смерти
    [SerializeField] private float fadeOutDuration = 1f; // Длительность плавного исчезновения

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

    [Header("События")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnEnemyDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;

    private bool isDead = false;
    private Renderer[] renderers; // Все рендереры врага для плавного исчезновения
    private Material[] originalMaterials;
    private Color[] originalColors;

    private void Start()
    {
        currentHealth = maxHealth;
        
        // Получаем все рендереры для плавного исчезновения
        renderers = GetComponentsInChildren<Renderer>();
        originalMaterials = new Material[renderers.Length];
        originalColors = new Color[renderers.Length];
        
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                originalMaterials[i] = renderers[i].material;
                originalColors[i] = renderers[i].material.color;
            }
        }
        
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);
        
        OnHealthChanged?.Invoke(currentHealth);

        if (currentHealth <= 0f && !isDead)
        {
            Die();
        }
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
        
        // Отключаем компоненты врага (поведение, стрельбу и т.д.)
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
        
        DisableNavMeshAgents();
        
        // Проигрываем particle system эффект
        if (deathParticleSystem != null)
        {
            deathParticleSystem.Play();
        }
        else if (spawnProceduralDeathParticles)
        {
            SpawnProceduralDeathParticles();
        }
        
        // Вызываем событие смерти
        OnEnemyDeath?.Invoke();
        
        // Запускаем плавное исчезновение
        StartCoroutine(FadeOutAndDestroy());
    }

    private void SpawnProceduralDeathParticles()
    {
        GameObject go = new GameObject("EnemyDeathVFX");
        go.transform.position = transform.position + Vector3.up * 0.9f;

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        // На некоторых конфигурациях Unity/рендер-пайплайна PS может стартовать сразу после добавления компонента.
        // Останавливаем и чистим, чтобы безопасно настроить модули без предупреждений.
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
        shape.rotation = new Vector3(90f, 0f, 0f); // выброс по плоскости XZ

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
        // На всякий: если шейдер поддерживает _BaseColor — красим им.
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", proceduralParticleColor);
        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", proceduralParticleColor);

        proceduralParticleMaterial = mat;
        return proceduralParticleMaterial;
    }

    private IEnumerator FadeOutAndDestroy()
    {
        float elapsedTime = 0f;
        
        while (elapsedTime < fadeOutDuration)
        {
            elapsedTime += Time.deltaTime;
            float alpha = 1f - (elapsedTime / fadeOutDuration);
            alpha = Mathf.Clamp01(alpha);
            
            // Плавно уменьшаем прозрачность всех рендереров
            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i] != null && renderers[i].material != null)
                {
                    Color color = originalColors[i];
                    color.a = alpha;
                    renderers[i].material.color = color;
                }
            }
            
            yield return null;
        }
        
        // Оставляем объект в сцене неактивным — так его можно восстановить из сохранения.
        gameObject.SetActive(false);
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        
        // Восстанавливаем цвета
        for (int i = 0; i < renderers.Length && i < originalColors.Length; i++)
        {
            if (renderers[i] != null && renderers[i].material != null)
            {
                renderers[i].material.color = originalColors[i];
            }
        }
        
        OnHealthChanged?.Invoke(currentHealth);
    }

    /// <summary>
    /// Устанавливает здоровье врага при загрузке сохранения
    /// </summary>
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
        
        // Если здоровье 0 или меньше, помечаем как мертвого, но не вызываем Die() (чтобы избежать эффектов)
        if (currentHealth <= 0f)
        {
            isDead = true;
            // Отключаем компоненты врага
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
            
            DisableNavMeshAgents();
        }
        else
        {
            // Сбрасываем флаг смерти, если здоровье восстановлено
            isDead = false;
        }
        
        OnHealthChanged?.Invoke(currentHealth);
    }
}
