using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// День 4 ОКР, ветка «Забудь(?)»: игрок подходит к кровати и ложится спать (нажатие E).
/// Уровень считается ПРОЙДЕННЫМ (возврат в лобби), но пациент засчитывается как
/// ПОТЕРЯННЫЙ — герой сдался и не стал ничего исправлять.
/// Альтернатива: собрать все точки FixAll → «Излечись» → пациент спасён (HealExit).
///
/// Зона прячется на старте и появляется вместе с точками FixAll —
/// её достаточно добавить в Enable During Black у момента «Выберись».
/// </summary>
[RequireComponent(typeof(Collider))]
public class OCDSleepGiveUpZone : PlayerInteractionZone
{
    [Header("Засыпание")]
    [SerializeField] private float fadeDuration = 1.6f;
    [Tooltip("Сколько держать экран чёрным после засыпания, перед возвратом в лобби.")]
    [SerializeField] private float holdBlackDuration = 1.2f;
    [SerializeField] private bool lockPlayerMovement = true;

    [Header("Звук засыпания (опционально)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip sleepSound;
    [SerializeField, Range(0f, 1f)] private float sleepSoundVolume = 1f;

    [Header("Сцена возврата")]
    [SerializeField] private string lobbySceneName = "Main";

    private CanvasGroup _fadeCanvasGroup;
    private bool _sequenceStarted;

    protected override void Start()
    {
        base.Start();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (_fadeCanvasGroup != null && _fadeCanvasGroup.alpha > 0.99f)
            _fadeCanvasGroup.alpha = 0f;
    }

    protected override void OnInteractPressed()
    {
        if (_sequenceStarted)
            return;

        _sequenceStarted = true;
        InteractionBusy = true;
        PlayerInZone = false;
        Reveal = 0f;

        if (PromptView != null)
            PromptView.SetReveal(0f);

        PressEPromptCoordinator.Refresh();
        StartCoroutine(SleepSequence());
    }

    private IEnumerator SleepSequence()
    {
        LevelCompletionManager.SetFadingState(true);
        GameplayInputBlocker.SetBlocked(true);

        if (lockPlayerMovement)
            SetPlayerControlLocked(true);

        GameTimer gameTimer = FindFirstObjectByType<GameTimer>();
        if (gameTimer != null)
            gameTimer.StopTimer();

        if (sleepSound != null && audioSource != null)
            audioSource.PlayOneShot(sleepSound, sleepSoundVolume);

        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
        yield return ScreenFadeRunner.FadeToBlack(fadeDuration, _fadeCanvasGroup);

        if (holdBlackDuration > 0f)
            yield return new WaitForSecondsRealtime(holdBlackDuration);

        // Уровень пройден, но герой сдался → пациент засчитывается как ПОТЕРЯННЫЙ.
        string scene = SceneManager.GetActiveScene().name;
        LobbyPatientProgress.MarkLost(scene);

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveCurrentGame();

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        LevelCompletionManager.SetFadingState(false);
        SceneManager.LoadScene(string.IsNullOrWhiteSpace(lobbySceneName) ? "Main" : lobbySceneName);
    }
}
