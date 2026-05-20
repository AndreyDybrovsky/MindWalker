using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Пример использования таймера с отображением времени на UI
/// </summary>
public class TimerExample : MonoBehaviour
{
    [Header("Ссылки")]
    public GameTimer gameTimer;
    public Text timeDisplayText; // UI Text для отображения времени
    
    void Start()
    {
        // Если таймер не назначен, попробуем найти его автоматически
        if (gameTimer == null)
        {
            gameTimer = FindFirstObjectByType<GameTimer>();
        }
        
        // Подписываемся на события таймера
        if (gameTimer != null)
        {
            gameTimer.OnTimerEnd += OnTimerFinished;
            gameTimer.OnTimeUpdate += OnTimeUpdated;
        }
    }
    
    void OnDestroy()
    {
        // Отписываемся от событий при уничтожении объекта
        if (gameTimer != null)
        {
            gameTimer.OnTimerEnd -= OnTimerFinished;
            gameTimer.OnTimeUpdate -= OnTimeUpdated;
        }
    }
    
    /// <summary>
    /// Обработчик обновления времени
    /// </summary>
    private void OnTimeUpdated(float remainingTime)
    {
        if (timeDisplayText != null)
        {
            // Форматируем время в формат ММ:СС
            int minutes = Mathf.FloorToInt(remainingTime / 60f);
            int seconds = Mathf.FloorToInt(remainingTime % 60f);
            timeDisplayText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }
    
    /// <summary>
    /// Обработчик окончания таймера
    /// </summary>
    private void OnTimerFinished()
    {
        Debug.Log("Таймер закончился! Игра окончена.");
        if (timeDisplayText != null)
        {
            timeDisplayText.text = "00:00";
        }
        
        // Здесь можно добавить логику окончания игры
        // Например, показать экран Game Over, остановить игру и т.д.
    }
    
    /// <summary>
    /// Метод для запуска таймера (можно вызвать из UI кнопки)
    /// </summary>
    public void StartGameTimer()
    {
        if (gameTimer != null)
        {
            gameTimer.StartTimer();
        }
    }
    
    /// <summary>
    /// Метод для остановки таймера
    /// </summary>
    public void StopGameTimer()
    {
        if (gameTimer != null)
        {
            gameTimer.StopTimer();
        }
    }
}

