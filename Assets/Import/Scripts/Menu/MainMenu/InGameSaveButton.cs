using UnityEngine;
using UnityEngine.UI;

public class InGameSaveButton : MonoBehaviour
{
    [Header("UI Elements")]
    public Button saveButton;
    
    [Header("Settings")]
    public bool showConfirmation = true;
    public string saveSuccessMessage = "Игра сохранена!";
    
    private void Start()
    {
        if (saveButton != null)
        {
            saveButton.onClick.AddListener(OnSaveButtonClicked);
        }
        else
        {
            // Если кнопка не назначена, пытаемся найти её на этом объекте
            saveButton = GetComponent<Button>();
            if (saveButton != null)
            {
                saveButton.onClick.AddListener(OnSaveButtonClicked);
            }
            else
            {
                Debug.LogWarning("InGameSaveButton: Кнопка сохранения не найдена!");
            }
        }
    }
    
    private void OnDestroy()
    {
        if (saveButton != null)
        {
            saveButton.onClick.RemoveListener(OnSaveButtonClicked);
        }
    }
    
    private void OnSaveButtonClicked()
    {
        SaveCurrentGame();
    }
    
    public void SaveCurrentGame()
    {
        SaveManager saveManager = SaveManager.Instance;
        
        if (saveManager == null)
        {
            Debug.LogError("SaveManager не найден! Убедитесь, что SaveManager существует в сцене.");
            return;
        }
        
        int currentSlot = saveManager.GetCurrentSaveSlot();
        
        if (currentSlot < 0)
        {
            Debug.LogWarning("Нет активного слота сохранения. Сохранение невозможно.");
            Debug.LogWarning($"Попытка загрузить слот из PlayerPrefs: {PlayerPrefs.GetInt("CurrentSaveSlot", -1)}");
            // Попытка восстановить слот из PlayerPrefs
            int savedSlot = PlayerPrefs.GetInt("CurrentSaveSlot", -1);
            if (savedSlot >= 0)
            {
                saveManager.SetCurrentSaveSlot(savedSlot);
                currentSlot = savedSlot;
                Debug.Log($"Восстановлен слот из PlayerPrefs: {savedSlot}");
            }
            else
            {
                // Можно предложить выбрать слот или создать новое сохранение
                return;
            }
        }
        
        bool success = saveManager.SaveCurrentGame();
        
        if (success)
        {
            if (showConfirmation)
            {
                Debug.Log(saveSuccessMessage);
                // Здесь можно добавить UI уведомление о успешном сохранении
                ShowSaveConfirmation();
            }
        }
        else
        {
            Debug.LogError("Не удалось сохранить игру!");
        }
    }
    
    private void ShowSaveConfirmation()
    {
        // Здесь можно добавить показ UI сообщения о успешном сохранении
        // Например, через UI менеджер или простой текст на экране
        // StartCoroutine(ShowMessageCoroutine(saveSuccessMessage, 2f));
    }
    
    // Альтернативный метод для сохранения в конкретный слот
    public void SaveToSlot(int slotIndex)
    {
        SaveManager saveManager = SaveManager.Instance;
        
        if (saveManager == null)
        {
            Debug.LogError("SaveManager не найден!");
            return;
        }
        
        saveManager.SetCurrentSaveSlot(slotIndex);
        SaveCurrentGame();
    }
}
