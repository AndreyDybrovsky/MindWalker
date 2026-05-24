using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Общая логика успешного прохождения уровня и выбора сцены возврата (лобби / финал).
/// </summary>
public static class LevelSuccessFlow
{
    public static void MarkCurrentLevelCompleted()
    {
        string clearedScene = SceneManager.GetActiveScene().name;
        LevelSceneProgress.MarkCompleted(clearedScene);

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveCurrentGame();
    }

    public static string ResolveReturnScene(string lobbySceneName, string victorySceneName)
    {
        if (GlobalProgressTracker.Instance != null
            && GlobalProgressTracker.Instance.CompletedLevelsCount >= GlobalProgressTracker.Instance.TotalLevels
            && !string.IsNullOrWhiteSpace(victorySceneName))
        {
            return victorySceneName;
        }

        return string.IsNullOrWhiteSpace(lobbySceneName) ? "Main" : lobbySceneName;
    }
}
