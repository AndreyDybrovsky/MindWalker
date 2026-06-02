using UnityEngine;

/// <summary>
/// Подсказка E у заспавненной «панической» модели. Вызывает успокоение через <see cref="DepressionPanicMomentZone"/>.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DepressionCalmInteraction : PlayerInteractionZone
{
    private DepressionPanicMomentZone _owner;
    private bool _enabled;

    public bool IsCalmInteractionEnabled => _enabled;

    public void Bind(DepressionPanicMomentZone owner)
    {
        _owner = owner;
        ApplyLocalizedPrompt();
    }

    protected override void ApplyLocalizedPrompt()
    {
        if (PromptView == null)
            return;

        string key = _owner != null ? _owner.CalmPromptLocalizationKey : "scene.depression.calm_interact";
        string fallback = _owner != null ? _owner.CalmPromptFallback : "Успокойте";
        string text = PressEPromptUtility.ResolveLocalizedText(key, fallback);
        PromptView.SetText(text);
    }

    public void SetInteractionEnabled(bool enabled)
    {
        _enabled = enabled;
        if (!enabled)
            PlayerInZone = false;
    }

    protected override void Awake()
    {
        base.Awake();
        SetInteractionEnabled(false);
    }

    protected override void OnInteractPressed()
    {
        if (!_enabled || _owner == null)
            return;

        _owner.TryCalm();
    }

    protected override void Update()
    {
        if (!_enabled)
        {
            PlayerInZone = false;
            UpdatePromptReveal();
            return;
        }

        base.Update();
    }
}
