using UnityEngine;
using TMPro;
using System.Collections;

public class BossSpawnManager : MonoBehaviour
{
    public static BossSpawnManager Instance { get; private set; }

    [Header("Ссылки")]
    [SerializeField] private EnemyCounter enemyCounter;

    [Header("Boss")]
    [SerializeField] private GameObject bossPrefab;
    [SerializeField] private Transform bossSpawnPoint;

    [Header("UI сообщение")]
    [SerializeField] private TextMeshProUGUI bossMessageTextUI;
    [SerializeField] private TextMesh bossMessageText3D;
    [Tooltip("Ключ в String Table LocalizationBase. Если строка не найдена — используется bossMessage.")]
    [SerializeField] private string bossSpawnMessageKey = "boss.spawn_message";
    [SerializeField] private string bossMessage = "Появился БОСС!";
    [SerializeField] private float messageDuration = 3f;

    [Header("Звук")]
    [SerializeField] private AudioClip bossSpawnSound;
    [SerializeField] private float bossSpawnSoundVolume = 1f;

    private AudioSource audioSource;
    private bool bossSpawned = false;
    private GameObject currentBossInstance = null;
    private bool bossMessageVisible;
    private LocalizationManager localizationSubscription;

    public bool IsBossSpawned => bossSpawned;
    public bool WillSpawnBoss => bossPrefab != null;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
    }

    private void OnEnable()
    {
        TrySubscribeLocalization();
    }

    private void OnDisable()
    {
        if (localizationSubscription != null)
        {
            localizationSubscription.OnLanguageChanged -= OnLanguageChanged;
            localizationSubscription = null;
        }
    }

    private void TrySubscribeLocalization()
    {
        LocalizationManager loc = LocalizationManager.Instance;
        if (loc == null || localizationSubscription == loc)
            return;

        if (localizationSubscription != null)
            localizationSubscription.OnLanguageChanged -= OnLanguageChanged;

        localizationSubscription = loc;
        localizationSubscription.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        if (!bossMessageVisible)
            return;

        SetMessageText(GetLocalizedBossMessage());
    }

    private void Start()
    {
        if (enemyCounter == null) enemyCounter = FindFirstObjectByType<EnemyCounter>();

        if (enemyCounter != null)
            enemyCounter.OnAllEnemiesDefeated.AddListener(OnAllEnemiesDefeated);

        TrySubscribeLocalization();
        SetMessageVisible(false);
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnAllEnemiesDefeated()
    {
        if (bossSpawned) return;
        if (bossPrefab == null) return;
        SpawnBoss();
    }

    private void SpawnBoss()
    {
<<<<<<< HEAD
        SpawnBossInternal(
            bossSpawnPoint != null ? bossSpawnPoint.position : transform.position,
            bossSpawnPoint != null ? bossSpawnPoint.rotation : transform.rotation,
            showMessage: true,
            playSound: true
        );
=======
        SpawnBossFromEncounter(showMessage: true, playSound: true);
    }

    /// <summary>Сцена казино: бой после диалога с боссом (без ожидания уничтожения всех врагов).</summary>
    public void SpawnBossFromEncounter(bool showMessage = false, bool playSound = true)
    {
        if (bossSpawned || bossPrefab == null)
            return;

        SpawnBossInternal(
            bossSpawnPoint != null ? bossSpawnPoint.position : transform.position,
            bossSpawnPoint != null ? bossSpawnPoint.rotation : transform.rotation,
            showMessage,
            playSound);
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
    }
    
    private void SpawnBossInternal(Vector3 position, Quaternion rotation, bool showMessage = true, bool playSound = true)
    {
        bossSpawned = true;

        GameObject boss = Instantiate(bossPrefab, position, rotation);
        boss.name = $"{bossPrefab.name} (Boss)";
        currentBossInstance = boss;

        if (showMessage) StartCoroutine(ShowBossMessageRoutine());
        if (playSound) PlayBossSpawnSound();

        EnemyHealth bossHealth = boss.GetComponent<EnemyHealth>();
        if (bossHealth != null && enemyCounter != null)
        {
            enemyCounter.AddEnemy(boss, bossHealth);
        }
    }
    
    public void LoadBossState(Vector3 position, Quaternion rotation, float health, float maxHealth, bool isAlive)
    {
        if (bossPrefab == null) return;
        
        SpawnBossInternal(position, rotation, showMessage: false, playSound: false);
        
        if (currentBossInstance != null)
        {
            currentBossInstance.SetActive(isAlive);
            
            EnemyHealth bossHealth = currentBossInstance.GetComponent<EnemyHealth>();
            if (bossHealth != null)
            {
                bossHealth.SetHealth(health, maxHealth);
            }
        }
    }

    private void PlayBossSpawnSound()
    {
        if (bossSpawnSound != null && audioSource != null)
            audioSource.PlayOneShot(bossSpawnSound, bossSpawnSoundVolume);
    }

    private string GetLocalizedBossMessage()
    {
        if (string.IsNullOrEmpty(bossSpawnMessageKey))
            return bossMessage;

        if (LocalizationManager.Instance == null)
            return bossMessage;

        string localized = LocalizationManager.Instance.T(bossSpawnMessageKey);
        if (string.IsNullOrEmpty(localized) || localized == bossSpawnMessageKey)
            return bossMessage;

        return localized;
    }

    private IEnumerator ShowBossMessageRoutine()
    {
        bossMessageVisible = true;
        SetMessageText(GetLocalizedBossMessage());
        SetMessageVisible(true);
        yield return new WaitForSeconds(messageDuration);
        SetMessageVisible(false);
        bossMessageVisible = false;
    }

    private void SetMessageText(string text)
    {
        if (bossMessageTextUI != null) bossMessageTextUI.text = text;
        if (bossMessageText3D != null) bossMessageText3D.text = text;
    }

    private void SetMessageVisible(bool visible)
    {
        if (bossMessageTextUI != null) bossMessageTextUI.gameObject.SetActive(visible);
        if (bossMessageText3D != null) bossMessageText3D.gameObject.SetActive(visible);
    }
}
