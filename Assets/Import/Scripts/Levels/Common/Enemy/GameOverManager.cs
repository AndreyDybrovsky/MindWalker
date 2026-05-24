using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private bool isFading;
    private AudioSource audioSource;

    public static bool IsFading { get; private set; }

    private void Awake()
    {
        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null)
            playerHealth.OnPlayerDeath.AddListener(StartGameOver);
    }

    private void Start()
    {
        EnsureFadeReference();

        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void EnsureFadeReference()
    {
        if (fadeCanvasGroup != null || !createFadeCanvasIfMissing)
            return;

        fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
    }

    public void StartGameOver()
    {
        if (isFading)
            return;

        isFading = true;
        IsFading = true;
        StartCoroutine(FadeAndQuit());
    }

    private IEnumerator FadeAndQuit()
    {
        EnsureFadeReference();
        if (fadeCanvasGroup == null)
            yield break;

        if (deathSound != null && audioSource != null)
            audioSource.PlayOneShot(deathSound, deathSoundVolume);

        yield return ScreenFadeRunner.FadeToBlack(fadeDuration, fadeCanvasGroup);
        yield return new WaitForSecondsRealtime(holdTime);

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
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        else
            SceneManager.LoadScene(mainMenuSceneName);

        IsFading = false;
    }

    private void OnDestroy()
    {
        IsFading = false;
    }
}
