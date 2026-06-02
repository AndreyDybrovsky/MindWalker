using System.Collections;
using TMPro;
using UnityEngine;

/// <summary>
/// Depression: первое появление — таймер «найти пациента», затем таймер успокоения.
/// При провале поиска — урон герою и повтор с фиксированной точки. После первого излечения —
/// пауза 6–13 с и спавн в случайной точке зоны с таймером успокоения.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DepressionPanicMomentZone : MonoBehaviour
{
    [Header("Спавн")]
    [Tooltip("Фиксированная точка первого появления.")]
    [SerializeField] private Transform spawnPoint;
    [Tooltip("Зона случайного спавна после первого излечения (ZoneSpawn Box Collider).")]
    [SerializeField] private Collider spawnArea;
    [SerializeField] private float spawnHeightOffset = 0f;
    [SerializeField] private GameObject panicCharacterPrefab;

    [Header("Первый спавн — поиск")]
    [SerializeField] private float findTimeLimitSeconds = 60f;
    [SerializeField] private float findFailDamage = 25f;
    [Tooltip("Дистанция до пациента, при которой считается, что герой его нашёл.")]
    [SerializeField] private float findProximityDistance = 10f;
    [SerializeField] private string findTimerLocalizationKey = "scene.depression.find_timer";
    [SerializeField] private string findTimerFallbackText = "Найдите пациента: {0}";

    [Header("Повтор после излечения")]
    [SerializeField] private float respawnDelayMin = 6f;
    [SerializeField] private float respawnDelayMax = 13f;

    [Header("Таймер успокоения")]
    [SerializeField] private float calmTimeLimitSeconds = 25f;
    [SerializeField] private string timerLocalizationKey = "scene.depression.calm_timer";
    [SerializeField] private string timerFallbackText = "Успокойте: {0}";
    [SerializeField] private TMP_Text timerStyleReference;

    [Header("Подсказка E у модели")]
    [SerializeField] private string calmPromptLocalizationKey = "scene.depression.calm_interact";
    [SerializeField] private string calmPromptFallback = "Успокойте";

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip panicAppearSound;
    [SerializeField] private AudioClip calmSuccessSound;
    [SerializeField] private AudioClip calmFailSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("Поведение")]
    [SerializeField] private float cleanupDelay = 0.5f;

    private bool _zoneLoopRunning;
    private bool _eventActive;
    private bool _findPhaseActive;
    private bool _isFirstSpawn = true;
    private GameObject _spawnedCharacter;
    private DepressionCalmInteraction _calmInteraction;
    private Coroutine _loopRoutine;

    public string CalmPromptLocalizationKey => calmPromptLocalizationKey;
    public string CalmPromptFallback => calmPromptFallback;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        EnsureTriggerRigidbody();

        if (audioSource == null)
            TryGetComponent(out audioSource);

        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        AudioMixerRoutingUtility.BindSourceToSfx(audioSource);

        if (spawnPoint == null)
        {
            GameObject point = new GameObject("PanicSpawnPoint");
            point.transform.SetParent(transform);
            point.transform.localPosition = Vector3.zero;
            spawnPoint = point.transform;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_zoneLoopRunning || !IsPlayerCollider(other))
            return;

        _loopRoutine = StartCoroutine(ZonePanicLoop());
    }

    private IEnumerator ZonePanicLoop()
    {
        _zoneLoopRunning = true;

        while (true)
        {
            if (panicCharacterPrefab == null)
            {
                Debug.LogWarning($"DepressionPanicMomentZone '{name}': не задан Panic Character Prefab.", this);
                yield break;
            }

            if (_isFirstSpawn)
            {
                yield return RunFirstSpawnPanicEvent();
                _isFirstSpawn = false;
            }
            else
            {
                yield return RunCalmOnlyPanicEvent(false);
            }

            float waitMin = Mathf.Min(respawnDelayMin, respawnDelayMax);
            float waitMax = Mathf.Max(respawnDelayMin, respawnDelayMax);
            float wait = waitMax > waitMin ? Random.Range(waitMin, waitMax) : waitMin;
            if (wait > 0.01f)
                yield return new WaitForSeconds(wait);
        }
    }

    private IEnumerator RunFirstSpawnPanicEvent()
    {
        while (true)
        {
            if (!TrySpawnPatient(true))
                yield break;

            _findPhaseActive = true;
            _calmInteraction.SetInteractionEnabled(false);

            DepressionCountdownUI.StartCountdown(
                findTimeLimitSeconds,
                findTimerLocalizationKey,
                findTimerFallbackText,
                timerStyleReference,
                OnFindTimerExpired);

            while (_findPhaseActive && _eventActive)
            {
                if (IsPlayerNearSpawnedPatient())
                    BeginCalmPhaseAfterFind();
                yield return null;
            }

            if (!_eventActive)
            {
                yield return CleanupSpawnedCharacter();
                continue;
            }

            while (_eventActive)
                yield return null;

            yield return CleanupSpawnedCharacter();
            yield break;
        }
    }

    private IEnumerator RunCalmOnlyPanicEvent(bool useFixedSpawn)
    {
        if (!TrySpawnPatient(useFixedSpawn))
            yield break;

        BeginCalmPhaseAfterFind();

        while (_eventActive)
            yield return null;

        yield return CleanupSpawnedCharacter();
    }

    private bool TrySpawnPatient(bool useFixedSpawn)
    {
        if (!TryGetSpawnPose(useFixedSpawn, out Vector3 position, out Quaternion rotation))
        {
            _eventActive = false;
            return false;
        }

        _spawnedCharacter = Instantiate(panicCharacterPrefab, position, rotation);
        _eventActive = true;

        if (!_spawnedCharacter.TryGetComponent(out _calmInteraction))
            _calmInteraction = _spawnedCharacter.AddComponent<DepressionCalmInteraction>();

        _calmInteraction.Bind(this);
        PlayOneShot(panicAppearSound);
        return true;
    }

    private void BeginCalmPhaseAfterFind()
    {
        if (!_findPhaseActive && _calmInteraction != null && _calmInteraction.IsCalmInteractionEnabled)
            return;

        _findPhaseActive = false;
        DepressionCountdownUI.StopCountdown();

        if (_calmInteraction != null)
            _calmInteraction.SetInteractionEnabled(true);

        DepressionCountdownUI.StartCountdown(
            calmTimeLimitSeconds,
            timerLocalizationKey,
            timerFallbackText,
            timerStyleReference,
            OnCalmTimerExpired);
    }

    private bool IsPlayerNearSpawnedPatient()
    {
        if (_spawnedCharacter == null)
            return false;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

        Vector3 patientPos = _spawnedCharacter.transform.position;
        Vector3 playerPos = body.position;
        playerPos.y = patientPos.y;
        float maxDist = Mathf.Max(0.5f, findProximityDistance);
        return (playerPos - patientPos).sqrMagnitude <= maxDist * maxDist;
    }

    public void TryCalm()
    {
        if (!_eventActive || _findPhaseActive)
            return;

        _eventActive = false;

        DepressionCountdownUI.StopCountdown();
        PlayOneShot(calmSuccessSound);

        if (_calmInteraction != null)
            _calmInteraction.SetInteractionEnabled(false);
    }

    private void OnFindTimerExpired()
    {
        if (!_eventActive || !_findPhaseActive)
            return;

        _findPhaseActive = false;
        _eventActive = false;

        DepressionCountdownUI.StopCountdown();
        ApplyFindFailDamage();
        PlayOneShot(calmFailSound);

        if (_calmInteraction != null)
            _calmInteraction.SetInteractionEnabled(false);
    }

    private void OnCalmTimerExpired()
    {
        if (!_eventActive)
            return;

        _eventActive = false;
        PlayOneShot(calmFailSound);

        if (_calmInteraction != null)
            _calmInteraction.SetInteractionEnabled(false);
    }

    private void ApplyFindFailDamage()
    {
        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return;

        if (!body.TryGetComponent(out PlayerHealth health))
            health = body.GetComponentInParent<PlayerHealth>();

        if (health != null && !health.IsDead)
            health.TakeDamage(findFailDamage);
    }

    private IEnumerator CleanupSpawnedCharacter()
    {
        float delay = Mathf.Max(0f, cleanupDelay);
        if (delay > 0.001f)
            yield return new WaitForSeconds(delay);

        if (_spawnedCharacter != null)
        {
            Destroy(_spawnedCharacter);
            _spawnedCharacter = null;
            _calmInteraction = null;
        }

        _findPhaseActive = false;
    }

    private bool TryGetSpawnPose(bool useFixedSpawn, out Vector3 position, out Quaternion rotation)
    {
        rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        if (useFixedSpawn || spawnArea == null)
        {
            position = spawnPoint != null ? spawnPoint.position : transform.position;
            position.y += spawnHeightOffset;
            return true;
        }

        Collider area = ResolveSpawnArea();
        if (area != null && TryGetRandomPointInBounds(area.bounds, out position))
        {
            position.y += spawnHeightOffset;
            return true;
        }

        position = spawnPoint != null ? spawnPoint.position : transform.position;
        position.y += spawnHeightOffset;
        return true;
    }

    private Collider ResolveSpawnArea()
    {
        if (spawnArea != null)
            return spawnArea;

        if (spawnPoint != null && spawnPoint.TryGetComponent(out Collider onPoint) && onPoint != GetComponent<Collider>())
            return onPoint;

        return GetComponent<Collider>();
    }

    private static bool TryGetRandomPointInBounds(Bounds bounds, out Vector3 point)
    {
        if (bounds.size.sqrMagnitude < 0.0001f)
        {
            point = bounds.center;
            return false;
        }

        point = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.center.y,
            Random.Range(bounds.min.z, bounds.max.z));

        return true;
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, soundVolume);
    }

    private void EnsureTriggerRigidbody()
    {
        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        return other.transform.root.CompareTag("Player");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (spawnPoint != null)
        {
            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.95f);
            Gizmos.DrawWireSphere(spawnPoint.position, 0.5f);
        }

        Collider area = ResolveSpawnArea();
        if (area != null)
        {
            Gizmos.color = new Color(0.85f, 0.35f, 0.9f, 0.35f);
            Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
        }
    }
#endif
}
