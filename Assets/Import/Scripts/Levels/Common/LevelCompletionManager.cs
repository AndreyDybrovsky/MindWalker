using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LevelCompletionManager : MonoBehaviour
{
    [Header("Ссылки")]
    [SerializeField] private EnemyCounter enemyCounter;
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    [Header("Настройки завершения")]
    [SerializeField] private string mainSceneName = "Main";
    [SerializeField] private string victorySceneName = "Victory";
    [SerializeField] private AudioClip victorySound;
    [SerializeField] private float victorySoundVolume = 1f;
    [SerializeField] private float fadeDuration = 2f;
    [SerializeField] private float holdTime = 1f;

    [Header("Автоматическое создание")]
    [SerializeField] private bool createFadeCanvasIfMissing = true;

    private AudioSource audioSource;
    private bool isCompleting;

    public static bool IsFading { get; private set; }

    public static void SetFadingState(bool fading) => IsFading = fading;

    private void Awake()
    {
        if (enemyCounter == null)
            enemyCounter = FindFirstObjectByType<EnemyCounter>();

        if (enemyCounter != null)
            enemyCounter.OnAllEnemiesDefeated.AddListener(OnAllEnemiesDefeated);
    }

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        EnsureFadeReference();
    }

    private void EnsureFadeReference()
    {
        if (fadeCanvasGroup != null || !createFadeCanvasIfMissing)
            return;

        fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
    }

    private void OnAllEnemiesDefeated()
    {
        if (isCompleting)
            return;

        BossSpawnManager bossSpawnManager = BossSpawnManager.Instance != null
            ? BossSpawnManager.Instance
            : FindFirstObjectByType<BossSpawnManager>();

        if (bossSpawnManager != null && bossSpawnManager.WillSpawnBoss && !bossSpawnManager.IsBossSpawned)
            return;

        if (bossSpawnManager != null && bossSpawnManager.IsBossEncounterActive)
            return;

        isCompleting = true;
        StartCoroutine(CompleteLevel());
    }

    private IEnumerator CompleteLevel()
    {
        SetFadingState(true);
        GameStatsTracker.Instance?.OnLevelCompleted(SceneManager.GetActiveScene().name);

        GameTimer gameTimer = FindFirstObjectByType<GameTimer>();
        if (gameTimer != null)
            gameTimer.StopTimer();

        if (victorySound != null && audioSource != null)
            audioSource.PlayOneShot(victorySound, victorySoundVolume);

        EnsureFadeReference();
        if (fadeCanvasGroup == null)
        {
            SetFadingState(false);
            yield break;
        }

        yield return ScreenFadeRunner.FadeToBlack(fadeDuration, fadeCanvasGroup);
        yield return new WaitForSecondsRealtime(holdTime);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        LevelSuccessFlow.MarkCurrentLevelCompleted();
        string targetScene = LevelSuccessFlow.ResolveReturnScene(mainSceneName, victorySceneName);

        SetFadingState(false);
        SceneManager.LoadScene(targetScene);
    }

    private void OnDestroy()
    {
        SetFadingState(false);
    }
}
