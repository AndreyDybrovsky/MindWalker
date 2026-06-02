using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// Колесо фортуны: подъём из-под земли и вращение всегда на проигрышный сектор.
/// </summary>
public class FortuneWheelController : MonoBehaviour
{
    [Header("Вращение")]
    [SerializeField] private Transform spinPivot;
    [Tooltip("Локальный угол Y (градусы), куда останавливается «проигрышная» секция.")]
    [SerializeField] private float loseSectorAngleY = 12f;
    [SerializeField] private int fullSpins = 4;
    [SerializeField] private float spinDuration = 4.5f;

    [Header("Подъём")]
    [SerializeField] private Transform riseRoot;
    [SerializeField] private float hiddenLocalY = -2.8f;
    [SerializeField] private float raisedLocalY = 0f;
    [SerializeField] private float riseDuration = 2.2f;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip riseSound;
    [SerializeField] private AudioClip spinLoop;
    [SerializeField] private AudioClip spinStopSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;

    private bool _isRaised;
    private bool _isSpinning;
    private bool _hasSpun;
    private Vector3 _raisedLocalPosition;
    private Coroutine _riseRoutine;
    private Coroutine _spinRoutine;
    private Action _onSpinComplete;

    public bool IsRaised => _isRaised;
    public bool CanSpin => _isRaised && !_isSpinning && !_hasSpun;

    private void Awake()
    {
        if (spinPivot == null)
            spinPivot = transform;

        if (riseRoot == null)
            riseRoot = transform;

        _raisedLocalPosition = riseRoot.localPosition;
        Vector3 hidden = _raisedLocalPosition;
        hidden.y = hiddenLocalY;
        riseRoot.localPosition = hidden;
        _isRaised = false;

        if (audioSource == null)
            TryGetComponent(out audioSource);
    }

    public void HideUnderground()
    {
        if (_riseRoutine != null)
            StopCoroutine(_riseRoutine);

        if (_spinRoutine != null)
            StopCoroutine(_spinRoutine);

        _isRaised = false;
        _isSpinning = false;
        _hasSpun = false;
        _onSpinComplete = null;

        Vector3 hidden = _raisedLocalPosition;
        hidden.y = hiddenLocalY;
        riseRoot.localPosition = hidden;
    }

    public void Rise(Action onComplete = null)
    {
        if (_isRaised)
        {
            onComplete?.Invoke();
            return;
        }

        if (_riseRoutine != null)
            StopCoroutine(_riseRoutine);

        _riseRoutine = StartCoroutine(RiseRoutine(onComplete));
    }

    public void RequestSpin(Action onComplete)
    {
        if (!CanSpin)
            return;

        _onSpinComplete = onComplete;
        if (_spinRoutine != null)
            StopCoroutine(_spinRoutine);

        _spinRoutine = StartCoroutine(SpinRoutine());
    }

    private IEnumerator RiseRoutine(Action onComplete)
    {
        PlayOneShot(riseSound);

        Vector3 from = riseRoot.localPosition;
        Vector3 to = _raisedLocalPosition;
        to.y = raisedLocalY;
        float duration = Mathf.Max(0.05f, riseDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            riseRoot.localPosition = Vector3.Lerp(from, to, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            yield return null;
        }

        riseRoot.localPosition = to;
        _isRaised = true;
        _riseRoutine = null;
        onComplete?.Invoke();
    }

    private IEnumerator SpinRoutine()
    {
        _isSpinning = true;
        _hasSpun = true;

        if (spinLoop != null && audioSource != null)
        {
            audioSource.clip = spinLoop;
            audioSource.loop = true;
            audioSource.volume = soundVolume;
            audioSource.Play();
        }

        float startY = spinPivot.localEulerAngles.y;
        float targetY = loseSectorAngleY + 360f * Mathf.Max(1, fullSpins);
        float duration = Mathf.Max(0.5f, spinDuration);
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            float y = Mathf.LerpAngle(startY, targetY, t);
            Vector3 euler = spinPivot.localEulerAngles;
            euler.y = y;
            spinPivot.localEulerAngles = euler;
            yield return null;
        }

        Vector3 finalEuler = spinPivot.localEulerAngles;
        finalEuler.y = loseSectorAngleY;
        spinPivot.localEulerAngles = finalEuler;

        if (audioSource != null && audioSource.isPlaying)
            audioSource.Stop();

        PlayOneShot(spinStopSound);
        _isSpinning = false;
        _spinRoutine = null;

        Action callback = _onSpinComplete;
        _onSpinComplete = null;
        callback?.Invoke();
    }

    private void PlayOneShot(AudioClip clip)
    {
        if (clip == null || audioSource == null)
            return;

        audioSource.PlayOneShot(clip, soundVolume);
    }
}
