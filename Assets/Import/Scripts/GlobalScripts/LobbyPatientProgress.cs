using System.Collections.Generic;

/// <summary>
/// Статусы пациентов в лобби по имени сцены локации. «Потерян» хранится в сохранении;
/// «Здоров» = локация в completedLevels; «Болен» = не завершено и не потеряно.
/// </summary>
public static class LobbyPatientProgress
{
    private static readonly HashSet<string> LostLevels = new HashSet<string>();

    public static void LoadFromSave(IEnumerable<string> lostLevelsFromSave)
    {
        LostLevels.Clear();
        if (lostLevelsFromSave == null)
            return;

        foreach (string s in lostLevelsFromSave)
        {
            if (!string.IsNullOrEmpty(s))
                LostLevels.Add(s);
        }
    }

    public static List<string> ExportToList()
    {
        return new List<string>(LostLevels);
    }

    public static void MarkLost(string levelSceneName)
    {
        if (string.IsNullOrEmpty(levelSceneName))
            return;
        LostLevels.Add(levelSceneName);
    }

    /// <summary>Локация успешно пройдена — снимаем «потерян», если был.</summary>
    public static void ClearLost(string levelSceneName)
    {
        if (string.IsNullOrEmpty(levelSceneName))
            return;
        LostLevels.Remove(levelSceneName);
    }

    public static bool IsLost(string levelSceneName)
    {
        return !string.IsNullOrEmpty(levelSceneName) && LostLevels.Contains(levelSceneName);
    }

    public static void ClearAll()
    {
        LostLevels.Clear();
    }

    /// <summary>
    /// Количество уникальных потерянных групп пациентов
    /// (PTSD + "PTSD in Danger" считаются как одна группа).
    /// </summary>
    public static int LostUniqueGroupsCount =>
        LevelSceneProgress.CountUniquePatientGroups(LostLevels);
}
