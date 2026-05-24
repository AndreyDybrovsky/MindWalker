using TMPro;
using UnityEngine;

/// <summary>
/// Синхронизирует начальное значение TMP_Dropdown языка с LocalizationManager.
/// Обработку смены языка выполняет <see cref="SettingsPanelUi"/>.
/// </summary>
[RequireComponent(typeof(TMP_Dropdown))]
public class LanguageDropdownBinder : MonoBehaviour
{
    [SerializeField] private TMP_Dropdown dropdown;

    private void Reset()
    {
        dropdown = GetComponent<TMP_Dropdown>();
    }

    private void Start()
    {
        if (dropdown == null)
            dropdown = GetComponent<TMP_Dropdown>();

        if (dropdown == null || LocalizationManager.Instance == null)
            return;

        dropdown.SetValueWithoutNotify(LanguageToIndex(LocalizationManager.Instance.GetLanguage()));
        dropdown.RefreshShownValue();
    }

    private static int LanguageToIndex(GameLanguage lang)
    {
        return lang switch
        {
            GameLanguage.Ru => 0,
            GameLanguage.Es => 2,
            _ => 1
        };
    }
}
