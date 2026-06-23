using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class FirstPlayTutorial : MonoBehaviour
{
    private const string PrefKey = "tutorial_shown";
    private static FirstPlayTutorial _instance;
    private GameObject _panel;
    private CanvasGroup _panelGroup;

    // Общий шрифт игры (horta), берётся с уже существующего TMP в сцене.
    private static TMP_FontAsset s_gameFont;

    // Единая палитра под стиль игры.
    private static readonly Color AccentColor = new Color(0.91f, 0.70f, 0.25f, 1f);   // тёплый янтарь
    private static readonly Color PanelColor = new Color(0.06f, 0.06f, 0.08f, 0.98f);
    private static readonly Color BorderColor = new Color(0.91f, 0.70f, 0.25f, 0.55f);

    private static string Accent(string text) => $"<color=#E8B23E>{text}</color>";

    private static TMP_FontAsset ResolveGameFont()
    {
        TextMeshProUGUI[] all = FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        TMP_FontAsset fallback = null;
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] == null || all[i].font == null)
                continue;

            fallback ??= all[i].font;

            string n = all[i].gameObject.name;
            if (n == "QuestText" || n.Contains("PressE") || n.Contains("Quest"))
                return all[i].font;
        }

        return fallback;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        // Инстанс создаётся всегда и живёт всю сессию (DDOL), чтобы показывать
        // панель при входе в Main и после сброса флага (New Game).
        // Решение «показывать или нет» принимается в TryShowForScene по флагу.
        GameObject host = new GameObject(nameof(FirstPlayTutorial));
        _instance = host.AddComponent<FirstPlayTutorial>();
        DontDestroyOnLoad(host);
        _instance.TryShowForScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>Сбросить флаг, чтобы панель управления снова показалась (вызывается при New Game).</summary>
    public static void ResetForNewGame()
    {
        PlayerPrefs.DeleteKey(PrefKey);
        PlayerPrefs.Save();
    }

    /// <summary>Показать панель управления по запросу (кнопка паузы / клавиша F1), без учёта флага.</summary>
    public static void ShowOnDemand()
    {
        EnsureInstance();
        _instance.ShowImmediate();
    }

    private static void EnsureInstance()
    {
        if (_instance != null)
            return;

        GameObject host = new GameObject(nameof(FirstPlayTutorial));
        _instance = host.AddComponent<FirstPlayTutorial>();
        DontDestroyOnLoad(host);
    }

    private void ShowImmediate()
    {
        if (_panel != null)
            return;

        ShowPanel();
        StartCoroutine(FadePanelIn(0.25f));
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        TryShowForScene(scene.name);
    }

    private void TryShowForScene(string sceneName)
    {
        if (sceneName != "Main")
            return;

        if (PlayerPrefs.GetInt(PrefKey, 0) != 0)
            return;

        if (_panel != null)
            return;

        StartCoroutine(ShowAfterFade());
    }

    private IEnumerator ShowAfterFade()
    {
        // Ждём, пока завершится входной fade сцены...
        while (LevelEntryScreenFade.IsFading)
            yield return null;

        // ...и пока экранное затемнение полностью раскроется (иначе панель
        // окажется под чёрным слоем FadeCanvas и её не будет видно).
        GameObject fadeCanvas = GameObject.Find("FadeCanvas");
        if (fadeCanvas != null && fadeCanvas.TryGetComponent(out CanvasGroup screenFade))
        {
            while (screenFade != null && screenFade.alpha > 0.05f)
                yield return null;
        }

        yield return new WaitForSecondsRealtime(0.2f);
        ShowPanel();
        yield return FadePanelIn(0.35f);
    }

    private IEnumerator FadePanelIn(float duration)
    {
        if (_panelGroup == null)
            yield break;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            _panelGroup.alpha = Mathf.Clamp01(t / duration);
            yield return null;
        }

        _panelGroup.alpha = 1f;
    }

    private void ShowPanel()
    {
        if (_panel != null)
            return;

        s_gameFont = ResolveGameFont();

        var canvasGo = new GameObject("TutorialCanvas");
        canvasGo.transform.SetParent(transform);

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Выше экранного затемнения FadeCanvas (32700), чтобы панель не пряталась под ним.
        canvas.sortingOrder = 32760;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasGo.AddComponent<GraphicRaycaster>();

        // Группа для плавного появления всей панели.
        _panelGroup = canvasGo.AddComponent<CanvasGroup>();
        _panelGroup.alpha = 0f;

        // Dark overlay
        var overlay = MakeImage(canvasGo.transform, "Overlay", new Color(0f, 0f, 0f, 0.8f));
        var overlayRect = overlay.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = overlayRect.offsetMax = Vector2.zero;

        // Panel box
        var panelGo = new GameObject("Panel");
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImg = panelGo.AddComponent<Image>();
        panelImg.color = PanelColor;
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(720f, 540f);

        // Тонкая янтарная рамка (фон-подложка чуть больше панели)
        var border = MakeImage(canvasGo.transform, "PanelBorder", BorderColor);
        var borderRect = border.GetComponent<RectTransform>();
        borderRect.anchorMin = new Vector2(0.5f, 0.5f);
        borderRect.anchorMax = new Vector2(0.5f, 0.5f);
        borderRect.pivot = new Vector2(0.5f, 0.5f);
        borderRect.sizeDelta = panelRect.sizeDelta + new Vector2(4f, 4f);
        border.transform.SetSiblingIndex(panelGo.transform.GetSiblingIndex());

        // Title
        MakeTmp(panelGo.transform, "Title", "УПРАВЛЕНИЕ", 34f, FontStyles.Bold, AccentColor,
            TextAlignmentOptions.Center,
            anchorMin: new Vector2(0f, 1f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 1f),
            anchoredPos: new Vector2(0f, -30f),
            sizeDelta: new Vector2(-40f, 46f),
            characterSpacing: 6f);

        // Separator line
        var sep = MakeImage(panelGo.transform, "Sep", BorderColor);
        var sepRect = sep.GetComponent<RectTransform>();
        sepRect.anchorMin = new Vector2(0f, 1f);
        sepRect.anchorMax = new Vector2(1f, 1f);
        sepRect.pivot = new Vector2(0.5f, 1f);
        sepRect.anchoredPosition = new Vector2(0f, -78f);
        sepRect.sizeDelta = new Vector2(-48f, 2f);

        // Controls list
        string controls =
            $"{Accent("W A S D")}            Движение\n" +
            $"{Accent("Мышь")}               Обзор\n" +
            $"{Accent("Shift (левый)")}       Бег\n" +
            $"{Accent("Ctrl (левый)")}        Присесть\n" +
            $"{Accent("ЛКМ")}                Выстрел\n" +
            $"{Accent("Tab")}                Часы (HP / враги на уровне)\n" +
            $"{Accent("E")}                  Взаимодействие / подобрать предмет\n" +
            $"{Accent("Esc")}                Меню паузы";

        MakeTmp(panelGo.transform, "Controls", controls, 20f, FontStyles.Normal, Color.white,
            TextAlignmentOptions.Left,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 1f),
            pivot: new Vector2(0.5f, 0.5f),
            anchoredPos: new Vector2(0f, 24f),
            sizeDelta: new Vector2(-90f, -160f),
            lineSpacing: 18f);

        // Hint
        MakeTmp(panelGo.transform, "Hint",
            $"Нажмите  {Accent("E")} / {Accent("Пробел")} / {Accent("Enter")},  чтобы закрыть",
            15f, FontStyles.Normal, new Color(0.70f, 0.70f, 0.72f, 1f),
            TextAlignmentOptions.Center,
            anchorMin: new Vector2(0f, 0f), anchorMax: new Vector2(1f, 0f),
            pivot: new Vector2(0.5f, 0f),
            anchoredPos: new Vector2(0f, 66f),
            sizeDelta: new Vector2(-40f, 26f));

        // Button
        var btnGo = new GameObject("CloseBtn");
        btnGo.transform.SetParent(panelGo.transform, false);
        var btnImg = btnGo.AddComponent<Image>();
        btnImg.color = AccentColor;
        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(0.5f, 0f);
        btnRect.anchorMax = new Vector2(0.5f, 0f);
        btnRect.pivot = new Vector2(0.5f, 0f);
        btnRect.anchoredPosition = new Vector2(0f, 18f);
        btnRect.sizeDelta = new Vector2(220f, 48f);

        var btnLabelGo = new GameObject("Label");
        btnLabelGo.transform.SetParent(btnGo.transform, false);
        var label = btnLabelGo.AddComponent<TextMeshProUGUI>();
        if (s_gameFont != null)
            label.font = s_gameFont;
        label.text = "Понятно!";
        label.fontSize = 20f;
        label.fontStyle = FontStyles.Bold;
        label.color = new Color(0.05f, 0.04f, 0.02f);
        label.alignment = TextAlignmentOptions.Center;
        var labelRect = btnLabelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = labelRect.offsetMax = Vector2.zero;

        var btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        btn.onClick.AddListener(Close);

        _panel = canvasGo;
    }

    private void Update()
    {
        if (_panel == null)
        {
            // F1 — открыть справку по управлению в любой момент.
            if (Input.GetKeyDown(KeyCode.F1))
                ShowImmediate();
            return;
        }

        if (Input.GetKeyDown(KeyCode.E) || Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.Return))
            Close();
    }

    private void Close()
    {
        if (_panel == null)
            return;

        PlayerPrefs.SetInt(PrefKey, 1);
        PlayerPrefs.Save();

        Destroy(_panel);
        _panel = null;
        _panelGroup = null;

        // Инстанс НЕ уничтожаем: он остаётся жить (DDOL) и снова покажет панель
        // при следующем входе в Main после ResetForNewGame().
    }

    private static GameObject MakeImage(Transform parent, string name, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<Image>().color = color;
        return go;
    }

    private static void MakeTmp(Transform parent, string name, string text,
        float fontSize, FontStyles style, Color color, TextAlignmentOptions alignment,
        Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        Vector2 anchoredPos, Vector2 sizeDelta,
        float characterSpacing = 0f, float lineSpacing = 0f)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        if (s_gameFont != null)
            tmp.font = s_gameFont;
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.characterSpacing = characterSpacing;
        tmp.lineSpacing = lineSpacing;
        tmp.enableWordWrapping = false;
        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = sizeDelta;
    }
}
