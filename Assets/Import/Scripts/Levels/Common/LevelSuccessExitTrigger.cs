using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Успешный выход с уровня: при входе в зону (или по E) — затемнение, блок из трёх звуков, лобби и отметка прохождения.
/// </summary>
public class LevelSuccessExitTrigger : PlayerInteractionZone
{
    public enum ActivationMode
    {
        OnPlayerEnter,
        OnInteract
    }

    [Header("Запуск")]
    [SerializeField] private ActivationMode activation = ActivationMode.OnPlayerEnter;
    [SerializeField] private bool playOnce = true;

    [Header("Звуки (по порядку)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip weaponPickupSound;
    [SerializeField] private AudioClip shootingSound;
    [SerializeField] private AudioClip levelCompleteSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
    [Tooltip("Пауза после звука подбора оружия, перед выстрелом.")]
    [SerializeField] private float pauseAfterWeaponPickup = 0.55f;
    [Tooltip("Сколько раз повторить звук выстрела.")]
    [SerializeField] private int shootingRepeatCount = 1;
    [Tooltip("Пауза между повторными выстрелами.")]
    [SerializeField] private float pauseBetweenShots = 0.28f;
    [Tooltip("Пауза после последнего выстрела, перед финальным звуком уровня.")]
    [SerializeField] private float pauseAfterShooting = 0.65f;
    [Tooltip("Пауза после финального звука, перед загрузкой лобби.")]
    [SerializeField] private float pauseAfterLevelComplete = 0.9f;

    [Header("Затемнение")]
    [SerializeField] private float fadeDuration = 1.4f;
    [SerializeField] private float delayBeforeSoundBlock = 0.15f;
    [SerializeField] private bool lockPlayerMovement = true;

    [Header("Сцены")]
    [SerializeField] private string lobbySceneName = "Main";
    [SerializeField] private string victorySceneName = "Victory";

    private CanvasGroup _fadeCanvasGroup;
    private bool _sequenceStarted;

    protected override void Awake()
    {
        base.Awake();

        if (activation == ActivationMode.OnPlayerEnter && PromptView != null)
            PromptView.gameObject.SetActive(false);
    }

    protected override void Start()
    {
        base.Start();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (_fadeCanvasGroup != null && _fadeCanvasGroup.alpha > 0.99f)
            _fadeCanvasGroup.alpha = 0f;
    }

    protected override void Update()
    {
        base.Update();

        if (activation != ActivationMode.OnPlayerEnter || InteractionBusy || _sequenceStarted)
            return;

        if (PlayerInZone)
            TryBeginSequence();
    }

    protected override void OnInteractPressed()
    {
        if (activation != ActivationMode.OnInteract)
            return;

        TryBeginSequence();
    }

    private void TryBeginSequence()
    {
        if (_sequenceStarted)
            return;

        _sequenceStarted = true;
        InteractionBusy = true;
        PlayerInZone = false;
        Reveal = 0f;

        GameTimer gameTimer = FindFirstObjectByType<GameTimer>();
        if (gameTimer != null)
            gameTimer.StopTimer();

        if (PromptView != null)
            PromptView.SetReveal(0f);

        StartCoroutine(SuccessExitSequence());
    }

    private IEnumerator SuccessExitSequence()
    {
        LevelCompletionManager.SetFadingState(true);
        GameplayInputBlocker.SetBlocked(true);

        if (lockPlayerMovement)
            SetPlayerControlLocked(true);

        yield return ScreenFadeRunner.FadeToBlack(fadeDuration, _fadeCanvasGroup);

        if (delayBeforeSoundBlock > 0f)
            yield return new WaitForSecondsRealtime(delayBeforeSoundBlock);

        yield return PlayClipAndWait(weaponPickupSound, pauseAfterWeaponPickup);

        int repeats = Mathf.Max(1, shootingRepeatCount);
        for (int i = 0; i < repeats; i++)
        {
            float pause = (i < repeats - 1) ? pauseBetweenShots : pauseAfterShooting;
            yield return PlayClipAndWait(shootingSound, pause);
        }

        yield return PlayClipAndWait(levelCompleteSound, pauseAfterLevelComplete);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        LevelSuccessFlow.MarkCurrentLevelCompleted();
        string targetScene = LevelSuccessFlow.ResolveReturnScene(lobbySceneName, victorySceneName);

        LevelCompletionManager.SetFadingState(false);
        SceneManager.LoadScene(targetScene);
    }

    private IEnumerator PlayClipAndWait(AudioClip clip, float pauseAfter)
    {
        if (audioSource == null)
            yield break;

        if (clip != null)
        {
            audioSource.Stop();
            audioSource.clip = clip;
            audioSource.volume = soundVolume;
            audioSource.Play();

            while (audioSource.isPlaying)
                yield return null;
        }

        if (pauseAfter > 0f)
            yield return new WaitForSecondsRealtime(pauseAfter);
    }
}
