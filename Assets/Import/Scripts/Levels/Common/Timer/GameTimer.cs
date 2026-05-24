using UnityEngine;
using System.Collections;
using System;
using UnityEngine.SceneManagement;

[System.Serializable]
public class WarningSoundEntry
{
    [Tooltip("Предупреждающий звук")]
    public AudioClip sound;
    
    [Tooltip("Время в секундах до окончания, когда воспроизводится этот звук")]
    public float warningTime;
}

public class GameTimer : MonoBehaviour
{
    [Header("Основные настройки таймера")]
    [Tooltip("Общее время таймера в секундах")]
    public float totalTime = 60f;
    
    [Tooltip("Запускать таймер автоматически при старте")]
    public bool autoStart = true;
    
    [Header("Звуковые эффекты")]
    [Tooltip("Звук, который воспроизводится по окончанию времени")]
    public AudioClip endTimeSound;

    [Header("Переход при окончании таймера")]
    [SerializeField] private bool loadMainMenuOnTimerEnd = true;
    [SerializeField] private string mainMenuSceneName = "MainMenu";
    
    [Header("Предупреждающие звуки")]
    [Tooltip("Настройки предупреждающих звуков. Для каждого звука можно задать время воспроизведения")]
    public WarningSoundEntry[] warningSounds = new WarningSoundEntry[]
    {
        new WarningSoundEntry { warningTime = 45f },
        new WarningSoundEntry { warningTime = 30f },
        new WarningSoundEntry { warningTime = 15f },
        new WarningSoundEntry { warningTime = 5f }
    };
    
    private AudioSource audioSource;
    private float currentTime;
    private bool timerActive = false;
    private bool[] warningPlayed;
    private bool endSoundPlayed = false;
    
    // События для других скриптов
    public System.Action OnTimerEnd;
    public System.Action<float> OnTimeUpdate; // Передает оставшееся время
    
    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
        }
        
        // Настраиваем AudioSource для правильной работы
        if (audioSource != null)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            // Убеждаемся, что AudioSource включен
            audioSource.enabled = true;
        }
        
        InitializeTimer();
        
        // Автоматический запуск таймера, если включено
        if (autoStart)
        {
            StartTimer();
        }
    }
    
    void Update()
    {
        if (timerActive)
        {
            currentTime -= Time.deltaTime;
            
            // Обновляем событие для других скриптов
            if (OnTimeUpdate != null)
            {
                OnTimeUpdate(currentTime);
            }
            
            // Проверяем промежуточные звуки
            CheckWarningSounds();
            
            // Проверяем окончание времени
            if (currentTime <= 0f)
            {
                EndTimer();
            }
        }
    }
    
    /// <summary>
    /// Инициализация таймера
    /// </summary>
    public void InitializeTimer()
    {
        currentTime = totalTime;
        timerActive = false;
        
        // Инициализируем массив флагов в зависимости от количества звуков
        if (warningSounds != null)
        {
            warningPlayed = new bool[warningSounds.Length];
            // Сбрасываем флаги воспроизведения звуков
            for (int i = 0; i < warningPlayed.Length; i++)
            {
                warningPlayed[i] = false;
            }
        }
        else
        {
            warningPlayed = new bool[0];
        }
        endSoundPlayed = false;
    }
    
    /// <summary>
    /// Запуск таймера
    /// </summary>
    public void StartTimer()
    {
        InitializeTimer();
        timerActive = true;
    }
    
    /// <summary>
    /// Остановка таймера
    /// </summary>
    public void StopTimer()
    {
        timerActive = false;
    }
    
    /// <summary>
    /// Пауза таймера
    /// </summary>
    public void PauseTimer()
    {
        timerActive = false;
    }
    
    /// <summary>
    /// Возобновление таймера
    /// </summary>
    public void ResumeTimer()
    {
        timerActive = true;
    }
    
    /// <summary>
    /// Проверка и воспроизведение промежуточных звуков
    /// </summary>
    private void CheckWarningSounds()
    {
        if (warningSounds == null || warningSounds.Length == 0)
            return;
            
        float timeRemaining = currentTime;
        
        // Создаем массив времен для сортировки
        float[] warningTimes = new float[warningSounds.Length];
        for (int i = 0; i < warningSounds.Length; i++)
        {
            warningTimes[i] = warningSounds[i].warningTime;
        }
        
        // Проверяем каждый звук в порядке от большего к меньшему времени
        for (int i = 0; i < warningTimes.Length; i++)
        {
            // Проверяем, что время до окончания меньше или равно заданному времени предупреждения
            // И что оставшееся время больше следующего предупреждения (чтобы не воспроизводить все сразу)
            bool shouldPlay = false;
            
            if (i == warningTimes.Length - 1)
            {
                // Для последнего звука просто проверяем, что время <= warningTime
                shouldPlay = timeRemaining <= warningTimes[i] && timeRemaining > 0;
            }
            else
            {
                // Для остальных проверяем, что мы в диапазоне между текущим и следующим предупреждением
                float nextWarningTime = warningTimes[i + 1];
                shouldPlay = timeRemaining <= warningTimes[i] && timeRemaining > nextWarningTime;
            }
            
            if (!warningPlayed[i] && shouldPlay && warningSounds[i].sound != null)
            {
                PlayWarningSound(i);
                warningPlayed[i] = true;
            }
        }
    }
    
    /// <summary>
    /// Воспроизведение промежуточного звука
    /// </summary>
    private void PlayWarningSound(int index)
    {
        if (audioSource == null)
        {
            Debug.LogError("AudioSource не найден! Звук не может быть воспроизведен.");
            return;
        }
        
        if (index < 0 || index >= warningSounds.Length)
        {
            Debug.LogWarning($"Индекс звука {index} вне диапазона!");
            return;
        }
        
        if (warningSounds[index].sound == null)
        {
            Debug.LogWarning($"Предупреждающий звук #{index + 1} не назначен в Inspector!");
            return;
        }
        
        if (!audioSource.enabled)
        {
            Debug.LogWarning("AudioSource отключен! Включаю...");
            audioSource.enabled = true;
        }
        
        // Проверяем, что AudioSource готов к воспроизведению
        if (audioSource.isActiveAndEnabled)
        {
            audioSource.PlayOneShot(warningSounds[index].sound);
            Debug.Log($"Воспроизведен предупреждающий звук #{index + 1} (осталось времени: {currentTime:F1} сек, время предупреждения: {warningSounds[index].warningTime} сек)");
        }
        else
        {
            Debug.LogError($"AudioSource не активен! GameObject: {gameObject.name}, Active: {gameObject.activeInHierarchy}");
        }
    }
    
    /// <summary>
    /// Окончание таймера
    /// </summary>
    private void EndTimer()
    {
        timerActive = false;
        currentTime = 0f;
        
        // Воспроизводим финальный звук
        if (!endSoundPlayed)
        {
            if (endTimeSound == null)
            {
                Debug.LogWarning("Финальный звук не назначен в Inspector!");
            }
            else if (audioSource == null)
            {
                Debug.LogError("AudioSource не найден! Финальный звук не может быть воспроизведен.");
            }
            else
            {
                if (!audioSource.enabled)
                {
                    audioSource.enabled = true;
                }
                
                if (audioSource.isActiveAndEnabled)
                {
                    audioSource.PlayOneShot(endTimeSound);
                    endSoundPlayed = true;
                    Debug.Log("Время вышло! Воспроизведен финальный звук.");
                }
                else
                {
                    Debug.LogError($"AudioSource не активен! Финальный звук не воспроизведен.");
                }
            }
        }
        
        // По запросу: при окончании таймера отправляем в MainMenu
        if (loadMainMenuOnTimerEnd)
        {
            // На всякий случай возвращаем время (таймер может закончиться во время паузы/меню)
            Time.timeScale = 1f;
            Time.fixedDeltaTime = 0.02f;

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (!string.IsNullOrEmpty(mainMenuSceneName))
            {
                Debug.Log($"GameTimer: Время вышло! Переход в {mainMenuSceneName}");
                SceneManager.LoadScene(mainMenuSceneName);
            }
            else
            {
                Debug.LogError("GameTimer: mainMenuSceneName не задан!");
            }
        }
        
        // Вызываем событие
        if (OnTimerEnd != null)
        {
            OnTimerEnd();
        }
    }
    
    /// <summary>
    /// Получить оставшееся время
    /// </summary>
    public float GetRemainingTime()
    {
        return Mathf.Max(0f, currentTime);
    }
    
    /// <summary>
    /// Установить новое время таймера
    /// </summary>
    public void SetTotalTime(float newTime)
    {
        totalTime = newTime;
        if (!timerActive)
        {
            currentTime = totalTime;
        }
    }
    
    /// <summary>
    /// Установить оставшееся время таймера (для загрузки сохранения)
    /// </summary>
    public void SetRemainingTime(float remainingTime)
    {
        currentTime = Mathf.Max(0f, remainingTime);
        totalTime = Mathf.Max(totalTime, currentTime); // Обновляем totalTime, если currentTime больше
        // Сбрасываем флаги предупреждений, так как время изменилось
        if (warningPlayed != null)
        {
            for (int i = 0; i < warningPlayed.Length; i++)
            {
                warningPlayed[i] = false;
            }
        }
        endSoundPlayed = false;
        Debug.Log($"GameTimer: Установлено оставшееся время: {currentTime:F1} сек");
    }
    
    /// <summary>
    /// Установить время для предупреждающего звука по индексу
    /// </summary>
    public void SetWarningTime(int index, float time)
    {
        if (warningSounds != null && index >= 0 && index < warningSounds.Length)
        {
            warningSounds[index].warningTime = time;
        }
        else
        {
            Debug.LogWarning($"Индекс {index} вне диапазона массива warningSounds!");
        }
    }
    
    /// <summary>
    /// Получить время для предупреждающего звука по индексу
    /// </summary>
    public float GetWarningTime(int index)
    {
        if (warningSounds != null && index >= 0 && index < warningSounds.Length)
        {
            return warningSounds[index].warningTime;
        }
        return 0f;
    }
    
    /// <summary>
    /// Тестовый метод для проверки воспроизведения звука (можно вызвать из Inspector или кода)
    /// </summary>
    [ContextMenu("Тест: Воспроизвести первый предупреждающий звук")]
    public void TestPlayWarningSound1()
    {
        if (warningSounds != null && warningSounds.Length > 0 && warningSounds[0].sound != null && audioSource != null)
        {
            audioSource.PlayOneShot(warningSounds[0].sound);
            Debug.Log("Тест: Воспроизведен первый предупреждающий звук");
        }
        else
        {
            Debug.LogError("Тест не удался: звук не назначен или AudioSource отсутствует");
        }
    }
    
    [ContextMenu("Тест: Воспроизвести финальный звук")]
    public void TestPlayEndSound()
    {
        if (endTimeSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(endTimeSound);
            Debug.Log("Тест: Воспроизведен финальный звук");
        }
        else
        {
            Debug.LogError("Тест не удался: звук не назначен или AudioSource отсутствует");
        }
    }
    
    /// <summary>
    /// Проверка настроек AudioSource (для отладки)
    /// </summary>
    [ContextMenu("Проверить настройки AudioSource")]
    public void CheckAudioSourceSettings()
    {
        if (audioSource == null)
        {
            Debug.LogError("AudioSource не найден!");
            return;
        }
        
        Debug.Log($"=== Настройки AudioSource ===");
        Debug.Log($"Включен: {audioSource.enabled}");
        Debug.Log($"Активен: {audioSource.isActiveAndEnabled}");
        Debug.Log($"Громкость: {audioSource.volume}");
        Debug.Log($"Воспроизводится: {audioSource.isPlaying}");
        Debug.Log($"На паузе: {audioSource.time}");
        Debug.Log($"GameObject активен: {gameObject.activeInHierarchy}");
        Debug.Log($"=============================");
    }

}