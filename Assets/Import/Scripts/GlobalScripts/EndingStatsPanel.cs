using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панель финальной статистики — появляется справа, каждый пункт выезжает справа налево.
///
/// Настройка в инспекторе:
///   Header Template — любой TMP объект из сцены, чей шрифт/размер/цвет возьмётся для заголовка.
///   Item Template   — то же для строк статистики.
///   Назначь оба поля, остальное создаётся автоматически.
/// </summary>
public class EndingStatsPanel : MonoBehaviour
{
    [Header("Шаблоны стиля (берутся шрифт, размер, цвет)")]
    [Tooltip("TMP объект из сцены — его стиль применится к надписи СТАТИСТИКА.")]
    [SerializeField] private TextMeshProUGUI headerTemplate;
    [Tooltip("TMP объект из сцены — его стиль применится ко всем строкам статистики.")]
    [SerializeField] private TextMeshProUGUI itemTemplate;

    [Header("Анимация")]
    [SerializeField] private float slideDistance     = 360f;
    [SerializeField] private float itemSlideDuration = 0.38f;
    [SerializeField] private float delayBetweenItems = 0.10f;
    [SerializeField] private float pauseAfterHeader  = 0.28f;

    [Header("Расположение (правая сторона экрана)")]
    [Tooltip("Ширина панели как доля ширины экрана (0–1).")]
    [SerializeField, Range(0.2f, 0.6f)] private float panelWidthFraction = 0.36f;
    [Tooltip("Отступ снизу как доля высоты экрана.")]
    [SerializeField, Range(0f, 0.5f)]   private float panelBottomFraction = 0.1f;
    [Tooltip("Отступ сверху как доля высоты экрана.")]
    [SerializeField, Range(0f, 0.5f)]   private float panelTopFraction    = 0.15f;
    [SerializeField] private float rowHeight      = 38f;
    [SerializeField] private float headerHeight   = 54f;
    [SerializeField] private float separatorHeight = 20f;
    [SerializeField] private float rowSpacing     = 4f;

    // ─── Приватные поля ────────────────────────────────────────────────────

    private CanvasGroup _rootGroup;
    private Transform   _container;
    private float       _nextY;

    // ─── Публичный API ─────────────────────────────────────────────────────

    /// <summary>Анимирует все пункты один за другим. yield return statsPanel.PlayEntrance();</summary>
    public IEnumerator PlayEntrance()
    {
        BuildCanvas();

        // Заголовок
        var (hTmp, hRt, hPos) = AddRow(TL("ending.stats.header", "СТАТИСТИКА"), headerHeight, isHeader: true);
        yield return SlideIn(hRt, hTmp, hPos);
        yield return new WaitForSeconds(pauseAfterHeader);

        // Разделитель (берёт стиль item, но меньший размер и серый цвет)
        var (sTmp, sRt, sPos) = AddRow("──────────────────────────────────", separatorHeight, isHeader: false);
        if (sTmp != null)
        {
            sTmp.enableAutoSizing = false;  // без этого Auto Size перебьёт наш fontSize
            sTmp.fontSize  = Mathf.Max(10f, (itemTemplate != null ? itemTemplate.fontSize : 18f) * 0.7f);
            sTmp.color     = new Color(0.45f, 0.45f, 0.45f, 1f);
        }
        yield return SlideIn(sRt, sTmp, sPos);

        // Строки статистики
        foreach ((string label, string value) in GetStatRows())
        {
            string rowText = $"{label}:   {value}";
            var (rTmp, rRt, rPos) = AddRow(rowText, rowHeight, isHeader: false);
            yield return SlideIn(rRt, rTmp, rPos);
            yield return new WaitForSeconds(delayBetweenItems);
        }
    }

    /// <summary>Задаёт шаблоны из кода (используется SimpleEndingCutscene).</summary>
    public void SetTemplatesFromCode(TextMeshProUGUI header, TextMeshProUGUI item)
    {
        if (header != null) headerTemplate = header;
        if (item   != null) itemTemplate   = item;
    }

    /// <summary>Используется TrueEndingCutscene для одновременного затухания всего.</summary>
    public void SetRootAlpha(float alpha)
    {
        if (_rootGroup != null)
            _rootGroup.alpha = alpha;
    }

    // ─── Построение Canvas ─────────────────────────────────────────────────

    private void BuildCanvas()
    {
        _nextY = 0f;

        GameObject canvasGo = new GameObject("StatsPanelCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 205;

        _rootGroup = canvasGo.AddComponent<CanvasGroup>();

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode     = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight  = 0.5f;

        // Контейнер — правая часть экрана, якорь по правому краю сверху вниз
        GameObject containerGo = new GameObject("StatsContainer");
        containerGo.transform.SetParent(canvasGo.transform, false);
        RectTransform cr = containerGo.AddComponent<RectTransform>();
        cr.anchorMin        = new Vector2(1f - panelWidthFraction, panelBottomFraction);
        cr.anchorMax        = new Vector2(1f,                       1f - panelTopFraction);
        cr.offsetMin        = Vector2.zero;
        cr.offsetMax        = Vector2.zero;
        cr.pivot            = new Vector2(0.5f, 1f);

        _container = containerGo.transform;
    }

    // ─── Добавление строки ─────────────────────────────────────────────────

    private (TextMeshProUGUI tmp, RectTransform rt, Vector2 finalPos) AddRow(
        string text, float heightHint, bool isHeader)
    {
        GameObject go = new GameObject("StatsRow");
        go.transform.SetParent(_container, false);

        TextMeshProUGUI tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text     = text;
        tmp.alpha    = 0f;
        tmp.richText = true;

        ApplyTemplate(tmp, isHeader);

        // Высота строки = максимум из подсказки и размера шрифта * коэффициент
        float height = Mathf.Max(heightHint, tmp.fontSize * 2.2f);

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot     = new Vector2(0f, 1f);
        rt.sizeDelta = new Vector2(0f, height);

        Vector2 finalPos = new Vector2(0f, _nextY);
        rt.anchoredPosition = finalPos;
        _nextY -= height + rowSpacing;

        return (tmp, rt, finalPos);
    }

    // ─── Копирование стиля из шаблона ──────────────────────────────────────

    private void ApplyTemplate(TextMeshProUGUI target, bool isHeader)
    {
        TextMeshProUGUI src = isHeader ? headerTemplate : itemTemplate;

        if (src == null)
        {
            target.fontSize  = isHeader ? 28f : 18f;
            target.color     = isHeader ? new Color(1f, 0.85f, 0.25f) : Color.white;
            target.alignment = isHeader
                ? TextAlignmentOptions.Center
                : TextAlignmentOptions.Left;
            return;
        }

        // Сначала шрифт — без материала, чтобы TMP создал свежий для нашего Canvas
        target.font = src.font;

        // enableAutoSizing ставим ДО fontSize, иначе TMP может перебить значение своим Auto
        target.enableAutoSizing = src.enableAutoSizing;
        target.fontSizeMin      = src.fontSizeMin;
        target.fontSizeMax      = src.fontSizeMax;
        target.fontSize         = src.fontSize;

        target.fontStyle        = src.fontStyle;
        target.color            = src.color;
        target.alignment        = src.alignment;
        target.characterSpacing = src.characterSpacing;
        target.wordSpacing      = src.wordSpacing;
        target.lineSpacing      = src.lineSpacing;
        target.paragraphSpacing = src.paragraphSpacing;
        target.overflowMode     = TextOverflowModes.Overflow;
    }

    // ─── Анимация выезда ───────────────────────────────────────────────────

    private IEnumerator SlideIn(RectTransform rt, TextMeshProUGUI tmp, Vector2 finalPos)
    {
        if (rt == null || tmp == null)
            yield break;

        Vector2 startPos = finalPos + new Vector2(slideDistance, 0f);
        rt.anchoredPosition = startPos;
        tmp.alpha = 0f;

        float elapsed = 0f;
        while (elapsed < itemSlideDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / itemSlideDuration));
            rt.anchoredPosition = Vector2.Lerp(startPos, finalPos, t);
            tmp.alpha = t;
            yield return null;
        }

        rt.anchoredPosition = finalPos;
        tmp.alpha = 1f;
    }

    // ─── Данные статистики ─────────────────────────────────────────────────

    private static IEnumerable<(string label, string value)> GetStatRows()
    {
        GameStatsTracker t = GameStatsTracker.Instance;
        if (t == null)
        {
            yield return (TL("ending.stats.no_data", "Данные недоступны"), "—");
            yield break;
        }

        GameStatsData s = t.Stats;
        yield return (TL("ending.stats.playtime",        "Время прохождения"),    GameStatsTracker.FormatTime(s.totalPlayTimeSeconds));
        yield return (TL("ending.stats.damage_received", "Получено урона"),       $"{Mathf.RoundToInt(s.damageReceived)}");
        yield return (TL("ending.stats.damage_dealt",    "Нанесено урона"),       $"{Mathf.RoundToInt(s.damageDealt)}");
        yield return (TL("ending.stats.enemies_killed",  "Убито врагов"),         $"{s.enemiesKilled}");
        yield return (TL("ending.stats.slot_caught",     "Слот-машина поймала"),  InflectL(s.slotMachineCaught));
        yield return (TL("ending.stats.patients_calmed", "Успокоений пациентки"), InflectL(s.patientCalmedCount));
        yield return (TL("ending.stats.fastest_level",   "Быстрый уровень"),      t.GetFastestLevelDisplay());
        yield return (TL("ending.stats.slowest_level",   "Долгий уровень"),       t.GetSlowestLevelDisplay());
    }

    private static string TL(string key, string fallback)
    {
        LocalizationManager loc = LocalizationManager.Instance;
        if (loc == null) return fallback;
        string val = loc.T(key);
        return (string.IsNullOrEmpty(val) || val == key) ? fallback : val;
    }

    private static string InflectL(int n)
    {
        string f1 = TL("ending.stats.times_1", "раз");
        string f2 = TL("ending.stats.times_2", "раза");
        string f5 = TL("ending.stats.times_5", "раз");
        return Inflect(n, f1, f2, f5);
    }

    private static string Inflect(int n, string f1, string f2, string f5)
    {
        int m10 = Mathf.Abs(n) % 10, m100 = Mathf.Abs(n) % 100;
        string f = (m100 >= 11 && m100 <= 19) ? f5
                 : m10 == 1                   ? f1
                 : (m10 >= 2 && m10 <= 4)     ? f2
                                               : f5;
        return $"{n} {f}";
    }
}
