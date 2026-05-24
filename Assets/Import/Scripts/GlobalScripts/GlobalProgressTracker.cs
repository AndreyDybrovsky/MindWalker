using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

/// <summary>
/// Отслеживает глобальный прогресс по локациям "Спаси их: X/6"
/// </summary>
public class GlobalProgressTracker : MonoBehaviour
{
    private static GlobalProgressTracker instance;
    public static GlobalProgressTracker Instance
    {
        get
        {
            if (instance == null)
            {
                instance = FindFirstObjectByType<GlobalProgressTracker>();
                if (instance == null)
                {
                    GameObject go = new GameObject("GlobalProgressTracker");
                    instance = go.AddComponent<GlobalProgressTracker>();
                    DontDestroyOnLoad(go);
                }
            }
            return instance;
        }
    }

    [Header("Настройки")]
    [SerializeField] private int totalLevels = 6; // Всего локаций для закрытия
    [SerializeField] private TextMeshProUGUI progressTextUI; // UI текст для отображения "Спаси их: X/6"
    [SerializeField] private TextMesh progressText3D; // 3D текст для отображения (если используется 3D текст)

    [Header("Автопривязка UI после загрузки сцены")]
    [SerializeField] private bool autoRebindTextsOnSceneLoad = true;
    [SerializeField] private string progressTextUIGameObjectName = "GlobalProgressText";
    [SerializeField] private string progressText3DGameObjectName = "";

    [Header("Список локаций")]
    [SerializeField] private List<string> levelSceneNames = new List<string>(); // Имена сцен локаций

    private HashSet<string> completedLevels = new HashSet<string>(); // Завершенные локации
    private string progressTextKey = "GlobalProgress_CompletedLevels"; // Ключ для PlayerPrefs

    public int TotalLevels => totalLevels;
    public int CompletedLevelsCount => completedLevels.Count;

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        instance = this;
        DontDestroyOnLoad(gameObject);
        
        // НЕ загружаем прогресс из PlayerPrefs при Awake - прогресс будет загружен из сохранения
        // Это предотвращает наследование прогресса между сохранениями
        // LoadProgress(); // Закомментировано - прогресс загружается только из сохранения
        
        LoadLevelSceneNames();
    }

    private void OnEnable()
    {
        if (autoRebindTextsOnSceneLoad)
            SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (autoRebindTextsOnSceneLoad)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        // Сохраняем список локаций при старте (если он был задан в инспекторе)
        if (levelSceneNames != null && levelSceneNames.Count > 0)
        {
            SaveLevelSceneNames();
        }
        
        UpdateProgressDisplay();
        TryAutoBindTexts();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // После смены сцены старые ссылки на UI/3D текст становятся недействительны
        TryAutoBindTexts();
        UpdateProgressDisplay();
    }

    private void TryAutoBindTexts()
    {
        // Если ссылки уже заданы (например, вручную в инспекторе), не трогаем
        if (progressTextUI == null)
        {
            // 1) По имени объекта (самый предсказуемый способ)
            if (!string.IsNullOrWhiteSpace(progressTextUIGameObjectName))
            {
                GameObject go = GameObject.Find(progressTextUIGameObjectName);
                if (go != null)
                    progressTextUI = go.GetComponent<TextMeshProUGUI>();
            }

            // 2) Фоллбек: ищем любой TMPUGUI в текущей сцене (если он один — почти наверняка нужный)
            if (progressTextUI == null)
            {
                var all = Resources.FindObjectsOfTypeAll<TextMeshProUGUI>();
                TextMeshProUGUI candidate = null;
                int countInScene = 0;
                Scene activeScene = SceneManager.GetActiveScene();

                foreach (var t in all)
                {
                    if (t == null) continue;
                    GameObject tGo = t.gameObject;
                    if (!tGo.scene.IsValid() || tGo.scene != activeScene) continue;
                    if ((tGo.hideFlags & HideFlags.NotEditable) != 0) continue;
                    if ((tGo.hideFlags & HideFlags.HideAndDontSave) != 0) continue;

                    candidate = t;
                    countInScene++;
                    if (countInScene > 1) break;
                }

                if (countInScene == 1)
                    progressTextUI = candidate;
            }
        }

        if (progressText3D == null && !string.IsNullOrWhiteSpace(progressText3DGameObjectName))
        {
            GameObject go3D = GameObject.Find(progressText3DGameObjectName);
            if (go3D != null)
                progressText3D = go3D.GetComponent<TextMesh>();
        }
    }

    /// <summary>
    /// Отметить локацию как завершенную
    /// </summary>
    public void MarkLevelCompleted(string levelSceneName)
    {
        if (string.IsNullOrEmpty(levelSceneName))
            return;
        
        if (!completedLevels.Contains(levelSceneName))
        {
            completedLevels.Add(levelSceneName);
            SaveProgress();
            UpdateProgressDisplay();
            Debug.Log($"GlobalProgressTracker: Локация {levelSceneName} отмечена как завершенная. Прогресс: {completedLevels.Count}/{totalLevels}");
        }
    }

    /// <summary>
    /// Проверить, завершена ли локация
    /// </summary>
    public bool IsLevelCompleted(string levelSceneName)
    {
        return completedLevels.Contains(levelSceneName);
    }

    /// <summary>
    /// Сбросить весь прогресс
    /// </summary>
    public void ResetProgress()
    {
        completedLevels.Clear();
        SaveProgress();
        UpdateProgressDisplay();
    }

    /// <summary>
    /// Обновить отображение прогресса
    /// </summary>
    public void UpdateProgressDisplay()
    {
        string progressString = $"Спаси их: {completedLevels.Count}/{totalLevels}";
        
        if (progressTextUI != null)
            progressTextUI.text = progressString;
        
        if (progressText3D != null)
            progressText3D.text = progressString;
    }

    /// <summary>
    /// Сохранить прогресс
    /// </summary>
    private void SaveProgress()
    {
        // Сохраняем количество завершенных локаций
        PlayerPrefs.SetInt("GlobalProgress_Count", completedLevels.Count);
        
        // Сохраняем список завершенных локаций
        int index = 0;
        foreach (string levelName in completedLevels)
        {
            PlayerPrefs.SetString($"GlobalProgress_Level_{index}", levelName);
            index++;
        }
        
        // Очищаем оставшиеся ключи (если было больше сохранений ранее)
        for (int i = index; i < totalLevels; i++)
        {
            if (PlayerPrefs.HasKey($"GlobalProgress_Level_{i}"))
            {
                PlayerPrefs.DeleteKey($"GlobalProgress_Level_{i}");
            }
        }
        
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Загрузить прогресс
    /// </summary>
    private void LoadProgress()
    {
        completedLevels.Clear();
        
        int count = PlayerPrefs.GetInt("GlobalProgress_Count", 0);
        
        for (int i = 0; i < count; i++)
        {
            string levelName = PlayerPrefs.GetString($"GlobalProgress_Level_{i}", "");
            if (!string.IsNullOrEmpty(levelName))
            {
                completedLevels.Add(levelName);
            }
        }
        
        Debug.Log($"GlobalProgressTracker: Загружен прогресс: {completedLevels.Count}/{totalLevels} локаций завершено");
    }

    /// <summary>
    /// Загрузить список имен сцен локаций из PlayerPrefs (чтобы не терять при DontDestroyOnLoad)
    /// </summary>
    private void LoadLevelSceneNames()
    {
        // Если список пустой в инспекторе, пытаемся загрузить из PlayerPrefs
        if (levelSceneNames == null || levelSceneNames.Count == 0)
        {
            levelSceneNames = new List<string>();
            int savedCount = PlayerPrefs.GetInt("GlobalProgress_LevelSceneNames_Count", 0);
            
            for (int i = 0; i < savedCount; i++)
            {
                string sceneName = PlayerPrefs.GetString($"GlobalProgress_LevelSceneName_{i}", "");
                if (!string.IsNullOrEmpty(sceneName))
                {
                    levelSceneNames.Add(sceneName);
                }
            }
            
            if (levelSceneNames.Count > 0)
            {
                Debug.Log($"GlobalProgressTracker: Загружено {levelSceneNames.Count} имен локаций из PlayerPrefs");
            }
        }
        else
        {
            // Если список заполнен в инспекторе, сохраняем его в PlayerPrefs
            SaveLevelSceneNames();
        }
    }

    /// <summary>
    /// Сохранить список имен сцен локаций в PlayerPrefs
    /// </summary>
    private void SaveLevelSceneNames()
    {
        if (levelSceneNames == null || levelSceneNames.Count == 0)
            return;
        
        PlayerPrefs.SetInt("GlobalProgress_LevelSceneNames_Count", levelSceneNames.Count);
        
        for (int i = 0; i < levelSceneNames.Count; i++)
        {
            PlayerPrefs.SetString($"GlobalProgress_LevelSceneName_{i}", levelSceneNames[i]);
        }
        
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Установить ссылки на UI элементы для отображения прогресса (вызывается из UI скрипта на сцене Main)
    /// </summary>
    public void SetProgressTextUI(TextMeshProUGUI textUI)
    {
        progressTextUI = textUI;
        UpdateProgressDisplay();
    }

    /// <summary>
    /// Установить ссылки на 3D текст для отображения прогресса
    /// </summary>
    public void SetProgressText3D(TextMesh text3D)
    {
        progressText3D = text3D;
        UpdateProgressDisplay();
    }

    /// <summary>
    /// Загрузить прогресс из сохранения (вызывается при загрузке игры)
    /// </summary>
    public void LoadProgressFromSave(List<string> completedLevelsFromSave)
    {
        if (completedLevelsFromSave == null)
            return;
        
        completedLevels.Clear();
        completedLevels.UnionWith(completedLevelsFromSave);
        LevelSceneProgress.SyncLinkedProgressFromSave();

        SaveProgress();
        UpdateProgressDisplay();
        
        Debug.Log($"GlobalProgressTracker: Загружен прогресс из сохранения: {completedLevels.Count}/{totalLevels} локаций завершено");
    }

    public List<string> ExportCompletedLevels()
    {
        return new List<string>(completedLevels);
    }
}
