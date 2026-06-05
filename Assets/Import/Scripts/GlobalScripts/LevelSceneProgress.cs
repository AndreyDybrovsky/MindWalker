using System.Collections.Generic;

/// <summary>
/// Связанные сцены одного пациента (например PTSD + PTSD in Danger) и блокировка входа.
/// </summary>
public static class LevelSceneProgress
{
    private static readonly Dictionary<string, string[]> LinkedScenes = new()
    {
        { "PTSD", new[] { "PTSD", "PTSD in Danger" } },
        { "PTSD in Danger", new[] { "PTSD", "PTSD in Danger" } },
    };

    public static bool IsEntryBlocked(string levelSceneName)
    {
        if (string.IsNullOrEmpty(levelSceneName))
            return false;

        foreach (string scene in GetLinkedScenes(levelSceneName))
        {
            if (LobbyPatientProgress.IsLost(scene))
                return true;

            if (GlobalProgressTracker.Instance != null && GlobalProgressTracker.Instance.IsLevelCompleted(scene))
                return true;
        }

        return false;
    }

    public static void MarkCompleted(string clearedSceneName)
    {
        if (string.IsNullOrEmpty(clearedSceneName))
            return;

        foreach (string scene in GetLinkedScenes(clearedSceneName))
        {
            LobbyPatientProgress.ClearLost(scene);
            GlobalProgressTracker.Instance?.MarkLevelCompleted(scene);
        }
    }

    public static IEnumerable<string> GetLinkedScenes(string levelSceneName)
    {
        if (!string.IsNullOrEmpty(levelSceneName) && LinkedScenes.TryGetValue(levelSceneName, out string[] group))
            return group;

        return new[] { levelSceneName };
    }

    /// <summary>
    /// Считает уникальных пациентов — связанные сцены (напр. PTSD + PTSD in Danger) считаются как один.
    /// Используется для проверки "все пациенты пройдены" без двойного счёта.
    /// </summary>
    public static int CountUniquePatientGroups(System.Collections.Generic.IEnumerable<string> completedScenes)
    {
        if (completedScenes == null)
            return 0;

        var canonical = new System.Collections.Generic.HashSet<string>();
        foreach (string scene in completedScenes)
        {
            string key = scene;
            foreach (string linked in GetLinkedScenes(scene))
            {
                if (string.Compare(linked, key, System.StringComparison.Ordinal) < 0)
                    key = linked;
            }
            canonical.Add(key);
        }
        return canonical.Count;
    }

    /// <summary>
    /// Старые сохранения могли отметить только «PTSD in Danger» — синхронизируем связанные сцены.
    /// </summary>
    public static void SyncLinkedProgressFromSave()
    {
        if (GlobalProgressTracker.Instance == null)
            return;

        foreach (string[] group in LinkedScenes.Values)
        {
            bool anyCompleted = false;
            bool anyLost = false;

            for (int i = 0; i < group.Length; i++)
            {
                string scene = group[i];
                if (GlobalProgressTracker.Instance.IsLevelCompleted(scene))
                    anyCompleted = true;
                if (LobbyPatientProgress.IsLost(scene))
                    anyLost = true;
            }

            if (anyCompleted)
            {
                for (int i = 0; i < group.Length; i++)
                {
                    string scene = group[i];
                    LobbyPatientProgress.ClearLost(scene);
                    GlobalProgressTracker.Instance.MarkLevelCompleted(scene);
                }
            }

            if (anyLost)
            {
                for (int i = 0; i < group.Length; i++)
                    LobbyPatientProgress.MarkLost(group[i]);
            }
        }
    }
}
