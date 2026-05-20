using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Менеджер пользовательского интерфейса для управления игровым меню
/// </summary>
public class UIManager : MonoBehaviour
{
    [Header("Настройки сцен")]
    [SerializeField] private string mainMenuSceneName = "MainMenu"; // Название сцены главного меню
    
    [Header("Ссылки")]
    [SerializeField] private GameSettingsScript gameSettingsScript; // Ссылка на скрипт игрового меню
    
    private void Awake()
    {
        // Подписываемся на событие загрузки сцены для переинициализации ссылок
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    
    private void OnDestroy()
    {
        // Отписываемся от события при уничтожении
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Переинициализируем ссылку на GameSettingsScript при загрузке новой сцены
        // Это решает проблему когда UIManager в DontDestroyOnLoad, а GameSettingsScript пересоздается
        gameSettingsScript = FindFirstObjectByType<GameSettingsScript>();
        
        if (gameSettingsScript == null)
        {
            Debug.LogWarning("UIManager: GameSettingsScript не найден в загруженной сцене!");
        }
    }
    
    /// <summary>
    /// Возврат в главное меню (вызывается из кнопки Exit)
    /// </summary>
    public void ExitToMainMenu()
    {
        // Восстанавливаем время перед загрузкой сцены
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f; // Стандартное значение
        
        // Разблокируем курсор
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        
        // Загружаем сцену главного меню
        SceneManager.LoadScene(mainMenuSceneName);
    }
    
    /// <summary>
    /// Продолжение игры (вызывается из кнопки Play)
    /// </summary>
    public void ResumeGame()
    {
        if (gameSettingsScript != null)
        {
            // Используем метод закрытия меню из GameSettingsScript
            gameSettingsScript.CloseMenu();
        }
        else
        {
            Debug.LogWarning("GameSettingsScript не найден! Меню не может быть закрыто.");
        }
    }
    
    /// <summary>
    /// Установка названия сцены главного меню программно
    /// </summary>
    public void SetMainMenuSceneName(string sceneName)
    {
        mainMenuSceneName = sceneName;
    }
}
