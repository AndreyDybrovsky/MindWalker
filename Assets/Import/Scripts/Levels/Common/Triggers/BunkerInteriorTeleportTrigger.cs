using System.Collections;
using UnityEngine;

/// <summary>
/// Вход в землянку: E → звук двери → плавное затемнение → телепорт внутрь.
/// </summary>
public class BunkerInteriorTeleportTrigger : PlayerInteractionZone
{
    [Header("Телепорт")]
    [SerializeField] private Transform destination;
    [SerializeField] private bool matchDestinationRotation = true;
    [SerializeField] private float destinationHeightOffset;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip doorOpenSound;

    [Header("Затемнение")]
    [SerializeField] private float fadeOutDuration = 0.8f;
    [SerializeField] private float fadeInDuration = 0.9f;
    [SerializeField] private float blackHoldDuration = 0.15f;
    [SerializeField] private bool lockPlayerMovement = true;

    private CanvasGroup _fadeCanvasGroup;

    protected override void Start()
    {
        base.Start();

        if (destination == null)
            Debug.LogWarning($"BunkerInteriorTeleportTrigger ({name}): не назначен Destination.", this);

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
    }

    protected override void OnInteractPressed()
    {
        if (InteractionBusy)
            return;

        if (destination == null)
        {
            Debug.LogWarning($"BunkerInteriorTeleportTrigger ({name}): телепорт отменён — Destination не назначен.", this);
            return;
        }

        InteractionBusy = true;
        PlayerInZone = false;

        if (PromptView != null)
            PromptView.SetReveal(0f);

        StartCoroutine(TeleportSequence());
    }

    private IEnumerator TeleportSequence()
    {
        if (lockPlayerMovement)
            SetPlayerControlLocked(true);

        GameplayInputBlocker.SetBlocked(true);

        if (doorOpenSound != null && audioSource != null)
            audioSource.PlayOneShot(doorOpenSound);

        yield return ScreenFadeRunner.FadeToBlack(fadeOutDuration, _fadeCanvasGroup);

        if (blackHoldDuration > 0f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);

        PlayerTeleportUtility.TeleportTo(destination, matchDestinationRotation, destinationHeightOffset);

        yield return null;
        Physics.SyncTransforms();

        yield return ScreenFadeRunner.FadeFromBlack(fadeInDuration, _fadeCanvasGroup);

        if (lockPlayerMovement)
            SetPlayerControlLocked(false);

        GameplayInputBlocker.SetBlocked(false);
        InteractionBusy = false;
    }
}
