using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections;

public class GameOverManager : MonoBehaviour
{
    [Header("Настройки затемнения")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;
    [SerializeField] private float fadeDuration = 2f;
    [SerializeField] private float holdTime = 1f;

    [Header("Звук смерти")]
    [SerializeField] private AudioClip deathSound;
    [SerializeField] private float deathSoundVolume = 1f;

    [Header("Настройки сцены")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Автоматическое создание")]
    [SerializeField] private bool createFadeCanvasIfMissing = true;

    private bool isFading = false;
    private AudioSource audioSource;
    public static bool IsFading { get; private set; } = false;

    private void Awake()
    {
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
        {
            playerHealth.OnPlayerDeath.AddListener(StartGameOver);
        }
    }

    private void Start()
    {
        if (fadeCanvasGroup == null && createFadeCanvasIfMissing) SetupFadeCanvas();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }
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

    public void StartGameOver()
    {
        if (isFading) return;
        
        isFading = true;
        IsFading = true;
        StartCoroutine(FadeAndQuit());
    }

    private IEnumerator FadeAndQuit()
    {
        if (fadeCanvasGroup == null && createFadeCanvasIfMissing) SetupFadeCanvas();

        if (fadeCanvasGroup == null) yield break;

        if (deathSound != null && audioSource != null)
            audioSource.PlayOneShot(deathSound, deathSoundVolume);

        fadeCanvasGroup.blocksRaycasts = true;
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsedTime / fadeDuration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
        yield return new WaitForSeconds(holdTime);

        string failedScene = SceneManager.GetActiveScene().name;
        LobbyPatientProgress.MarkLost(failedScene);
        if (SaveManager.Instance != null && SaveManager.Instance.GetCurrentSaveSlot() >= 0)
            SaveManager.Instance.SaveCurrentGame();

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        if (SaveManager.Instance != null)
        {
            int currentSlot = SaveManager.Instance.GetCurrentSaveSlot();
            if (currentSlot >= 0)
            {
                GameSaveData currentSave = SaveManager.Instance.GetSaveData(currentSlot);
                
                if (currentSave != null && !currentSave.IsEmpty() && currentSave.playerHealth <= 0f)
                {
                    SaveManager.Instance.DeleteSave(currentSlot, keepCurrentSlot: false);
                    SaveManager.Instance.LoadAllSaves();
                    
                    GameSaveData remainingSave = SaveManager.Instance.GetSaveData(currentSlot);
                    
                    if (remainingSave != null && !remainingSave.IsEmpty() && remainingSave.playerHealth > 0f)
                    {
                        SaveManager.Instance.SetCurrentSaveSlot(currentSlot);
                        SaveManager.Instance.ApplySaveData(remainingSave);
                        yield break;
                    }
                }
            }
        }

        GameTimer gameTimer = FindFirstObjectByType<GameTimer>();
        if (gameTimer != null && gameTimer.GetRemainingTime() <= 0f)
        {
            string currentSceneName = SceneManager.GetActiveScene().name;
            SceneManager.LoadScene(currentSceneName);
        }
        else
        {
            SceneManager.LoadScene(mainMenuSceneName);
        }
        
        IsFading = false;
    }
    
    private void OnDestroy()
    {
        IsFading = false;
    }
}
