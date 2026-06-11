using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    public string sceneName = "GameScene";

    public GameObject mainMenu;

    public GameObject settingsMenu;

    public GameObject saveMenu;

    public WelcomeMenuController welcomeMenuController;

    [Header("Заголовок главного меню (картинка, дочерний объект Main / GameTitle)")]
    [SerializeField] private Image mainMenuTitleImage;

    [Header("Плавные панели")]
    [SerializeField] private float panelFadeDuration = 0.22f;

    [Header("Выход из игры")]
    [SerializeField] private CanvasGroup quitFadeCanvasGroup;
    [SerializeField] private float quitFadeDuration = 1f;

    [Header("Настройки")]
    [SerializeField] private MonoBehaviour settingsMenuUiBehaviour;

    private bool _panelFadeBusy;
    private bool _quitInProgress;

    private void OnEnable()
    {
        WireMainMenuTitleReference();
        ResolveSettingsMenuUi();
    }

    private ISettingsMenuUi GetSettingsMenuUi() => ResolveSettingsMenuUi();

    private ISettingsMenuUi ResolveSettingsMenuUi()
    {
        if (settingsMenuUiBehaviour is ISettingsMenuUi direct)
            return direct;

        if (settingsMenuUiBehaviour == null)
            settingsMenuUiBehaviour = GetComponent<MonoBehaviour>();

        if (settingsMenuUiBehaviour is ISettingsMenuUi onSelf)
            return onSelf;

        if (settingsMenu != null)
        {
            MonoBehaviour[] behaviours = settingsMenu.GetComponentsInChildren<MonoBehaviour>(true);
            for (int i = 0; i < behaviours.Length; i++)
            {
                if (behaviours[i] is ISettingsMenuUi ui)
                {
                    settingsMenuUiBehaviour = behaviours[i];
                    return ui;
                }
            }
        }

        MonoBehaviour[] all = FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] is ISettingsMenuUi ui)
            {
                settingsMenuUiBehaviour = all[i];
                return ui;
            }
        }

        return null;
    }

    private void WireMainMenuTitleReference()
    {
        if (mainMenuTitleImage != null || mainMenu == null)
            return;

        Transform titleTransform = mainMenu.transform.Find("GameTitle");
        if (titleTransform != null)
            mainMenuTitleImage = titleTransform.GetComponent<Image>();
    }

    public void LoadScene()
    {
        SceneManager.LoadScene(sceneName);
    }

    public void StartNewGame()
    {
        if (welcomeMenuController != null)
        {
            Debug.Log("Запускаем катсцену через назначенный WelcomeMenuController");
            welcomeMenuController.StartWelcomeSequence();
            return;
        }

        Debug.LogWarning("WelcomeMenuController не назначен в MainMenuController! Ищем в сцене...");
        WelcomeMenuController foundController = FindFirstObjectByType<WelcomeMenuController>(FindObjectsInactive.Include);

        if (foundController != null)
        {
            Debug.Log("WelcomeMenuController найден в сцене, запускаем катсцену");
            foundController.StartWelcomeSequence();
        }
        else
        {
            Debug.LogError("WelcomeMenuController не найден! Загружаем сцену напрямую. Убедитесь, что WelcomeMenuController добавлен в сцену и назначен в MainMenuController.");
            LoadScene();
        }
    }

    public void LoadSceneByIndex(int sceneIndex)
    {
        SceneManager.LoadScene(sceneIndex);
    }

    public void QuitGame()
    {
        if (_quitInProgress)
            return;

        RunQuitWithFadeAsync();
    }

    private async void RunQuitWithFadeAsync()
    {
        _quitInProgress = true;
        await ScreenFadeUtility.FadeToBlackAsync(quitFadeDuration);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    public void OpenSettings()
    {
        if (settingsMenu == null)
        {
            Debug.LogWarning("Settings Menu не назначен в инспекторе!");
            return;
        }

        RunOpenSettingsAnimated();
    }

    public void CloseSettings()
    {
        if (settingsMenu == null)
        {
            Debug.LogWarning("Settings Menu не назначен в инспекторе!");
            return;
        }

        RunCloseSettingsAnimated();
    }

    public void ToggleSettings()
    {
        if (settingsMenu == null)
        {
            Debug.LogWarning("Settings Menu не назначен в инспекторе!");
            return;
        }

        if (settingsMenu.activeSelf)
            CloseSettings();
        else
            OpenSettings();
    }

    public void OpenSaveMenu()
    {
        if (saveMenu == null)
        {
            Debug.LogWarning("Save Menu не назначен в инспекторе!");
            return;
        }

        RunOpenSaveMenuAnimated();
    }

    public void CloseSaveMenu()
    {
        if (saveMenu == null)
        {
            Debug.LogWarning("Save Menu не назначен в инспекторе!");
            return;
        }

        RunCloseSaveMenuAnimated();
    }

    /// <summary>
    /// Мгновенно возвращает главные кнопки (например перед закрытием всего пауз-меню).
    /// </summary>
    public void SnapPanelsToMainMenu()
    {
        if (settingsMenu != null)
        {
            settingsMenu.SetActive(false);
            CanvasGroup sCg = settingsMenu.GetComponent<CanvasGroup>();
            if (sCg != null)
                sCg.alpha = 1f;
        }

        if (saveMenu != null)
        {
            saveMenu.SetActive(false);
            CanvasGroup vCg = saveMenu.GetComponent<CanvasGroup>();
            if (vCg != null)
                vCg.alpha = 1f;
        }

        if (mainMenu == null)
            return;

        mainMenu.SetActive(true);
        CanvasGroup cg = mainMenu.GetComponent<CanvasGroup>();
        if (cg != null)
        {
            cg.alpha = 1f;
            cg.blocksRaycasts = true;
            cg.interactable = true;
        }
    }

    public void ClosePauseMenuLikeEsc()
    {
        GameSettingsScript gameSettings = FindFirstObjectByType<GameSettingsScript>(FindObjectsInactive.Include);
        if (gameSettings != null)
            gameSettings.CloseMenu();
        else
            Debug.LogWarning("MainMenuController: GameSettingsScript не найден в сцене.");
    }

    private async void RunOpenSettingsAnimated()
    {
        if (_panelFadeBusy)
            return;

        _panelFadeBusy = true;
        try
        {
            CanvasGroup mainCg = mainMenu != null ? UiMenuTransitions.EnsureCanvasGroup(mainMenu) : null;
            CanvasGroup settingsCg = UiMenuTransitions.EnsureCanvasGroup(settingsMenu);

            settingsMenu.SetActive(true);
            settingsCg.alpha = 0f;
            settingsCg.blocksRaycasts = false;
            settingsCg.interactable = false;

            await UiMenuTransitions.CrossFadePanels(mainCg, settingsCg, panelFadeDuration);

            if (mainMenu != null)
                mainMenu.SetActive(false);

            settingsCg.blocksRaycasts = true;
            settingsCg.interactable = true;
            settingsCg.alpha = 1f;

            GetSettingsMenuUi()?.NotifySettingsMenuOpened();
        }
        finally
        {
            _panelFadeBusy = false;
        }
    }

    private async void RunCloseSettingsAnimated()
    {
        if (_panelFadeBusy)
            return;

        _panelFadeBusy = true;
        try
        {
            GetSettingsMenuUi()?.NotifySettingsMenuClosed();

            CanvasGroup mainCg = mainMenu != null ? UiMenuTransitions.EnsureCanvasGroup(mainMenu) : null;
            CanvasGroup settingsCg = UiMenuTransitions.EnsureCanvasGroup(settingsMenu);

            if (mainMenu != null)
            {
                mainMenu.SetActive(true);
                if (mainCg != null)
                    mainCg.alpha = 0f;
            }

            settingsCg.blocksRaycasts = false;
            settingsCg.interactable = false;

            await UiMenuTransitions.CrossFadePanels(settingsCg, mainCg, panelFadeDuration);

            settingsMenu.SetActive(false);

            if (mainCg != null)
            {
                mainCg.alpha = 1f;
                mainCg.blocksRaycasts = true;
                mainCg.interactable = true;
            }
        }
        finally
        {
            _panelFadeBusy = false;
        }
    }

    private async void RunOpenSaveMenuAnimated()
    {
        if (_panelFadeBusy)
            return;

        _panelFadeBusy = true;
        try
        {
            CanvasGroup mainCg = mainMenu != null ? UiMenuTransitions.EnsureCanvasGroup(mainMenu) : null;
            CanvasGroup saveCg = UiMenuTransitions.EnsureCanvasGroup(saveMenu);

            saveMenu.SetActive(true);
            saveCg.alpha = 0f;
            saveCg.blocksRaycasts = false;
            saveCg.interactable = false;

            await UiMenuTransitions.CrossFadePanels(mainCg, saveCg, panelFadeDuration);

            if (mainMenu != null)
                mainMenu.SetActive(false);

            saveCg.blocksRaycasts = true;
            saveCg.interactable = true;
            saveCg.alpha = 1f;
        }
        finally
        {
            _panelFadeBusy = false;
        }
    }

    private async void RunCloseSaveMenuAnimated()
    {
        if (_panelFadeBusy)
            return;

        _panelFadeBusy = true;
        try
        {
            CanvasGroup mainCg = mainMenu != null ? UiMenuTransitions.EnsureCanvasGroup(mainMenu) : null;
            CanvasGroup saveCg = UiMenuTransitions.EnsureCanvasGroup(saveMenu);

            if (mainMenu != null)
            {
                mainMenu.SetActive(true);
                if (mainCg != null)
                    mainCg.alpha = 0f;
            }

            saveCg.blocksRaycasts = false;
            saveCg.interactable = false;

            await UiMenuTransitions.CrossFadePanels(saveCg, mainCg, panelFadeDuration);

            saveMenu.SetActive(false);

            if (mainCg != null)
            {
                mainCg.alpha = 1f;
                mainCg.blocksRaycasts = true;
                mainCg.interactable = true;
            }
        }
        finally
        {
            _panelFadeBusy = false;
        }
    }
}
