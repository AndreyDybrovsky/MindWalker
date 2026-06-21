using System.Collections;
using System.Collections.Generic;
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

    [Header("Звук в темноте")]
    [SerializeField] private AudioSource audioSource;
    [Tooltip("Устаревшее поле: если Black Screen Sounds пуст — используется как единственный звук.")]
    [SerializeField] private AudioClip momentSound;
    [Tooltip("Несколько звуков подряд, пока экран чёрный.")]
    [SerializeField] private AudioClip[] blackScreenSounds;
    [Tooltip("Пауза перед каждым следующим звуком (длина = звуков − 1). Пусто — Black Screen Sound Gap.")]
    [SerializeField] private float[] delaysBetweenBlackSounds;
    [SerializeField] private float blackScreenSoundGap = 0.12f;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 1f;
    [SerializeField] private float soundDelayAfterFade = 0.08f;

    [Header("Звук при появлении нижнего текста")]
    [SerializeField] private AudioClip captionRevealSound;
    [SerializeField, Range(0f, 1f)] private float captionRevealSoundVolume = 1f;

    [Header("Атмосфера (улица / вечер дома)")]
    [Tooltip("Пресет света, пост-обработки и ambient-музыки. Нужен OCDSceneAtmosphereController на сцене.")]
    [SerializeField] private OCDAtmospherePreset atmospherePreset = OCDAtmospherePreset.None;
    [SerializeField] private bool applyAtmosphereOnBlack = true;
    [Tooltip("Плавный переход атмосферы во время осветления экрана.")]
    [SerializeField] private bool blendAtmosphereDuringFadeOut = true;
    [SerializeField] private OCDAtmosphereOverrides atmosphereOverrides;

    [Header("Тайминг")]
    [SerializeField] private float screenFadeInDuration = 0.75f;
    [SerializeField] private float captionHoldDuration = 3f;
    [Tooltip("Осветление экрана после звука.")]
    [SerializeField] private float screenFadeOutDuration = 0.75f;
    [Tooltip("Сколько держать экран чёрным после старта звука (не дольше длины клипа).")]
    [SerializeField] private float maxBlackHoldAfterSound = 0.75f;
    [SerializeField] private bool lockPlayerMovement = true;
    [Tooltip("Если выключено — момент запускается только через OCDAutoMomentZone или внешний вызов BeginMomentSequence.")]
    [SerializeField] private bool requirePressE = true;

    [Header("Без затемнения (выход за дверь, финальные точки)")]
    [Tooltip("Если включено — момент проигрывается БЕЗ затемнения экрана: только звук, нижняя надпись и переход к следующему триггеру. " +
             "Движение игрока не блокируется. Подходит для 'Выйди' и точек, где экран не должен темнеть.")]
    [SerializeField] private bool skipScreenFade = false;

    [Header("Сохранение")]
    [Tooltip("Уникальный ID для системы сохранений. Оставьте пустым — генерируется автоматически по позиции в иерархии.")]
    [SerializeField] private string momentSaveId;

    private bool _isUnlocked;
    private bool _hasPlayed;
    private bool _momentSequenceRunning;
    private CanvasGroup _fadeCanvasGroup;
    private bool _isAutoClosingCaption;
    private bool _controlReleasedInSequence;
    private bool _saveRestoreApplied;
    private bool _activatedBeforeStart;

    public float PressPromptReveal => _isUnlocked ? Reveal : 0f;
    public bool IsPressPromptVisible => _isUnlocked && PlayerInZone && !InteractionBusy && PromptView != null;

    protected override void Start()
    {
        base.Start();

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
        // Сбрасываем остаточный фейд от предыдущей сцены ТОЛЬКО у стартового триггера.
        // Триггеры, которые активируются mid-game (Day2, Day3...), не должны трогать
        // глобальный фейд — он может быть чёрным из-за идущего перехода.
        if (activeAtStart && _fadeCanvasGroup != null && _fadeCanvasGroup.alpha > 0.99f)
            _fadeCanvasGroup.alpha = 0f;

        if (activeAtStart)
            ReleaseScreenFadeBlock();

        // Если состояние уже восстановлено из сохранения — не затираем его
        if (_saveRestoreApplied)
        {
            ApplyUnlockedState();
            if (_isUnlocked && !_hasPlayed)
                ShowObjectiveForThisMoment();
            return;
        }

        // Если ActivateInSequence был вызван до Start() (EnableDayOnly из предыдущего дня) — не затираем
        if (_activatedBeforeStart)
        {
            ApplyUnlockedState();
            return;
        }

        _isUnlocked = activeAtStart;
        ApplyUnlockedState();

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

        if (!requirePressE)
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

        BeginMomentSequence();
    }

    /// <summary>Запуск момента без E (авто-зона, сбор всех точек FixAll и т.п.).</summary>
    public bool CanBeginFromAutoZone =>
        _isUnlocked && !InteractionBusy && !(_hasPlayed && disableAfterPlayed);

    public void BeginMomentSequence()
    {
        if (!_isUnlocked || InteractionBusy || (_hasPlayed && disableAfterPlayed))
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

        _activatedBeforeStart = true;
        _isUnlocked = true;
        ApplyUnlockedState();
        UpdateGuideLightState();
        // Цель уже выставлена через AdvanceObjectiveAfterCompletion у предыдущего триггера.
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
        _controlReleasedInSequence = false;
        ApplyUnlockedState();

        // В режиме без затемнения не блокируем ввод и движение — игрок продолжает идти.
        if (!skipScreenFade)
        {
            GameplayInputBlocker.SetBlocked(true);

            if (lockPlayerMovement)
                SetPlayerControlLocked(true);
        }

        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
        ReleaseScreenFadeBlock();

        if (!skipScreenFade)
            SetMomentHudVisible(false);

        OCDCaptionUI captionUi = captionUI != null ? captionUI : OCDCaptionUI.GetSharedOverlay();

        try
        {
            // 1) Полное затемнение (пропускается в режиме без затемнения)
            if (!skipScreenFade)
                yield return ScreenFadeRunner.FadeToBlack(screenFadeInDuration, _fadeCanvasGroup);

            AdvanceObjectiveAfterCompletion();

            // Переключение объектов делаем в темноте, чтобы игрок не видел "поп".
            ApplyBlackSwap();
            ApplyAtmosphereTransition();

            // 2) После полного затемнения — звук
            if (soundDelayAfterFade > 0f)
                yield return new WaitForSecondsRealtime(soundDelayAfterFade);

            float soundDuration = ComputeBlackSoundsDuration();
            yield return PlayBlackScreenSoundsRoutine();

            // EnableDayOnly включает новый день без выключения старого — иначе корутина триггера
            // умрёт вместе с корнём старого дня. DeactivateInactiveDays вызовется в Release.
            if (nextMoment != null)
            {
                if (OCDMissionDayController.Instance != null)
                    OCDMissionDayController.Instance.EnableDayOnly(nextMoment);
                nextMoment.ActivateInSequence();
            }

            UpdateGuideLightState();

            // В режиме без затемнения экран и не темнел — держать чёрным и осветлять нечего.
            if (!skipScreenFade)
            {
                float blackHold = Mathf.Min(soundDuration, Mathf.Max(0f, maxBlackHoldAfterSound));
                if (blackHold > 0.001f)
                    yield return new WaitForSecondsRealtime(blackHold);

                // 3) Плавно убираем затемнение (атмосфера может меняться параллельно)
                if (blendAtmosphereDuringFadeOut && atmospherePreset != OCDAtmospherePreset.None)
                    StartAtmosphereBlend(screenFadeOutDuration);

                yield return ScreenFadeRunner.FadeFromBlack(screenFadeOutDuration, _fadeCanvasGroup);
            }

            SetMomentHudVisible(true);

            // 4) Нижняя надпись: управление — сразу после появления текста
            string caption = ResolveCaptionText();
            if (captionUi != null)
                yield return captionUi.ShowCaptionFadeIn(caption, captionFadeInDuration);

            PlayCaptionRevealSound();

            ReleaseMomentPlayerControl();

            // Запускаем на captionUi, а не на this — после DeactivateInactiveDays() этот объект уже неактивен
            if (captionUi != null)
                captionUi.RunCaptionHoldAndFadeOut(captionHoldDuration, captionFadeOutDuration);
        }
        finally
        {
            ReleaseScreenFadeBlock();
            SetMomentHudVisible(true);
            ReleaseMomentPlayerControl();
        }
    }

    private void ReleaseMomentPlayerControl()
    {
        if (_controlReleasedInSequence)
            return;

        _controlReleasedInSequence = true;
        GameplayInputBlocker.SetBlocked(false);

        if (lockPlayerMovement)
            SetPlayerControlLocked(false);

        InteractionBusy = false;
        _momentSequenceRunning = false;
        _isUnlocked = false;
        ApplyUnlockedState();

        OCDMissionDayController.Instance?.DeactivateInactiveDays();
    }

    private static void ReleaseScreenFadeBlock()
    {
        CanvasGroup fade = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (fade == null)
            return;

        fade.blocksRaycasts = false;
        fade.interactable = false;
    }

    private void ApplyAtmosphereTransition()
    {
        if (!applyAtmosphereOnBlack || atmospherePreset == OCDAtmospherePreset.None)
            return;

        OCDSceneAtmosphereController atmosphere = OCDSceneAtmosphereController.Instance;
        if (atmosphere == null)
            atmosphere = FindFirstObjectByType<OCDSceneAtmosphereController>();

        if (atmosphere == null)
        {
            Debug.LogWarning(
                $"OCDMomentTrigger '{name}': задан Atmosphere Preset, но на сцене нет OCDSceneAtmosphereController.",
                this);
            return;
        }

        if (!blendAtmosphereDuringFadeOut)
            atmosphere.ApplyPresetImmediate(atmospherePreset, atmosphereOverrides);
    }

    private void StartAtmosphereBlend(float duration)
    {
        OCDSceneAtmosphereController atmosphere = OCDSceneAtmosphereController.Instance;
        if (atmosphere == null)
            return;

        atmosphere.ApplyPresetDuringFade(atmospherePreset, duration, atmosphereOverrides);
    }

    private AudioClip[] GetEffectiveBlackScreenSounds()
    {
        if (blackScreenSounds != null && blackScreenSounds.Length > 0)
            return blackScreenSounds;

        if (momentSound != null)
            return new[] { momentSound };

        return System.Array.Empty<AudioClip>();
    }

    private float GetDelayBeforeBlackSound(int soundIndex)
    {
        if (soundIndex <= 0)
            return 0f;

        if (delaysBetweenBlackSounds != null && soundIndex - 1 < delaysBetweenBlackSounds.Length)
            return Mathf.Max(0f, delaysBetweenBlackSounds[soundIndex - 1]);

        return Mathf.Max(0f, blackScreenSoundGap);
    }

    private float ComputeBlackSoundsDuration()
    {
        AudioClip[] clips = GetEffectiveBlackScreenSounds();
        float total = 0f;

        for (int i = 0; i < clips.Length; i++)
        {
            if (i > 0)
                total += GetDelayBeforeBlackSound(i);

            if (clips[i] != null)
                total += clips[i].length;
        }

        return total;
    }

    private IEnumerator PlayBlackScreenSoundsRoutine()
    {
        AudioClip[] clips = GetEffectiveBlackScreenSounds();
        if (audioSource == null || clips.Length == 0)
            yield break;

        for (int i = 0; i < clips.Length; i++)
        {
            float delay = GetDelayBeforeBlackSound(i);
            if (delay > 0.001f)
                yield return new WaitForSecondsRealtime(delay);

            if (clips[i] != null)
                audioSource.PlayOneShot(clips[i], soundVolume);
        }
    }

    private void PlayCaptionRevealSound()
    {
        if (captionRevealSound == null || audioSource == null)
            return;

        audioSource.PlayOneShot(captionRevealSound, captionRevealSoundVolume);
    }

    private void SetMomentHudVisible(bool visible)
    {
        OCDObjectiveUI objective = ResolveObjectivePanel();
        if (objective != null)
            objective.SetHudVisible(visible);

        if (visible)
            return;

        OCDCaptionUI caption = captionUI != null ? captionUI : OCDCaptionUI.GetSharedOverlay();
        if (caption != null && caption.IsVisible)
            StartCoroutine(caption.HideSmooth(autoCloseCaptionDuration));
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

    // ──────────── Save / Restore ────────────

    public string GetMomentSaveId()
    {
        if (!string.IsNullOrEmpty(momentSaveId))
            return momentSaveId;

        // Автоматический уникальный ID на основе позиции в иерархии
        var indices = new List<int>(8);
        Transform t = transform;
        while (t != null)
        {
            indices.Add(t.GetSiblingIndex());
            t = t.parent;
        }
        indices.Reverse();
        return "m:" + string.Join("/", indices);
    }

    /// <summary>Восстанавливает состояние момента из сохранения. Безопасно вызывать до Start().</summary>
    public void RestoreState(bool hasPlayed, bool isUnlocked)
    {
        _saveRestoreApplied = true;
        _hasPlayed = hasPlayed;
        _isUnlocked = isUnlocked;
        ApplyUnlockedState();
        if (hasPlayed)
            ApplyBlackSwap(); // восстанавливаем состояния объектов (кровать, объекты сцены)
        // ShowObjectiveForThisMoment() будет вызван из Start() после инициализации базового класса
    }

    public static void CaptureAllToSave(GameSaveData saveData)
    {
        if (saveData == null)
            return;

        OCDMomentTrigger[] triggers = Object.FindObjectsByType<OCDMomentTrigger>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (triggers == null || triggers.Length == 0)
            return;

        var data = new OCDCheckpointSaveData();
        data.activeDayIndex = OCDMissionDayController.Instance != null
            ? OCDMissionDayController.Instance.GetActiveDayIndex()
            : 1;

        for (int i = 0; i < triggers.Length; i++)
        {
            OCDMomentTrigger t = triggers[i];
            if (t == null)
                continue;

            if (t._hasPlayed)
                data.playedMomentIds.Add(t.GetMomentSaveId());

            if (t._isUnlocked && !t._hasPlayed)
                data.currentUnlockedMomentId = t.GetMomentSaveId();
        }

        SaveGameCustomDataUtility.Write(saveData, "ocd_checkpoints", data);
    }

    public static void RestoreAllFromSave(GameSaveData saveData)
    {
        if (saveData == null)
            return;

        if (!SaveGameCustomDataUtility.TryRead(saveData, "ocd_checkpoints", out OCDCheckpointSaveData data))
            return;

        if (OCDMissionDayController.Instance != null)
            OCDMissionDayController.Instance.SetActiveDayIndex(data.activeDayIndex);

        OCDMomentTrigger[] triggers = Object.FindObjectsByType<OCDMomentTrigger>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        if (triggers == null)
            return;

        for (int i = 0; i < triggers.Length; i++)
        {
            OCDMomentTrigger t = triggers[i];
            if (t == null)
                continue;

            string id = t.GetMomentSaveId();
            bool hasPlayed = data.playedMomentIds != null && data.playedMomentIds.Contains(id);
            bool isUnlocked = !string.IsNullOrEmpty(data.currentUnlockedMomentId) &&
                              id == data.currentUnlockedMomentId;
            t.RestoreState(hasPlayed, isUnlocked);
        }
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

