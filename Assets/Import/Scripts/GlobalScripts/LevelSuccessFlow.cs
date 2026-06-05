using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Общая логика успешного прохождения уровня и выбора сцены возврата (лобби / финал).
/// </summary>
public static class LevelSuccessFlow
{
    public const string TrueVictorySceneName = "TrueVictory";

    public static void MarkCurrentLevelCompleted()
    {
        string clearedScene = SceneManager.GetActiveScene().name;
        LevelSceneProgress.MarkCompleted(clearedScene);

        if (SaveManager.Instance != null)
            SaveManager.Instance.SaveCurrentGame();
    }

    public static string ResolveReturnScene(string lobbySceneName, string victorySceneName)
    {
        bool allLevelsDone = GlobalProgressTracker.Instance != null
            && GlobalProgressTracker.Instance.CompletedPatientGroupsCount
            >= GlobalProgressTracker.Instance.TotalLevels;

        if (allLevelsDone && CollectableDocumentProgress.AllCollected)
            return TrueVictorySceneName;

        if (allLevelsDone && !string.IsNullOrWhiteSpace(victorySceneName))
            return victorySceneName;

        return string.IsNullOrWhiteSpace(lobbySceneName) ? "Main" : lobbySceneName;
    }
}
