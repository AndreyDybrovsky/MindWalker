using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public class SaveSlotItem : MonoBehaviour
{
    [Header("UI Elements")]
    public TextMeshProUGUI saveNameText;
    public TextMeshProUGUI saveDateText;
    public TextMeshProUGUI saveInfoText; // Дополнительная информация (HP и т.д.)
    public TextMeshProUGUI saveTimeText; // Отдельное поле для времени таймера
    public Button slotButton;
    public Image backgroundImage;
    public GameObject emptySlotIndicator;
    public GameObject filledSlotIndicator;
    
    [Header("Visual Settings")]
    public Color normalColor = Color.white;
    public Color selectedColor = Color.yellow;
    public Color emptyColor = Color.gray;
    
    private int slotIndex;
    private SaveSlotListController listController;
    private bool isSelected = false;

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
        }

        // Если язык меняют через Unity Localization (не через наш менеджер/дропдаун),
        // всё равно обновляем UI.
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
        {
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
        }

        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        UpdateDisplay();
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        UpdateDisplay();
    }
    
    public void Initialize(int index, SaveSlotListController controller)
    {
        slotIndex = index;
        listController = controller;
        
        // Если кнопка не назначена, пытаемся найти её на этом объекте
        if (slotButton == null)
        {
            slotButton = GetComponent<Button>();
        }
        
        if (slotButton != null)
        {
            slotButton.onClick.RemoveAllListeners();
            slotButton.onClick.AddListener(OnSlotClicked);
            Debug.Log($"Слот {slotIndex} инициализирован, кнопка назначена");
        }
        else
        {
            Debug.LogError($"Слот {slotIndex}: кнопка не найдена! Убедитесь, что Button назначен в инспекторе или находится на том же объекте.");
        }
        
        UpdateDisplay();
    }
    
    public void UpdateDisplay()
    {
        LocalizationManager loc = LocalizationManager.Instance;

        if (SaveManager.Instance == null)
        {
            // Если SaveManager еще не создан, показываем пустой слот
            if (saveNameText != null)
                saveNameText.text = loc != null ? loc.T("save.default_name", slotIndex + 1) : $"Сохранение {slotIndex + 1}";
            if (saveDateText != null)
                saveDateText.text = "";
            if (saveInfoText != null)
                saveInfoText.text = "";
            if (saveTimeText != null)
                saveTimeText.text = "";
            return;
        }
        
        GameSaveData saveData = SaveManager.Instance.GetSaveData(slotIndex);
        
        if (saveData == null || saveData.IsEmpty())
        {
            // Отображаем пустой слот
            if (saveNameText != null)
                saveNameText.text = loc != null ? loc.T("save.default_name", slotIndex + 1) : $"Сохранение {slotIndex + 1}";
            
            if (saveDateText != null)
                saveDateText.text = "";
            
            if (saveInfoText != null)
                saveInfoText.text = "";
            
            if (saveTimeText != null)
                saveTimeText.text = "";
            
            if (emptySlotIndicator != null)
                emptySlotIndicator.SetActive(true);
            
            if (filledSlotIndicator != null)
                filledSlotIndicator.SetActive(false);
            
            if (backgroundImage != null)
                backgroundImage.color = emptyColor;
        }
        else
        {
            // Отображаем заполненный слот
            if (saveNameText != null)
                saveNameText.text = GetDisplaySaveName(saveData, slotIndex);
            
            if (saveDateText != null)
            {
                // Отображаем время сохранения
                string date = string.IsNullOrEmpty(saveData.saveDate) ? "" : saveData.saveDate;
                string line = loc != null ? loc.T("save.saved_at", date) : "";
                // Если перевода нет — показываем просто дату (без подписи), чтобы метаданные не пропадали
                saveDateText.text = IsMissingTranslation(line, "save.saved_at") ? date : line;
            }
            
            // Отдельное отображение времени таймера
            if (saveTimeText != null)
            {
                if (saveData.timerRemainingTime > 0f)
                {
                    int minutes = Mathf.FloorToInt(saveData.timerRemainingTime / 60f);
                    int seconds = Mathf.FloorToInt(saveData.timerRemainingTime % 60f);
                    string mm = minutes.ToString("00");
                    string ss = seconds.ToString("00");
                    string line = loc != null ? loc.T("save.time", mm, ss) : $"Время: {mm}:{ss}";
                    saveTimeText.text = IsMissingTranslation(line, "save.time") ? $"{mm}:{ss}" : line;
                }
                else
                {
                    saveTimeText.text = ""; // Пусто, если таймера нет
                }
            }
            
            if (saveInfoText != null)
            {
                // Формируем информацию: Уровень, HP
                string sceneName = saveData.currentScene;
                if (string.IsNullOrEmpty(sceneName))
                {
                    string unknown = loc != null ? loc.T("save.unknown") : "Неизвестно";
                    sceneName = IsMissingTranslation(unknown, "save.unknown") ? "Неизвестно" : unknown;
                }
                else
                {
                    // Убираем расширение .unity если есть
                    if (sceneName.EndsWith(".unity"))
                    {
                        sceneName = sceneName.Replace(".unity", "");
                    }
                }
                
                string levelLine = loc != null ? loc.T("save.level", sceneName) : "";
                if (IsMissingTranslation(levelLine, "save.level"))
                    levelLine = sceneName; // без подписи, если перевода нет

                string hpLine = loc != null
                    ? loc.T("save.hp", saveData.playerHealth.ToString("F0"), saveData.playerMaxHealth.ToString("F0"))
                    : $"HP: {saveData.playerHealth:F0}/{saveData.playerMaxHealth:F0}";
                if (IsMissingTranslation(hpLine, "save.hp"))
                    hpLine = $"HP: {saveData.playerHealth:F0}/{saveData.playerMaxHealth:F0}";

                string info = $"{levelLine}\n{hpLine}";
                saveInfoText.text = info;
            }
            
            if (emptySlotIndicator != null)
                emptySlotIndicator.SetActive(false);
            
            if (filledSlotIndicator != null)
                filledSlotIndicator.SetActive(true);
            
            if (backgroundImage != null && !isSelected)
                backgroundImage.color = normalColor;
        }
        
        UpdateSelectionVisual();
    }

    private string GetDisplaySaveName(GameSaveData saveData, int index)
    {
        LocalizationManager loc = LocalizationManager.Instance;
        if (saveData == null)
            return loc != null ? loc.T("save.default_name", index + 1) : $"Сохранение {index + 1}";

        if (!string.IsNullOrEmpty(saveData.saveNameKey) && loc != null)
        {
            int n = saveData.saveNameNumber > 0 ? saveData.saveNameNumber : index + 1;
            string name = loc.T(saveData.saveNameKey, n);
            return IsMissingTranslation(name, saveData.saveNameKey) ? $"Сохранение {n}" : name;
        }

        if (!string.IsNullOrEmpty(saveData.saveName))
            return saveData.saveName;

        return loc != null ? loc.T("save.default_name", index + 1) : $"Сохранение {index + 1}";
    }

    private bool IsMissingTranslation(string value, string key)
    {
        if (string.IsNullOrEmpty(value))
            return true;
        return value == key;
    }
    
    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateSelectionVisual();
    }
    
    private void UpdateSelectionVisual()
    {
        if (backgroundImage != null)
        {
            if (isSelected)
            {
                backgroundImage.color = selectedColor;
            }
            else
            {
                if (SaveManager.Instance != null)
                {
                    GameSaveData saveData = SaveManager.Instance.GetSaveData(slotIndex);
                    if (saveData != null && !saveData.IsEmpty())
                    {
                        backgroundImage.color = normalColor;
                    }
                    else
                    {
                        backgroundImage.color = emptyColor;
                    }
                }
                else
                {
                    backgroundImage.color = emptyColor;
                }
            }
        }
    }
    
    private void OnSlotClicked()
    {
        Debug.Log($"Клик по слоту {slotIndex}");
        if (listController != null)
        {
            listController.OnSlotSelected(slotIndex);
        }
        else
        {
            Debug.LogError($"Слот {slotIndex}: listController не назначен!");
        }
    }
    
    public int GetSlotIndex()
    {
        return slotIndex;
    }
}
