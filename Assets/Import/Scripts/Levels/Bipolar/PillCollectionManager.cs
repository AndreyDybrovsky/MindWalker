using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Менеджер сбора таблеток в уровне Bipolar.
/// Показывает задание «Собери лекарство: X/6» слева сверху.
/// После сбора всех — затемнение, телепорт в GoodWorld, освещение.
/// </summary>
public class PillCollectionManager : MonoBehaviour
{
    private static PillCollectionManager s_instance;

    [Header("Сбор")]
    [SerializeField] private int totalPills = 6;

    [Header("Возврат в мирную локацию")]
    [SerializeField] private Transform goodWorldSpawnPoint;
    [Tooltip("Продолжительность затемнения при уходе.")]
    [SerializeField] private float fadeOutDuration = 0.9f;
    [Tooltip("Продолжительность проявления в мирной локации.")]
    [SerializeField] private float fadeInDuration  = 1.3f;

    [Header("Звук завершения (опционально)")]
    [SerializeField] private AudioClip allCollectedClip;
    [SerializeField, Range(0f, 1f)] private float allCollectedVolume = 1f;

    [Header("UI (стиль по образцу)")]
    [Tooltip("TMP-образец для стиля текста — QuestText с HUD игрока. Можно оставить пустым.")]
    [SerializeField] private TMP_Text styleReference;

    private int         _collected;
    private bool        _allDone;
    private CanvasGroup _fadeCanvasGroup;
    private TMP_Text    _questText;
    private Canvas      _questCanvas;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatic() => s_instance = null;

    private void Awake()
    {
        s_instance = this;
        BuildQuestUI();
    }

    private void Start()
    {
        _fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();

        if (styleReference == null)
            styleReference = FindQuestTextReference();

        if (styleReference != null && _questText != null)
            ApplyStyle(styleReference, _questText);

        UpdateUI();
        HideQuestUI(); // скрыть до входа в Location3
    }

    private void OnDestroy()
    {
        if (s_instance == this)
            s_instance = null;
    }

    // ── Публичный API ─────────────────────────────────────────────────────────

    public static void ShowQuest()
    {
        if (s_instance != null)
            s_instance.ShowQuestUI();
    }

    public static void RegisterPickup()
    {
        if (s_instance != null)
            s_instance.OnPickup();
        else
            Debug.LogWarning("PillCollectionManager: нет активного экземпляра.");
    }

    // ── Логика ───────────────────────────────────────────────────────────────

    private void OnPickup()
    {
        if (_allDone) return;

        _collected = Mathf.Min(_collected + 1, totalPills);
        UpdateUI();

        if (_collected >= totalPills)
        {
            _allDone = true;
            StartCoroutine(AllCollectedRoutine());
        }
    }

    private IEnumerator AllCollectedRoutine()
    {
        // Короткая пауза — игрок видит финальный счёт
        yield return new WaitForSeconds(0.6f);

        GameplayInputBlocker.SetBlocked(true);
        PlayerInteractionZone.SetPlayerControlLocked(true);

        if (allCollectedClip != null && Camera.main != null)
            AudioSource.PlayClipAtPoint(allCollectedClip, Camera.main.transform.position, allCollectedVolume);

        // Затемнение
        if (_fadeCanvasGroup != null && fadeOutDuration > 0.001f)
            yield return ScreenFadeRunner.FadeToBlack(fadeOutDuration, _fadeCanvasGroup);

        // Переключить пост-обработку на Happy
        BipolarMindscapeController ctrl = FindFirstObjectByType<BipolarMindscapeController>();
        if (ctrl != null)
            ctrl.ApplyModeImmediate(BipolarMindscapeMode.Meadow);

        // Телепорт в GoodWorld
        if (goodWorldSpawnPoint != null)
            PlayerTeleportUtility.TeleportTo(goodWorldSpawnPoint, matchRotation: true);
        else
            Debug.LogWarning("PillCollectionManager: goodWorldSpawnPoint не задан!");

        yield return null;
        Physics.SyncTransforms();

        // Скрыть UI задания перед проявлением
        HideQuestUI();

        // Проявление
        if (_fadeCanvasGroup != null && fadeInDuration > 0.001f)
            yield return ScreenFadeRunner.FadeFromBlack(fadeInDuration, _fadeCanvasGroup);

        PlayerInteractionZone.SetPlayerControlLocked(false);
        GameplayInputBlocker.SetBlocked(false);
    }

    // ── UI ────────────────────────────────────────────────────────────────────

    private void UpdateUI()
    {
        if (_questText != null)
            _questText.text = $"Собери лекарство: {_collected}/{totalPills}";
    }

    public void ShowQuestUI()
    {
        if (_questCanvas != null)
            _questCanvas.gameObject.SetActive(true);
    }

    private void HideQuestUI()
    {
        if (_questCanvas != null)
            _questCanvas.gameObject.SetActive(false);
    }

    private void BuildQuestUI()
    {
        // Корневой Canvas
        GameObject canvasGo = new GameObject("PillQuestCanvas");
        canvasGo.transform.SetParent(transform, false);

        _questCanvas                = canvasGo.AddComponent<Canvas>();
        _questCanvas.renderMode     = RenderMode.ScreenSpaceOverlay;
        _questCanvas.sortingOrder   = 10050;

        CanvasScaler scaler         = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode          = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution  = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight   = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        // Тёмная полупрозрачная подложка для читаемости
        GameObject bgGo = new GameObject("Background");
        bgGo.transform.SetParent(canvasGo.transform, false);
        Image bgImage        = bgGo.AddComponent<Image>();
        bgImage.color        = new Color(0f, 0f, 0f, 0.45f);
        bgImage.raycastTarget = false;

        RectTransform bgRt   = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin       = new Vector2(0f, 1f);
        bgRt.anchorMax       = new Vector2(0f, 1f);
        bgRt.pivot           = new Vector2(0f, 1f);
        bgRt.anchoredPosition = new Vector2(12f, -12f);
        bgRt.sizeDelta       = new Vector2(370f, 52f);

        // Текст задания
        GameObject textGo = new GameObject("QuestLabel");
        textGo.transform.SetParent(bgGo.transform, false);

        RectTransform textRt    = textGo.AddComponent<RectTransform>();
        textRt.anchorMin        = Vector2.zero;
        textRt.anchorMax        = Vector2.one;
        textRt.offsetMin        = new Vector2(10f,  4f);
        textRt.offsetMax        = new Vector2(-10f, -4f);

        _questText              = textGo.AddComponent<TextMeshProUGUI>();
        _questText.fontSize     = 26f;
        _questText.fontStyle    = FontStyles.Bold;
        _questText.color        = Color.white;
        _questText.alignment    = TextAlignmentOptions.MidlineLeft;
        _questText.enableWordWrapping = false;
        _questText.raycastTarget = false;
    }

    private static void ApplyStyle(TMP_Text src, TMP_Text dst)
    {
        if (src == null || dst == null) return;
        TmpTextStyleUtility.CopyStyle(dst, src, TmpTextStyleUtility.CopyMode.MatchReferenceSize);
        dst.alignment = TextAlignmentOptions.MidlineLeft;
    }

    private static TMP_Text FindQuestTextReference()
    {
        GameObject named = GameObject.Find("QuestText");
        if (named != null && named.TryGetComponent(out TMP_Text t))
            return t;
        return null;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (goodWorldSpawnPoint != null)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawSphere(goodWorldSpawnPoint.position, 0.4f);
            Gizmos.DrawLine(transform.position, goodWorldSpawnPoint.position);
        }
    }
#endif
}
