using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Хитмаркер: короткая вспышка «X» в центре экрана при попадании по врагу.
/// DDOL, создаётся автоматически. Вызывать <see cref="Show"/> из логики попадания пули.
/// </summary>
public class HitmarkerUI : MonoBehaviour
{
    private static HitmarkerUI _instance;

    private CanvasGroup _group;
    private float _life;
    private const float Duration = 0.18f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        GameObject host = new GameObject(nameof(HitmarkerUI));
        DontDestroyOnLoad(host);
        _instance = host.AddComponent<HitmarkerUI>();
        _instance.Build();
    }

    /// <summary>Показать хитмаркер (попадание по врагу).</summary>
    public static void Show()
    {
        if (_instance == null)
            return;

        _instance._life = Duration;
        if (_instance._group != null)
            _instance._group.alpha = 1f;
    }

    private void Build()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 30000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        _group = gameObject.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.interactable = false;
        _group.blocksRaycasts = false;

        // Четыре коротких штриха «X» вокруг центра.
        Color c = new Color(1f, 1f, 1f, 0.9f);
        MakeStroke(45f, new Vector2(14f, 14f), c);
        MakeStroke(-45f, new Vector2(-14f, 14f), c);
        MakeStroke(45f, new Vector2(-14f, -14f), c);
        MakeStroke(-45f, new Vector2(14f, -14f), c);
    }

    private void MakeStroke(float angle, Vector2 offset, Color color)
    {
        var go = new GameObject("Stroke");
        go.transform.SetParent(transform, false);

        var img = go.AddComponent<Image>();
        img.color = color;
        img.raycastTarget = false;

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(14f, 3f);
        rt.anchoredPosition = offset;
        rt.localRotation = Quaternion.Euler(0f, 0f, angle);
    }

    private void Update()
    {
        if (_life <= 0f)
            return;

        _life -= Time.unscaledDeltaTime;
        if (_group != null)
            _group.alpha = Mathf.Clamp01(_life / Duration);
    }
}
