using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class GameSettingsScript : MonoBehaviour
{
    [Header("UI Элементы")]
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private Button exitButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button saveButton;
    [SerializeField] private Button startButton;

    [Header("Панели")]
    [SerializeField] private GameObject savesListPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private Transform savesListContent;
    [SerializeField] private GameObject saveSlotPrefab;

    [Header("Настройки")]
    [SerializeField] private KeyCode menuKey = KeyCode.Escape;
    [SerializeField] private bool pauseAudio = true;

    [Header("Связь с кнопками паузы (MainMenuController на UIManager)")]
    [Tooltip("Нужен для плавного CloseSettings и сброса панелей при закрытии паузы.")]
    [SerializeField] private MainMenuController linkedMenuController;

    [Header("Плавное появление")]
    [SerializeField] private float menuFadeDuration = 0.22f;
    [SerializeField] private Color backdropDimColor = new Color(0f, 0f, 0f, 0.34f);
    [SerializeField] private Color backdropVeilColor = new Color(0.9f, 0.92f, 0.96f, 0.1f);

    [Header("Предупреждение о сохранении")]
    [SerializeField] private GameObject unsavedWarningPanel;
    [SerializeField] private Button continueExitButton;
    [SerializeField] private Button cancelExitButton;
    [SerializeField] private Button closeWarningButton;

    [Header("Сцена главного меню")]
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    [SerializeField] private float exitFadeDuration = 0.9f;

    [Header("Локализация Warning panel")]
    [SerializeField] private TMP_Text warningQuestionText;
    [SerializeField] private TMP_Text warningContinueExitText;
    [SerializeField] private TMP_Text warningCancelExitText;

    private bool isMenuOpen;
    private readonly List<GameObject> saveSlots = new List<GameObject>();
    private float savedFixedDeltaTime;
    private bool isWarningShown;
    private bool _menuTransitionBusy;
    private bool _ignoreMenuKeyUntilReleased;

    private CanvasGroup _menuPanelGroup;
    private CanvasGroup _backdropGroup;
    private CanvasGroup _backdropDimGroup;
    private CanvasGroup _backdropVeilGroup;
    private GameObject _backdropRoot;
    private CanvasGroup _warningGroup;
    private CanvasGroup _savesGroup;

    private void Awake()
    {
        _menuPanelGroup = menuPanel != null ? UiMenuTransitions.EnsureCanvasGroup(menuPanel) : null;
        _warningGroup = unsavedWarningPanel != null ? UiMenuTransitions.EnsureCanvasGroup(unsavedWarningPanel) : null;
        _savesGroup = savesListPanel != null ? UiMenuTransitions.EnsureCanvasGroup(savesListPanel) : null;
        BuildBackdropLayers();
    }

    private void Start()
    {
        savedFixedDeltaTime = Time.fixedDeltaTime;

        if (menuPanel != null)
            menuPanel.SetActive(false);
        if (savesListPanel != null)
            savesListPanel.SetActive(false);
        if (settingsPanel != null)
            settingsPanel.SetActive(false);
        if (unsavedWarningPanel != null)
            unsavedWarningPanel.SetActive(false);

        if (_backdropRoot != null)
            _backdropRoot.SetActive(false);

        if (exitButton != null)
            exitButton.onClick.AddListener(OnExitButtonClicked);
        if (saveButton != null)
            saveButton.onClick.AddListener(OnSaveButtonClicked);
        if (startButton != null)
            startButton.onClick.AddListener(OnStartButtonClicked);

        if (continueExitButton != null)
        {
            continueExitButton.onClick.RemoveListener(OnContinueExitClicked);
            continueExitButton.onClick.AddListener(OnContinueExitClicked);
        }

        if (cancelExitButton != null)
        {
            cancelExitButton.onClick.RemoveListener(OnCancelExitClicked);
            cancelExitButton.onClick.AddListener(OnCancelExitClicked);
        }

        if (closeWarningButton != null)
        {
            closeWarningButton.onClick.RemoveListener(OnCloseWarningClicked);
            closeWarningButton.onClick.AddListener(OnCloseWarningClicked);
        }

        ResolveWarningLocalizedTexts();
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnWarningLanguageChanged;
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnWarningLanguageChanged;
    }

    private void OnWarningLanguageChanged(GameLanguage _)
    {
        ApplyWarningLocalizedTexts();
    }

    private void ResolveWarningLocalizedTexts()
    {
        if (unsavedWarningPanel == null)
            return;

        if (warningQuestionText == null)
        {
            Transform t = unsavedWarningPanel.transform.Find("WarningText");
            if (t != null)
                t.TryGetComponent(out warningQuestionText);
        }

        if (warningContinueExitText == null && continueExitButton != null)
            warningContinueExitText = continueExitButton.GetComponentInChildren<TMP_Text>(true);

        if (warningCancelExitText == null && cancelExitButton != null)
            warningCancelExitText = cancelExitButton.GetComponentInChildren<TMP_Text>(true);

        ApplyWarningLocalizedTexts();
    }

    private void ApplyWarningLocalizedTexts()
    {
        if (warningQuestionText != null)
            warningQuestionText.text = PressEPromptUtility.ResolveLocalizedText(
                "pause.exit_warning.question", "Вы уверены?");

        if (warningContinueExitText != null)
            warningContinueExitText.text = PressEPromptUtility.ResolveLocalizedText(
                "pause.exit_warning.confirm", "Принять");

        if (warningCancelExitText != null)
            warningCancelExitText.text = PressEPromptUtility.ResolveLocalizedText(
                "pause.exit_warning.cancel", "Отмена");
    }

    private void BuildBackdropLayers()
    {
        if (menuPanel == null)
            return;

        Canvas canvas = menuPanel.GetComponentInParent<Canvas>();
        if (canvas == null)
            return;

        Transform root = canvas.transform;
        if (root.Find("PauseMenuBackdrop") != null)
            return;

        _backdropRoot = new GameObject("PauseMenuBackdrop", typeof(RectTransform));
        _backdropRoot.layer = menuPanel.layer;
        RectTransform backdropRt = _backdropRoot.GetComponent<RectTransform>();
        backdropRt.SetParent(root, false);
        backdropRt.SetAsFirstSibling();
        backdropRt.anchorMin = Vector2.zero;
        backdropRt.anchorMax = Vector2.one;
        backdropRt.pivot = new Vector2(0.5f, 0.5f);
        backdropRt.sizeDelta = Vector2.zero;
        backdropRt.anchoredPosition = Vector2.zero;

        _backdropGroup = _backdropRoot.AddComponent<CanvasGroup>();
        _backdropGroup.alpha = 0f;
        _backdropGroup.blocksRaycasts = false;
        _backdropGroup.interactable = false;

        GameObject dimGo = CreateStretchImageChild(_backdropRoot.transform, "Dim", backdropDimColor);
        _backdropDimGroup = dimGo.AddComponent<CanvasGroup>();
        _backdropDimGroup.alpha = 1f;

        GameObject veilGo = CreateStretchImageChild(_backdropRoot.transform, "Veil", backdropVeilColor);
        _backdropVeilGroup = veilGo.AddComponent<CanvasGroup>();
        _backdropVeilGroup.alpha = 1f;

        Image dimImage = dimGo.GetComponent<Image>();
        dimImage.raycastTarget = true;
        veilGo.GetComponent<Image>().raycastTarget = false;
    }

    private static GameObject CreateStretchImageChild(Transform parent, string name, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        go.layer = parent.gameObject.layer;
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.sizeDelta = Vector2.zero;
        rt.anchoredPosition = Vector2.zero;

        var image = go.GetComponent<Image>();
        image.sprite = null;
        image.type = Image.Type.Simple;
        image.color = color;
        return go;
    }

    private void Update()
    {
        if (FadeStart.IsAnyFadeActive || LevelCompletionManager.IsFading || GameOverManager.IsFading)
            return;

        PlayerHealth playerHealth = FindFirstObjectByType<PlayerHealth>();
        if (playerHealth != null && playerHealth.IsDead)
            return;

        if (_ignoreMenuKeyUntilReleased)
        {
            if (Input.GetKey(menuKey))
                return;
            _ignoreMenuKeyUntilReleased = false;
        }

        if (!Input.GetKeyDown(menuKey))
            return;

        if (isWarningShown)
        {
            HideUnsavedWarning();
            return;
        }

        if (isMenuOpen)
        {
            bool settingsOpen = settingsPanel != null && settingsPanel.activeSelf;
            bool savesOpen = savesListPanel != null && savesListPanel.activeSelf;

            if (settingsOpen || savesOpen)
            {
                if (settingsOpen)
                {
                    if (linkedMenuController != null)
                        linkedMenuController.CloseSettings();
                    else if (settingsPanel != null)
                    {
                        settingsPanel.SetActive(false);
                        if (menuPanel != null)
                            menuPanel.SetActive(true);
                    }
                }

                if (savesOpen)
                    RunCloseSavesAnimated();

                return;
            }

            ToggleMenu();
        }
        else
        {
            ToggleMenu();
        }
    }

    public void ToggleMenu()
    {
        if (_menuTransitionBusy)
            return;

        RunToggleMenuAnimated();
    }

    private async void RunToggleMenuAnimated()
    {
        _menuTransitionBusy = true;
        try
        {
            bool opening = !isMenuOpen;
            if (opening)
                GameplayInputBlocker.SetBlocked(true);

            isMenuOpen = opening;

            if (opening)
            {
                if (_backdropRoot != null)
                {
                    _backdropRoot.SetActive(true);
                    if (_backdropGroup != null)
                        _backdropGroup.alpha = 0f;
                }

                if (menuPanel != null)
                {
                    menuPanel.SetActive(true);
                    if (_menuPanelGroup != null)
                    {
                        _menuPanelGroup.alpha = 0f;
                        _menuPanelGroup.blocksRaycasts = false;
                        _menuPanelGroup.interactable = false;
                    }
                }

                PauseGame();

                await AnimateMenuAndBackdrop(0f, 1f, menuFadeDuration);

                if (_menuPanelGroup != null)
                {
                    _menuPanelGroup.blocksRaycasts = true;
                    _menuPanelGroup.interactable = true;
                }

                if (_backdropGroup != null)
                    _backdropGroup.blocksRaycasts = true;
            }
            else
            {
                if (Input.GetKey(menuKey))
                    _ignoreMenuKeyUntilReleased = true;

                linkedMenuController?.SnapPanelsToMainMenu();

                if (_menuPanelGroup != null)
                {
                    _menuPanelGroup.blocksRaycasts = false;
                    _menuPanelGroup.interactable = false;
                }

                if (_backdropGroup != null)
                    _backdropGroup.blocksRaycasts = false;

                await AnimateMenuAndBackdrop(1f, 0f, menuFadeDuration);

                if (menuPanel != null)
                    menuPanel.SetActive(false);
                if (_backdropRoot != null)
                    _backdropRoot.SetActive(false);

                if (savesListPanel != null)
                    savesListPanel.SetActive(false);
                if (unsavedWarningPanel != null)
                    unsavedWarningPanel.SetActive(false);

                ResumeGame();
            }
        }
        finally
        {
            _menuTransitionBusy = false;
        }
    }

    private async Awaitable AnimateMenuAndBackdrop(float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            if (_menuPanelGroup != null)
                _menuPanelGroup.alpha = to;
            if (_backdropGroup != null)
                _backdropGroup.alpha = to;
            return;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            float a = Mathf.Lerp(from, to, k);
            if (_menuPanelGroup != null)
                _menuPanelGroup.alpha = a;
            if (_backdropGroup != null)
                _backdropGroup.alpha = a;
            await Awaitable.NextFrameAsync();
        }

        if (_menuPanelGroup != null)
            _menuPanelGroup.alpha = to;
        if (_backdropGroup != null)
            _backdropGroup.alpha = to;
    }

    private void PauseGame()
    {
        _ignoreMenuKeyUntilReleased = false;
        GameplayInputBlocker.SetBlocked(true);
        Time.timeScale = 0f;
        Time.fixedDeltaTime = 0f;
        GameplayInputBlocker.UnlockCursorForMenu();

        if (pauseAudio)
        {
            AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (AudioSource audio in audioSources)
            {
                if (audio.isPlaying)
                    audio.Pause();
            }
        }
    }

    private void ResumeGame()
    {
        Time.timeScale = 1f;
        Time.fixedDeltaTime = savedFixedDeltaTime;
        GameplayInputBlocker.BeginMouseLookSuppress(6);
        GameplayInputBlocker.SetBlocked(false);
        _ = LockCursorAfterMenuKeyReleasedAsync();

        if (pauseAudio)
        {
            AudioSource[] audioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            foreach (AudioSource audio in audioSources)
            {
                if (audio.clip == null)
                    continue;
                if (!audio.isPlaying && audio.time > 0f)
                    audio.UnPause();
            }
        }

    }

    private async Awaitable LockCursorAfterMenuKeyReleasedAsync()
    {
        await Awaitable.NextFrameAsync();

        while (Input.GetKey(menuKey))
            await Awaitable.NextFrameAsync();

        await Awaitable.NextFrameAsync();
        GameplayInputBlocker.LockCursorForGameplay(15);
    }

    private void LateUpdate()
    {
        GameplayInputBlocker.TickFrame();
    }

    private void OnExitButtonClicked()
    {
        if (isWarningShown)
            return;
        ShowUnsavedWarning();
    }

    private async void ShowUnsavedWarning()
    {
        if (unsavedWarningPanel == null)
        {
            ExitToMainMenuWithFade();
            return;
        }

        isWarningShown = true;
        ApplyWarningLocalizedTexts();

        if (!isMenuOpen)
        {
            isMenuOpen = true;
            if (menuPanel != null)
                menuPanel.SetActive(true);
            if (_backdropRoot != null)
                _backdropRoot.SetActive(true);
            if (_backdropGroup != null)
                _backdropGroup.alpha = 1f;
            if (_menuPanelGroup != null)
                _menuPanelGroup.alpha = 1f;
            PauseGame();
        }

        unsavedWarningPanel.SetActive(true);
        if (_warningGroup != null)
        {
            _warningGroup.alpha = 0f;
            _warningGroup.blocksRaycasts = false;
            await UiMenuTransitions.AnimateCanvasGroupAlpha(_warningGroup, 0f, 1f, menuFadeDuration * 0.75f);
            _warningGroup.blocksRaycasts = true;
        }
    }

    private async void HideUnsavedWarning()
    {
        isWarningShown = false;
        if (_warningGroup != null && unsavedWarningPanel != null && unsavedWarningPanel.activeSelf)
        {
            await UiMenuTransitions.AnimateCanvasGroupAlpha(_warningGroup, _warningGroup.alpha, 0f, menuFadeDuration * 0.6f);
            _warningGroup.blocksRaycasts = false;
        }

        if (unsavedWarningPanel != null)
            unsavedWarningPanel.SetActive(false);
    }

    private void OnContinueExitClicked()
    {
        HideUnsavedWarning();
        ExitToMainMenuWithFade();
    }

    private void OnCancelExitClicked()
    {
        HideUnsavedWarning();
    }

    private void OnCloseWarningClicked()
    {
        HideUnsavedWarning();
    }

    public void ExitToMainMenuWithFade()
    {
        StartCoroutine(ExitToMainMenuRoutine());
    }

    private IEnumerator ExitToMainMenuRoutine()
    {
        GameplayInputBlocker.SetBlocked(false);
        Time.timeScale = 1f;
        Time.fixedDeltaTime = savedFixedDeltaTime;
        GameplayInputBlocker.UnlockCursorForMenu();

        CanvasGroup fade = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (fade != null)
            yield return ScreenFadeRunner.FadeToBlack(exitFadeDuration, fade);

        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }

    private async void OnSaveButtonClicked()
    {
        if (savesListPanel == null)
            return;

        bool opening = !savesListPanel.activeSelf;
        if (opening)
        {
            savesListPanel.SetActive(true);
            if (_savesGroup != null)
            {
                _savesGroup.alpha = 0f;
                _savesGroup.blocksRaycasts = false;
                await UiMenuTransitions.AnimateCanvasGroupAlpha(_savesGroup, 0f, 1f, menuFadeDuration);
                _savesGroup.blocksRaycasts = true;
            }

            LoadSavesList();
        }
        else
        {
            RunCloseSavesAnimated();
        }
    }

    private async void RunCloseSavesAnimated()
    {
        if (savesListPanel == null || !savesListPanel.activeSelf)
            return;

        if (_savesGroup != null)
        {
            _savesGroup.blocksRaycasts = false;
            await UiMenuTransitions.AnimateCanvasGroupAlpha(_savesGroup, _savesGroup.alpha, 0f, menuFadeDuration * 0.85f);
        }

        savesListPanel.SetActive(false);
    }

    private void LoadSavesList()
    {
        foreach (GameObject slot in saveSlots)
        {
            if (slot != null)
                Destroy(slot);
        }

        saveSlots.Clear();

        if (savesListContent == null || saveSlotPrefab == null)
            return;

        SaveManager saveManager = SaveManager.Instance;
        if (saveManager == null)
            return;

        for (int i = 0; i < 4; i++)
        {
            GameObject slot = Instantiate(saveSlotPrefab, savesListContent);
            saveSlots.Add(slot);

            SaveSlotItem slotItem = slot.GetComponent<SaveSlotItem>();
            if (slotItem != null)
            {
                slotItem.Initialize(i, null);
                slotItem.UpdateDisplay();
            }
        }
    }

    public async void OnStartButtonClicked()
    {
        linkedMenuController?.SnapPanelsToMainMenu();

        if (savesListPanel != null && savesListPanel.activeSelf)
        {
            if (_savesGroup != null)
                await UiMenuTransitions.AnimateCanvasGroupAlpha(_savesGroup, _savesGroup.alpha, 0f, menuFadeDuration * 0.5f);
            savesListPanel.SetActive(false);
        }

        if (unsavedWarningPanel != null && unsavedWarningPanel.activeSelf)
            HideUnsavedWarning();

        if (isMenuOpen)
            ToggleMenu();
    }

    public void CloseMenu()
    {
        if (isMenuOpen)
            ToggleMenu();
    }
}
