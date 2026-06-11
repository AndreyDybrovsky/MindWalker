using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Общая логика успешного прохождения уровня и выбора сцены возврата (лобби / концовка).
/// Концовка определяется <see cref="EndingEvaluator"/>, когда все пациенты обработаны
/// (спасены + потеряны >= totalLevels).
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
        if (GlobalProgressTracker.Instance == null)
            return string.IsNullOrWhiteSpace(lobbySceneName) ? "Main" : lobbySceneName;

        int saved = GlobalProgressTracker.Instance.CompletedPatientGroupsCount;
        int total = GlobalProgressTracker.Instance.TotalLevels;
        int lost  = LobbyPatientProgress.LostUniqueGroupsCount;

        bool allProcessed = (saved + lost) >= total;

        if (!allProcessed)
            return string.IsNullOrWhiteSpace(lobbySceneName) ? "Main" : lobbySceneName;

        EndingType ending = EndingEvaluator.Evaluate(saved, total);
        return EndingEvaluator.GetSceneName(ending);
    }
}
