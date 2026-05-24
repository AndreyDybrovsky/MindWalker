using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class SaveSlotListController : MonoBehaviour
{
    [Header("UI Elements - Слоты сохранений")]
    [Tooltip("4 кнопки слотов сохранений (должны иметь компонент SaveSlotItem)")]
    public SaveSlotItem[] saveSlots = new SaveSlotItem[4];
    
    [Header("UI Elements - Кнопки действий")]
    public Button newGameButton; // Кнопка "Новая игра"
    public Button loadButton; // Кнопка "Загрузить"
    public Button backButton; // Кнопка "Назад"
    
    private int selectedSlotIndex = -1;
    private bool slotsInitialized = false;

    private void OnEnable()
    {
        // При повторном открытии меню Start() не вызывается, поэтому обновляем слоты здесь.
        if (!slotsInitialized)
        {
            InitializeSlots();
        }

        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;

        RefreshSlots();
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }
    
    private void Start()
    {
        // Подписываемся на события кнопок
        if (newGameButton != null)
            newGameButton.onClick.AddListener(OnNewGameClicked);
        
        if (loadButton != null)
            loadButton.onClick.AddListener(OnLoadClicked);
        
        if (backButton != null)
            backButton.onClick.AddListener(OnBackClicked);
        
        // Инициализируем слоты
        if (!slotsInitialized)
            InitializeSlots();
        
        // Подписываемся на события SaveManager
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSavesLoaded += RefreshSlots;
            SaveManager.Instance.OnSaveCreated += OnSaveCreated;
            SaveManager.Instance.OnSaveDeleted += OnSaveDeleted;
            RefreshSlots();
        }
        else
        {
            Debug.LogWarning("SaveManager не найден! Убедитесь, что SaveManager существует в сцене.");
        }
        
        // Изначально все кнопки неактивны
        UpdateButtonStates();
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        RefreshSlots();
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        RefreshSlots();
    }
    
    private void OnDestroy()
    {
        // Отписываемся от событий
        if (SaveManager.Instance != null)
        {
            SaveManager.Instance.OnSavesLoaded -= RefreshSlots;
            SaveManager.Instance.OnSaveCreated -= OnSaveCreated;
            SaveManager.Instance.OnSaveDeleted -= OnSaveDeleted;
        }
        
        if (newGameButton != null)
            newGameButton.onClick.RemoveListener(OnNewGameClicked);
        
        if (loadButton != null)
            loadButton.onClick.RemoveListener(OnLoadClicked);
        
        if (backButton != null)
            backButton.onClick.RemoveListener(OnBackClicked);
    }
    
    private void InitializeSlots()
    {
        // Инициализируем существующие слоты (должны быть назначены в инспекторе)
        int initializedCount = 0;
        for (int i = 0; i < saveSlots.Length && i < 4; i++)
        {
            if (saveSlots[i] != null)
            {
                saveSlots[i].Initialize(i, this);
                initializedCount++;
            }
            else
            {
                Debug.LogWarning($"Слот сохранения {i} не назначен в инспекторе! Назначьте его в SaveSlotListController.");
            }
        }
        
        if (initializedCount == 0)
        {
            Debug.LogError("Ни один слот сохранения не назначен! Назначьте 4 кнопки слотов в массиве saveSlots в инспекторе.");
        }
        else
        {
            Debug.Log($"Инициализировано слотов: {initializedCount} из 4");
        }

        slotsInitialized = initializedCount > 0;
    }
    
    public void RefreshSlots()
    {
        foreach (var slotItem in saveSlots)
        {
            if (slotItem != null)
            {
                slotItem.UpdateDisplay();
            }
        }
        
        UpdateButtonStates();
    }
    
    private void OnSaveCreated(int slotIndex)
    {
        RefreshSlots();
        // Автоматически выбираем сохраненный слот
        if (slotIndex >= 0 && slotIndex < saveSlots.Length && saveSlots[slotIndex] != null)
        {
            OnSlotSelected(slotIndex);
        }
    }
    
    private void OnSaveDeleted(int slotIndex)
    {
        RefreshSlots();
        // НЕ сбрасываем selectedSlotIndex, если удаляется выбранный слот
        // Это нужно для случая, когда мы создаём новую игру (сначала удаляем старое сохранение)
        // if (selectedSlotIndex == slotIndex)
        // {
        //     selectedSlotIndex = -1;
        // }
        UpdateButtonStates();
    }
    
    public void OnSlotSelected(int slotIndex)
    {
        Debug.Log($"Выбран слот: {slotIndex}");
        
        // Проверяем валидность индекса
        if (slotIndex < 0 || slotIndex >= saveSlots.Length)
        {
            Debug.LogError($"Неверный индекс слота: {slotIndex} (должен быть от 0 до {saveSlots.Length - 1})");
            return;
        }
        
        // Снимаем выделение с предыдущего слота
        if (selectedSlotIndex >= 0 && selectedSlotIndex < saveSlots.Length)
        {
            if (saveSlots[selectedSlotIndex] != null)
            {
                saveSlots[selectedSlotIndex].SetSelected(false);
            }
        }
        
        // Выделяем новый слот
        selectedSlotIndex = slotIndex;
        if (saveSlots[selectedSlotIndex] != null)
        {
            saveSlots[selectedSlotIndex].SetSelected(true);
            Debug.Log($"Слот {selectedSlotIndex} выделен");
        }
        else
        {
            Debug.LogError($"Слот {selectedSlotIndex} не назначен в массиве saveSlots!");
        }
        
        UpdateButtonStates();
    }
    
    private void UpdateButtonStates()
    {
        bool hasSelection = selectedSlotIndex >= 0;
        bool isSlotFilled = false;
        
        if (hasSelection && SaveManager.Instance != null)
        {
            GameSaveData saveData = SaveManager.Instance.GetSaveData(selectedSlotIndex);
            isSlotFilled = saveData != null && !saveData.IsEmpty();
        }
        
        // Кнопка "Новая игра" активна только если выбран слот (любой)
        if (newGameButton != null)
            newGameButton.interactable = hasSelection;
        
        // Кнопка "Загрузить" активна только если выбран заполненный слот
        if (loadButton != null)
            loadButton.interactable = hasSelection && isSlotFilled;
    }
    
    private void OnNewGameClicked()
    {
        Debug.Log($"OnNewGameClicked вызван, selectedSlotIndex = {selectedSlotIndex}");
        
        if (selectedSlotIndex < 0)
        {
            Debug.LogWarning("Выберите слот для новой игры! Нажмите на один из слотов сохранения перед нажатием 'Новая игра'.");
            return;
        }
        
        if (SaveManager.Instance == null)
        {
            Debug.LogError("SaveManager не найден!");
            return;
        }
        
        // Сохраняем выбранный слот в локальную переменную, чтобы не потерять его
        int slotToUse = selectedSlotIndex;
        Debug.Log($"Сохраняем слот для использования: {slotToUse}");
        
        // СНАЧАЛА устанавливаем текущий слот, чтобы он не сбросился при удалении
        SaveManager.Instance.SetCurrentSaveSlot(slotToUse);
        Debug.Log($"Установлен активный слот для новой игры: {slotToUse}");
        
        // ПОТОМ очищаем сохранение в выбранном слоте (если было)
        // Передаём флаг, чтобы не сбрасывать currentSaveSlot
        SaveManager.Instance.DeleteSave(slotToUse, keepCurrentSlot: true);
        
        // Дополнительная проверка после установки
        int verifySlot = SaveManager.Instance.GetCurrentSaveSlot();
        Debug.Log($"Проверка: текущий слот в SaveManager = {verifySlot}");
        
        // Запускаем катсцену (приветственное меню) перед переходом на игровую сцену
        MainMenuController menuController = FindFirstObjectByType<MainMenuController>();
        if (menuController != null)
        {
            // Используем StartNewGame() для запуска катсцены
            menuController.StartNewGame();
        }
        else
        {
            // Если нет MainMenuController, пытаемся найти WelcomeMenuController напрямую
            WelcomeMenuController welcomeController = FindFirstObjectByType<WelcomeMenuController>();
            if (welcomeController != null)
            {
                welcomeController.StartWelcomeSequence();
            }
            else
            {
                Debug.LogError("MainMenuController и WelcomeMenuController не найдены! Невозможно запустить катсцену.");
            }
        }
    }
    
    private void OnLoadClicked()
    {
        if (selectedSlotIndex < 0)
        {
            Debug.LogWarning("Выберите слот для загрузки");
            return;
        }
        
        if (SaveManager.Instance == null)
        {
            Debug.LogError("SaveManager не найден!");
            return;
        }
        
        GameSaveData saveData = SaveManager.Instance.LoadGame(selectedSlotIndex);
        if (saveData != null)
        {
            // Устанавливаем текущий слот ПЕРЕД загрузкой сцены
            SaveManager.Instance.SetCurrentSaveSlot(selectedSlotIndex);
            Debug.Log($"Установлен активный слот: {selectedSlotIndex}");
            // ApplySaveData сам загрузит сцену, не нужно вызывать LoadScene отдельно
            SaveManager.Instance.ApplySaveData(saveData);
        }
        else
        {
            Debug.LogWarning("Не удалось загрузить сохранение");
        }
    }
    
    private void OnBackClicked()
    {
        // Закрываем меню сохранений
        MainMenuController menuController = FindFirstObjectByType<MainMenuController>();
        if (menuController != null)
        {
            menuController.CloseSaveMenu();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}
