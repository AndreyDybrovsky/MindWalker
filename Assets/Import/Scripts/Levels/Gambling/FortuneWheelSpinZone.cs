using UnityEngine;

/// <summary>
/// Зона E у поднятого колеса фортуны.
/// </summary>
[RequireComponent(typeof(Collider))]
public class FortuneWheelSpinZone : PlayerInteractionZone
{
    [SerializeField] private FortuneWheelController wheel;

    private bool _waitingForSpin;
    private System.Action _onSpinComplete;

    protected override void Awake()
    {
        promptLocalizationKey = "scene.gambling.wheel_spin";
        promptFallbackText = "Крутить";
        base.Awake();
    }

    public void Bind(FortuneWheelController controller)
    {
        wheel = controller;
    }

    public void SetAwaitingSpin(bool awaiting)
    {
        _waitingForSpin = awaiting;
        if (!awaiting)
        {
            _onSpinComplete = null;
            PlayerInZone = false;
        }
    }

    public void BeginWaitForSpin(FortuneWheelController controller, System.Action onComplete)
    {
        wheel = controller;
        _onSpinComplete = onComplete;
        _waitingForSpin = true;
        InteractionBusy = false;
    }

    protected override void ApplyLocalizedPrompt()
    {
        if (PromptView == null)
            return;

        string text = PressEPromptUtility.ResolveLocalizedText(promptLocalizationKey, promptFallbackText);
        PromptView.SetText(text);
    }

    protected override void Update()
    {
        if (!_waitingForSpin || wheel == null || !wheel.CanSpin)
        {
            PlayerInZone = false;
            UpdatePromptReveal();
            return;
        }

        base.Update();
    }

    protected override void OnInteractPressed()
    {
        if (!_waitingForSpin || wheel == null || !wheel.CanSpin || InteractionBusy)
            return;

        PlayerInZone = false;
        _waitingForSpin = false;
        System.Action complete = _onSpinComplete;
        _onSpinComplete = null;
        wheel.RequestSpin(complete);
    }
}
