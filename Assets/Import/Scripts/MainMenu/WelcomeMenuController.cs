using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

/// <summary>
/// Контроллер приветственного меню с плавным появлением текста
/// </summary>
public class WelcomeMenuController : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private Image fadePanel;
    [SerializeField] private TextMeshProUGUI welcomeText;
    [SerializeField] private TextMeshProUGUI continueHint;
    [SerializeField] private Button continueButton;

    [Header("Настройки анимации")]
    [SerializeField] private float fadeDuration = 1f;
    [SerializeField] private float textFadeDuration = 2f;
    [SerializeField] private float hintFadeDuration = 0.5f;

    [Header("Тексты приветствия")]
    [TextArea(3, 10)]
    [Tooltip("Оставьте пустым — строки подтянутся из локализации (welcome.0 …). Можно указать ключи welcome.N.")]
    [SerializeField] private string[] welcomeTexts;

    [Header("Настройки перехода")]
    [SerializeField] private string nextSceneName = "Main";

    [Header("Настройки анимации меню")]
    [SerializeField] private float menuFadeOutDuration = 0.5f;

    [Header("Затухание звука на MainMenu")]
    [SerializeField] private float audioFadeDuration = 2f;

    private int currentTextIndex;
    private bool isTextFullyVisible;
    private bool isProcessing;
    private Canvas welcomeCanvas;
    private float _savedListenerVolume;
    private float _fadePanelTargetAlpha = 1f;
    private CanvasGroup _fadePanelCanvasGroup;
    private readonly Dictionary<AudioSource, float> _audioSourceStartVolumes = new Dictionary<AudioSource, float>();

    private void Awake()
    {
        if (fadePanel != null)
            _fadePanelCanvasGroup = fadePanel.GetComponent<CanvasGroup>();
    }

    private void Start()
    {
        welcomeCanvas = GetComponentInParent<Canvas>();
        if (welcomeCanvas == null)
            Debug.LogError("WelcomeMenuController: Canvas не найден!");

        PrepareFadePanelHidden();

        if (welcomeText != null)
        {
            Color textColor = welcomeText.color;
            textColor.a = 0f;
            welcomeText.color = textColor;
            welcomeText.gameObject.SetActive(false);
        }

        if (continueHint != null)
        {
            Color hintColor = continueHint.color;
            hintColor.a = 0f;
            continueHint.color = hintColor;
            continueHint.gameObject.SetActive(false);
        }

        if (continueButton != null)
        {
            continueButton.interactable = false;
            continueButton.onClick.AddListener(OnContinueButtonClicked);
        }

        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ResolveWelcomeTexts();
        RefreshContinueHint();
    }

    public void StartWelcomeSequence()
    {
        if (isProcessing)
            return;

        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (welcomeCanvas == null)
            welcomeCanvas = GetComponentInParent<Canvas>();

        if (welcomeCanvas != null && !welcomeCanvas.gameObject.activeInHierarchy)
            welcomeCanvas.gameObject.SetActive(true);

        PrepareFadePanelHidden();
        ResolveWelcomeTexts();
        RefreshContinueHint();

        if (welcomeTexts == null || welcomeTexts.Length == 0)
        {
            Debug.LogWarning("WelcomeMenuController: нет текстов приветствия, загружаем следующую сцену.");
            LoadNextScene();
            return;
        }

        isProcessing = true;
        currentTextIndex = 0;

        StartCoroutine(FadeOutSaveMenuCoroutine());
        StartCoroutine(FadeMainMenuAudioCoroutine());
        StartCoroutine(WelcomeSequenceCoroutine());
    }

    private void PrepareFadePanelHidden()
    {
        if (fadePanel == null)
            return;

        int uiLayer = fadePanel.transform.parent != null
            ? fadePanel.transform.parent.gameObject.layer
            : LayerMask.NameToLayer("UI");
        fadePanel.gameObject.layer = uiLayer;
        fadePanel.transform.SetAsLastSibling();
        fadePanel.gameObject.SetActive(true);

        if (_fadePanelCanvasGroup == null)
            _fadePanelCanvasGroup = fadePanel.GetComponent<CanvasGroup>();

        _fadePanelTargetAlpha = _fadePanelCanvasGroup != null
            ? _fadePanelCanvasGroup.alpha
            : fadePanel.color.a;

        if (_fadePanelTargetAlpha <= 0f)
            _fadePanelTargetAlpha = 1f;

        SetFadePanelAlpha(0f);
    }

    private void SetFadePanelAlpha(float alpha)
    {
        if (fadePanel == null)
            return;

        if (_fadePanelCanvasGroup != null)
        {
            _fadePanelCanvasGroup.alpha = alpha;
            return;
        }

        Color color = fadePanel.color;
        color.a = alpha;
        fadePanel.color = color;
    }

    private void ResolveWelcomeTexts()
    {
        var resolved = new List<string>();

        if (welcomeTexts == null || welcomeTexts.Length == 0)
        {
            for (int i = 0; i < 8; i++)
            {
                string key = $"welcome.{i}";
                string value = ResolveLocalizedString(key);
                if (string.IsNullOrEmpty(value) || value == key)
                    break;
                resolved.Add(value);
            }
        }
        else
        {
            foreach (string entry in welcomeTexts)
            {
                if (string.IsNullOrWhiteSpace(entry))
                    continue;

                string trimmed = entry.Trim();
                if (LooksLikeLocalizationKey(trimmed))
                {
                    string localized = ResolveLocalizedString(trimmed);
                    if (!string.IsNullOrEmpty(localized) && localized != trimmed)
                        resolved.Add(localized);
                }
                else
                {
                    resolved.Add(trimmed);
                }
            }
        }

        welcomeTexts = resolved.Count > 0 ? resolved.ToArray() : System.Array.Empty<string>();
    }

    private static bool LooksLikeLocalizationKey(string value)
    {
        return value.Contains('.') && !value.Contains(' ');
    }

    private static string ResolveLocalizedString(string key)
    {
        if (LocalizationManager.Instance != null)
        {
            string fromJson = LocalizationManager.Instance.T(key);
            if (!string.IsNullOrEmpty(fromJson) && fromJson != key)
                return fromJson;
        }

        try
        {
            var db = LocalizationSettings.StringDatabase;
            if (db != null)
            {
                string fromTable = db.GetLocalizedString(LocalizationManager.StringTableCollectionName, key);
                if (LocalizationStringUtility.IsValidTranslation(fromTable, key))
                    return fromTable;
            }
        }
        catch
        {
            // LocalizationSettings может быть не готов
        }

        return key;
    }

    private void RefreshContinueHint()
    {
        if (continueHint == null)
            return;

        continueHint.text = ResolveLocalizedString("welcome.continue");
        if (continueHint.text == "welcome.continue")
            continueHint.text = "Продолжить...";
    }

    private IEnumerator FadeMainMenuAudioCoroutine()
    {
        _savedListenerVolume = AudioListener.volume;
        _audioSourceStartVolumes.Clear();

        AudioSource[] sources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (AudioSource source in sources)
        {
            if (source == null || !source.isPlaying)
                continue;

            _audioSourceStartVolumes[source] = source.volume;
        }

        float elapsed = 0f;
        while (elapsed < audioFadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = 1f - Mathf.Clamp01(elapsed / audioFadeDuration);

            AudioListener.volume = _savedListenerVolume * t;

            foreach (KeyValuePair<AudioSource, float> pair in _audioSourceStartVolumes)
            {
                if (pair.Key != null)
                    pair.Key.volume = pair.Value * t;
            }

            yield return null;
        }

        AudioListener.volume = 0f;
        foreach (KeyValuePair<AudioSource, float> pair in _audioSourceStartVolumes)
        {
            if (pair.Key != null)
                pair.Key.volume = 0f;
        }
    }

    private IEnumerator WelcomeSequenceCoroutine()
    {
        PrepareFadePanelHidden();
        yield return null;
        yield return StartCoroutine(FadeInFadePanelCoroutine(fadeDuration));
        yield return StartCoroutine(ShowTextCoroutine(0));

        isProcessing = false;
    }

    private IEnumerator FadeInFadePanelCoroutine(float duration)
    {
        if (fadePanel == null)
            yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(0f, _fadePanelTargetAlpha, elapsed / duration);
            SetFadePanelAlpha(alpha);
            yield return null;
        }

        SetFadePanelAlpha(_fadePanelTargetAlpha);
    }

    private IEnumerator ShowTextCoroutine(int textIndex)
    {
        if (textIndex < 0 || textIndex >= welcomeTexts.Length)
            yield break;

        if (welcomeText != null)
        {
            welcomeText.text = welcomeTexts[textIndex];
            welcomeText.gameObject.SetActive(true);
        }

        if (continueHint != null)
            continueHint.gameObject.SetActive(false);

        isTextFullyVisible = false;

        if (welcomeText != null)
        {
            float elapsed = 0f;
            Color textColor = welcomeText.color;

            while (elapsed < textFadeDuration)
            {
                elapsed += Time.deltaTime;
                textColor.a = Mathf.Lerp(0f, 1f, elapsed / textFadeDuration);
                welcomeText.color = textColor;
                yield return null;
            }

            textColor.a = 1f;
            welcomeText.color = textColor;
        }

        isTextFullyVisible = true;
        yield return StartCoroutine(ShowContinueHintCoroutine());

        if (continueButton != null)
            continueButton.interactable = true;
    }

    private IEnumerator ShowContinueHintCoroutine()
    {
        if (continueHint == null)
            yield break;

        RefreshContinueHint();
        continueHint.gameObject.SetActive(true);

        float elapsed = 0f;
        Color hintColor = continueHint.color;

        while (elapsed < hintFadeDuration)
        {
            elapsed += Time.deltaTime;
            hintColor.a = Mathf.Lerp(0f, 1f, elapsed / hintFadeDuration);
            continueHint.color = hintColor;
            yield return null;
        }

        hintColor.a = 1f;
        continueHint.color = hintColor;
    }

    private void OnContinueButtonClicked()
    {
        if (!isTextFullyVisible || isProcessing)
            return;

        StartCoroutine(ContinueToNextTextCoroutine());
    }

    private IEnumerator ContinueToNextTextCoroutine()
    {
        isProcessing = true;

        if (continueHint != null)
            yield return StartCoroutine(FadeOutCoroutine(continueHint, hintFadeDuration));

        if (welcomeText != null)
            yield return StartCoroutine(FadeOutCoroutine(welcomeText, textFadeDuration * 0.5f));

        if (continueButton != null)
            continueButton.interactable = false;

        currentTextIndex++;

        if (currentTextIndex < welcomeTexts.Length)
        {
            yield return StartCoroutine(ShowTextCoroutine(currentTextIndex));
            isProcessing = false;
        }
        else
        {
            LoadNextScene();
        }
    }

    private IEnumerator FadeOutCoroutine(Graphic graphic, float duration)
    {
        if (graphic == null)
            yield break;

        float elapsed = 0f;
        Color color = graphic.color;
        float startAlpha = color.a;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, 0f, elapsed / duration);
            graphic.color = color;
            yield return null;
        }

        color.a = 0f;
        graphic.color = color;
        graphic.gameObject.SetActive(false);
    }

    private IEnumerator FadeOutSaveMenuCoroutine()
    {
        MainMenuController menuController = FindFirstObjectByType<MainMenuController>();
        if (menuController == null || menuController.saveMenu == null)
            yield break;

        GameObject saveMenuObject = menuController.saveMenu;
        if (!saveMenuObject.activeInHierarchy)
            yield break;

        List<Graphic> uiElements = new List<Graphic>();
        CanvasGroup canvasGroup = saveMenuObject.GetComponent<CanvasGroup>();
        Graphic[] graphics = saveMenuObject.GetComponentsInChildren<Graphic>(true);
        uiElements.AddRange(graphics);

        if (uiElements.Count == 0 && canvasGroup == null)
        {
            saveMenuObject.SetActive(false);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < menuFadeOutDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / menuFadeOutDuration);

            if (canvasGroup != null)
                canvasGroup.alpha = alpha;

            foreach (Graphic graphic in uiElements)
            {
                if (graphic == null)
                    continue;

                Color color = graphic.color;
                color.a = alpha;
                graphic.color = color;
            }

            yield return null;
        }

        if (canvasGroup != null)
            canvasGroup.alpha = 0f;

        foreach (Graphic graphic in uiElements)
        {
            if (graphic == null)
                continue;

            Color color = graphic.color;
            color.a = 0f;
            graphic.color = color;
        }

        saveMenuObject.SetActive(false);
    }

    private void LoadNextScene()
    {
        if (string.IsNullOrEmpty(nextSceneName))
        {
            Debug.LogWarning("WelcomeMenuController: не указана следующая сцена!");
            return;
        }

        SettingsManager.Instance.ApplyAudioSettingsImmediate();
        SceneManager.LoadScene(nextSceneName);
    }

    private void Update()
    {
        if (isTextFullyVisible && !isProcessing)
        {
            if (Input.anyKeyDown && !Input.GetMouseButtonDown(0) && !Input.GetMouseButtonDown(1) && !Input.GetMouseButtonDown(2))
                OnContinueButtonClicked();
        }
    }
}
