using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Переопределение ambient-музыки и пост-обработки для одного <see cref="OCDMomentTrigger"/>.
/// </summary>
[System.Serializable]
public struct OCDAtmosphereOverrides
{
    [Tooltip("Если включено — при смене атмосферы в этом моменте используются поля ниже.")]
    public bool enabled;

    [Tooltip("Свой ambient-клип вместо клипа пресета. Пусто — музыка пресета.")]
    public AudioClip ambientMusicClip;

    [Tooltip("Дополнительный Global Volume на сцене (ночь на улице и т.п.). Weight → 1, остальные Volume → 0.")]
    public Volume postProcessVolume;
}
