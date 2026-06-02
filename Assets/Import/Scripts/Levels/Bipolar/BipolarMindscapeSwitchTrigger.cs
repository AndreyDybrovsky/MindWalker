using System.Collections;
using UnityEngine;

/// <summary>
/// Триггер на уровне Bipolar: мгновенное затемнение → телепорт → смена пост-обработки → мгновенное осветление.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BipolarMindscapeSwitchTrigger : MonoBehaviour
{
    [Header("Переключение")]
    [SerializeField] private BipolarMindscapeController controller;
    [SerializeField] private BipolarMindscapeMode targetMode = BipolarMindscapeMode.Nightmare;
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private bool matchSpawnRotation = true;
    [SerializeField] private float spawnHeightOffset;

    [Header("Поведение")]
    [SerializeField] private bool lockPlayerMovement = true;
    [Tooltip("Плавное осветление после телепорта (затемнение всегда мгновенное).")]
    [SerializeField] private float fadeOutDuration = 1.35f;

    [Header("Звук (опционально)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip switchSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private bool _isSwitching;
    private CanvasGroup _fadeCanvasGroup;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        EnsureTriggerRigidbody();

        if (controller == null)
            controller = FindFirstObjectByType<BipolarMindscapeController>();
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
        if (_isSwitching || !IsPlayerCollider(other))
            return;

        if (controller == null)
        {
            Debug.LogWarning($"BipolarMindscapeSwitchTrigger ({name}): не найден BipolarMindscapeController.", this);
            return;
        }

        if (controller.CurrentMode == targetMode)
            return;

        StartCoroutine(SwitchMindscapeRoutine());
    }

    private IEnumerator SwitchMindscapeRoutine()
    {
        _isSwitching = true;

        GameplayInputBlocker.SetBlocked(true);
        if (lockPlayerMovement)
            PlayerInteractionZone.SetPlayerControlLocked(true);

        SetFadeInstant(1f);

        if (switchSound != null && audioSource != null)
            audioSource.PlayOneShot(switchSound, soundVolume);

        controller.ApplyModeImmediate(targetMode);

        if (playerSpawn != null)
            PlayerTeleportUtility.TeleportTo(playerSpawn, matchSpawnRotation, spawnHeightOffset);

        yield return null;
        Physics.SyncTransforms();

        if (fadeOutDuration > 0.001f)
            yield return ScreenFadeRunner.FadeFromBlack(fadeOutDuration, _fadeCanvasGroup);
        else
            SetFadeInstant(0f);

        if (lockPlayerMovement)
            PlayerInteractionZone.SetPlayerControlLocked(false);

        GameplayInputBlocker.SetBlocked(false);
        _isSwitching = false;
    }

    private void SetFadeInstant(float alpha)
    {
        if (_fadeCanvasGroup == null)
            _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();

        if (_fadeCanvasGroup == null)
            return;

        _fadeCanvasGroup.gameObject.SetActive(alpha > 0.001f);
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
        Gizmos.color = targetMode == BipolarMindscapeMode.Nightmare
            ? new Color(0.85f, 0.15f, 0.15f, 0.35f)
            : new Color(0.2f, 0.85f, 0.35f, 0.35f);

        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        if (playerSpawn != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(playerSpawn.position, 0.35f);
            Gizmos.DrawLine(transform.position, playerSpawn.position);
        }
    }
#endif
}
