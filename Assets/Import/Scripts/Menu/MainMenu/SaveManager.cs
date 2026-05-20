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
        return SaveGame(currentSaveSlot, saveData);
    }
    
    // Сохраняет игру в новый слот (создает новую игру)
    public bool SaveNewGame(int slotIndex)
    {
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
    
    // Применяет загруженные данные к игре
    public void ApplySaveData(GameSaveData saveData)
    {
        if (saveData == null || saveData.IsEmpty())
        {
            Debug.LogWarning("Попытка применить пустые данные сохранения");
            return;
        }
        
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
        
        // Применяем данные игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            Debug.Log($"Игрок найден, применяем позицию: {saveData.playerPosition}");
            
            // Для правильного телепорта с CharacterController нужно временно отключить его
            CharacterController characterController = player.GetComponent<CharacterController>();
            bool wasEnabled = false;
            if (characterController != null)
            {
                wasEnabled = characterController.enabled;
                characterController.enabled = false;
            }
            
            // Применяем позицию и поворот
            player.transform.position = saveData.playerPosition;
            player.transform.rotation = saveData.playerRotation;
            
            // Включаем CharacterController обратно
            if (characterController != null && wasEnabled)
            {
                characterController.enabled = true;
            }
            
            // Применяем здоровье игрока
            PlayerHealth healthComponent = player.GetComponent<PlayerHealth>();
            if (healthComponent != null)
            {
                healthComponent.SetHealth(saveData.playerHealth, saveData.playerMaxHealth);
                Debug.Log($"Здоровье игрока установлено: {saveData.playerHealth}/{saveData.playerMaxHealth}");
            }
            else
            {
                Debug.LogWarning("Компонент PlayerHealth не найден на игроке!");
            }
        }
        else
        {
            Debug.LogError("Игрок не найден в сцене! Убедитесь, что объект имеет тег 'Player'.");
        }
        
        // Применяем данные противников
        // Сначала получаем всех противников в сцене
        GameObject[] allEnemies = GameObject.FindGameObjectsWithTag("Enemy");
        Debug.Log($"Найдено противников в сцене: {allEnemies.Length}");
        Debug.Log($"Сохранено противников в данных: {saveData.enemies.Count}");
        
        // Создаем словарь для быстрого поиска противников по имени
        Dictionary<string, GameObject> enemyDict = new Dictionary<string, GameObject>();
        foreach (GameObject enemy in allEnemies)
        {
            if (enemy != null)
            {
                string enemyName = enemy.name;
                // Убираем "(Clone)" из имени, если оно есть (для инстансов префабов)
                if (enemyName.EndsWith("(Clone)"))
                {
                    enemyName = enemyName.Replace("(Clone)", "");
                }
                
                if (!enemyDict.ContainsKey(enemyName))
                {
                    enemyDict[enemyName] = enemy;
                }
                else
                {
                    // Если имя дублируется, используем полное имя с (Clone)
                    enemyDict[enemy.name] = enemy;
                }
            }
        }
        
        // Сначала деактивируем всех противников в сцене
        foreach (GameObject enemy in allEnemies)
        {
            if (enemy != null)
            {
                enemy.SetActive(false);
            }
        }
        
        // Затем активируем и позиционируем сохраненных противников
        int restoredCount = 0;
        foreach (var enemyData in saveData.enemies)
        {
            GameObject enemy = null;
            
            // Пытаемся найти противника по сохраненному имени
            if (enemyDict.ContainsKey(enemyData.enemyId))
            {
                enemy = enemyDict[enemyData.enemyId];
            }
            else
            {
                // Пытаемся найти по полному имени (с учетом (Clone))
                enemy = GameObject.Find(enemyData.enemyId);
            }
            
            if (enemy != null)
            {
                // Отключаем NavMeshAgent перед перемещением, чтобы избежать конфликтов
                UnityEngine.AI.NavMeshAgent agent = enemy.GetComponent<UnityEngine.AI.NavMeshAgent>();
                bool agentWasEnabled = false;
                if (agent != null)
                {
                    agentWasEnabled = agent.enabled;
                    agent.enabled = false;
                }
                
                // Применяем позицию и поворот
                enemy.transform.position = enemyData.position;
                enemy.transform.rotation = enemyData.rotation;
                
                // Включаем NavMeshAgent обратно, если он был включен
                if (agent != null && agentWasEnabled)
                {
                    agent.enabled = true;
                    // Обновляем позицию агента на NavMesh
                    if (agent.isOnNavMesh)
                    {
                        agent.Warp(enemyData.position);
                    }
                }
                
                // Применяем здоровье противника
                EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    // Устанавливаем здоровье через метод SetHealth
                    enemyHealth.SetHealth(enemyData.health, enemyData.maxHealth > 0 ? enemyData.maxHealth : enemyHealth.MaxHealth);
                    Debug.Log($"Восстановлено здоровье противника {enemyData.enemyId}: {enemyData.health}/{enemyData.maxHealth}");
                }
                
                // Устанавливаем активность противника
                enemy.SetActive(enemyData.isAlive);
                restoredCount++;
                Debug.Log($"Восстановлен противник: {enemyData.enemyId}, позиция: {enemyData.position}, жив: {enemyData.isAlive}, здоровье: {enemyData.health}");
            }
            else
            {
                Debug.LogWarning($"Противник с именем '{enemyData.enemyId}' не найден в сцене! Доступные имена: {string.Join(", ", enemyDict.Keys)}");
            }
        }
        
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
        
        // Восстанавливаем состояние счетчика врагов
        EnemyCounter enemyCounter = FindFirstObjectByType<EnemyCounter>();
        if (enemyCounter != null && saveData.enemyCounterTotalEnemies > 0)
        {
            enemyCounter.SetState(saveData.enemyCounterTotalEnemies, saveData.enemyCounterDefeatedEnemies);
        }
        
        // Восстанавливаем босса, если он был заспавнен
        if (saveData.bossWasSpawned)
        {
            BossSpawnManager bossSpawnManager = BossSpawnManager.Instance != null
                ? BossSpawnManager.Instance
                : FindFirstObjectByType<BossSpawnManager>();
            
            if (bossSpawnManager != null && bossSpawnManager.WillSpawnBoss)
            {
                // Заспавниваем босса с сохраненными данными
                bossSpawnManager.LoadBossState(saveData.bossPosition, saveData.bossRotation, 
                    saveData.bossHealth, saveData.bossMaxHealth, saveData.bossIsAlive);
                Debug.Log($"Восстановлен босс: позиция {saveData.bossPosition}, здоровье {saveData.bossHealth}/{saveData.bossMaxHealth}, жив: {saveData.bossIsAlive}");
            }
            else
            {
                Debug.LogWarning("SaveManager: Босс был заспавнен в сохранении, но BossSpawnManager не найден или не настроен!");
            }
        }
        
        // Дополнительная задержка для обновления NavMesh агентов
        yield return new WaitForSeconds(0.1f);
        
        Debug.Log($"Данные сохранения применены к игре. Восстановлено противников: {restoredCount}/{saveData.enemies.Count}");
    }
    
    // Собирает текущие данные игры для сохранения
    private GameSaveData CollectGameData()
    {
        GameSaveData saveData = new GameSaveData(currentSaveSlot >= 0 ? currentSaveSlot : 0);
        
        // Сохраняем текущую сцену
        saveData.currentScene = SceneManager.GetActiveScene().name;
        
        // Собираем данные игрока
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            saveData.playerPosition = player.transform.position;
            saveData.playerRotation = player.transform.rotation;
            
            // Получаем здоровье игрока
            PlayerHealth healthComponent = player.GetComponent<PlayerHealth>();
            if (healthComponent != null)
            {
                saveData.playerHealth = healthComponent.CurrentHealth;
                saveData.playerMaxHealth = healthComponent.MaxHealth;
            }
        }
        
        // Собираем данные противников
        GameObject[] enemies = GameObject.FindGameObjectsWithTag("Enemy");
        saveData.enemies.Clear();
        
        // Проверяем, есть ли босс в сцене (чтобы исключить его из обычных врагов)
        BossSpawnManager bossSpawnManager = BossSpawnManager.Instance != null
            ? BossSpawnManager.Instance
            : FindFirstObjectByType<BossSpawnManager>();
        
        GameObject bossInScene = null;
        if (bossSpawnManager != null && bossSpawnManager.IsBossSpawned)
        {
            // Ищем босса в сцене по имени (босс имеет имя "{bossPrefab.name} (Boss)")
            foreach (GameObject enemy in enemies)
            {
                if (enemy != null && enemy.name.Contains("(Boss)"))
                {
                    bossInScene = enemy;
                    break;
                }
            }
        }
        
        Debug.Log($"Собираем данные противников. Найдено: {enemies.Length} (босс: {(bossInScene != null ? "есть" : "нет")})");
        
        foreach (GameObject enemy in enemies)
        {
            if (enemy != null)
            {
                // Пропускаем босса - он сохраняется отдельно
                if (enemy == bossInScene)
                {
                    continue;
                }
                
                // Сохраняем имя противника, убирая "(Clone)" для единообразия
                string enemyName = enemy.name;
                if (enemyName.EndsWith("(Clone)"))
                {
                    enemyName = enemyName.Replace("(Clone)", "");
                }
                
                GameSaveData.EnemyData enemyData = new GameSaveData.EnemyData
                {
                    enemyId = enemyName,
                    position = enemy.transform.position,
                    rotation = enemy.transform.rotation,
                    isAlive = enemy.activeSelf,
                    enemyType = "EnemyController" // Тип противника
                };
                
                // Сохраняем здоровье противника, если есть компонент EnemyHealth
                // Сохраняем здоровье противника, если есть компонент EnemyHealth
                EnemyHealth enemyHealth = enemy.GetComponent<EnemyHealth>();
                if (enemyHealth != null)
                {
                    enemyData.health = enemyHealth.CurrentHealth;
                    enemyData.maxHealth = enemyHealth.MaxHealth;
                    Debug.Log($"Сохранено здоровье противника {enemyName}: {enemyData.health}/{enemyData.maxHealth}");
                }
                else
                {
                    // Если нет EnemyHealth, используем дефолтные значения
                    enemyData.health = enemy.activeSelf ? 100f : 0f;
                    enemyData.maxHealth = 100f;
                }
                
                saveData.enemies.Add(enemyData);
                Debug.Log($"Сохранен противник: {enemyName}, позиция: {enemy.transform.position}, жив: {enemy.activeSelf}");
            }
        }
        
        Debug.Log($"Всего сохранено противников: {saveData.enemies.Count}");
        
        // Сохраняем данные босса (если он был заспавнен)
        if (bossInScene != null)
        {
            saveData.bossWasSpawned = true;
            saveData.bossPosition = bossInScene.transform.position;
            saveData.bossRotation = bossInScene.transform.rotation;
            saveData.bossIsAlive = bossInScene.activeSelf;
            
            EnemyHealth bossHealth = bossInScene.GetComponent<EnemyHealth>();
            if (bossHealth != null)
            {
                saveData.bossHealth = bossHealth.CurrentHealth;
                saveData.bossMaxHealth = bossHealth.MaxHealth;
            }
            else
            {
                saveData.bossHealth = bossInScene.activeSelf ? 100f : 0f;
                saveData.bossMaxHealth = 100f;
            }
            
            Debug.Log($"Сохранены данные босса: позиция {saveData.bossPosition}, здоровье {saveData.bossHealth}/{saveData.bossMaxHealth}, жив: {saveData.bossIsAlive}");
        }
        else
        {
            saveData.bossWasSpawned = false;
        }
        
        // Сохраняем время таймера
        GameTimer gameTimer = FindFirstObjectByType<GameTimer>();
        if (gameTimer != null)
        {
            saveData.timerRemainingTime = gameTimer.GetRemainingTime();
            Debug.Log($"Сохранено оставшееся время таймера: {saveData.timerRemainingTime:F1} сек");
        }
        else
        {
            saveData.timerRemainingTime = 0f;
        }
        
        // Сохраняем состояние счетчика врагов
        EnemyCounter enemyCounter = FindFirstObjectByType<EnemyCounter>();
        if (enemyCounter != null)
        {
            saveData.enemyCounterTotalEnemies = enemyCounter.TotalEnemies;
            saveData.enemyCounterDefeatedEnemies = enemyCounter.DefeatedEnemies;
            Debug.Log($"Сохранено состояние счетчика врагов: {saveData.enemyCounterDefeatedEnemies}/{saveData.enemyCounterTotalEnemies}");
        }
        else
        {
            saveData.enemyCounterTotalEnemies = 0;
            saveData.enemyCounterDefeatedEnemies = 0;
        }
        
        // Сохраняем прогресс завершенных локаций из GlobalProgressTracker
        // ВАЖНО: Сохраняем ТОЛЬКО текущий прогресс из GlobalProgressTracker, который привязан к текущему сохранению
        // НЕ сохраняем прогресс, если это новое сохранение (чтобы избежать наследования прогресса из других сохранений)
        if (GlobalProgressTracker.Instance != null && currentSaveSlot >= 0)
        {
            // Копируем текущий список завершенных локаций из GlobalProgressTracker
            saveData.completedLevels.Clear();
            // Используем внутренний HashSet из GlobalProgressTracker (через рефлексию или публичный метод)
            // Пока используем метод, который получает текущий прогресс из PlayerPrefs (который уже синхронизирован с сохранением)
            int completedCount = PlayerPrefs.GetInt("GlobalProgress_Count", 0);
            for (int i = 0; i < completedCount; i++)
            {
                string levelName = PlayerPrefs.GetString($"GlobalProgress_Level_{i}", "");
                if (!string.IsNullOrEmpty(levelName))
                {
                    saveData.completedLevels.Add(levelName);
                }
            }
            Debug.Log($"Сохранено завершенных локаций: {saveData.completedLevels.Count}");
        }
        else
        {
            // Если нет активного сохранения (новое сохранение), оставляем список пустым
            saveData.completedLevels.Clear();
            Debug.Log("Сохранение прогресса пропущено (новое сохранение или нет активного слота)");
        }

        saveData.lostPatientLevels = LobbyPatientProgress.ExportToList();
        
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
