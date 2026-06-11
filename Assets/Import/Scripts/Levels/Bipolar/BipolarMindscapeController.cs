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

    [Header("Туман — Луг")]
    [Tooltip("Бледно-зелёный тёплый туман")]
    [SerializeField] private Color meadowFogColor   = new Color(0.78f, 0.87f, 0.71f, 1f);
    [SerializeField] private float meadowFogDensity = 0.006f;

    [Header("Туман — Кошмар")]
    [Tooltip("Тёмный синеватый туман")]
    [SerializeField] private Color nightmareFogColor   = new Color(0.04f, 0.04f, 0.11f, 1f);
    [SerializeField] private float nightmareFogDensity = 0.04f;

    public BipolarMindscapeMode CurrentMode { get; private set; } = BipolarMindscapeMode.Meadow;

    public event Action<BipolarMindscapeMode> OnModeChanged;

    private void Awake()
    {
        ApplyModeImmediate(CurrentMode);
    }

    public void ApplyModeImmediate(BipolarMindscapeMode mode)
    {
        CurrentMode = mode;

        SetVolumeActive(meadowPostProcessVolume,    mode == BipolarMindscapeMode.Meadow);
        SetVolumeActive(nightmarePostProcessVolume, mode == BipolarMindscapeMode.Nightmare);

        if (meadowDirectionalLight    != null) meadowDirectionalLight.enabled    = mode == BipolarMindscapeMode.Meadow;
        if (nightmareDirectionalLight != null) nightmareDirectionalLight.enabled = mode == BipolarMindscapeMode.Nightmare;

        if      (mode == BipolarMindscapeMode.Meadow     && meadowSkybox    != null) RenderSettings.skybox = meadowSkybox;
        else if (mode == BipolarMindscapeMode.Nightmare  && nightmareSkybox != null) RenderSettings.skybox = nightmareSkybox;

        // Туман — применяется мгновенно (экран всегда чёрный в момент переключения)
        bool isMeadow = mode == BipolarMindscapeMode.Meadow;
        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.Exponential;
        RenderSettings.fogColor   = isMeadow ? meadowFogColor   : nightmareFogColor;
        RenderSettings.fogDensity = isMeadow ? meadowFogDensity : nightmareFogDensity;

        OnModeChanged?.Invoke(mode);
    }

    private static void SetVolumeActive(Volume volume, bool active)
    {
        if (volume == null) return;
        volume.enabled = active;
        volume.weight  = active ? 1f : 0f;
    }
}
