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
        
        UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
        if (agent != null)
            agent.enabled = false;
        
        // Проигрываем particle system эффект
        if (deathParticleSystem != null)
        {
            deathParticleSystem.Play();
        }
        
        // Вызываем событие смерти
        OnEnemyDeath?.Invoke();
        
        // Запускаем плавное исчезновение
        StartCoroutine(FadeOutAndDestroy());
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
        
        // Делаем врага неактивным или уничтожаем его
        gameObject.SetActive(false);
        
        // Ждем, пока particle system завершится, перед уничтожением
        if (deathParticleSystem != null)
        {
            yield return new WaitForSeconds(deathParticleSystem.main.duration);
        }
        
        Destroy(gameObject);
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
            
            UnityEngine.AI.NavMeshAgent agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null)
                agent.enabled = false;
        }
        else
        {
            // Сбрасываем флаг смерти, если здоровье восстановлено
            isDead = false;
        }
        
        OnHealthChanged?.Invoke(currentHealth);
    }
}
