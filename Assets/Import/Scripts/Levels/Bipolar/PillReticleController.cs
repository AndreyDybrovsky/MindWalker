using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Круглый прицел для подбора таблеток в Bipolar Location3.
/// Рейкастит с центра камеры; при наведении на PillBottlePickup показывает
/// подсказку "[E] Поднять" и обрабатывает нажатие E.
/// Скрыт по умолчанию — активируется через Show() из PillCollectionManager.
/// </summary>
public class PillReticleController : MonoBehaviour
{
    [SerializeField] private float  interactionRadius = 2.5f;
    [SerializeField] private string promptText        = "Поднять [E]";

    private Canvas           _canvas;
    private Image            _ring;
    private GameObject       _promptBg;
    private PillBottlePickup _aimed;

    private static readonly Color ColIdle   = new Color(1f, 1f,    1f,    0.70f);
    private static readonly Color ColActive = new Color(1f, 0.92f, 0.35f, 0.95f);

    private void Awake()
    {
        BuildUI();
        _canvas.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (_canvas == null || !_canvas.gameObject.activeSelf) return;

        _aimed = DetectPill();

        _ring.color = _aimed != null ? ColActive : ColIdle;
        _promptBg.SetActive(_aimed != null);

        if (_aimed != null && Input.GetKeyDown(KeyCode.E))
            _aimed.TryCollect();
    }

    public void Show() { if (_canvas != null) _canvas.gameObject.SetActive(true); }
    public void Hide() { if (_canvas != null) _canvas.gameObject.SetActive(false); }

    // ── Детекция ────────────────────────────────────────────────────────────

    private PillBottlePickup DetectPill()
    {
        Transform player = PlayerInteractionZone.GetPlayerBodyTransform();
        if (player == null) return null;

        Collider[] hits = Physics.OverlapSphere(
            player.position, interactionRadius, ~0, QueryTriggerInteraction.Collide);

        PillBottlePickup best     = null;
        float            bestDist = float.MaxValue;

        foreach (Collider c in hits)
        {
            PillBottlePickup p = c.GetComponentInParent<PillBottlePickup>();
            if (p == null || p.IsCollected) continue;

            float dist = Vector3.Distance(player.position, p.transform.position);
            if (dist < bestDist) { bestDist = dist; best = p; }
        }

        return best;
    }

    // ── Построение UI ────────────────────────────────────────────────────────

    private void BuildUI()
    {
        // Canvas
        var canvasGo = new GameObject("PillReticleCanvas");
        canvasGo.transform.SetParent(transform, false);

        _canvas              = canvasGo.AddComponent<Canvas>();
        _canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 10045;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode         = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight  = 0.5f;

        // Кольцо-прицел по центру
        var ringGo = new GameObject("Ring");
        ringGo.transform.SetParent(canvasGo.transform, false);

        var ringRt = ringGo.AddComponent<RectTransform>();
        ringRt.anchorMin        = ringRt.anchorMax = new Vector2(0.5f, 0.5f);
        ringRt.anchoredPosition = Vector2.zero;
        ringRt.sizeDelta        = new Vector2(34f, 34f);

        _ring              = ringGo.AddComponent<Image>();
        _ring.sprite       = CreateRingSprite(64, 4);
        _ring.color        = ColIdle;
        _ring.raycastTarget = false;

        // Подсказка: тёмная подложка + текст
        _promptBg = new GameObject("PromptBg");
        _promptBg.transform.SetParent(canvasGo.transform, false);

        var bgRt = _promptBg.AddComponent<RectTransform>();
        bgRt.anchorMin        = bgRt.anchorMax = new Vector2(0.5f, 0.5f);
        bgRt.anchoredPosition = new Vector2(0f, -32f);
        bgRt.sizeDelta        = new Vector2(200f, 34f);

        var bgImg = _promptBg.AddComponent<Image>();
        bgImg.color        = new Color(0f, 0f, 0f, 0.50f);
        bgImg.raycastTarget = false;

        var textGo = new GameObject("PromptText");
        textGo.transform.SetParent(_promptBg.transform, false);

        var textRt = textGo.AddComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(6f,  2f);
        textRt.offsetMax = new Vector2(-6f, -2f);

        var label = textGo.AddComponent<TextMeshProUGUI>();
        label.text       = promptText;
        label.fontSize   = 21f;
        label.fontStyle  = FontStyles.Bold;
        label.color      = Color.white;
        label.alignment  = TextAlignmentOptions.Center;
        label.raycastTarget    = false;
        label.enableWordWrapping = false;

        _promptBg.SetActive(false);
    }

    // ── Генерация кольца-спрайта ────────────────────────────────────────────

    private static Sprite CreateRingSprite(int size, int thickness)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.filterMode = FilterMode.Bilinear;

        float cx     = size * 0.5f;
        float cy     = size * 0.5f;
        float outerR = size * 0.5f - 1f;
        float innerR = outerR - thickness;

        var pixels = new Color32[size * size];
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float dx    = x - cx, dy = y - cy;
            float d     = Mathf.Sqrt(dx * dx + dy * dy);
            float alpha = Mathf.Clamp01(Mathf.Min(outerR - d, d - innerR));
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }

        tex.SetPixels32(pixels);
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
    }
}
