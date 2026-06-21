using System;
using UnityEngine;

/// <summary>
/// Цвет-стихия для механики уровня Autism.
/// </summary>
public enum ElementColor
{
    Red = 0,
    Blue = 1,
    Green = 2
}

/// <summary>
/// Центральное состояние цветовой механики уровня Autism:
/// текущий цвет оружия, разблокировка крашения и маппинг enum → UnityEngine.Color.
/// Статический, чтобы пуля/оружие/враг ссылались без жёстких связей.
/// </summary>
public static class ColorManager
{
    /// <summary>Вызывается при смене текущего цвета оружия.</summary>
    public static event Action<ElementColor> OnColorChanged;

    /// <summary>Открыта ли возможность красить оружие (после подбора ColorGunObject).</summary>
    public static bool ColoringUnlocked { get; private set; }

    /// <summary>Текущий выбранный цвет оружия.</summary>
    public static ElementColor CurrentColor { get; private set; } = ElementColor.Red;

    // Сброс статики на старте Play (на случай отключённого Domain Reload).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        OnColorChanged = null;
        ColoringUnlocked = false;
        CurrentColor = ElementColor.Red;
    }

    /// <summary>Открыть возможность смены цвета (вызывается при подборе ColorGunObject).</summary>
    public static void Unlock()
    {
        ColoringUnlocked = true;
        // Уведомляем подписчиков, чтобы оружие сразу перекрасилось в текущий цвет.
        OnColorChanged?.Invoke(CurrentColor);
    }

    /// <summary>Установить конкретный цвет и оповестить подписчиков.</summary>
    public static void SetColor(ElementColor color)
    {
        CurrentColor = color;
        OnColorChanged?.Invoke(CurrentColor);
    }

    /// <summary>Сдвинуть цвет по кругу: dir &gt; 0 — вперёд, dir &lt; 0 — назад.</summary>
    public static void Cycle(int dir)
    {
        int count = 3;
        int next = (((int)CurrentColor + dir) % count + count) % count;
        SetColor((ElementColor)next);
    }

    /// <summary>Соответствие enum → видимый цвет.</summary>
    public static Color ToColor(ElementColor color)
    {
        switch (color)
        {
            case ElementColor.Red:   return new Color(0.90f, 0.15f, 0.15f, 1f);
            case ElementColor.Blue:  return new Color(0.20f, 0.45f, 1.00f, 1f);
            case ElementColor.Green: return new Color(0.20f, 0.85f, 0.30f, 1f);
            default:                 return Color.white;
        }
    }

    /// <summary>Человекочитаемое имя цвета (для UI/логов).</summary>
    public static string ToRu(ElementColor color)
    {
        switch (color)
        {
            case ElementColor.Red:   return "Красный";
            case ElementColor.Blue:  return "Синий";
            case ElementColor.Green: return "Зелёный";
            default:                 return color.ToString();
        }
    }
}
