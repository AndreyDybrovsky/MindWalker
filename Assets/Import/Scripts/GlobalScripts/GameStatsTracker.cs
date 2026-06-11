using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton DontDestroyOnLoad. Собирает статистику прохождения и сохраняет
/// её в customData текущего слота сохранения через SaveGameCustomDataUtility.
/// Создаётся автоматически при старте игры.
/// </summary>
public class GameStatsTracker : MonoBehaviour
{
    public static GameStatsTracker Instance { get; private set; }

    private const string StatsKey = "game_stats";

    private static readonly HashSet<string> MenuScenes = new HashSet<string>(
        System.StringComparer.OrdinalIgnoreCase)
    {
        "MainMenu", "Main", "Boot", "Loading"
    };

    private GameStatsData _stats = new GameStatsData();
    private string _currentLevelName;
    private float  _levelStartTimeReal;
    private bool   _levelTimerActive;

    public GameStatsData Stats => _stats;

    // ─── Авто-создание ─────────────────────────────────────────────────────

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        if (Instance != null)
            return;

        GameObject go = new GameObject("[GameStatsTracker]");
        go.AddComponent<GameStatsTracker>();
    }

    // ─── Lifecycle ──────────────────────────────────────────────────────────

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        LoadFromSave();

        string startScene = SceneManager.GetActiveScene().name;
        if (!MenuScenes.Contains(startScene))
            BeginLevelTimer(startScene);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (Instance == this)
            Instance = null;
    }

    private void Update()
    {
        if (_levelTimerActive)
            _stats.totalPlayTimeSeconds += Time.unscaledDeltaTime;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        bool isMenu = MenuScenes.Contains(scene.name);

        if (isMenu)
        {
            _levelTimerActive = false;
            Save();
        }
        else
        {
            BeginLevelTimer(scene.name);
        }
    }

    // ─── Уровни ────────────────────────────────────────────────────────────

    private void BeginLevelTimer(string sceneName)
    {
        _currentLevelName  = sceneName;
        _levelStartTimeReal = Time.realtimeSinceStartup;
        _levelTimerActive  = true;
    }

    /// <summary>Вызывается из LevelCompletionManager при завершении уровня.</summary>
    public void OnLevelCompleted(string sceneName)
    {
        if (!_levelTimerActive)
            return;

        float duration = Time.realtimeSinceStartup - _levelStartTimeReal;
        _levelTimerActive = false;

        int idx = _stats.levelNames.IndexOf(sceneName);
        if (idx >= 0)
            _stats.levelTimesSec[idx] = duration;
        else
        {
            _stats.levelNames.Add(sceneName);
            _stats.levelTimesSec.Add(duration);
        }

        Save();
    }

    // ─── Запись событий ────────────────────────────────────────────────────

    public void RecordDamageReceived(float amount) { if (amount > 0f) _stats.damageReceived    += amount; }
    public void RecordDamageDealt(float amount)    { if (amount > 0f) _stats.damageDealt       += amount; }
    public void RecordEnemyKilled()                { _stats.enemiesKilled++; }
    public void RecordSlotMachineCatch()           { _stats.slotMachineCaught++; }
    public void RecordPatientCalmed()              { _stats.patientCalmedCount++; }

    // ─── Сохранение / загрузка ─────────────────────────────────────────────

    public void Save()
    {
        if (SaveManager.Instance == null) return;
        int slot = SaveManager.Instance.GetCurrentSaveSlot();
        if (slot < 0) return;
        GameSaveData data = SaveManager.Instance.GetSaveData(slot);
        if (data == null) return;
        SaveGameCustomDataUtility.Write(data, StatsKey, _stats);
        SaveManager.Instance.SaveGame(slot, data);
    }

    private void LoadFromSave()
    {
        if (SaveManager.Instance == null) return;
        int slot = SaveManager.Instance.GetCurrentSaveSlot();
        if (slot < 0) return;
        GameSaveData data = SaveManager.Instance.GetSaveData(slot);
        if (data == null) return;
        if (SaveGameCustomDataUtility.TryRead(data, StatsKey, out GameStatsData loaded) && loaded != null)
            _stats = loaded;
    }

    // ─── Отображение ───────────────────────────────────────────────────────

    public string GetFastestLevelDisplay() => GetExtremeLevelDisplay(fastest: true);
    public string GetSlowestLevelDisplay()  => GetExtremeLevelDisplay(fastest: false);

    private string GetExtremeLevelDisplay(bool fastest)
    {
        if (_stats.levelNames == null || _stats.levelNames.Count == 0)
            return "—";

        float extreme = fastest ? float.MaxValue : float.MinValue;
        string name   = "—";

        for (int i = 0; i < _stats.levelNames.Count; i++)
        {
            float t  = _stats.levelTimesSec[i];
            bool ok  = fastest ? t < extreme : t > extreme;
            if (!ok) continue;
            extreme = t;
            name    = _stats.levelNames[i];
        }

        return $"{name} ({FormatTime(extreme)})";
    }

    public static string FormatTime(float seconds)
    {
        int h = Mathf.FloorToInt(seconds / 3600f);
        int m = Mathf.FloorToInt(seconds % 3600f / 60f);
        int s = Mathf.FloorToInt(seconds % 60f);
        return h > 0 ? $"{h}:{m:D2}:{s:D2}" : $"{m:D2}:{s:D2}";
    }
}
