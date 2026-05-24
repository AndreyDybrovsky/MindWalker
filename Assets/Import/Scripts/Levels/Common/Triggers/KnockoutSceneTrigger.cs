using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Удар / нокаут: подсказка E, звук, затемнение, загрузка сцены.
/// </summary>
public class KnockoutSceneTrigger : PlayerInteractionZone
{
    private const string DefaultTargetScene = "PTSD in Danger";

    [Header("Сцена")]
    [SerializeField] private string targetSceneName = DefaultTargetScene;

    [Header("Нокаут")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip batonHitSound;
    [SerializeField] private float hitSoundDelay = 0.05f;
    [SerializeField] private float fadeDelayAfterHit = 0.35f;
    [SerializeField] private float fadeDuration = 1.2f;
    [SerializeField] private bool fastKnockoutFade;
    [SerializeField] private float fastFadeDuration = 0.35f;
    [SerializeField] private bool lockPlayerMovement = true;

    private CanvasGroup _fadeCanvasGroup;

    protected override void Start()
    {
        base.Start();

        if (string.IsNullOrWhiteSpace(targetSceneName))
            targetSceneName = DefaultTargetScene;

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
        if (LevelSceneProgress.IsEntryBlocked(targetSceneName))
        {
            PlayerInZone = false;
            Reveal = 0f;
            PressEPromptCoordinator.Refresh();
            return;
        }

        base.Update();
    }

    protected override void OnInteractPressed()
    {
        if (InteractionBusy || LevelSceneProgress.IsEntryBlocked(targetSceneName))
            return;

        InteractionBusy = true;
        PlayerInZone = false;
        Reveal = 0f;

        if (PromptView != null)
            PromptView.SetReveal(0f);

        if (lockPlayerMovement)
            SetPlayerControlLocked(true);

        StartCoroutine(KnockoutSequence());
    }

    private IEnumerator KnockoutSequence()
    {
        yield return new WaitForSecondsRealtime(hitSoundDelay);

        if (batonHitSound != null && audioSource != null)
            audioSource.PlayOneShot(batonHitSound);

        yield return new WaitForSecondsRealtime(fadeDelayAfterHit);

        float duration = fastKnockoutFade ? fastFadeDuration : fadeDuration;
        yield return ScreenFadeRunner.FadeToBlack(duration, _fadeCanvasGroup);

        yield return new WaitForSecondsRealtime(0.1f);
        SceneManager.LoadScene(targetSceneName);
    }
}
