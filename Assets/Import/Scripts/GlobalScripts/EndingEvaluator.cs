/// <summary>
/// Определяет, какую из четырёх концовок получит игрок, исходя из:
///   — количества собранных записок (нужно 6 для «полных»)
///   — количества уникальных спасённых пациентов
/// </summary>
public static class EndingEvaluator
{
    public const string TrueEndingScene  = "TrueVictory";
    public const string FalseEndingScene = "FalseEnding";
    public const string BadEndingScene   = "BadEnding";
    public const string FailureScene     = "Failure";

    private const int DocsRequired        = 6;
    private const int PatientsForFalse    = 3;

    /// <summary>
    /// Оцениваем концовку.
    /// <paramref name="savedPatients"/> — спасённые уникальные пациенты (CompletedPatientGroupsCount).
    /// <paramref name="totalPatients"/>  — всего пациентов в игре (TotalLevels).
    /// </summary>
    public static EndingType Evaluate(int savedPatients, int totalPatients)
    {
        bool allDocs  = CollectableDocumentProgress.AllCollected;
        bool allSaved = savedPatients >= totalPatients;

        if (allDocs  &&  allSaved)                   return EndingType.TrueEnding;
        if (allDocs  && !allSaved)                   return EndingType.BadEnding;
        if (!allDocs &&  savedPatients >= PatientsForFalse) return EndingType.FalseEnding;
        return EndingType.Failure;
    }

    /// <summary>Перегрузка без аргументов — читает данные из синглтонов.</summary>
    public static EndingType Evaluate()
    {
        int saved = GlobalProgressTracker.Instance?.CompletedPatientGroupsCount ?? 0;
        int total = GlobalProgressTracker.Instance?.TotalLevels ?? DocsRequired;
        return Evaluate(saved, total);
    }

    public static string GetSceneName(EndingType type) => type switch
    {
        EndingType.TrueEnding  => TrueEndingScene,
        EndingType.FalseEnding => FalseEndingScene,
        EndingType.BadEnding   => BadEndingScene,
        EndingType.Failure     => FailureScene,
        _                      => "Main",
    };
}
