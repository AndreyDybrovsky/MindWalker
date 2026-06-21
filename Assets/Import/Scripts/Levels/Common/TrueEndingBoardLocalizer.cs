using TMPro;
using UnityEngine;

/// <summary>
/// Локализует текстовые поля ClipBoard в сцене TrueVictory.
/// Назначается на корень ClipBoard; ссылки на TMP-поля задаются в инспекторе.
/// </summary>
public class TrueEndingBoardLocalizer : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI ageText;
    [SerializeField] private TextMeshProUGUI diagnosisText;
    [SerializeField] private TextMeshProUGUI notesText;
    [SerializeField] private TextMeshProUGUI statusText;

    private void Start()
    {
        ApplyLocalization();
    }

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(GameLanguage _) => ApplyLocalization();

    private void ApplyLocalization()
    {
        LocalizationManager loc = LocalizationManager.Instance;
        if (loc == null) return;

        Set(nameText,      loc.T("patient.ivan.name"));
        Set(ageText,       loc.T("patient.ivan.age_text"));
        Set(diagnosisText, loc.T("patient.ivan.diagnosis"));
        Set(notesText,     loc.T("patient.ivan.notes"));
        Set(statusText,    loc.T("patient.status_sick"));
    }

    private static void Set(TextMeshProUGUI tmp, string value)
    {
        if (tmp != null && !string.IsNullOrEmpty(value))
            tmp.text = value;
    }
}
