using System.Collections;
using Unity.Cinemachine;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Истинная концовка — Cinemachine 3.x.
///
/// Поля в инспекторе:
///   Brain          → CinemachineBrain на Main Camera
///   Doc Close VC   → CutSceneCamera/DocCamStart/CinemachineCamera
///   Doc Far VC     → CutSceneCamera/DocCamEnd/CinemachineCamera (4)
///   Face VC        → CutSceneCamera/FaceCamStart/CinemachineCamera (1)
///   Face Mid VC    → CutSceneCamera/FaceCamEnd/CinemachineCamera (2)
///   Peephole VC    → CutSceneCamera/PeepholeCamEnd/CinemachineCamera (3)
/// </summary>
public class TrueEndingCutscene : MonoBehaviour
{
    [Header("Cinemachine Brain (компонент на Main Camera)")]
    [SerializeField] private CinemachineBrain brain;

    [Header("Виртуальные камеры — Фаза 1: Документы")]
    [Tooltip("Вплотную к бумагам на столе.")]
    [SerializeField] private CinemachineCamera docCloseVC;
    [Tooltip("Отъезд — виден весь стол с документами.")]
    [SerializeField] private CinemachineCamera docFarVC;
    [SerializeField] private float docPullBackDuration = 5f;
    [SerializeField] private float holdAfterDocPullBack = 0.3f;

    [Header("Виртуальные камеры — Фаза 2: Лицо → щеколда")]
    [Tooltip("Резкий кат: крупный план лица пациента.")]
    [SerializeField] private CinemachineCamera faceVC;
    [Tooltip("Промежуточная: отъехала от лица, внутри комнаты.")]
    [SerializeField] private CinemachineCamera faceMidVC;
    [Tooltip("Финал: снаружи двери, смотрит в щеколду.")]
    [SerializeField] private CinemachineCamera peepholeVC;
    [SerializeField] private float delayBeforeFaceCam = 1.5f;
    [SerializeField] private float facePullBackDuration = 7f;
    [SerializeField] private float faceMidToPeepholeDuration = 3f;

    [Header("Эффект щеколды")]
    [Tooltip("Материал с шейдером Custom/PeepholeVignette.")]
    [SerializeField] private Material peepholeMaterial;
    [SerializeField] private float peepholeStartRadius = 0.52f;
    [SerializeField] private float peepholeSoftness = 0.04f;
    [SerializeField] private float peepholeDuration = 2.5f;

    [Header("Текст")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI subtitleText;
    [SerializeField] private float textFadeInDuration = 1.2f;
    [SerializeField] private float delayBetweenTexts = 0.6f;

    [Header("Тайминги")]
    [SerializeField] private float fadeInDuration = 1.5f;
    [SerializeField] private float fadeOutDuration = 1.5f;
    [SerializeField] private float pauseBeforeText = 1f;
    [SerializeField] private float holdTextDuration = 3f;
    [SerializeField] private float textFadeOutDuration = 1.5f;

    [Header("Сцена")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";

    [Header("Статистика")]
    [SerializeField] private EndingStatsPanel statsPanel;

    // ─── Приватные ─────────────────────────────────────────────────────────

    private Image    _fadePanel;
    private RawImage _peepholeOverlay;
    private Material _peepholeMaterialInstance;

    private CinemachineCamera[] _allVCs;

    // ─── Жизненный цикл ────────────────────────────────────────────────────

    private void Awake()
    {
        _allVCs = new[] { docCloseVC, docFarVC, faceVC, faceMidVC, peepholeVC };

        // Стартовое состояние: только docClose активна, остальные выключены
        foreach (var vc in _allVCs)
            if (vc != null) vc.gameObject.SetActive(false);
        if (docCloseVC != null) docCloseVC.gameObject.SetActive(true);
    }

    private void Start()
    {
        BuildUI();
        StartCoroutine(Play());
    }

    private void OnDestroy()
    {
        if (_peepholeMaterialInstance != null)
            Destroy(_peepholeMaterialInstance);
    }

    // ─── Построение UI ─────────────────────────────────────────────────────

    private void BuildUI()
    {
        GameObject canvasGo = new GameObject("EndingCutsceneCanvas");
        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 200;
        canvasGo.AddComponent<CanvasScaler>();

        _fadePanel = CreateFullscreenImage(canvasGo.transform, "FadePanel", Color.black);

        if (peepholeMaterial != null)
        {
            _peepholeMaterialInstance = Instantiate(peepholeMaterial);
            _peepholeMaterialInstance.SetFloat("_Radius",   peepholeStartRadius);
            _peepholeMaterialInstance.SetFloat("_Softness", peepholeSoftness);

            GameObject pGo = new GameObject("PeepholeOverlay");
            pGo.transform.SetParent(canvasGo.transform, false);
            _peepholeOverlay = pGo.AddComponent<RawImage>();
            _peepholeOverlay.material = _peepholeMaterialInstance;
            _peepholeOverlay.color    = Color.white;
            StretchToParent(_peepholeOverlay.rectTransform);
            _peepholeOverlay.enabled = false;
        }

        MigrateTextToCanvas(titleText,    canvasGo.transform);
        MigrateTextToCanvas(subtitleText, canvasGo.transform);
        SetTextAlpha(titleText,    0f);
        SetTextAlpha(subtitleText, 0f);
    }

    // ─── Главная последовательность ────────────────────────────────────────

    private IEnumerator Play()
    {
        // 1. Fade in — docClose уже активна
        yield return FadePanel(1f, 0f, fadeInDuration);

        // 2. Плавный отъезд от стола с документами
        yield return BlendToVC(docCloseVC, docFarVC, docPullBackDuration,
                               CinemachineBlendDefinition.Styles.EaseInOut);
        yield return new WaitForSeconds(holdAfterDocPullBack);

        // 3. Пауза перед лицом — нагнетение
        yield return new WaitForSeconds(delayBeforeFaceCam);

        // 4. Резкий кат на лицо
        CutToVC(docFarVC, faceVC);
        yield return null;

        // 5. Плавный отъезд от лица — медленный старт
        yield return BlendToVC(faceVC, faceMidVC, facePullBackDuration,
                               CinemachineBlendDefinition.Styles.EaseIn);

        // 6. Через дверь к щеколде — ускоряется к концу
        yield return BlendToVC(faceMidVC, peepholeVC, faceMidToPeepholeDuration,
                               CinemachineBlendDefinition.Styles.EaseOut);

        // 7. Щеколда закрывается
        if (_peepholeOverlay != null)
        {
            _peepholeOverlay.enabled = true;
            yield return AnimatePeephole(peepholeStartRadius, 0f, peepholeDuration);
        }

        // 8. Затемнение
        yield return FadePanel(0f, 1f, fadeOutDuration);
        if (_peepholeOverlay != null)
            _peepholeOverlay.enabled = false;

        yield return new WaitForSeconds(pauseBeforeText);

        // 9. Текст появляется
        yield return FadeText(titleText,    0f, 1f, textFadeInDuration);
        yield return new WaitForSeconds(delayBetweenTexts);
        yield return FadeText(subtitleText, 0f, 1f, textFadeInDuration);

        // 10. Статистика выезжает пункт за пунктом
        GameStatsTracker.Instance?.Save();
        if (statsPanel != null)
            yield return statsPanel.PlayEntrance();

        // 11. Пауза — всё пропадает одновременно
        yield return new WaitForSeconds(holdTextDuration);
        yield return FadeTextsBothOut(textFadeOutDuration);

        // 12. Главное меню
        SceneManager.LoadScene(mainMenuSceneName);
    }

    // ─── Cinemachine 3 управление ──────────────────────────────────────────

    /// <summary>Мгновенный кат без перехода.</summary>
    private void CutToVC(CinemachineCamera from, CinemachineCamera to)
    {
        SetBrainBlend(CinemachineBlendDefinition.Styles.Cut, 0f);
        if (from != null) from.gameObject.SetActive(false);
        if (to   != null) to.gameObject.SetActive(true);
    }

    /// <summary>Плавный переход к целевой камере за указанное время.</summary>
    private IEnumerator BlendToVC(CinemachineCamera from, CinemachineCamera to,
        float duration, CinemachineBlendDefinition.Styles style)
    {
        // Устанавливаем blend ДО включения камеры — иначе brain не успевает подхватить
        SetBrainBlend(style, duration);

        if (to != null) to.gameObject.SetActive(true);   // brain видит новую активную VC → начинает blend

        yield return new WaitForSeconds(duration);

        if (from != null) from.gameObject.SetActive(false); // убираем старую после завершения
    }

    private void SetBrainBlend(CinemachineBlendDefinition.Styles style, float duration)
    {
        if (brain == null) return;
        // DefaultBlend — struct, нужен copy-modify-assign
        CinemachineBlendDefinition blend = brain.DefaultBlend;
        blend.Style = style;
        blend.Time  = duration;
        brain.DefaultBlend = blend;
    }

    // ─── Эффект щеколды ────────────────────────────────────────────────────

    private IEnumerator AnimatePeephole(float fromRadius, float toRadius, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / duration));
            _peepholeMaterialInstance.SetFloat("_Radius", Mathf.Lerp(fromRadius, toRadius, t));
            yield return null;
        }
        _peepholeMaterialInstance.SetFloat("_Radius", toRadius);
    }

    // ─── UI утилиты ────────────────────────────────────────────────────────

    private static Image CreateFullscreenImage(Transform parent, string goName, Color color)
    {
        GameObject go = new GameObject(goName);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        StretchToParent(img.rectTransform);
        return img;
    }

    private static void StretchToParent(RectTransform rt)
    {
        rt.anchorMin        = Vector2.zero;
        rt.anchorMax        = Vector2.one;
        rt.sizeDelta        = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;
    }

    private IEnumerator FadePanel(float from, float to, float duration)
    {
        float elapsed = 0f;
        Color c = _fadePanel.color;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            c.a = Mathf.Lerp(from, to, elapsed / duration);
            _fadePanel.color = c;
            yield return null;
        }
        c.a = to;
        _fadePanel.color = c;
    }

    private IEnumerator FadeText(TextMeshProUGUI text, float from, float to, float duration)
    {
        if (text == null) yield break;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            text.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }
        text.alpha = to;
    }

    private IEnumerator FadeTextsBothOut(float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float a = Mathf.Lerp(1f, 0f, elapsed / duration);
            SetTextAlpha(titleText,    a);
            SetTextAlpha(subtitleText, a);
            statsPanel?.SetRootAlpha(a);
            yield return null;
        }
        SetTextAlpha(titleText,    0f);
        SetTextAlpha(subtitleText, 0f);
        statsPanel?.SetRootAlpha(0f);
    }

    private static void SetTextAlpha(TextMeshProUGUI text, float alpha)
    {
        if (text != null) text.alpha = alpha;
    }

    private static void MigrateTextToCanvas(TextMeshProUGUI text, Transform canvasTransform)
    {
        if (text == null) return;
        RectTransform rt     = text.rectTransform;
        Vector2 anchorMin    = rt.anchorMin;
        Vector2 anchorMax    = rt.anchorMax;
        Vector2 pivot        = rt.pivot;
        Vector2 anchoredPos  = rt.anchoredPosition;
        Vector2 sizeDelta    = rt.sizeDelta;
        rt.SetParent(canvasTransform, false);
        rt.anchorMin        = anchorMin;
        rt.anchorMax        = anchorMax;
        rt.pivot            = pivot;
        rt.anchoredPosition = anchoredPos;
        rt.sizeDelta        = sizeDelta;
    }
}
