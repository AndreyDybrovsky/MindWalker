using TMPro;
using UnityEngine;

/// <summary>
/// Копирует визуальный стиль TMP с образца. Текст образца не копируется.
/// </summary>
public static class TmpTextStyleUtility
{
    public enum CopyMode
    {
        /// <summary>Шрифт, цвет, градиент; размер и autosize как у цели (для MessageText).</summary>
        PreserveTargetSize = 0,
        /// <summary>Полный стиль с образца, без autosize (для сгенерированных строк целей).</summary>
        MatchReferenceSize = 1,
    }

    public static void CopyStyle(TMP_Text target, TMP_Text source, CopyMode mode = CopyMode.PreserveTargetSize,
        bool strikethrough = false)
    {
        if (target == null || source == null || target == source)
            return;

        float savedSize = target.fontSize;
        float savedMin = target.fontSizeMin;
        float savedMax = target.fontSizeMax;
        bool savedAutoSize = target.enableAutoSizing;

        target.font = source.font;
        target.fontSharedMaterial = source.fontSharedMaterial;
        target.fontMaterial = source.fontMaterial;
        target.fontWeight = source.fontWeight;
        target.fontStyle = source.fontStyle;
        target.characterSpacing = source.characterSpacing;
        target.wordSpacing = source.wordSpacing;
        target.lineSpacing = source.lineSpacing;
        target.paragraphSpacing = source.paragraphSpacing;
        target.color = source.color;

        // faceColor/outlineColor/outlineWidth обращаются к инстанс-материалу шрифта
        // (m_fontMaterial). В play-режиме у только что созданного текста (авто-таймер
        // DepressionCountdownUI) этот материал ещё не сгенерирован CanvasRenderer'ом,
        // и TMP.SetOutlineThickness кидает NullReferenceException — это валило корутину
        // события паники (девочка спавнилась, но таймер/кнопка E не появлялись).
        // Проверки fontSharedMaterial недостаточно (это другой материал), поэтому
        // оборачиваем косметические свойства в try/catch — стиль не критичен, а падать нельзя.
        try
        {
            target.faceColor = source.faceColor;
            target.outlineColor = source.outlineColor;
            target.outlineWidth = source.outlineWidth;
        }
        catch (System.NullReferenceException)
        {
            // Материал ещё не готов — пропускаем обводку/заливку, текст всё равно отрисуется.
        }

        target.enableVertexGradient = source.enableVertexGradient;
        target.colorGradient = source.colorGradient;
        target.colorGradientPreset = source.colorGradientPreset;
        target.alignment = source.alignment;
        target.horizontalAlignment = source.horizontalAlignment;
        target.verticalAlignment = source.verticalAlignment;
        target.textWrappingMode = source.textWrappingMode;
        target.overflowMode = TextOverflowModes.Overflow;
        target.richText = source.richText;

        if (target is TextMeshProUGUI targetUi && source is TextMeshProUGUI sourceUi)
        {
            if (mode == CopyMode.PreserveTargetSize)
                targetUi.margin = sourceUi.margin;

            targetUi.raycastTarget = false;
        }

        switch (mode)
        {
            case CopyMode.MatchReferenceSize:
                target.enableAutoSizing = false;
                target.fontSize = source.fontSize;
                target.fontSizeMin = source.fontSize;
                target.fontSizeMax = source.fontSize;
                break;

            default:
                target.fontSize = savedSize;
                target.fontSizeMin = savedMin;
                target.fontSizeMax = savedMax;
                target.enableAutoSizing = savedAutoSize;
                break;
        }

        if (strikethrough)
        {
            target.fontStyle |= FontStyles.Strikethrough;
            Color c = target.color;
            c.a *= 0.55f;
            target.color = c;
        }
    }
}
