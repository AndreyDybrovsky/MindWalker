using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Автосохранение после полной загрузки игровой сцены + локализованная подпись.
/// </summary>
public class AutoSaveSceneNotifier : MonoBehaviour
{
    private const string LocalizationKey = "game.autosave";
    private const string FallbackRu = "Автосохранение";

    [SerializeField] private string[] skipSceneNames = { "MainMenu", "Victory" };
    [SerializeField] private float saveDelayAfterLoad = 0.4f;
    [SerializeField] private float fadeInDuration = 0.45f;
    [SerializeField] private float visibleDuration = 1.6f;
    [SerializeField] private float fadeOutDuration = 0.55f;

    private static AutoSaveSceneNotifier _instance;
    private CanvasGroup _toastGroup;
    private TextMeshProUGUI _toastLabel;
    private Coroutine _toastRoutine;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        GameObject go = new GameObject(nameof(AutoSaveSceneNotifier));
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<AutoSaveSceneNotifier>();
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        EnsureToastUi();
        SceneManager.sceneLoaded += OnSceneLoaded;

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;

        if (_instance == this)
            _instance = null;
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        ApplyLocalizedToastText();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (ShouldSkipScene(scene.name))
            return;

        if (SaveManager.Instance == null || SaveManager.Instance.GetCurrentSaveSlot() < 0)
            return;

        StartCoroutine(AutoSaveWhenSceneReady(scene.name));
    }

    private IEnumerator AutoSaveWhenSceneReady(string sceneName)
    {
        yield return null;
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds(saveDelayAfterLoad);

        if (SaveManager.Instance == null || SaveManager.Instance.GetCurrentSaveSlot() < 0)
            yield break;

        if (SaveManager.Instance.ConsumeAutoSaveSkip())
            yield break;

        if (!SaveManager.Instance.ShouldRunAutoSaveForScene(sceneName))
            yield break;

        SaveManager.Instance.NotifyAutoSaveCompleted(sceneName);
        SaveManager.Instance.SaveCurrentGame();
        ShowToast();
    }

    private bool ShouldSkipScene(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
            return true;

        for (int i = 0; i < skipSceneNames.Length; i++)
        {
            if (sceneName == skipSceneNames[i])
                return true;
        }

        return false;
    }

    private void ShowToast()
    {
        EnsureToastUi();
        ApplyLocalizedToastText();

        if (_toastRoutine != null)
            StopCoroutine(_toastRoutine);

        _toastRoutine = StartCoroutine(ToastRoutine());
    }

    private IEnumerator ToastRoutine()
    {
        _toastGroup.gameObject.SetActive(true);
        _toastGroup.alpha = 0f;

        float t = 0f;
        while (t < fadeInDuration)
        {
            t += Time.unscaledDeltaTime;
            _toastGroup.alpha = Mathf.Clamp01(t / fadeInDuration);
            yield return null;
        }

        _toastGroup.alpha = 1f;
        yield return new WaitForSecondsRealtime(visibleDuration);

        t = 0f;
        while (t < fadeOutDuration)
        {
            t += Time.unscaledDeltaTime;
            _toastGroup.alpha = 1f - Mathf.Clamp01(t / fadeOutDuration);
            yield return null;
        }

        _toastGroup.alpha = 0f;
        _toastGroup.gameObject.SetActive(false);
        _toastRoutine = null;
    }

    private void ApplyLocalizedToastText()
    {
        if (_toastLabel == null)
            return;

        _toastLabel.text = PressEPromptUtility.ResolveLocalizedText(LocalizationKey, FallbackRu);
    }

    private void EnsureToastUi()
    {
        if (_toastGroup != null)
            return;

        GameObject canvasGo = new GameObject("AutoSaveToastCanvas");
        canvasGo.transform.SetParent(transform, false);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 8500;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        canvasGo.AddComponent<GraphicRaycaster>();

        GameObject panel = new GameObject("ToastPanel", typeof(RectTransform));
        panel.transform.SetParent(canvasGo.transform, false);

        _toastGroup = panel.AddComponent<CanvasGroup>();
        _toastGroup.alpha = 0f;
        _toastGroup.blocksRaycasts = false;
        _toastGroup.interactable = false;

        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(0f, 0f);
        panelRt.anchorMax = new Vector2(0f, 0f);
        panelRt.pivot = new Vector2(0f, 0f);
        panelRt.anchoredPosition = new Vector2(48f, 48f);
        panelRt.sizeDelta = new Vector2(520f, 72f);

        Image bg = panel.AddComponent<Image>();
        bg.color = new Color(0.08f, 0.08f, 0.08f, 0.72f);
        bg.raycastTarget = false;

        GameObject textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(panel.transform, false);
        _toastLabel = textGo.AddComponent<TextMeshProUGUI>();
        _toastLabel.fontSize = 32f;
        _toastLabel.color = Color.white;
        _toastLabel.alignment = TextAlignmentOptions.MidlineLeft;
        _toastLabel.raycastTarget = false;

        TextMeshProUGUI fontSource = FindAnyObjectByType<TextMeshProUGUI>();
        if (fontSource != null)
            _toastLabel.font = fontSource.font;

        RectTransform textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(20f, 8f);
        textRt.offsetMax = new Vector2(-12f, -8f);

        panel.SetActive(false);
    }
}
