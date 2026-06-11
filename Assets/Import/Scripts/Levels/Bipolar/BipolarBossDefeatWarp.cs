using System.Collections;
using UnityEngine;

/// <summary>
/// Возврат в добрый мир после смерти босса локации Bipolar.
/// Слушает EnemyHealth.OnEnemyDeath: затемнение → переключение восприятия (Meadow)
/// → телепорт игрока на точку (TP2) → плавное осветление.
/// Вешать НА ОТДЕЛЬНЫЙ объект (не на врага): враг деактивируется при смерти и оборвал бы корутину.
/// </summary>
[DisallowMultipleComponent]
public class BipolarBossDefeatWarp : MonoBehaviour
{
    [Header("Источник смерти")]
    [Tooltip("Здоровье босса локации. Если не задано — берётся EnemyHealth с этого же объекта.")]
    [SerializeField] private EnemyHealth bossHealth;

    [Header("Назначение")]
    [SerializeField] private BipolarMindscapeController controller;
    [SerializeField] private BipolarMindscapeMode targetMode = BipolarMindscapeMode.Meadow;
    [Tooltip("Куда телепортировать игрока в добром мире (например, TP2).")]
    [SerializeField] private Transform playerSpawn;
    [SerializeField] private bool matchSpawnRotation = true;
    [SerializeField] private float spawnHeightOffset;

    [Header("Тайминги")]
    [Tooltip("Пауза перед затемнением — дать доиграть смерть/частицы врага.")]
    [SerializeField] private float delayBeforeWarp = 1.2f;
    [Tooltip("Плавное осветление после телепорта (затемнение всегда мгновенное).")]
    [SerializeField] private float fadeOutDuration = 1.35f;
    [SerializeField] private bool lockPlayerMovement = true;

    [Header("Звук (опционально)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip warpSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private bool _triggered;
    private bool _subscribed;
    private CanvasGroup _fadeCanvasGroup;

    private void Awake()
    {
        if (controller == null)
            controller = FindFirstObjectByType<BipolarMindscapeController>();

        if (bossHealth == null)
            bossHealth = GetComponent<EnemyHealth>();
    }

    private void OnEnable()
    {
        Subscribe();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Start()
    {
        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (_fadeCanvasGroup != null && _fadeCanvasGroup.alpha > 0.99f)
            _fadeCanvasGroup.alpha = 0f;

        // На случай, если bossHealth назначили после Awake.
        Subscribe();
    }

    private void Subscribe()
    {
        if (_subscribed || bossHealth == null)
            return;

        bossHealth.OnEnemyDeath.AddListener(OnBossDefeated);
        _subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!_subscribed || bossHealth == null)
            return;

        bossHealth.OnEnemyDeath.RemoveListener(OnBossDefeated);
        _subscribed = false;
    }

    private void OnBossDefeated()
    {
        if (_triggered)
            return;

        if (controller == null)
        {
            Debug.LogWarning($"BipolarBossDefeatWarp ({name}): не найден BipolarMindscapeController.", this);
            return;
        }

        _triggered = true;
        StartCoroutine(WarpRoutine());
    }

    private IEnumerator WarpRoutine()
    {
        if (delayBeforeWarp > 0f)
            yield return new WaitForSeconds(delayBeforeWarp);

        GameplayInputBlocker.SetBlocked(true);
        if (lockPlayerMovement)
            PlayerInteractionZone.SetPlayerControlLocked(true);

        SetFadeInstant(1f);

        if (warpSound != null && audioSource != null)
            audioSource.PlayOneShot(warpSound, soundVolume);

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

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (playerSpawn == null)
            return;

        Gizmos.color = new Color(0.2f, 0.85f, 0.35f, 0.9f);
        Gizmos.DrawSphere(playerSpawn.position, 0.35f);
        Gizmos.DrawLine(transform.position, playerSpawn.position);
    }
#endif
}
