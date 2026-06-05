using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class SaveManager : MonoBehaviour
{
    private static SaveManager instance;
    public static SaveManager Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<SaveManager>();
                if (instance == null && Application.isPlaying)
                {
                    GameObject go = new GameObject("SaveManager");
                    instance = go.AddComponent<SaveManager>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }
    
    [Header("Настройки сохранений")]
    [SerializeField] private int maxSaveSlots = 4;
    [SerializeField] private string saveFolderName = "Saves";
    
    private string savesDirectory;
    private Dictionary<int, GameSaveData> saveSlots = new Dictionary<int, GameSaveData>();
    private int currentSaveSlot = -1; // Текущий активный слот сохранения
    
    // Время последнего сохранения (в реальном времени, не зависит от timeScale)
    private float lastSaveTime = 0f;

    private const string LobbySceneName = "Main";
    private bool _skipNextAutoSave;
    
    // События для уведомления об изменениях
    public event Action<int> OnSaveCreated;
    public event Action<int> OnSaveDeleted;
    public event Action OnSavesLoaded;
    
    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        if (Application.isPlaying)
            DontDestroyOnLoad(gameObject);
        
        // Создаем директорию для сохранений
        savesDirectory = Path.Combine(Application.persistentDataPath, saveFolderName);
        if (!Directory.Exists(savesDirectory))
        {
            Directory.CreateDirectory(savesDirectory);
        }
        
        // Загружаем все сохранения
        LoadAllSaves();
        
        // Загружаем текущий активный слот из PlayerPrefs
        currentSaveSlot = PlayerPrefs.GetInt("CurrentSaveSlot", -1);
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
    }
    
    // Загружает все сохранения из файлов
    public void LoadAllSaves()
    {
        saveSlots.Clear();
        
        for (int i = 0; i < maxSaveSlots; i++)
        {
            string savePath = GetSavePath(i);
            if (File.Exists(savePath))
            {
                try
                {
                    string json = File.ReadAllText(savePath);
                    GameSaveData saveData = JsonUtility.FromJson<GameSaveData>(json);
                    // Мягкая миграция старых сейвов: если имя было записано русским текстом, переводим на ключ+номер
                    if (saveData != null)
                    {
                        // Любой существующий файл считаем непустым, чтобы метаданные отображались
                        saveData.isEmpty = false;

                        if (string.IsNullOrEmpty(saveData.saveNameKey))
                        {
                            // Если это дефолтное имя из старой версии
                            if (!string.IsNullOrEmpty(saveData.saveName) && saveData.saveName.StartsWith("Сохранение "))
                            {
                                saveData.saveNameKey = "save.default_name";
                                saveData.saveNameNumber = i + 1;
                            }
                        }
                        else if (saveData.saveNameNumber <= 0)
                        {
                            saveData.saveNameNumber = i + 1;
                        }

                        // Если по какой-то причине дата пустая, выставим текущую
                        if (string.IsNullOrEmpty(saveData.saveDate))
                        {
                            saveData.saveDate = System.DateTime.Now.ToString("dd.MM.yyyy HH:mm");
                        }

                        if (saveData.lostPatientLevels == null)
                            saveData.lostPatientLevels = new List<string>();
                        if (saveData.autoSavedScenes == null)
                            saveData.autoSavedScenes = new List<string>();
                    }
                    saveSlots[i] = saveData;
                }
                catch (Exception e)
                {
                    Debug.LogError($"Ошибка при загрузке сохранения {i}: {e.Message}");
                }
            }
            else
            {
                // Создаем пустое сохранение
                saveSlots[i] = new GameSaveData(i);
            }
        }
        
        OnSavesLoaded?.Invoke();
        Debug.Log($"Загружено сохранений: {saveSlots.Count}");

        if (currentSaveSlot >= 0 && saveSlots.TryGetValue(currentSaveSlot, out GameSaveData activeData) && activeData != null)
            LobbyPatientProgress.LoadFromSave(activeData.lostPatientLevels);
    }
    
    // Сохраняет данные в указанный слот
    public bool SaveGame(int slotIndex, GameSaveData saveData)
    {
        if (slotIndex < 0 || slotIndex >= maxSaveSlots)
        {
            Debug.LogError($"Неверный индекс слота сохранения: {slotIndex}");
            return false;
        }
        
        try
        {
            saveData.saveSlotIndex = slotIndex;
            saveData.UpdateSaveDate();
            
            string json = JsonUtility.ToJson(saveData, true);
            string savePath = GetSavePath(slotIndex);
            File.WriteAllText(savePath, json);
            
            saveSlots[slotIndex] = saveData;
            currentSaveSlot = slotIndex;
            
            // Обновляем время последнего сохранения
            lastSaveTime = Time.realtimeSinceStartup;
            
            OnSaveCreated?.Invoke(slotIndex);
            Debug.Log($"Игра сохранена в слот {slotIndex}");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при сохранении игры в слот {slotIndex}: {e.Message}");
            return false;
        }
    }
    
    // Сохраняет текущую игру в текущий слот
    public bool SaveCurrentGame()
    {
        if (currentSaveSlot < 0)
        {
            Debug.LogWarning("Нет активного слота сохранения. Создайте новую игру или загрузите существующую.");
            return false;
        }

        GameSaveData saveData = CollectGameData();
        PreserveSlotMetadata(currentSaveSlot, saveData);
        return SaveGame(currentSaveSlot, saveData);
    }

    private void PreserveSlotMetadata(int slotIndex, GameSaveData saveData)
    {
        if (saveData == null || !saveSlots.TryGetValue(slotIndex, out GameSaveData existing) || existing == null)
            return;

        if (!string.IsNullOrEmpty(existing.saveName))
            saveData.saveName = existing.saveName;
        if (!string.IsNullOrEmpty(existing.saveNameKey))
            saveData.saveNameKey = existing.saveNameKey;
        if (existing.saveNameNumber > 0)
            saveData.saveNameNumber = existing.saveNameNumber;
    }
    
    // Сохраняет игру в новый слот (создает новую игру)
    public bool SaveNewGame(int slotIndex)
    {
        CollectableDocumentProgress.ClearForNewGame();
        GameSaveData saveData = CollectGameData();
        saveData.saveName = "";
        saveData.saveNameKey = "save.default_name";
        saveData.saveNameNumber = slotIndex + 1;
        return SaveGame(slotIndex, saveData);
    }
    
    // Загружает сохранение из указанного слота
    public GameSaveData LoadGame(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= maxSaveSlots)
        {
            Debug.LogError($"Неверный индекс слота сохранения: {slotIndex}");
            return null;
        }
        
        if (!saveSlots.ContainsKey(slotIndex) || saveSlots[slotIndex].IsEmpty())
        {
            Debug.LogWarning($"Слот сохранения {slotIndex} пуст");
            return null;
        }
        
        currentSaveSlot = slotIndex;
        return saveSlots[slotIndex];
    }
    
    /// <summary>Пропустить одно автосохранение (загрузка слота из меню).</summary>
    public bool ConsumeAutoSaveSkip()
    {
        if (!_skipNextAutoSave)
            return false;

        _skipNextAutoSave = false;
        return true;
    }

    public bool ShouldRunAutoSaveForScene(string sceneName)
    {
        if (currentSaveSlot < 0)
            return false;

        GameSaveData data = GetSaveData(currentSaveSlot);
        if (data == null || data.IsEmpty())
            return false;

        if (data.autoSavedScenes == null)
            data.autoSavedScenes = new List<string>();

        if (sceneName == LobbySceneName)
            return true;

        return !data.autoSavedScenes.Contains(sceneName);
    }

    public void NotifyAutoSaveCompleted(string sceneName)
    {
        if (currentSaveSlot < 0 || sceneName == LobbySceneName)
            return;

        GameSaveData data = GetSaveData(currentSaveSlot);
        if (data == null)
            return;

        if (data.autoSavedScenes == null)
            data.autoSavedScenes = new List<string>();

        if (!data.autoSavedScenes.Contains(sceneName))
            data.autoSavedScenes.Add(sceneName);
    }

    // Применяет загруженные данные к игре
    public void ApplySaveData(GameSaveData saveData)
    {
        if (saveData == null || saveData.IsEmpty())
        {
            Debug.LogWarning("Попытка применить пустые данные сохранения");
            return;
        }

        _skipNextAutoSave = true;
        
        // Сохраняем данные для применения после загрузки сцены
        pendingSaveData = saveData;
        
        // Загружаем сцену
        if (!string.IsNullOrEmpty(saveData.currentScene))
        {
            // Подписываемся на событие загрузки сцены
            SceneManager.sceneLoaded += OnSceneLoaded;
            SceneManager.LoadScene(saveData.currentScene);
        }
        else
        {
            // Если сцена не указана, применяем данные к текущей сцене
            StartCoroutine(ApplySaveDataAfterSceneLoad(saveData));
        }
    }
    
    private GameSaveData pendingSaveData;
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // Отписываемся от события
        SceneManager.sceneLoaded -= OnSceneLoaded;
        
        // Применяем данные после загрузки сцены
        if (pendingSaveData != null)
        {
            StartCoroutine(ApplySaveDataAfterSceneLoad(pendingSaveData));
            pendingSaveData = null;
        }
    }
    
    private System.Collections.IEnumerator ApplySaveDataAfterSceneLoad(GameSaveData saveData)
    {
        // Ждем несколько кадров, чтобы все объекты инициализировалисьd
        yield return null; // Пропускаем текущий кадр
        yield return new WaitForEndOfFrame(); // Ждем конца кадра
        yield return new WaitForSeconds(0.3f); // Увеличиваем задержку для полной инициализации всех скриптов
        
        Debug.Log("Начинаем применение данных сохранения...");
        
        bool applyPlayerTransform = !SaveGamePlayerUtility.IsPtsdInDangerScene();
        yield return SaveGamePlayerUtility.ApplyPlayerStateWhenReady(saveData, applyPlayerTransform);

        if (applyPlayerTransform)
            Debug.Log($"Игрок восстановлен: HP {saveData.playerHealth}/{saveData.playerMaxHealth}, позиция {saveData.playerPosition}");
        else
            Debug.Log($"Игрок восстановлен (без телепорта): HP {saveData.playerHealth}/{saveData.playerMaxHealth}");

        int restoredCount = SaveGameEnemyUtility.RestoreEnemies(saveData);
        
        // Применяем время таймера
        GameTimer gameTimer = FindFirstObjectByType<GameTimer>();
        if (gameTimer != null && saveData.timerRemainingTime > 0f)
        {
            // Устанавливаем оставшееся время таймера из сохранения
            gameTimer.StopTimer();
            gameTimer.SetRemainingTime(saveData.timerRemainingTime);
            gameTimer.ResumeTimer(); // Возобновляем таймер с сохраненным временем
            Debug.Log($"Время таймера восстановлено: {saveData.timerRemainingTime:F1} сек");
        }
        
        // Загружаем прогресс завершенных локаций из сохранения в GlobalProgressTracker
        if (GlobalProgressTracker.Instance != null)
        {
            // Всегда загружаем прогресс из сохранения (даже если он пустой)
            // Это гарантирует, что прогресс привязан к сохранению, а не к глобальному PlayerPrefs
            if (saveData.completedLevels != null && saveData.completedLevels.Count > 0)
            {
                GlobalProgressTracker.Instance.LoadProgressFromSave(saveData.completedLevels);
                Debug.Log($"Восстановлен прогресс завершенных локаций: {saveData.completedLevels.Count} локаций");
            }
            else
            {
                // Если в сохранении нет прогресса (новое сохранение), очищаем глобальный прогресс
                GlobalProgressTracker.Instance.LoadProgressFromSave(new List<string>());
                Debug.Log("Прогресс локаций очищен (новое сохранение)");
            }
        }

        if (saveData.lostPatientLevels != null)
            LobbyPatientProgress.LoadFromSave(saveData.lostPatientLevels);
        else
            LobbyPatientProgress.ClearAll();
        
        // Восстанавливаем босса, если он был заспавнен
        if (saveData.bossWasSpawned)
        {
            BossSpawnManager bossSpawnManager = BossSpawnManager.Instance != null
                ? BossSpawnManager.Instance
                : FindFirstObjectByType<BossSpawnManager>();
            
            if (bossSpawnManager != null && bossSpawnManager.WillSpawnBoss)
            {
                bossSpawnManager.LoadBossState(saveData.bossPosition, saveData.bossRotation, 
                    saveData.bossHealth, saveData.bossMaxHealth, saveData.bossIsAlive);
                Debug.Log($"Восстановлен босс: позиция {saveData.bossPosition}, здоровье {saveData.bossHealth}/{saveData.bossMaxHealth}, жив: {saveData.bossIsAlive}");
            }
            else
            {
                Debug.LogWarning("SaveManager: Босс был заспавнен в сохранении, но BossSpawnManager не найден или не настроен!");
            }
        }

        EnemyCounter enemyCounter = FindFirstObjectByType<EnemyCounter>();
        if (enemyCounter != null)
            enemyCounter.RebuildFromScene();
        
        // Дополнительная задержка для обновления NavMesh агентов
        yield return new WaitForSeconds(0.1f);
        
        DepressionPanicMomentZone.RestoreAllFromSave(saveData);
        OCDMomentTrigger.RestoreAllFromSave(saveData);
        CollectableDocumentProgress.LoadFromSave(saveData);
        CollectableDocumentProgress.RefreshSceneDocuments();

        Debug.Log($"Данные сохранения применены к игре. Восстановлено противников: {restoredCount}/{saveData.enemies.Count}");
        _skipNextAutoSave = false;
    }

    /// <summary>
    /// Перезагрузка чекпоинта текущего слота (после смерти). Не перезаписывает слот «мёртвым» состоянием.
    /// </summary>
    public bool ReloadCurrentCheckpoint(bool persistLostPatientForActiveScene = false)
    {
        if (currentSaveSlot < 0)
            return false;

        LoadAllSaves();
        GameSaveData data = GetSaveData(currentSaveSlot);
        if (data == null || data.IsEmpty())
            return false;

        if (persistLostPatientForActiveScene)
        {
            string scene = SceneManager.GetActiveScene().name;
            if (data.lostPatientLevels == null)
                data.lostPatientLevels = new List<string>();

            LobbyPatientProgress.LoadFromSave(data.lostPatientLevels);
            LobbyPatientProgress.MarkLost(scene);
            data.lostPatientLevels = LobbyPatientProgress.ExportToList();
        }

        if (data.playerHealth <= 0f)
            data.playerHealth = data.playerMaxHealth > 0f ? data.playerMaxHealth : 100f;

        SaveGame(currentSaveSlot, data);

        _skipNextAutoSave = true;
        pendingSaveData = data;

        string sceneToLoad = string.IsNullOrEmpty(data.currentScene)
            ? SceneManager.GetActiveScene().name
            : data.currentScene;

        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.LoadScene(sceneToLoad);
        return true;
    }

    // Собирает текущие данные игры для сохранения
    private GameSaveData CollectGameData()
    {
        int slotIndex = currentSaveSlot >= 0 ? currentSaveSlot : 0;
        GameSaveData saveData = GetSaveData(slotIndex);
        if (saveData == null || saveData.IsEmpty())
            saveData = new GameSaveData(slotIndex);

        saveData.UpdateSaveDate();
        saveData.currentScene = SceneManager.GetActiveScene().name;

        bool collectPlayerTransform = !SaveGamePlayerUtility.IsPtsdInDangerScene();
        if (!SaveGamePlayerUtility.TryCollectPlayerState(saveData, collectPlayerTransform))
            Debug.Log("SaveManager: игрок в сцене не найден — позиция и HP оставлены из предыдущего сохранения.");
        
        // Собираем данные противников (в лобби врагов нет — не затираем список из сейва)
        EnemyHealth[] enemyHealthComponents = FindObjectsByType<EnemyHealth>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (saveData.enemies == null)
            saveData.enemies = new List<GameSaveData.EnemyData>();

        if (enemyHealthComponents.Length > 0)
        {
            saveData.enemies.Clear();

            BossSpawnManager bossSpawnManager = BossSpawnManager.Instance != null
                ? BossSpawnManager.Instance
                : FindFirstObjectByType<BossSpawnManager>();

            GameObject bossInScene = null;
            if (bossSpawnManager != null && bossSpawnManager.IsBossSpawned)
            {
                foreach (EnemyHealth bossHealth in enemyHealthComponents)
                {
                    GameObject enemy = bossHealth != null ? bossHealth.gameObject : null;
                    if (enemy != null && enemy.name.Contains("(Boss)"))
                    {
                        bossInScene = enemy;
                        break;
                    }
                }
            }

            foreach (EnemyHealth enemyHealth in enemyHealthComponents)
            {
                if (enemyHealth == null)
                    continue;

                GameObject enemy = enemyHealth.gameObject;
                if (!enemy.CompareTag("Enemy") || enemy == bossInScene)
                    continue;

                string enemyName = enemy.name;
                if (enemyName.EndsWith("(Clone)"))
                    enemyName = enemyName.Replace("(Clone)", "");

                bool isPatrolGuard = enemy.TryGetComponent(out PatrolConeGuardEnemy _);

                GameSaveData.EnemyData enemyData = new GameSaveData.EnemyData
                {
                    enemyId = enemyName,
                    position = isPatrolGuard ? Vector3.zero : enemy.transform.position,
                    rotation = isPatrolGuard ? Quaternion.identity : enemy.transform.rotation,
                    isAlive = !enemyHealth.IsDead,
                    enemyType = isPatrolGuard ? "PatrolConeGuard" : "EnemyController",
                    health = enemyHealth.CurrentHealth,
                    maxHealth = enemyHealth.MaxHealth
                };

                saveData.enemies.Add(enemyData);
            }

            if (bossInScene != null)
            {
                saveData.bossWasSpawned = true;
                saveData.bossPosition = bossInScene.transform.position;
                saveData.bossRotation = bossInScene.transform.rotation;
                saveData.bossIsAlive = bossInScene.activeSelf;

                if (bossInScene.TryGetComponent(out EnemyHealth bossHealth))
                {
                    saveData.bossHealth = bossHealth.CurrentHealth;
                    saveData.bossMaxHealth = bossHealth.MaxHealth;
                }
            }
            else
            {
                saveData.bossWasSpawned = false;
            }
        }
        
        GameTimer gameTimer = FindFirstObjectByType<GameTimer>();
        if (gameTimer != null)
            saveData.timerRemainingTime = gameTimer.GetRemainingTime();

        EnemyCounter enemyCounter = FindFirstObjectByType<EnemyCounter>();
        if (enemyCounter != null)
        {
            saveData.enemyCounterTotalEnemies = enemyCounter.TotalEnemies;
            saveData.enemyCounterDefeatedEnemies = enemyCounter.DefeatedEnemies;
        }
        
        // Сохраняем прогресс завершенных локаций из GlobalProgressTracker
        // ВАЖНО: Сохраняем ТОЛЬКО текущий прогресс из GlobalProgressTracker, который привязан к текущему сохранению
        // НЕ сохраняем прогресс, если это новое сохранение (чтобы избежать наследования прогресса из других сохранений)
        if (GlobalProgressTracker.Instance != null && currentSaveSlot >= 0)
        {
            saveData.completedLevels = GlobalProgressTracker.Instance.ExportCompletedLevels();
            Debug.Log($"Сохранено завершенных локаций: {saveData.completedLevels.Count}");
        }
        else if (saveData.completedLevels == null)
        {
            saveData.completedLevels = new List<string>();
        }

        saveData.lostPatientLevels = LobbyPatientProgress.ExportToList();
        DepressionPanicMomentZone.CaptureAllToSave(saveData);
        OCDMomentTrigger.CaptureAllToSave(saveData);
        CollectableDocumentProgress.WriteToSave(saveData);
        
        // Сохраняем время игры (если есть менеджер времени)
        // saveData.playTime = GameTimeManager.Instance.GetPlayTime();
        
        return saveData;
    }
    
    // Удаляет сохранение из указанного слота
    public bool DeleteSave(int slotIndex, bool keepCurrentSlot = false)
    {
        if (slotIndex < 0 || slotIndex >= maxSaveSlots)
        {
            Debug.LogError($"Неверный индекс слота сохранения: {slotIndex}");
            return false;
        }
        
        try
        {
            string savePath = GetSavePath(slotIndex);
            if (File.Exists(savePath))
            {
                File.Delete(savePath);
            }
            
            saveSlots[slotIndex] = new GameSaveData(slotIndex);
            
            // Очищаем прогресс GlobalProgressTracker при удалении сохранения (новая игра)
            if (GlobalProgressTracker.Instance != null)
            {
                GlobalProgressTracker.Instance.LoadProgressFromSave(new List<string>());
                Debug.Log($"SaveManager: Прогресс очищен при удалении сохранения {slotIndex} (новая игра)");
            }

            CollectableDocumentProgress.ClearForNewGame();
            LobbyPatientProgress.ClearAll();
            
            // Сбрасываем текущий слот только если не указано keepCurrentSlot
            if (!keepCurrentSlot && currentSaveSlot == slotIndex)
            {
                currentSaveSlot = -1;
                // Обновляем PlayerPrefs
                PlayerPrefs.SetInt("CurrentSaveSlot", -1);
                PlayerPrefs.Save();
                Debug.Log($"Текущий слот сброшен при удалении сохранения {slotIndex}");
            }
            
            OnSaveDeleted?.Invoke(slotIndex);
            Debug.Log($"Сохранение {slotIndex} удалено");
            return true;
        }
        catch (Exception e)
        {
            Debug.LogError($"Ошибка при удалении сохранения {slotIndex}: {e.Message}");
            return false;
        }
    }
    
    // Получает данные сохранения без загрузки
    public GameSaveData GetSaveData(int slotIndex)
    {
        if (saveSlots.ContainsKey(slotIndex))
        {
            return saveSlots[slotIndex];
        }
        return new GameSaveData(slotIndex);
    }
    
    // Получает путь к файлу сохранения
    private string GetSavePath(int slotIndex)
    {
        return Path.Combine(savesDirectory, $"save_{slotIndex}.json");
    }
    
    // Получает текущий активный слот
    public int GetCurrentSaveSlot()
    {
        return currentSaveSlot;
    }
    
    // Устанавливает текущий активный слот
    public void SetCurrentSaveSlot(int slotIndex)
    {
        Debug.Log($"SetCurrentSaveSlot вызван с индексом: {slotIndex}");
        if (slotIndex >= 0 && slotIndex < maxSaveSlots)
        {
            currentSaveSlot = slotIndex;
            // Сохраняем в PlayerPrefs для сохранения между сценами
            PlayerPrefs.SetInt("CurrentSaveSlot", slotIndex);
            PlayerPrefs.Save();
            Debug.Log($"Слот {slotIndex} установлен и сохранён в PlayerPrefs");

            if (saveSlots.TryGetValue(slotIndex, out GameSaveData slotData) && slotData != null)
                LobbyPatientProgress.LoadFromSave(slotData.lostPatientLevels);
        }
        else
        {
            Debug.LogError($"Неверный индекс слота: {slotIndex} (должен быть от 0 до {maxSaveSlots - 1})");
        }
    }
    
    // Получает максимальное количество слотов
    public int GetMaxSaveSlots()
    {
        return maxSaveSlots;
    }

    /// <summary>
    /// Проверяет, было ли сохранение недавно (в пределах указанного времени в секундах)
    /// </summary>
    public bool WasRecentSave(float timeThreshold = 5f)
    {
        if (lastSaveTime <= 0f)
        {
            // Сохранения еще не было
            return false;
        }
        
        float timeSinceLastSave = Time.realtimeSinceStartup - lastSaveTime;
        return timeSinceLastSave <= timeThreshold;
    }

    /// <summary>
    /// Получить время с последнего сохранения (в секундах)
    /// </summary>
    public float GetTimeSinceLastSave()
    {
        if (lastSaveTime <= 0f)
        {
            return float.MaxValue; // Сохранения еще не было
        }
        
        return Time.realtimeSinceStartup - lastSaveTime;
    }
}
