using System.Collections;
using System.Collections.Generic;
using ElmanGameDevTools.PlayerSystem;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Игровой автомат: зона притяжения, удержание игрока, мини-игра с последовательностью букв A–Z.
/// </summary>
public class SlotMachineController : MonoBehaviour
{
    private const string HintKey = "slotmachine.hint";
    private const string HintFallbackRu = "Нажмите показанную букву на клавиатуре";

    private static SlotMachineController s_activeTrap;

    public static bool IsPlayerTrapped => s_activeTrap != null;

    [Header("Ссылки (настройте в префабе)")]
    [Tooltip("Пустой объект: куда подводится игрок (перед экраном автомата).")]
    [SerializeField] private Transform playerStandPoint;
    [Tooltip("Точка «лица» автомата. Обычно пустой объект MachineFront на корне.")]
    [SerializeField] private Transform machineFront;

    [Header("Притяжение")]
    [SerializeField] private float pullSpeed = 4.5f;
    [Tooltip("Смещение точки взгляда от «лица» автомата по forward (метры).")]
    [SerializeField] private float lookTargetForwardOffset = 0.15f;
    [SerializeField] private float standArrivalDistance = 0.25f;
    [SerializeField] private float releasePushBackDistance = 0.6f;
    [Tooltip("Если до точки игрока дальше — притяжение не начнётся (защита от неверной точки).")]
    [SerializeField] private float maxLureDistance = 10f;

    [Header("Мини-игра")]
    [SerializeField] private int sequenceLength = 5;
    [SerializeField] private float secondsPerLetter = 4f;
    [SerializeField] private float healthDrainPerSecond = 10f;
    [SerializeField] private float wrongKeyDamage = 8f;
    [SerializeField] private float timeoutExtraDamage = 12f;
    [SerializeField] private float escapeCooldown = 7f;

    [Header("Звук")]
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioClip captureSound;
    [SerializeField, Range(0f, 1f)] private float captureSoundVolume = 0.7f;
    [SerializeField] private float captureFadeOutDuration = 1.5f;
    [SerializeField] private AudioClip correctKeySound;
    [SerializeField] private AudioClip wrongKeySound;
    [SerializeField] private AudioClip levelCompletedSound;
    [SerializeField] private AudioClip deathByMachineSound;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;

    [Header("Ambient казино")]
    [Tooltip("Включить, если этот автомат находится в казино. При захвате запустит casinoAmbientSource.")]
    [SerializeField] private bool triggersCasinoAmbient = false;
    [Tooltip("AudioSource с фоновым звуком казино (CasinoBackGround). Назначьте в инспекторе.")]
    [SerializeField] private AudioSource casinoAmbientSource;

    private enum TrapState
    {
        Idle,
        Pulling,
        Trapped
    }

    private TrapState _state = TrapState.Idle;
    private AudioSource _captureSource;
    private Coroutine _captureFadeRoutine;
    private Transform _playerBody;
    private Transform _moveRoot;
    private CharacterController _characterController;
    private PlayerController _playerController;
    private PlayerHealth _playerHealth;
    private readonly List<char> _sequence = new List<char>();
    private int _sequenceIndex;
    private float _letterDeadline;
    private float _cooldownEndTime;
    private bool _inputBlocked;
    private Coroutine _routine;

    private void Awake()
    {
        EnsureRuntimeSetup();
        EnsureAudioSources();
    }

    private void OnDestroy()
    {
        if (s_activeTrap == this)
            ForceReleasePlayer();
    }

    public void TryStartLure(Transform playerRoot)
    {
        if (_state != TrapState.Idle || Time.time < _cooldownEndTime)
            return;

        if (s_activeTrap != null && s_activeTrap != this)
            return;

        if (!TryResolvePlayer(playerRoot))
            return;

        if (_playerHealth != null && _playerHealth.IsDead)
            return;

        if (playerStandPoint == null)
        {
            Debug.LogWarning($"SlotMachine '{name}': не задан Player Stand Point.", this);
            return;
        }

        float dist = Vector3.Distance(_playerBody.position, playerStandPoint.position);
        if (dist > maxLureDistance)
        {
            Debug.LogWarning(
                $"SlotMachine '{name}': точка игрока слишком далеко ({dist:F1} м). " +
                "Подвиньте объект PlayerStandPoint / PlayerPoint в префабе (дочерний объект у автомата).",
                this);
            return;
        }

        GameStatsTracker.Instance?.RecordSlotMachineCatch();
        s_activeTrap = this;
        StartCasinoAmbient();
        _routine = StartCoroutine(LureAndTrapRoutine());
    }

    private IEnumerator LureAndTrapRoutine()
    {
        _state = TrapState.Pulling;
        LockPlayer();
        ShowApproachOverlay();

        while (_state == TrapState.Pulling)
        {
            if (_playerBody == null || (_playerHealth != null && _playerHealth.IsDead))
            {
                if (_playerHealth != null && _playerHealth.IsDead)
                    PlaySfx(deathByMachineSound);
                ForceReleasePlayer();
                yield break;
            }

            UpdateExternalLook();

            Vector3 targetPos = GetStandWorldPosition();

            MovePlayerTowards(targetPos);

            if ((_playerBody.position - targetPos).sqrMagnitude <= standArrivalDistance * standArrivalDistance)
            {
                SnapPlayerToStand(targetPos);
                _state = TrapState.Trapped;
                PlayCaptureLoop();
            }

            yield return null;
        }

        BuildSequence();
        ShowCurrentLetter();

        while (_state == TrapState.Trapped)
        {
            if (_playerBody == null || (_playerHealth != null && _playerHealth.IsDead))
            {
                if (_playerHealth != null && _playerHealth.IsDead)
                    PlaySfx(deathByMachineSound);
                ForceReleasePlayer();
                yield break;
            }

            UpdateExternalLook();

            if (healthDrainPerSecond > 0f && _playerHealth != null)
                _playerHealth.TakeDamage(healthDrainPerSecond * Time.deltaTime);

            if (Time.time >= _letterDeadline)
                OnWrongInput(timeout: true);

            if (TryReadLetterKey(out char pressed))
            {
                if (pressed == _sequence[_sequenceIndex])
                    OnCorrectLetter();
                else
                    OnWrongInput(timeout: false);
            }

            yield return null;
        }
    }

    private Vector3 GetStandWorldPosition()
    {
        Vector3 target = playerStandPoint.position;
        target.y = _playerBody.position.y;
        return target;
    }

    private void MovePlayerTowards(Vector3 targetWorldPos)
    {
        Vector3 bodyPos = _playerBody.position;
        Vector3 nextBodyPos = Vector3.MoveTowards(bodyPos, targetWorldPos, pullSpeed * Time.deltaTime);
        Vector3 delta = nextBodyPos - bodyPos;

        if (_moveRoot != null)
            _moveRoot.position += delta;
    }

    private Vector3 GetMachineLookTarget()
    {
        Transform face = ResolveMachineFaceTransform();
        if (face != null)
        {
            Vector3 target = face.position;
            target.y += 0.05f;
            return target;
        }

        if (machineFront != null)
            return machineFront.position + Vector3.up * 0.05f;

        return transform.position + transform.forward * lookTargetForwardOffset;
    }

    private void UpdateExternalLook()
    {
        if (_playerController == null)
            return;

        _playerController.SetExternalLookAtWorldPoint(GetMachineLookTarget(), true);
    }

    private void SnapExternalLook()
    {
        if (_playerController == null)
            return;

        _playerController.enabled = true;
        _playerController.SnapLookAtWorldPoint(GetMachineLookTarget());
    }

    private void ShowApproachOverlay()
    {
        SlotMachineLetterUI.GetSharedOverlay().ShowApproaching(ResolveHintText());
    }

    private string ResolveHintText()
    {
        string hint = LocalizationManager.Instance != null
            ? LocalizationManager.Instance.T(HintKey)
            : HintFallbackRu;

        if (string.IsNullOrEmpty(hint) || hint == HintKey)
            return HintFallbackRu;

        return hint;
    }

    private Transform ResolveMachineFaceTransform()
    {
        if (machineFront == null)
            return null;

        if (machineFront.name == "MachineFront")
            return machineFront;

        Transform namedFront = machineFront.Find("MachineFront");
        if (namedFront != null)
            return namedFront;

        Transform underObject = machineFront.Find("Object/MachineFront");
        return underObject != null ? underObject : machineFront;
    }

    private void SnapPlayerToStand(Vector3 targetWorldPos)
    {
        Vector3 delta = targetWorldPos - _playerBody.position;
        if (_moveRoot != null)
            _moveRoot.position += delta;

        Physics.SyncTransforms();
        SnapExternalLook();
    }

    private void OnCorrectLetter()
    {
        PlaySfx(correctKeySound);
        SlotMachineLetterUI.GetSharedOverlay().ResetLetterColor();
        _sequenceIndex++;

        if (_sequenceIndex >= _sequence.Count)
        {
            ReleasePlayerEscaped();
            return;
        }

        ShowCurrentLetter();
    }

    private void OnWrongInput(bool timeout)
    {
        PlaySfx(wrongKeySound);

        if (_playerHealth != null)
            _playerHealth.TakeDamage(timeout ? timeoutExtraDamage : wrongKeyDamage);

        _sequenceIndex = 0;
        SlotMachineLetterUI.GetSharedOverlay().FlashWrong();
        ShowCurrentLetter();
    }

    private void ShowCurrentLetter()
    {
        if (_sequenceIndex < 0 || _sequenceIndex >= _sequence.Count)
            return;

        char letter = _sequence[_sequenceIndex];
        _letterDeadline = Time.time + Mathf.Max(0.5f, secondsPerLetter);

        SlotMachineLetterUI.GetSharedOverlay().Show(letter, _sequenceIndex, _sequence.Count, ResolveHintText());
    }

    private void BuildSequence()
    {
        _sequence.Clear();
        int count = Mathf.Clamp(sequenceLength, 2, 12);

        for (int i = 0; i < count; i++)
        {
            char next;
            do
            {
                next = (char)('A' + Random.Range(0, 26));
            } while (i > 0 && next == _sequence[i - 1]);

            _sequence.Add(next);
        }

        _sequenceIndex = 0;
    }

    private static bool TryReadLetterKey(out char letter)
    {
        letter = default;

        for (int i = 0; i < 26; i++)
        {
            KeyCode key = KeyCode.A + i;
            if (Input.GetKeyDown(key))
            {
                letter = (char)('A' + i);
                return true;
            }
        }

        return false;
    }

    private void ReleasePlayerEscaped()
    {
        PlaySfx(levelCompletedSound);
        SlotMachineLetterUI.GetSharedOverlay().Hide();
        _cooldownEndTime = Time.time + escapeCooldown;
        NudgePlayerAwayFromMachine();
        ForceReleasePlayer();
    }

    private void NudgePlayerAwayFromMachine()
    {
        if (_moveRoot == null || playerStandPoint == null)
            return;

        Transform face = ResolveMachineFaceTransform();
        if (face == null)
            return;

        Vector3 away = playerStandPoint.position - face.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.001f)
            return;

        away.Normalize();
        _moveRoot.position += away * releasePushBackDistance;
        Physics.SyncTransforms();
    }

    private void ForceReleasePlayer()
    {
        if (_routine != null)
        {
            StopCoroutine(_routine);
            _routine = null;
        }

        _state = TrapState.Idle;
        SlotMachineLetterUI.GetSharedOverlay().Hide();

        if (s_activeTrap == this)
            s_activeTrap = null;

        StopCaptureSound();
        StopCasinoAmbient();
        UnlockPlayer();
    }

    private void StartCasinoAmbient()
    {
        if (!triggersCasinoAmbient || casinoAmbientSource == null)
            return;

        if (!casinoAmbientSource.isPlaying)
            casinoAmbientSource.Play();
    }

    private void PlayCaptureLoop()
    {
        if (captureSound == null || _captureSource == null)
            return;

        if (_captureFadeRoutine != null)
        {
            StopCoroutine(_captureFadeRoutine);
            _captureFadeRoutine = null;
        }

        float sfx = SettingsManager.Instance != null
            ? Mathf.Clamp01(SettingsManager.Instance.GetCurrentSettings().sfxVolume) : 1f;

        _captureSource.clip = captureSound;
        _captureSource.volume = captureSoundVolume * sfx;
        _captureSource.Play();
    }

    private void StopCaptureSound()
    {
        if (_captureSource == null || !_captureSource.isPlaying)
            return;

        if (_captureFadeRoutine != null)
            StopCoroutine(_captureFadeRoutine);

        if (gameObject.activeInHierarchy)
            _captureFadeRoutine = StartCoroutine(FadeCaptureOut());
        else
            _captureSource.Stop();
    }

    private System.Collections.IEnumerator FadeCaptureOut()
    {
        float startVol = _captureSource.volume;
        float duration = Mathf.Max(0.05f, captureFadeOutDuration);

        for (float t = 0f; t < duration; t += Time.deltaTime)
        {
            if (_captureSource == null) yield break;
            _captureSource.volume = Mathf.Lerp(startVol, 0f, t / duration);
            yield return null;
        }

        if (_captureSource != null)
        {
            _captureSource.Stop();
            _captureSource.volume = startVol;
        }
        _captureFadeRoutine = null;
    }

    private void StopCasinoAmbient()
    {
        if (!triggersCasinoAmbient || casinoAmbientSource == null)
            return;

        casinoAmbientSource.Stop();
    }

    private void LockPlayer()
    {
        if (_inputBlocked)
            return;

        _inputBlocked = true;
        GameplayInputBlocker.SetBlocked(true);

        if (_characterController != null)
            _characterController.enabled = false;

        if (_playerHealth != null)
            _playerHealth.SetSuppressDamageFeedback(true);

        DisableWeaponHandler(false);
        SnapExternalLook();
    }

    private void UnlockPlayer()
    {
        if (!_inputBlocked)
            return;

        if (_playerController != null)
            _playerController.SetExternalLookAtWorldPoint(Vector3.zero, false);

        _inputBlocked = false;
        GameplayInputBlocker.SetBlocked(false);
        GameplayInputBlocker.LockCursorForGameplay();

        if (_playerHealth != null)
            _playerHealth.SetSuppressDamageFeedback(false);

        if (_characterController != null)
        {
            _characterController.enabled = true;
            _characterController.Move(Vector3.zero);
        }

        DisableWeaponHandler(true);
        Physics.SyncTransforms();
    }

    private void DisableWeaponHandler(bool enabled)
    {
        if (_playerBody == null)
            return;

        MonoBehaviour[] behaviours = _playerBody.GetComponentsInChildren<MonoBehaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            MonoBehaviour behaviour = behaviours[i];
            if (behaviour != null && behaviour.GetType().Name == "WeaponHandler")
                behaviour.enabled = enabled;
        }
    }

    private bool TryResolvePlayer(Transform playerRoot)
    {
        _playerBody = null;
        _moveRoot = null;
        _characterController = null;
        _playerController = null;
        _playerHealth = null;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
        {
            if (playerRoot == null)
                return false;

            Transform root = playerRoot.root;
            if (root.TryGetComponent(out CharacterController cc))
                body = cc.transform;
            else
                body = root;
        }

        _playerBody = body;
        _characterController = body.GetComponent<CharacterController>();
        _playerController = body.GetComponent<PlayerController>();
        if (_playerController == null)
            _playerController = body.GetComponentInParent<PlayerController>();

        _moveRoot = _playerController != null ? _playerController.transform : body;

        _playerHealth = body.GetComponentInParent<PlayerHealth>();
        if (_playerHealth == null)
            _playerHealth = body.GetComponentInChildren<PlayerHealth>(true);

        return _playerBody != null && _moveRoot != null;
    }

    private void EnsureRuntimeSetup()
    {
        if (machineFront == null || machineFront.name == "Object")
            machineFront = ResolveMachineFaceTransform() ?? transform.Find("Object/MachineFront");

        if (playerStandPoint == null || playerStandPoint.name == "PlayerPoint")
            playerStandPoint = ResolvePlayerStandTransform();

        if (playerStandPoint == null)
            playerStandPoint = CreateAutoStandPoint();
    }

    private Transform ResolvePlayerStandTransform()
    {
        Transform stand = transform.Find("Object/PlayerStandPoint");
        if (stand == null)
            stand = transform.Find("PlayerStandPoint");
        if (stand == null)
            stand = transform.Find("Object/PlayerPoint");
        if (stand == null)
            stand = transform.Find("PlayerPoint");
        return stand;
    }

    private void EnsureAudioSources()
    {
        EnsureDedicatedSfxSource();
        EnsureCaptureSource();
    }

    private void EnsureCaptureSource()
    {
        _captureSource = gameObject.AddComponent<AudioSource>();
        _captureSource.playOnAwake = false;
        _captureSource.loop = true;
        _captureSource.spatialBlend = 0f;
        AudioMixerRoutingUtility.BindSourceToSfx(_captureSource);
    }

    private void EnsureDedicatedSfxSource()
    {
        if (sfxSource != null)
        {
            ConfigureSfxSource(sfxSource);
            return;
        }

        if (TryGetComponent(out AudioSource onRoot))
            sfxSource = onRoot;
        else
            sfxSource = gameObject.AddComponent<AudioSource>();

        ConfigureSfxSource(sfxSource);
    }

    private void ConfigureSfxSource(AudioSource source)
    {
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        AudioMixerRoutingUtility.BindSourceToSfx(source);
    }

    private float ResolveSfxVolumeScale()
    {
        if (SettingsManager.Instance == null)
            return sfxVolume;

        return sfxVolume * Mathf.Clamp01(SettingsManager.Instance.GetCurrentSettings().sfxVolume);
    }

    private void PlaySfx(AudioClip clip)
    {
        if (clip == null || sfxSource == null)
            return;

        sfxSource.PlayOneShot(clip, ResolveSfxVolumeScale());
    }

    private Transform CreateAutoStandPoint()
    {
        Transform stand = EnsureChild("PlayerStandPoint");
        stand.SetParent(machineFront, false);
        stand.localPosition = new Vector3(0f, 0f, 1.35f);
        stand.localRotation = Quaternion.LookRotation(-Vector3.forward, Vector3.up);
        return stand;
    }

    private Transform EnsureChild(string childName)
    {
        Transform existing = transform.Find(childName);
        if (existing != null)
            return existing;

        GameObject go = new GameObject(childName);
        go.transform.SetParent(transform, false);
        return go.transform;
    }

#if UNITY_EDITOR
    private void Reset()
    {
        ApplyEditorPrefabLayout();
    }

    /// <summary>Создаёт якоря LureZone / MachineFront / PlayerStandPoint в префабе.</summary>
    public void ApplyEditorPrefabLayout()
    {
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;
        transform.localScale = Vector3.one;

        Transform objectRoot = transform.Find("Object");
        machineFront = transform.Find("Object/MachineFront");
        if (machineFront == null)
            machineFront = transform.Find("MachineFront");
        if (machineFront == null)
        {
            machineFront = EnsureEditorChild("MachineFront", objectRoot != null ? objectRoot : transform);
            if (objectRoot != null)
            {
                machineFront.SetParent(objectRoot, false);
                machineFront.localPosition = new Vector3(0f, 1.1f, 0.4f);
                machineFront.localRotation = Quaternion.identity;
            }
        }

        playerStandPoint = transform.Find("Object/PlayerStandPoint");
        if (playerStandPoint == null)
            playerStandPoint = transform.Find("PlayerStandPoint");
        if (playerStandPoint == null)
        {
            playerStandPoint = EnsureEditorChild("PlayerStandPoint", objectRoot != null ? objectRoot : machineFront);
            playerStandPoint.localPosition = new Vector3(0f, 0f, 1.35f);
            playerStandPoint.localRotation = Quaternion.identity;
        }

        SlotMachineLetterUI uiComponent = GetComponent<SlotMachineLetterUI>();
        if (uiComponent != null)
            DestroyImmediate(uiComponent);

        Transform ui = transform.Find("UI");
        if (ui != null)
            DestroyImmediate(ui.gameObject);

        SerializedObject so = new SerializedObject(this);
        so.FindProperty("machineFront").objectReferenceValue = machineFront;
        so.FindProperty("playerStandPoint").objectReferenceValue = playerStandPoint;
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static Transform EnsureEditorChild(string childName, Transform parent)
    {
        Transform existing = parent.Find(childName);
        if (existing != null)
            return existing;

        GameObject go = new GameObject(childName);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private void OnDrawGizmosSelected()
    {
        EnsureRuntimeSetup();

        if (playerStandPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(playerStandPoint.position, 0.2f);
            if (machineFront != null)
                Gizmos.DrawLine(machineFront.position, playerStandPoint.position);
        }

    }
#endif
}
