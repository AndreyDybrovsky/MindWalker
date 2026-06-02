using System.Collections;
using ElmanGameDevTools.PlayerSystem;
using TMPro;
using UnityEngine;

/// <summary>
/// Финальная встреча с боссом казино: камера вверх, реплики, выбор Да/Нет, опционально колесо, затемнение и бой.
/// </summary>
[RequireComponent(typeof(Collider))]
public class CasinoBossEncounterTrigger : MonoBehaviour
{
    [Header("Цели")]
    [SerializeField] private Transform bossLookTarget;
    [SerializeField] private Transform cameraFocusPoint;
    [SerializeField] private FortuneWheelController fortuneWheel;
    [SerializeField] private FortuneWheelSpinZone wheelSpinZone;
    [SerializeField] private BossSpawnManager bossSpawnManager;
    [SerializeField] private GameObject sceneBossProp;
    [SerializeField] private OCDCaptionUI captionUI;

    [Header("Камера")]
    [SerializeField] private float cameraRiseDuration = 1.8f;
    [SerializeField] private float cameraRiseHeight = 1.35f;
    [SerializeField] private float cameraPitchDown = 8f;

    [Header("Реплики — вступление")]
    [SerializeField] private string introLine1Key = "scene.gambling.boss_intro_1";
    [SerializeField] private string introLine1Fallback = "Так ты посмел сюда вернуться, и ради чего?";
    [SerializeField] private string introLine2Key = "scene.gambling.boss_intro_2";
    [SerializeField] private string introLine2Fallback = "Неужели хочешь проверить свою удачу в последний раз?";

    [Header("Реплики — отказ")]
    [SerializeField] private string refuseLine1Key = "scene.gambling.boss_refuse_1";
    [SerializeField] private string refuseLine1Fallback = "Неужели чему-то научился?";
    [SerializeField] private string refuseLine2Key = "scene.gambling.boss_refuse_2";
    [SerializeField] private string refuseLine2Fallback = "Но тебе всё равно целым отсюда не уйти";

    [Header("Тайминг текста")]
    [SerializeField] private float captionFadeIn = 0.4f;
    [SerializeField] private float captionHold = 3.2f;
    [SerializeField] private float captionFadeOut = 0.35f;

    [Header("Затемнение перед боем")]
    [SerializeField] private float fadeToBlackDuration = 1.4f;
    [SerializeField] private float fadeFromBlackDuration = 0.35f;
    [SerializeField] private float blackHoldDuration = 0.15f;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bossLaughSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    [Header("UI выбора")]
    [SerializeField] private CasinoChoicePresenter choicePresenter;

    private bool _played;
    private bool _sequenceRunning;
    private bool _spinFinished;
    private CanvasGroup _fadeCanvasGroup;
    private Coroutine _sequenceRoutine;
    private PlayerController _playerController;
    private Transform _playerCamera;
    private Vector3 _cameraLocalStart;
    private float _cameraPitchStart;

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

        if (bossSpawnManager == null)
            bossSpawnManager = BossSpawnManager.Instance != null
                ? BossSpawnManager.Instance
                : FindFirstObjectByType<BossSpawnManager>();

        if (choicePresenter == null)
            choicePresenter = GetComponent<CasinoChoicePresenter>();

        if (choicePresenter == null)
            choicePresenter = gameObject.AddComponent<CasinoChoicePresenter>();

        if (fortuneWheel != null)
            fortuneWheel.HideUnderground();

        if (wheelSpinZone != null)
            wheelSpinZone.SetAwaitingSpin(false);

        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (_fadeCanvasGroup != null && _fadeCanvasGroup.alpha > 0.99f)
            _fadeCanvasGroup.alpha = 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_played || _sequenceRunning || !IsPlayerCollider(other))
            return;

        _sequenceRoutine = StartCoroutine(EncounterSequence());
    }

    private IEnumerator EncounterSequence()
    {
        _sequenceRunning = true;
        _played = true;

        if (!TryResolvePlayer())
        {
            _sequenceRunning = false;
            yield break;
        }

        LockPlayerForCinematic(true);
        yield return RaiseCameraRoutine();

        if (_playerController != null)
            _playerController.SetExternalLookAtWorldPoint(Vector3.zero, false);

        yield return ShowCaption(introLine1Key, introLine1Fallback);
        yield return ShowCaption(introLine2Key, introLine2Fallback);

        bool choiceMade = false;
        bool choseYes = false;

        choicePresenter.Show(
            () =>
            {
                choseYes = true;
                choiceMade = true;
            },
            () =>
            {
                choseYes = false;
                choiceMade = true;
            });

        while (!choiceMade)
            yield return null;

        if (choseYes)
        {
            LockPlayerForCinematic(false);
            yield return YesBranchRoutine();
            LockPlayerForCinematic(true);
        }
        else
        {
            yield return NoBranchRoutine();
        }

        yield return PreFightFadeRoutine();
        StartBossFight();

        RestoreCamera();
        LockPlayerForCinematic(false);
        _sequenceRunning = false;
        _sequenceRoutine = null;
    }

    private IEnumerator YesBranchRoutine()
    {
        if (fortuneWheel == null)
        {
            PlayLaugh();
            yield return new WaitForSecondsRealtime(0.6f);
            yield break;
        }

        bool riseDone = false;
        fortuneWheel.Rise(() => riseDone = true);
        while (!riseDone)
            yield return null;

        _spinFinished = false;
        if (wheelSpinZone != null)
            wheelSpinZone.BeginWaitForSpin(fortuneWheel, () => _spinFinished = true);
        else
            fortuneWheel.RequestSpin(() => _spinFinished = true);

        while (!_spinFinished)
            yield return null;

        PlayLaugh();
        yield return new WaitForSecondsRealtime(0.6f);
    }

    private IEnumerator NoBranchRoutine()
    {
        yield return ShowCaption(refuseLine1Key, refuseLine1Fallback);
        yield return ShowCaption(refuseLine2Key, refuseLine2Fallback);
        yield return new WaitForSecondsRealtime(0.35f);
    }

    private IEnumerator PreFightFadeRoutine()
    {
        if (_fadeCanvasGroup == null)
            yield break;

        yield return ScreenFadeRunner.FadeToBlack(fadeToBlackDuration, _fadeCanvasGroup);

        if (blackHoldDuration > 0.001f)
            yield return new WaitForSecondsRealtime(blackHoldDuration);

        yield return ScreenFadeRunner.FadeFromBlack(fadeFromBlackDuration, _fadeCanvasGroup);
    }

    private void StartBossFight()
    {
        if (sceneBossProp != null)
            sceneBossProp.SetActive(false);

        if (bossSpawnManager != null)
            bossSpawnManager.SpawnBossFromEncounter(showMessage: false, playSound: true);
    }

    private IEnumerator RaiseCameraRoutine()
    {
        if (_playerCamera == null)
            yield break;

        Vector3 targetLocal = _cameraLocalStart + Vector3.up * cameraRiseHeight;
        float targetPitch = _cameraPitchStart - cameraPitchDown;

        if (bossLookTarget != null && _playerController != null)
            _playerController.SetExternalLookAtWorldPoint(bossLookTarget.position, true);

        float duration = Mathf.Max(0.05f, cameraRiseDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            _playerCamera.localPosition = Vector3.Lerp(_cameraLocalStart, targetLocal, t);

            if (_playerController != null && bossLookTarget != null)
                _playerController.SetExternalLookAtWorldPoint(bossLookTarget.position, true);

            yield return null;
        }

        _playerCamera.localPosition = targetLocal;
    }

    private void RestoreCamera()
    {
        if (_playerCamera != null)
            _playerCamera.localPosition = _cameraLocalStart;

        if (_playerController != null)
            _playerController.SetExternalLookAtWorldPoint(Vector3.zero, false);
    }

    private IEnumerator ShowCaption(string key, string fallback)
    {
        string text = LocalizedTextResolver.Resolve(key, null, fallback);
        OCDCaptionUI ui = captionUI != null ? captionUI : OCDCaptionUI.GetSharedOverlay();
        if (ui == null)
            yield break;

        yield return ui.ShowRoutine(text, captionFadeIn, captionHold, captionFadeOut);
    }

    private void PlayLaugh()
    {
        if (bossLaughSound != null && audioSource != null)
            audioSource.PlayOneShot(bossLaughSound, soundVolume);
    }

    private bool TryResolvePlayer()
    {
        _playerController = null;
        _playerCamera = null;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

        _playerController = body.GetComponent<PlayerController>();
        if (_playerController == null)
            _playerController = body.GetComponentInParent<PlayerController>();

        if (_playerController != null)
            _playerCamera = _playerController.playerCamera;

        if (_playerCamera == null)
            _playerCamera = body.GetComponentInChildren<Camera>(true)?.transform;

        if (_playerCamera == null)
            return false;

        _cameraLocalStart = _playerCamera.localPosition;
        _cameraPitchStart = _playerCamera.localEulerAngles.x;
        return true;
    }

    private void LockPlayerForCinematic(bool locked)
    {
        GameplayInputBlocker.SetBlocked(locked);

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return;

        if (body.TryGetComponent(out CharacterController cc))
            cc.enabled = !locked;

        if (_playerController != null)
            _playerController.enabled = true;
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
}
