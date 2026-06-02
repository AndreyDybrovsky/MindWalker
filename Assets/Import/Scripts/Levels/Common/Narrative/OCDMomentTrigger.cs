using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Один шаг сцены в комнате пациента с ОКР:
/// подсказка E → затемнение → звук → нижняя надпись → следующий триггер.
/// Расставьте триггеры на сцене и свяжите полем Next Moment. У первого включите Active At Start.
/// </summary>
[RequireComponent(typeof(Collider))]
public class OCDMomentTrigger : PlayerInteractionZone
{
    [Header("Цепочка")]
    [Tooltip("Следующий триггер, который станет доступен после этого момента.")]
    [SerializeField] private OCDMomentTrigger nextMoment;
    [Tooltip("Только у первого триггера в цепочке.")]
    [SerializeField] private bool activeAtStart = true;
    [Tooltip("После проигрывания отключить этот триггер навсегда.")]
    [SerializeField] private bool disableAfterPlayed = true;
    [Tooltip("Опционально: подсветка/свет-указатель для этого триггера. Включается, когда триггер активен.")]
    [SerializeField] private GameObject guideLight;
    [SerializeField] private bool pulseGuideLight = true;

    [Header("Переключение объектов (опционально)")]
    [Tooltip("Объекты, которые отключатся в темноте (после полного затемнения). Например: незаправленная кровать.")]
    [SerializeField] private GameObject[] disableDuringBlack;
    [Tooltip("Объекты, которые включатся в темноте (после полного затемнения). Например: заправленная кровать.")]
    [SerializeField] private GameObject[] enableDuringBlack;

    [Header("Цель (слева сверху)")]
    [Tooltip("Панель целей на сцене (префаб с OCDObjectiveUI). Пусто — первая OCDObjectiveUI на сцене.")]
    [SerializeField] private OCDObjectiveUI objectivePanel;
    [Tooltip("TMP-образец стиля целей (например QuestText). Нижняя надпись (MessageText) не затрагивается.")]
    [FormerlySerializedAs("textStyleReference")]
    [SerializeField] private TMP_Text objectiveStyleReference;
    [Tooltip("Ключ в LocalizationBase (приоритет) / strings_*.json. Пусто — ключ нижней надписи.")]
    [SerializeField] private string objectiveLocalizationKey;
    [SerializeField, TextArea(2, 4)] private string objectiveFallbackText;
    [SerializeField] private string[] objectiveFormatArgs;

    [Header("Надпись (низ экрана)")]
    [Tooltip("Если задано — используем этот UI (например, из Player.prefab). Если пусто — возьмём общий OCDCaptionUI на сцене.")]
    [SerializeField] private OCDCaptionUI captionUI;
    [Tooltip("Ключ в strings_*.json / LocalizationBase. Пусто — используется Caption Fallback Text.")]
    [SerializeField] private string captionLocalizationKey;
    [SerializeField, TextArea(2, 6)] private string captionFallbackText = "Текст момента…";
    [Tooltip("Аргументы для string.Format ({0}, {1}…).")]
    [SerializeField] private string[] captionFormatArgs;
    [SerializeField] private float captionFadeInDuration = 0.45f;
    [SerializeField] private float captionFadeOutDuration = 0.35f;
    [Header("Поведение при входе в следующий триггер")]
    [Tooltip("Если игрок вошёл в зону, пока MessageText ещё виден — сначала плавно скрываем его, потом показываем подсказку E.")]
    [SerializeField] private bool autoCloseCaptionOnApproach = true;
    [SerializeField] private float autoCloseCaptionDuration = 0.22f;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip momentSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
    [SerializeField] private float soundDelayAfterFade = 0.08f;

    [Header("Тайминг")]
    [SerializeField] private float screenFadeInDuration = 0.75f;
    [SerializeField] private float captionHoldDuration = 3.5f;
    [SerializeField] private float screenFadeOutDuration = 0.65f;
    [SerializeField] private bool lockPlayerMovement = true;

    private bool _isUnlocked;
    private bool _hasPlayed;
    private bool _momentSequenceRunning;
    private CanvasGroup _fadeCanvasGroup;
    private bool _isAutoClosingCaption;

    public float PressPromptReveal => _isUnlocked ? Reveal : 0f;
    public bool IsPressPromptVisible => _isUnlocked && PlayerInZone && !InteractionBusy && PromptView != null;

    protected override void Start()
    {
        base.Start();

        _isUnlocked = activeAtStart;
        ApplyUnlockedState();

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f;
        }

        AudioMixerRoutingUtility.BindSourceToSfx(audioSource);

        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (_fadeCanvasGroup != null && _fadeCanvasGroup.alpha > 0.99f)
            _fadeCanvasGroup.alpha = 0f;

        if (activeAtStart)
            ShowObjectiveForThisMoment();
    }

    private OCDObjectiveUI ResolveObjectivePanel()
    {
        if (objectivePanel != null)
        {
            OCDObjectiveUI.SetSharedInstance(objectivePanel);
            return objectivePanel;
        }

        return OCDObjectiveUI.GetShared();
    }

    private void ApplyObjectiveStyle()
    {
        OCDObjectiveUI ui = ResolveObjectivePanel();
        if (ui == null)
            return;

        TMP_Text style = objectiveStyleReference != null
            ? objectiveStyleReference
            : ui.ResolveStyleReference();

        if (style != null)
            ui.ApplyStyleFrom(style);
    }

    protected override void Update()
    {
        if (!_isUnlocked)
        {
            PlayerInZone = false;
            PressEPromptCoordinator.Refresh();
            return;
        }

        // Если игрок подошёл к следующему триггеру, пока текст ещё на экране — прячем текст и только потом даём подсказку E.
        if (autoCloseCaptionOnApproach && !_isAutoClosingCaption && !InteractionBusy)
        {
            OCDCaptionUI ui = captionUI != null ? captionUI : OCDCaptionUI.GetSharedOverlay();
            if (ui != null && ui.IsVisible && PlayerInZone)
            {
                StartCoroutine(AutoCloseCaptionForPrompt(ui));
            }
        }

        base.Update();
    }

    private IEnumerator AutoCloseCaptionForPrompt(OCDCaptionUI ui)
    {
        _isAutoClosingCaption = true;
        InteractionBusy = true;
        Reveal = 0f;
        PressEPromptCoordinator.Refresh();

        if (ui != null)
            yield return ui.HideSmooth(autoCloseCaptionDuration);

        InteractionBusy = false;
        _isAutoClosingCaption = false;
        PressEPromptCoordinator.Refresh();
    }

    protected override void OnInteractPressed()
    {
        if (!_isUnlocked || InteractionBusy || _hasPlayed)
            return;

        InteractionBusy = true;
        PlayerInZone = false;
        Reveal = 0f;

        if (PromptView != null)
            PromptView.SetReveal(0f);

        PressEPromptCoordinator.Refresh();
        StartCoroutine(PlayMomentSequence());
    }

    /// <summary>Вызывается предыдущим триггером или директором.</summary>
    public void ActivateInSequence()
    {
        if (_hasPlayed && disableAfterPlayed && !_momentSequenceRunning)
            return;

        _isUnlocked = true;
        ApplyUnlockedState();
        UpdateGuideLightState();
    }

    private void ApplyUnlockedState()
    {
        if (ZoneCollider != null)
            ZoneCollider.enabled = _isUnlocked && !(_hasPlayed && disableAfterPlayed && !_momentSequenceRunning);

        UpdateGuideLightState();
    }

    private bool ShouldShowGuideLight()
    {
        if (!_isUnlocked)
            return false;

        // Пока идёт затемнение/звук/осветление — подсветка не гаснет досрочно.
        if (_momentSequenceRunning)
            return true;

        return !(_hasPlayed && disableAfterPlayed);
    }

    private void UpdateGuideLightState()
    {
        if (guideLight == null)
            return;

        bool show = ShouldShowGuideLight();
        guideLight.SetActive(show);

        if (!show)
            return;

        if (pulseGuideLight)
        {
            if (!guideLight.TryGetComponent(out OCDGuideLightPulse pulse))
                pulse = guideLight.AddComponent<OCDGuideLightPulse>();
            pulse.enabled = true;
        }
    }

    private IEnumerator PlayMomentSequence()
    {
        _hasPlayed = true;
        _momentSequenceRunning = true;
        ApplyUnlockedState();
        AdvanceObjectiveAfterCompletion();
        GameplayInputBlocker.SetBlocked(true);

        if (lockPlayerMovement)
            SetPlayerControlLocked(true);

        // 1) Полное затемнение
        yield return ScreenFadeRunner.FadeToBlack(screenFadeInDuration, _fadeCanvasGroup);

        // Переключение объектов делаем в темноте, чтобы игрок не видел "поп".
        ApplyBlackSwap();

        // 2) После полного затемнения — звук
        if (soundDelayAfterFade > 0f)
            yield return new WaitForSecondsRealtime(soundDelayAfterFade);

        float soundDuration = 0f;
        if (momentSound != null)
            soundDuration = Mathf.Max(0f, momentSound.length);

        if (momentSound != null && audioSource != null)
            audioSource.PlayOneShot(momentSound, soundVolume);

        // Новый триггер активируется сразу после звука в темноте.
        if (nextMoment != null)
            nextMoment.ActivateInSequence();

        UpdateGuideLightState();

        // Ждём, пока звук прозвучит, и только потом начинаем осветление.
        if (soundDuration > 0.001f)
            yield return new WaitForSecondsRealtime(soundDuration);

        // 3) Плавно убираем затемнение
        yield return ScreenFadeRunner.FadeFromBlack(screenFadeOutDuration, _fadeCanvasGroup);

        // 4) Возвращаем управление и одновременно показываем текст
        GameplayInputBlocker.SetBlocked(false);
        if (lockPlayerMovement)
            SetPlayerControlLocked(false);

        string caption = ResolveCaptionText();
        OCDCaptionUI ui = captionUI != null ? captionUI : OCDCaptionUI.GetSharedOverlay();
        if (ui != null)
            yield return ui.ShowRoutine(caption, captionFadeInDuration, captionHoldDuration, captionFadeOutDuration);

        InteractionBusy = false;
        _momentSequenceRunning = false;
        _isUnlocked = false;
        ApplyUnlockedState();
    }

    private void ApplyBlackSwap()
    {
        if (disableDuringBlack != null)
        {
            for (int i = 0; i < disableDuringBlack.Length; i++)
            {
                GameObject go = disableDuringBlack[i];
                if (go != null && !IsGuideLightObject(go))
                    go.SetActive(false);
            }
        }

        if (enableDuringBlack != null)
        {
            for (int i = 0; i < enableDuringBlack.Length; i++)
            {
                GameObject go = enableDuringBlack[i];
                if (go != null)
                    go.SetActive(true);
            }
        }
    }

    private void ShowObjectiveForThisMoment()
    {
        OCDObjectiveUI ui = ResolveObjectivePanel();
        if (ui == null)
            return;

        ApplyObjectiveStyle();
        ui.ShowCurrent(
            ResolveObjectiveKey(),
            objectiveFormatArgs,
            ResolveObjectiveFallback());
    }

    private void AdvanceObjectiveAfterCompletion()
    {
        OCDObjectiveUI ui = ResolveObjectivePanel();
        if (ui == null)
            return;

        if (nextMoment != null)
            nextMoment.ApplyObjectiveStyle();
        else
            ApplyObjectiveStyle();

        string completedKey = ResolveObjectiveKey();
        string completedFallback = ResolveObjectiveFallback();

        if (nextMoment != null)
        {
            ui.Advance(
                completedKey,
                objectiveFormatArgs,
                completedFallback,
                nextMoment.ResolveObjectiveKey(),
                nextMoment.GetObjectiveFormatArgs(),
                nextMoment.ResolveObjectiveFallback());
        }
        else
        {
            ui.Advance(
                completedKey,
                objectiveFormatArgs,
                completedFallback,
                null,
                null,
                null);
        }
    }

    private string ResolveObjectiveKey()
    {
        return !string.IsNullOrEmpty(objectiveLocalizationKey)
            ? objectiveLocalizationKey
            : captionLocalizationKey;
    }

    private string ResolveObjectiveFallback()
    {
        return !string.IsNullOrEmpty(objectiveFallbackText)
            ? objectiveFallbackText
            : captionFallbackText;
    }

    internal string[] GetObjectiveFormatArgs() => objectiveFormatArgs;

    private bool IsGuideLightObject(GameObject go)
    {
        if (go == null || guideLight == null)
            return false;

        if (go == guideLight)
            return true;

        Transform guideTransform = guideLight.transform;
        return go.transform == guideTransform || go.transform.IsChildOf(guideTransform);
    }

    private string ResolveCaptionText()
    {
        string text;

        text = LocalizedTextResolver.Resolve(captionLocalizationKey, captionFormatArgs, captionFallbackText);

        if (string.IsNullOrEmpty(text))
            return captionFallbackText ?? string.Empty;

        return text;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _isUnlocked || activeAtStart
            ? new Color(0.2f, 0.85f, 1f, 0.35f)
            : new Color(0.5f, 0.5f, 0.5f, 0.2f);

        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);

        if (nextMoment != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawLine(transform.position, nextMoment.transform.position);
        }
    }
#endif
}

