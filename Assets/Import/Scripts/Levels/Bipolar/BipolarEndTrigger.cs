using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Финал уровня Bipolar: игрок задевает триггер → затемнение → уровень засчитывается пройденным → лобби / Victory.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BipolarEndTrigger : MonoBehaviour
{
    [Header("Затемнение")]
    [Tooltip("0 — мгновенно, как у BipolarMindscapeSwitchTrigger.")]
    [SerializeField] private float fadeDuration;
    [SerializeField] private float holdOnBlackDuration = 0.35f;
    [SerializeField] private bool lockPlayerMovement = true;

    [Header("Сцены")]
    [SerializeField] private string lobbySceneName = "Main";
    [SerializeField] private string victorySceneName = "Victory";

    [Header("Звук (опционально)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip endSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private bool _sequenceStarted;
    private CanvasGroup _fadeCanvasGroup;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        EnsureTriggerRigidbody();
    }

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (_fadeCanvasGroup != null && _fadeCanvasGroup.alpha > 0.99f)
            _fadeCanvasGroup.alpha = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_sequenceStarted || !IsPlayerCollider(other))
            return;

        _sequenceStarted = true;
        StartCoroutine(EndLevelSequence());
    }

    private IEnumerator EndLevelSequence()
    {
        LevelCompletionManager.SetFadingState(true);
        GameplayInputBlocker.SetBlocked(true);

        if (lockPlayerMovement)
            PlayerInteractionZone.SetPlayerControlLocked(true);

        GameTimer gameTimer = FindFirstObjectByType<GameTimer>();
        if (gameTimer != null)
            gameTimer.StopTimer();

        if (endSound != null && audioSource != null)
            audioSource.PlayOneShot(endSound, soundVolume);

        if (fadeDuration > 0.001f)
            yield return ScreenFadeRunner.FadeToBlack(fadeDuration, _fadeCanvasGroup);
        else
            SetFadeInstant(1f);

        if (holdOnBlackDuration > 0f)
            yield return new WaitForSecondsRealtime(holdOnBlackDuration);

        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        LevelSuccessFlow.MarkCurrentLevelCompleted();
        string targetScene = LevelSuccessFlow.ResolveReturnScene(lobbySceneName, victorySceneName);

        LevelCompletionManager.SetFadingState(false);
        SceneManager.LoadScene(targetScene);
    }

    private void SetFadeInstant(float alpha)
    {
        if (_fadeCanvasGroup == null)
            _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();

        if (_fadeCanvasGroup == null)
            return;

        _fadeCanvasGroup.gameObject.SetActive(true);
        _fadeCanvasGroup.alpha = alpha;
        _fadeCanvasGroup.blocksRaycasts = alpha > 0.001f;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        return other.transform.root.CompareTag("Player");
    }

    private void EnsureTriggerRigidbody()
    {
        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.4f);
        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif
}
