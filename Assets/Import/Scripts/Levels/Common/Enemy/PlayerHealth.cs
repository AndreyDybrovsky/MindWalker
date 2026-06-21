using UnityEngine;
using UnityEngine.Events;

public class PlayerHealth : MonoBehaviour
{
    [Header("Настройки здоровья")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float currentHealth;

    [Header("Звук")]
    [SerializeField] private AudioClip damageSound;
    [SerializeField] private AudioClip healSound;
    [SerializeField, Range(0f, 1f)] private float damageSoundVolume = 0.8f;
    [SerializeField, Range(0f, 1f)] private float healSoundVolume = 0.75f;

    [Header("События")]
    public UnityEvent<float> OnHealthChanged;
    public UnityEvent OnPlayerDeath;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float HealthPercentage => maxHealth > 0 ? currentHealth / maxHealth : 0f;
    public bool IsDead => isDead; // Публичное свойство для проверки смерти

    public bool SuppressDamageFeedback { get; private set; }

    private bool isDead = false;
    private bool isInitialized;
    private AudioSource _audioSource;
    private float _lastDamageSoundTime = -10f;
    private const float DamageSoundCooldown = 0.45f;

    public void SetSuppressDamageFeedback(bool suppress) => SuppressDamageFeedback = suppress;

    private void Awake()
    {
        EnsureInitialized();
        EnsureAudioSource();
    }

    private void Start()
    {
        EnsureInitialized();
    }

    private void EnsureAudioSource()
    {
        if (_audioSource != null) return;
        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.playOnAwake = false;
        _audioSource.loop = false;
        _audioSource.spatialBlend = 0f;
        AudioMixerRoutingUtility.BindSourceToSfx(_audioSource);

        if (damageSound == null)
            damageSound = Resources.Load<AudioClip>("Sounds/UI SFX Free Pack/Assets/warning_55");
        if (healSound == null)
            healSound = Resources.Load<AudioClip>("Sounds/Other/Heal");
    }

    private void PlaySound(AudioClip clip, float volume)
    {
        if (_audioSource == null || clip == null) return;
        float sfx = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().sfxVolume : 1f;
        _audioSource.PlayOneShot(clip, volume * Mathf.Clamp01(sfx));
    }

    private void EnsureInitialized()
    {
        if (isInitialized)
            return;

        if (currentHealth <= 0f)
            currentHealth = maxHealth;

        isInitialized = true;
        OnHealthChanged?.Invoke(currentHealth);
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        GameStatsTracker.Instance?.RecordDamageReceived(damage);
        currentHealth -= damage;
        currentHealth = Mathf.Max(0f, currentHealth);

        if (!SuppressDamageFeedback && Time.time - _lastDamageSoundTime >= DamageSoundCooldown)
        {
            PlaySound(damageSound, damageSoundVolume);
            CameraShaker.Shake(0.3f); // в том же кулдауне, чтобы DoT не тряс экран постоянно
            _lastDamageSoundTime = Time.time;
        }
        OnHealthChanged?.Invoke(currentHealth);

        if (currentHealth <= 0f && !isDead)
            Die();
    }

    public void Heal(float amount)
    {
        if (isDead) return;

        currentHealth += amount;
        currentHealth = Mathf.Min(maxHealth, currentHealth);

        PlaySound(healSound, healSoundVolume);
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
