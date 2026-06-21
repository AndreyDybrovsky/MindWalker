using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Усиливает красную виньетку на Volume в зависимости от HP игрока пока тот захвачен автоматом.
/// </summary>
public class SlotMachineDangerFX : MonoBehaviour
{
    [SerializeField] private Volume dangerVolume;
    [SerializeField] private float fadeInSpeed  = 2f;
    [SerializeField] private float fadeOutSpeed = 2.5f;

    private PlayerHealth _health;

    private void Start()
    {
        if (dangerVolume == null)
            dangerVolume = GetComponent<Volume>();

        if (dangerVolume != null)
            dangerVolume.weight = 0f;
    }

    private void Update()
    {
        if (dangerVolume == null) return;

        if (!SlotMachineController.IsPlayerTrapped)
        {
            dangerVolume.weight = Mathf.MoveTowards(dangerVolume.weight, 0f, fadeOutSpeed * Time.deltaTime);
            return;
        }

        if (_health == null)
            _health = FindFirstObjectByType<PlayerHealth>();

        float dmgFraction = _health != null ? 1f - _health.HealthPercentage : 0f;
        float target = dmgFraction * 0.85f;
        dangerVolume.weight = Mathf.MoveTowards(dangerVolume.weight, target, fadeInSpeed * Time.deltaTime);
    }
}
