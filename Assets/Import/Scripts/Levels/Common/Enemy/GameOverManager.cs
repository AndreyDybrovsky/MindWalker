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
    [SerializeField] private string lobbySceneName = "Main";

    [Header("Экран результата")]
    [SerializeField] private LevelResultClipboard resultClipboard;

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
        foreach (string linked in LevelSceneProgress.GetLinkedScenes(failedScene))
            LobbyPatientProgress.MarkLost(linked);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // Сохраняем потерянного пациента в файл сохранения
        if (SaveManager.Instance != null && SaveManager.Instance.GetCurrentSaveSlot() >= 0)
            SaveManager.Instance.SaveCurrentGame();

        string lobby = string.IsNullOrWhiteSpace(lobbySceneName) ? "Main" : lobbySceneName;
        string targetScene = LevelSuccessFlow.ResolveReturnScene(lobby, string.Empty);
        SceneManager.LoadScene(targetScene);

        IsFading = false;
    }

    private void OnDestroy()
    {
        IsFading = false;
    }
}
