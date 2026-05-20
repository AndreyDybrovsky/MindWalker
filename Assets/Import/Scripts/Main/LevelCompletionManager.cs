using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;

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
    private bool isCompleting = false;
    public static bool IsFading { get; private set; } = false;

    private void Awake()
    {
        if (enemyCounter == null) enemyCounter = FindFirstObjectByType<EnemyCounter>();
        
        if (enemyCounter != null)
        {
            enemyCounter.OnAllEnemiesDefeated.AddListener(OnAllEnemiesDefeated);
        }
    }

    private void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        if (fadeCanvasGroup == null && createFadeCanvasIfMissing) SetupFadeCanvas();
    }

    private void SetupFadeCanvas()
    {
        GameObject fadeCanvas = GameObject.Find("FadeCanvas");
        
        if (fadeCanvas == null)
        {
            fadeCanvas = new GameObject("FadeCanvas");
            Canvas canvas = fadeCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 9999;
            
            CanvasScaler scaler = fadeCanvas.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            
            fadeCanvas.AddComponent<GraphicRaycaster>();
            
            GameObject imageObject = new GameObject("FadeImage");
            imageObject.transform.SetParent(fadeCanvas.transform, false);
            
            Image fadeImage = imageObject.AddComponent<Image>();
            fadeImage.color = Color.black;
            
            RectTransform rectTransform = imageObject.GetComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            
            fadeCanvasGroup = fadeCanvas.AddComponent<CanvasGroup>();
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
        else
        {
            fadeCanvasGroup = fadeCanvas.GetComponent<CanvasGroup>();
            if (fadeCanvasGroup == null)
            {
                fadeCanvasGroup = fadeCanvas.AddComponent<CanvasGroup>();
                fadeCanvasGroup.alpha = 0f;
                fadeCanvasGroup.blocksRaycasts = false;
            }
        }
    }

    private void OnAllEnemiesDefeated()
    {
        if (isCompleting) return;

        BossSpawnManager bossSpawnManager = BossSpawnManager.Instance != null
            ? BossSpawnManager.Instance
            : FindFirstObjectByType<BossSpawnManager>();

        if (bossSpawnManager != null && bossSpawnManager.WillSpawnBoss && !bossSpawnManager.IsBossSpawned)
        {
            return;
        }
        
        isCompleting = true;
        StartCoroutine(CompleteLevel());
    }

    private IEnumerator CompleteLevel()
    {
        IsFading = true;
        
        if (victorySound != null && audioSource != null)
        {
            audioSource.PlayOneShot(victorySound, victorySoundVolume);
        }

        if (fadeCanvasGroup == null && createFadeCanvasIfMissing) SetupFadeCanvas();

        if (fadeCanvasGroup == null)
        {
            IsFading = false;
            yield break;
        }

        fadeCanvasGroup.blocksRaycasts = true;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(holdTime);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        string clearedScene = SceneManager.GetActiveScene().name;
        LobbyPatientProgress.ClearLost(clearedScene);
        GlobalProgressTracker.Instance?.MarkLevelCompleted(clearedScene);

        string targetScene = mainSceneName;
        if (GlobalProgressTracker.Instance != null)
        {
            if (GlobalProgressTracker.Instance.CompletedLevelsCount >= GlobalProgressTracker.Instance.TotalLevels)
            {
                targetScene = victorySceneName;
            }
        }

        IsFading = false;
        SceneManager.LoadScene(targetScene);
    }
    
    private void OnDestroy()
    {
        IsFading = false;
    }
}
