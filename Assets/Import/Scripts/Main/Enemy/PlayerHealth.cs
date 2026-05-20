using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Настройки здоровья")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("События")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnPlayerDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsDead => isDead; // Публичное свойство для проверки смерти

    private bool isDead = false;
    private bool isInitialized = false; // Флаг инициализации

    private void Start()
    {
        // Инициализируем только если не идет загрузка сохранения
        // SaveManager применит данные после инициализации
        if (!isInitialized)
        {
            currentHealth = maxHealth;
            OnHealthChanged?.Invoke(currentHealth);
            isInitialized = true;
        }
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
        isDead = true;
        OnPlayerDeath?.Invoke();
    }

    public void ResetHealth()
    {
        isDead = false;
        currentHealth = maxHealth;
        OnHealthChanged?.Invoke(currentHealth);
    }
    
    // Метод для установки здоровья при загрузке сохранения
    public void SetHealth(float health, float maxHealthValue)
    {
        maxHealth = maxHealthValue;
        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        isInitialized = true; // Помечаем как инициализированный
        
        // Если здоровье 0 или меньше, вызываем смерть
        if (currentHealth <= 0f && !isDead)
        {
            Die();
        }
        else if (currentHealth > 0f)
        {
            // Сбрасываем флаг смерти, если здоровье восстановлено
            isDead = false;
        }
        
        OnHealthChanged?.Invoke(currentHealth);
    }
}
