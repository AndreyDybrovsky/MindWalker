using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

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
    [Tooltip("Одна зона спавна (устаревшее). Если заданы Spawn Areas или Spawn Area Roots — не используется.")]
    [SerializeField] private Collider spawnArea;
    [Tooltip("Несколько коллайдеров: пациент появится в случайной точке одной из зон (равный шанс на зону).")]
    [SerializeField] private Collider[] spawnAreas;
    [Tooltip("Родители с дочерними BoxCollider — все коллайдеры на них и детях участвуют в спавне.")]
    [SerializeField] private Transform[] spawnAreaRoots;
    [SerializeField] private float spawnHeightOffset = 0f;
    [Tooltip("Слои пола/террейна для raycast при спавне. Триггеры зон спавна игнорируются.")]
    [SerializeField] private LayerMask spawnGroundLayers = ~0;
    [SerializeField] private float spawnGroundRaycastUp = 16f;
    [SerializeField] private float spawnGroundRaycastDown = 48f;
    [SerializeField] private GameObject panicCharacterPrefab;

    [Header("Свет-указатель пациентки")]
    [SerializeField] private bool pulsePatientGuideLight = true;

    [Header("Первый спавн — поиск")]
    [SerializeField] private float findTimeLimitSeconds = 60f;
    [SerializeField] private float findFailDamage = 25f;
    [Tooltip("Урон при провале таймера успокоения (E). Если 0 — используется Find Fail Damage.")]
    [SerializeField] private float calmFailDamage;
    [Tooltip("Дистанция до пациента, при которой считается, что герой его нашёл.")]
    [SerializeField] private float findProximityDistance = 10f;
    [Tooltip("Максимальная разница по высоте (м) между игроком и пациентом, чтобы считалось что игрок его нашёл (для этажей/перепадов).")]
    [SerializeField] private float findMaxVerticalDifference = 2.2f;
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
    [SerializeField] private float panicAppearFadeOutDuration = 1.6f;
    [Tooltip("3D-звук плача у пациентки: ближе — громче, дальше — тише.")]
    [SerializeField] private float patientSoundMinDistance = 2f;
    [SerializeField] private float patientSoundMaxDistance = 24f;

    [Header("Поведение")]
    [SerializeField] private float cleanupDelay = 0.5f;
    [Tooltip("Задержка перед первым (туториальным) появлением девочки.")]
    [SerializeField] private float firstSpawnDelay = 1.5f;
    [Tooltip("Уникальный id зоны в сцене (для сохранения).")]
    [SerializeField] private string zoneSaveId = "main";

    private bool _zoneLoopRunning;
    private bool _eventActive;
    private bool _findPhaseActive;
    private bool _isFirstSpawn = true;
    private GameObject _spawnedCharacter;
    private DepressionCalmInteraction _calmInteraction;
    private PanicPatientAmbientAudio _patientAudio;
    private Coroutine _loopRoutine;
    private float _respawnWaitRemaining;
    private bool _restoreApplied;
    private readonly List<Collider> _spawnColliderBuffer = new();

    private const int PhaseIdle = 0;
    private const int PhaseFind = 1;
    private const int PhaseCalm = 2;

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

    public static void CaptureAllToSave(GameSaveData saveData)
    {
        if (saveData == null)
            return;

        DepressionPanicMomentZone[] zones = Object.FindObjectsByType<DepressionPanicMomentZone>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null)
                zones[i].CaptureToSave(saveData);
        }
    }

    public static void RestoreAllFromSave(GameSaveData saveData)
    {
        if (saveData == null)
            return;

        DepressionPanicMomentZone[] zones = Object.FindObjectsByType<DepressionPanicMomentZone>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < zones.Length; i++)
        {
            if (zones[i] != null)
                zones[i].ApplySaveState(saveData);
        }
    }

    private string BuildSaveKey()
    {
        string scene = SceneManager.GetActiveScene().name;
        string id = string.IsNullOrEmpty(zoneSaveId) ? name : zoneSaveId;
        return $"depression_panic:{scene}:{id}";
    }

    /// <summary>
    /// Проверяет в текущем слоте сохранения, было ли первое появление уже завершено.
    /// Позволяет пропустить туториал при повторном посещении сцены без явной загрузки сейва.
    /// </summary>
    private bool IsFirstEncounterCompletedInSave()
    {
        if (SaveManager.Instance == null)
            return false;

        int slot = SaveManager.Instance.GetCurrentSaveSlot();
        if (slot < 0)
            return false;

        GameSaveData data = SaveManager.Instance.GetSaveData(slot);
        if (data == null || data.IsEmpty())
            return false;

        if (!SaveGameCustomDataUtility.TryRead(data, BuildSaveKey(), out DepressionPanicSaveData panicData))
            return false;

        return panicData.firstEncounterCompleted;
    }

    public void CaptureToSave(GameSaveData saveData)
    {
        var data = new DepressionPanicSaveData
        {
            firstEncounterCompleted = !_isFirstSpawn,
            zoneLoopActive = _zoneLoopRunning,
            patientActive = _eventActive && _spawnedCharacter != null,
            phase = ResolvePhaseForSave(),
            waitingRespawn = _respawnWaitRemaining > 0.01f && !_eventActive,
            respawnWaitRemaining = Mathf.Max(0f, _respawnWaitRemaining)
        };

        if (data.patientActive && _spawnedCharacter != null)
        {
            data.patientPosition = _spawnedCharacter.transform.position;
            data.patientRotation = _spawnedCharacter.transform.rotation;
        }

        if (DepressionCountdownUI.TryCaptureActive(
                out float remaining,
                out string key,
                out string fallback))
        {
            data.countdownActive = true;
            data.countdownRemaining = remaining;
            data.countdownLocalizationKey = key;
            data.countdownFallback = fallback;
        }

        SaveGameCustomDataUtility.Write(saveData, BuildSaveKey(), data);
    }

    private int ResolvePhaseForSave()
    {
        if (!_eventActive || _spawnedCharacter == null)
            return PhaseIdle;

        return _findPhaseActive ? PhaseFind : PhaseCalm;
    }

    private void ApplySaveState(GameSaveData saveData)
    {
        if (_restoreApplied || saveData == null)
            return;

        if (!SaveGameCustomDataUtility.TryRead(saveData, BuildSaveKey(), out DepressionPanicSaveData data))
            return;

        _restoreApplied = true;
        _isFirstSpawn = !data.firstEncounterCompleted;

        if (data.patientActive)
        {
            if (data.firstEncounterCompleted)
                RestorePatientAfterFirstEncounter(data);
            else
                RestoreActivePatient(data);
        }

        if (data.countdownActive && data.countdownRemaining > 0.01f && _spawnedCharacter != null)
            ResumeCountdownFromSave(data);

        if (!data.zoneLoopActive || !IsPlayerInsideZone())
            return;

        _zoneLoopRunning = true;
        if (_loopRoutine != null)
            StopCoroutine(_loopRoutine);

        if (data.waitingRespawn && data.respawnWaitRemaining > 0.01f && !_eventActive)
            _loopRoutine = StartCoroutine(ZonePanicLoopFromRespawnWait(data.respawnWaitRemaining));
        else if (!_eventActive)
            _loopRoutine = StartCoroutine(ZonePanicLoop());
    }

    private void RestorePatientAfterFirstEncounter(DepressionPanicSaveData data)
    {
        _isFirstSpawn = false;

        if (!TrySpawnPatient(false))
            return;

        _findPhaseActive = false;
        if (_calmInteraction != null)
            _calmInteraction.SetInteractionEnabled(data.phase == PhaseCalm);

        if (data.phase == PhaseCalm)
            BeginCalmPhaseAfterFind();
    }

    private void RestoreActivePatient(DepressionPanicSaveData data)
    {
        if (panicCharacterPrefab == null)
            return;

        _spawnedCharacter = Instantiate(panicCharacterPrefab, data.patientPosition, data.patientRotation);
        if (_spawnedCharacter == null)
            return;

        AlignCharacterFeetToGround(_spawnedCharacter);
        SetupPatientGuideLight(_spawnedCharacter);
        _eventActive = true;
        _findPhaseActive = data.phase == PhaseFind;

        GameObject interactionGo = _spawnedCharacter;
        Collider childTrigger = _spawnedCharacter.GetComponentInChildren<Collider>(true);
        if (childTrigger != null && childTrigger.isTrigger)
            interactionGo = childTrigger.gameObject;

        if (!interactionGo.TryGetComponent(out _calmInteraction))
            _calmInteraction = interactionGo.AddComponent<DepressionCalmInteraction>();

        _calmInteraction.Bind(this);
        _calmInteraction.SetInteractionEnabled(data.phase == PhaseCalm);
        StartPatientPanicAudio();
    }

    private void ResumeCountdownFromSave(DepressionPanicSaveData data)
    {
        System.Action onExpired = data.phase == PhaseFind
            ? OnFindTimerExpired
            : OnCalmTimerExpired;

        string key = string.IsNullOrEmpty(data.countdownLocalizationKey)
            ? (data.phase == PhaseFind ? findTimerLocalizationKey : timerLocalizationKey)
            : data.countdownLocalizationKey;

        string fallback = string.IsNullOrEmpty(data.countdownFallback)
            ? (data.phase == PhaseFind ? findTimerFallbackText : timerFallbackText)
            : data.countdownFallback;

        DepressionCountdownUI.ResumeCountdown(
            data.countdownRemaining,
            key,
            fallback,
            timerStyleReference,
            onExpired);
    }

    private IEnumerator ZonePanicLoopFromRespawnWait(float waitSeconds)
    {
        _zoneLoopRunning = true;
        _respawnWaitRemaining = Mathf.Max(0f, waitSeconds);

        while (_respawnWaitRemaining > 0.01f)
        {
            _respawnWaitRemaining -= Time.deltaTime;
            yield return null;
        }

        _respawnWaitRemaining = 0f;
        yield return ZonePanicLoop();
    }

    private bool IsPlayerInsideZone()
    {
        Collider zone = GetComponent<Collider>();
        if (zone == null || !zone.isTrigger)
            return false;

        Bounds bounds = zone.bounds;
        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

        return bounds.Contains(body.position);
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

            // Если туториальное появление уже было завершено в сохранении этого слота —
            // не показываем его снова даже при навигации в сцену без явной загрузки сейва
            if (_isFirstSpawn && IsFirstEncounterCompletedInSave())
                _isFirstSpawn = false;

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
            _respawnWaitRemaining = wait;
            if (wait > 0.01f)
            {
                yield return new WaitForSeconds(wait);
                _respawnWaitRemaining = 0f;
            }
        }
    }

    private IEnumerator RunFirstSpawnPanicEvent()
    {
        // Небольшая задержка чтобы игрок успел осмотреться перед туториальным появлением
        if (firstSpawnDelay > 0f)
            yield return new WaitForSeconds(firstSpawnDelay);

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
        if (_spawnedCharacter == null)
        {
            Debug.LogWarning($"DepressionPanicMomentZone '{name}': Instantiate вернул null (panicCharacterPrefab).", this);
            _eventActive = false;
            return false;
        }

        AlignCharacterFeetToGround(_spawnedCharacter);
        SetupPatientGuideLight(_spawnedCharacter);
        _eventActive = true;

        // Важно: PlayerInteractionZone работает через Collider на ТОМ ЖЕ объекте.
        // Поэтому если у модели триггер находится на дочернем объекте (например, "Trigger"),
        // ставим DepressionCalmInteraction именно туда.
        GameObject interactionGo = _spawnedCharacter;
        Collider childTrigger = _spawnedCharacter.GetComponentInChildren<Collider>(true);
        if (childTrigger != null && childTrigger.isTrigger)
            interactionGo = childTrigger.gameObject;

        if (!interactionGo.TryGetComponent(out _calmInteraction))
            _calmInteraction = interactionGo.AddComponent<DepressionCalmInteraction>();

        if (_calmInteraction == null)
        {
            Debug.LogWarning($"DepressionPanicMomentZone '{name}': не удалось получить/добавить DepressionCalmInteraction.", this);
            _eventActive = false;
            return false;
        }

        _calmInteraction.Bind(this);
        StartPatientPanicAudio();
        return true;
    }

    private void StartPatientPanicAudio()
    {
        _patientAudio = _spawnedCharacter.GetComponent<PanicPatientAmbientAudio>();
        if (_patientAudio == null)
            _patientAudio = _spawnedCharacter.AddComponent<PanicPatientAmbientAudio>();

        _patientAudio.ApplySpatialSettings(patientSoundMinDistance, patientSoundMaxDistance);
        _patientAudio.Play(panicAppearSound, soundVolume);
    }

    private void FadeOutPatientPanicAudio()
    {
        if (_patientAudio != null)
            _patientAudio.FadeOut(panicAppearFadeOutDuration);
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
        float vertical = Mathf.Abs(playerPos.y - patientPos.y);
        if (vertical > Mathf.Max(0.1f, findMaxVerticalDifference))
            return false;

        // Горизонтальную дистанцию считаем в XZ — так на склонах/лестницах работает адекватнее.
        playerPos.y = patientPos.y;
        float maxDist = Mathf.Max(0.5f, findProximityDistance);
        return (playerPos - patientPos).sqrMagnitude <= maxDist * maxDist;
    }

    public void TryCalm()
    {
        if (!_eventActive || _findPhaseActive)
            return;

        _eventActive = false;
        GameStatsTracker.Instance?.RecordPatientCalmed();

        DepressionCountdownUI.StopCountdown();
        FadeOutPatientPanicAudio();
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
        ApplyFailDamage(findFailDamage);
        FadeOutPatientPanicAudio();
        PlayOneShot(calmFailSound);

        if (_calmInteraction != null)
            _calmInteraction.SetInteractionEnabled(false);
    }

    private void OnCalmTimerExpired()
    {
        if (!_eventActive)
            return;

        _eventActive = false;
        FadeOutPatientPanicAudio();
        float damage = calmFailDamage > 0f ? calmFailDamage : findFailDamage;
        ApplyFailDamage(damage);
        PlayOneShot(calmFailSound);

        if (_calmInteraction != null)
            _calmInteraction.SetInteractionEnabled(false);
    }

    private void ApplyFailDamage(float damage)
    {
        if (damage <= 0f)
            return;

        if (!TryResolvePlayerHealth(out PlayerHealth health))
        {
            Debug.LogWarning($"DepressionPanicMomentZone '{name}': не найден PlayerHealth для урона {damage}.", this);
            return;
        }

        if (!health.IsDead)
            health.TakeDamage(damage);
    }

    private static bool TryResolvePlayerHealth(out PlayerHealth health)
    {
        health = null;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

        if (body.TryGetComponent(out health))
            return true;

        health = body.GetComponentInParent<PlayerHealth>();
        if (health != null)
            return true;

        health = body.GetComponentInChildren<PlayerHealth>(true);
        if (health != null)
            return true;

        return body.root.TryGetComponent(out health);
    }

    private IEnumerator CleanupSpawnedCharacter()
    {
        if (_patientAudio != null && (_patientAudio.IsPlaying || _patientAudio.IsFading))
        {
            if (!_patientAudio.IsFading)
                _patientAudio.FadeOut(panicAppearFadeOutDuration);

            yield return new WaitForSeconds(panicAppearFadeOutDuration);
        }

        float delay = Mathf.Max(0f, cleanupDelay);
        if (delay > 0.001f)
            yield return new WaitForSeconds(delay);

        if (_spawnedCharacter != null)
        {
            Destroy(_spawnedCharacter);
            _spawnedCharacter = null;
            _calmInteraction = null;
            _patientAudio = null;
        }

        _findPhaseActive = false;
    }

    private bool TryGetSpawnPose(bool useFixedSpawn, out Vector3 position, out Quaternion rotation)
    {
        rotation = spawnPoint != null ? spawnPoint.rotation : transform.rotation;

        if (useFixedSpawn)
            position = spawnPoint != null ? spawnPoint.position : transform.position;
        else if (!TryGetRandomSpawnPoint(out position))
            position = spawnPoint != null ? spawnPoint.position : transform.position;

        TrySnapSpawnPositionToGround(ref position);
        return true;
    }

    private void AlignCharacterFeetToGround(GameObject patient)
    {
        if (patient == null)
            return;

        Vector3 probe = patient.transform.position;
        if (!TryGetGroundPoint(probe, out Vector3 groundPoint))
            return;

        if (TryGetWorldBounds(patient, out Bounds bounds))
        {
            float pivotToFeet = patient.transform.position.y - bounds.min.y;
            Vector3 pos = patient.transform.position;
            pos.y = groundPoint.y + pivotToFeet + spawnHeightOffset;
            patient.transform.position = pos;
            return;
        }

        Vector3 fallback = patient.transform.position;
        fallback.y = groundPoint.y + spawnHeightOffset;
        patient.transform.position = fallback;
    }

    private bool TrySnapSpawnPositionToGround(ref Vector3 position) =>
        TryGetGroundPoint(position, out position);

    private bool TryGetGroundPoint(Vector3 near, out Vector3 groundPoint)
    {
        float up = Mathf.Max(1f, spawnGroundRaycastUp);
        float down = Mathf.Max(up + 1f, spawnGroundRaycastDown);

        Vector3 start = new Vector3(near.x, near.y + up, near.z);
        if (Physics.Raycast(start, Vector3.down, out RaycastHit hit, up + down, spawnGroundLayers,
                QueryTriggerInteraction.Ignore))
        {
            groundPoint = hit.point;
            return true;
        }

        start = new Vector3(near.x, near.y + 40f, near.z);
        if (Physics.Raycast(start, Vector3.down, out hit, 80f, spawnGroundLayers, QueryTriggerInteraction.Ignore))
        {
            groundPoint = hit.point;
            return true;
        }

        groundPoint = near;
        return false;
    }

    private static bool TryGetWorldBounds(GameObject root, out Bounds bounds)
    {
        bounds = default;
        if (root == null)
            return false;

        Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null)
                continue;

            if (!hasBounds)
            {
                bounds = r.bounds;
                hasBounds = true;
            }
            else
                bounds.Encapsulate(r.bounds);
        }

        if (hasBounds)
            return true;

        Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < colliders.Length; i++)
        {
            Collider c = colliders[i];
            if (c == null || c.isTrigger)
                continue;

            if (!hasBounds)
            {
                bounds = c.bounds;
                hasBounds = true;
            }
            else
                bounds.Encapsulate(c.bounds);
        }

        return hasBounds;
    }

    private void SetupPatientGuideLight(GameObject patient)
    {
        if (!pulsePatientGuideLight || patient == null)
            return;

        Light light = patient.GetComponentInChildren<Light>(true);
        if (light == null)
            return;

        GameObject lightGo = light.gameObject;
        lightGo.SetActive(true);

        if (!lightGo.TryGetComponent(out OCDGuideLightPulse pulse))
            pulse = lightGo.AddComponent<OCDGuideLightPulse>();

        pulse.enabled = true;
    }

    private bool TryGetRandomSpawnPoint(out Vector3 position)
    {
        CollectSpawnColliders(_spawnColliderBuffer);
        if (_spawnColliderBuffer.Count == 0)
        {
            position = Vector3.zero;
            return false;
        }

        Collider area = _spawnColliderBuffer[Random.Range(0, _spawnColliderBuffer.Count)];
        return TryGetRandomPointInCollider(area, out position);
    }

    private void CollectSpawnColliders(List<Collider> buffer)
    {
        buffer.Clear();

        if (spawnAreas != null)
        {
            for (int i = 0; i < spawnAreas.Length; i++)
            {
                Collider c = spawnAreas[i];
                if (c != null && !IsZoneTriggerCollider(c))
                    buffer.Add(c);
            }
        }

        if (spawnArea != null && !IsZoneTriggerCollider(spawnArea) && !buffer.Contains(spawnArea))
            buffer.Add(spawnArea);

        if (spawnAreaRoots != null)
        {
            for (int i = 0; i < spawnAreaRoots.Length; i++)
            {
                Transform root = spawnAreaRoots[i];
                if (root == null)
                    continue;

                Collider[] children = root.GetComponentsInChildren<Collider>(true);
                for (int j = 0; j < children.Length; j++)
                {
                    Collider c = children[j];
                    if (c != null && !IsZoneTriggerCollider(c) && !buffer.Contains(c))
                        buffer.Add(c);
                }
            }
        }

        if (buffer.Count == 0 && spawnPoint != null &&
            spawnPoint.TryGetComponent(out Collider onPoint) && !IsZoneTriggerCollider(onPoint))
        {
            buffer.Add(onPoint);
        }
    }

    private bool IsZoneTriggerCollider(Collider col) => col != null && col == GetComponent<Collider>();

    private static bool TryGetRandomPointInCollider(Collider col, out Vector3 point)
    {
        point = Vector3.zero;
        if (col == null)
            return false;

        Bounds bounds = col.bounds;
        if (bounds.size.sqrMagnitude < 0.0001f)
            return false;

        point = new Vector3(
            Random.Range(bounds.min.x, bounds.max.x),
            bounds.max.y + 0.05f,
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

        _spawnColliderBuffer.Clear();
        CollectSpawnColliders(_spawnColliderBuffer);
        Gizmos.color = new Color(0.85f, 0.35f, 0.9f, 0.9f);
        for (int i = 0; i < _spawnColliderBuffer.Count; i++)
        {
            Collider area = _spawnColliderBuffer[i];
            if (area == null)
                continue;

            Gizmos.DrawWireCube(area.bounds.center, area.bounds.size);
        }
    }
#endif
}
