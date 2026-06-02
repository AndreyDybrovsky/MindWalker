using System;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Переключает пост-обработку и атмосферу уровня Bipolar (луг / кошмар).
/// Локации в сцене не отключаются — игрок телепортируется между далёкими зонами.
/// </summary>
public class BipolarMindscapeController : MonoBehaviour
{
    [Header("Пост-обработка (URP Volume)")]
    [SerializeField] private Volume meadowPostProcessVolume;
    [SerializeField] private Volume nightmarePostProcessVolume;

    [Header("Свет и небо (опционально)")]
    [SerializeField] private Light meadowDirectionalLight;
    [SerializeField] private Light nightmareDirectionalLight;
    [SerializeField] private Material meadowSkybox;
    [SerializeField] private Material nightmareSkybox;

    public BipolarMindscapeMode CurrentMode { get; private set; } = BipolarMindscapeMode.Meadow;

    public event Action<BipolarMindscapeMode> OnModeChanged;

    private void Awake()
    {
        ApplyModeImmediate(CurrentMode);
    }

    public void ApplyModeImmediate(BipolarMindscapeMode mode)
    {
        CurrentMode = mode;

        SetVolumeActive(meadowPostProcessVolume, mode == BipolarMindscapeMode.Meadow);
        SetVolumeActive(nightmarePostProcessVolume, mode == BipolarMindscapeMode.Nightmare);

        if (meadowDirectionalLight != null)
            meadowDirectionalLight.enabled = mode == BipolarMindscapeMode.Meadow;

        if (nightmareDirectionalLight != null)
            nightmareDirectionalLight.enabled = mode == BipolarMindscapeMode.Nightmare;

        if (mode == BipolarMindscapeMode.Meadow && meadowSkybox != null)
            RenderSettings.skybox = meadowSkybox;
        else if (mode == BipolarMindscapeMode.Nightmare && nightmareSkybox != null)
            RenderSettings.skybox = nightmareSkybox;

        OnModeChanged?.Invoke(mode);
    }

    private static void SetVolumeActive(Volume volume, bool active)
    {
        if (volume == null)
            return;

        volume.enabled = active;
        volume.weight = active ? 1f : 0f;
    }
}
