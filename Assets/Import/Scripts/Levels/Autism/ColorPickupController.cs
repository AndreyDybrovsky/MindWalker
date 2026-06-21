using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Подбираемый объект ColorGunObject: открывает возможность красить оружие.
/// При подборе показывает плашку с правилами и управлением; закрывается кнопкой ОК.
/// На время показа курсор виден, игровой ввод заблокирован.
/// </summary>
[RequireComponent(typeof(Collider))]
public class ColorPickupController : MonoBehaviour
{
    [Header("Анимация объекта")]
    [SerializeField] private float spinSpeed = 45f;
    [SerializeField] private float bobHeight = 0.15f;
    [SerializeField] private float bobSpeed = 1.8f;

    [Header("Звук подбора (опционально)")]
    [SerializeField] private AudioClip pickupClip;
    [SerializeField, Range(0f, 1f)] private float pickupVolume = 0.85f;

    [Header("Текст плашки")]
    [TextArea(2, 5)]
    [SerializeField] private string titleText = "Цветное оружие";
    [TextArea(3, 8)]
    [SerializeField] private string rulesText =
        "Подбери цвет оружия под цвет противника.\n\n" +
        "<b>Совпал цвет</b> — наносишь урон.\n" +
        "<b>Не совпал</b> — враг станет сильнее!";
    [TextArea(2, 4)]
    [SerializeField] private string controlsText =
        "Переключение цвета:  <b>Q</b> / <b>E</b>  или  колёсико мыши";

    private bool _collected;
    private Vector3 _startPos;
    private GameObject _panelRoot;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void Start()
    {
        _startPos = transform.position;
    }

    private void Update()
    {
        if (_collected)
            return;

        transform.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.World);

        if (bobHeight > 0f)
        {
            Vector3 p = _startPos;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.position = p;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_collected)
            return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player"))
            return;

        _collected = true;

        if (pickupClip != null)
            AudioSource.PlayClipAtPoint(pickupClip, transform.position, pickupVolume);

        ColorManager.Unlock();
        ShowPanel();

        // Скрыть визуал объекта, но не уничтожать, пока открыта плашка.
        SetVisualsActive(false);
    }

    private void SetVisualsActive(bool active)
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
            if (renderers[i] != null)
                renderers[i].enabled = active;
    }

    private void ShowPanel()
    {
        GameplayInputBlocker.SetBlocked(true);
        GameplayInputBlocker.UnlockCursorForMenu();
        EnsureEventSystem();

        _panelRoot = new GameObject("ColorRulesCanvas");
        Canvas canvas = _panelRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 10070;

        CanvasScaler scaler = _panelRoot.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;
        _panelRoot.AddComponent<GraphicRaycaster>();

        // Тёмный dim-фон
        GameObject dim = CreateChild(_panelRoot.transform, "Dim");
        Image dimImg = dim.AddComponent<Image>();
        dimImg.color = new Color(0.04f, 0.04f, 0.08f, 0.78f);
        StretchFull(dim.GetComponent<RectTransform>());

        // Основная панель
        GameObject panel = CreateChild(_panelRoot.transform, "Panel");
        Image panelImg = panel.AddComponent<Image>();
        panelImg.color = new Color(0.08f, 0.08f, 0.13f, 0.97f);
        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = panelRt.anchorMax = new Vector2(0.5f, 0.5f);
        panelRt.pivot = new Vector2(0.5f, 0.5f);
        panelRt.sizeDelta = new Vector2(860f, 570f);

        // Акцентная полоса сверху (градиент красный→синий→зелёный через три прямоугольника)
        Color[] accentColors = {
            new Color(0.90f, 0.15f, 0.15f, 1f),
            new Color(0.20f, 0.45f, 1.00f, 1f),
            new Color(0.20f, 0.85f, 0.30f, 1f)
        };
        for (int i = 0; i < 3; i++)
        {
            GameObject bar = CreateChild(panel.transform, "Bar" + i);
            Image barImg = bar.AddComponent<Image>();
            barImg.color = accentColors[i];
            RectTransform brt = bar.GetComponent<RectTransform>();
            brt.anchorMin = new Vector2(i / 3f, 1f);
            brt.anchorMax = new Vector2((i + 1) / 3f, 1f);
            brt.pivot = new Vector2(0.5f, 1f);
            brt.offsetMin = new Vector2(i == 0 ? 0f : 1f, -6f);
            brt.offsetMax = new Vector2(i == 2 ? 0f : -1f, 0f);
        }

        // Локализованные строки
        var loc = LocalizationManager.Instance;
        string localTitle    = loc != null ? loc.T("autism.color_pickup.title")    : titleText;
        string localRules    = loc != null ? loc.T("autism.color_pickup.rules")    : rulesText;
        string localControls = loc != null ? loc.T("autism.color_pickup.controls") : controlsText;
        string localOk       = loc != null ? loc.T("autism.color_pickup.ok_button") : "ПОНЯТНО";

        // Заголовок
        CreateLabel(panel.transform, "Title", localTitle, 48f, FontStyles.Bold,
            new Vector2(0f, 210f), new Vector2(800f, 72f), TextAlignmentOptions.Center,
            new Color(0.98f, 0.98f, 0.98f, 1f));

        // Тонкая разделительная линия под заголовком
        GameObject line = CreateChild(panel.transform, "Divider");
        Image lineImg = line.AddComponent<Image>();
        lineImg.color = new Color(1f, 1f, 1f, 0.12f);
        RectTransform lrt = line.GetComponent<RectTransform>();
        lrt.anchorMin = lrt.anchorMax = new Vector2(0.5f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = new Vector2(0f, 148f);
        lrt.sizeDelta = new Vector2(760f, 1f);

        // Три цветных кружка с подписями (иконки цветов)
        string[] colorNames =
        {
            loc != null ? loc.T("autism.color.red").ToUpper()   : "КРАСНЫЙ",
            loc != null ? loc.T("autism.color.blue").ToUpper()  : "СИНИЙ",
            loc != null ? loc.T("autism.color.green").ToUpper() : "ЗЕЛЁНЫЙ"
        };
        float[] dotX = { -230f, 0f, 230f };
        for (int i = 0; i < 3; i++)
        {
            GameObject dot = CreateChild(panel.transform, "Dot" + i);
            Image dotImg = dot.AddComponent<Image>();
            dotImg.color = accentColors[i];
            RectTransform drt = dot.GetComponent<RectTransform>();
            drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
            drt.pivot = new Vector2(0.5f, 0.5f);
            drt.anchoredPosition = new Vector2(dotX[i], 80f);
            drt.sizeDelta = new Vector2(40f, 40f);

            CreateLabel(panel.transform, "DotLabel" + i, colorNames[i], 18f, FontStyles.Bold,
                new Vector2(dotX[i], 46f), new Vector2(120f, 28f), TextAlignmentOptions.Center,
                new Color(accentColors[i].r, accentColors[i].g, accentColors[i].b, 0.85f));
        }

        // Правила
        CreateLabel(panel.transform, "Rules", localRules, 28f, FontStyles.Normal,
            new Vector2(0f, -30f), new Vector2(760f, 130f), TextAlignmentOptions.Center,
            new Color(0.88f, 0.88f, 0.92f, 1f));

        // Управление (мелкий акцентный текст)
        CreateLabel(panel.transform, "Controls", localControls, 24f, FontStyles.Normal,
            new Vector2(0f, -148f), new Vector2(760f, 40f), TextAlignmentOptions.Center,
            new Color(0.55f, 0.78f, 1f, 0.9f));

        // Кнопка ОК
        CreateOkButton(panel.transform, localOk);

        // Анимация появления
        StartCoroutine(AnimatePanelIn(panel.transform, _panelRoot.GetComponent<CanvasGroup>() ?? _panelRoot.AddComponent<CanvasGroup>()));
    }

    private System.Collections.IEnumerator AnimatePanelIn(Transform panel, CanvasGroup group)
    {
        group.alpha = 0f;
        panel.localScale = new Vector3(0.88f, 0.88f, 1f);

        float t = 0f;
        const float dur = 0.32f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            float s = Mathf.SmoothStep(0f, 1f, t);
            group.alpha = s;
            float scale = Mathf.Lerp(0.88f, 1f, s);
            panel.localScale = new Vector3(scale, scale, 1f);
            yield return null;
        }
        group.alpha = 1f;
        panel.localScale = Vector3.one;
    }

    private System.Collections.IEnumerator AnimatePanelOut(System.Action onDone)
    {
        CanvasGroup group = _panelRoot?.GetComponent<CanvasGroup>();
        if (group == null) { onDone?.Invoke(); yield break; }

        float t = 0f;
        const float dur = 0.18f;
        while (t < 1f)
        {
            t += Time.unscaledDeltaTime / dur;
            group.alpha = Mathf.Lerp(1f, 0f, Mathf.SmoothStep(0f, 1f, t));
            yield return null;
        }
        onDone?.Invoke();
    }

    private void CreateOkButton(Transform parent, string label = "ПОНЯТНО")
    {
        GameObject btnGo = CreateChild(parent, "OkButton");
        Image btnImg = btnGo.AddComponent<Image>();
        btnImg.color = new Color(0.18f, 0.42f, 0.72f, 1f);

        RectTransform rt = btnGo.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0f);
        rt.pivot = new Vector2(0.5f, 0f);
        rt.anchoredPosition = new Vector2(0f, 38f);
        rt.sizeDelta = new Vector2(240f, 62f);

        Button btn = btnGo.AddComponent<Button>();
        btn.targetGraphic = btnImg;
        ColorBlock cb = btn.colors;
        cb.normalColor      = new Color(0.18f, 0.42f, 0.72f, 1f);
        cb.highlightedColor = new Color(0.26f, 0.56f, 0.90f, 1f);
        cb.pressedColor     = new Color(0.12f, 0.28f, 0.52f, 1f);
        btn.colors = cb;
        btn.onClick.AddListener(() => StartCoroutine(AnimatePanelOut(FinishClose)));

        CreateLabel(btnGo.transform, "Label", label, 28f, FontStyles.Bold,
            Vector2.zero, new Vector2(240f, 62f), TextAlignmentOptions.Center,
            new Color(0.9f, 0.95f, 1f, 1f));
    }

    private void FinishClose()
    {
        if (_panelRoot != null) Destroy(_panelRoot);
        GameplayInputBlocker.SetBlocked(false);
        GameplayInputBlocker.LockCursorForGameplay();
        Destroy(gameObject);
    }

    private void ClosePanel()
    {
        StartCoroutine(AnimatePanelOut(FinishClose));
    }

    // ── Хелперы UI ────────────────────────────────────────────────────────────

    private static GameObject CreateChild(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return go;
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void CreateLabel(Transform parent, string name, string text, float fontSize,
        FontStyles style, Vector2 anchoredPos, Vector2 size, TextAlignmentOptions align, Color color)
    {
        GameObject go = CreateChild(parent, name);
        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.color = color;
        label.alignment = align;
        label.richText = true;
        label.enableWordWrapping = true;
        label.raycastTarget = false;

        RectTransform rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta = size;
    }

    private static void EnsureEventSystem()
    {
        if (FindFirstObjectByType<EventSystem>() != null)
            return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.3f, 0.4f);
        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif
}
