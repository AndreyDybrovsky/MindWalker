using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.SceneManagement;

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
    private bool bossDefeated = false;
    private GameObject currentBossInstance = null;
    private bool bossMessageVisible;
    private LocalizationManager localizationSubscription;

    public bool IsBossSpawned => bossSpawned;
    public bool WillSpawnBoss => bossPrefab != null || existingBoss != null;
    /// <summary>True пока существующий босс ещё жив — LevelCompletionManager должен ждать.</summary>
    public bool IsBossEncounterActive => existingBoss != null && bossSpawned && !bossDefeated;

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

        // Если bossPrefab не задан, ищем готового босса в сцене по имени
        if (existingBoss == null && bossPrefab == null)
        {
            foreach (var h in FindObjectsByType<EnemyHealth>(FindObjectsSortMode.None))
            {
                if (h.gameObject.name.IndexOf("Boss", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    existingBoss = h;
                    break;
                }
            }
        }

        if (existingBoss != null)
        {
            bossSpawned = true;
            currentBossInstance = existingBoss.gameObject;
            existingBoss.OnEnemyDeath.AddListener(OnEncounterBossDefeated);
        }
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
        SpawnBossFromEncounter(showMessage: true, playSound: true);
    }

    [Header("Завершение уровня при смерти босса (Gambling)")]
    [SerializeField] private string lobbySceneOnWin = "Main";
    [SerializeField] private float winDelayAfterBossDeath = 1.5f;
    [Tooltip("Босс уже стоит на сцене — подписаться на его смерть сразу, без ожидания всех врагов.")]
    [SerializeField] private EnemyHealth existingBoss;

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

        // Казино-босс — убийство = победа на уровне
        EnemyHealth encounterBossHealth = currentBossInstance != null
            ? currentBossInstance.GetComponent<EnemyHealth>()
            : null;
        if (encounterBossHealth != null)
            encounterBossHealth.OnEnemyDeath.AddListener(OnEncounterBossDefeated);
    }

    private void OnEncounterBossDefeated()
    {
        bossDefeated = true;
        StartCoroutine(EncounterBossWinSequence());
    }

    private IEnumerator EncounterBossWinSequence()
    {
        yield return new WaitForSecondsRealtime(winDelayAfterBossDeath);
        LevelSuccessFlow.MarkCurrentLevelCompleted();
        string target = LevelSuccessFlow.ResolveReturnScene(lobbySceneOnWin, string.Empty);
        SceneManager.LoadScene(target);
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
