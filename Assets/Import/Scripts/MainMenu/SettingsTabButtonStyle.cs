using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Подсветка активной вкладки настроек.
/// </summary>
public class SettingsTabButtonStyle : MonoBehaviour
{
    [SerializeField] private Graphic targetGraphic;
    [SerializeField] private Color normalColor = Color.white;
    [SerializeField] private Color selectedColor = new Color(0.75f, 0.88f, 1f, 1f);

    private void Reset()
    {
        targetGraphic = GetComponent<Graphic>();
    }

    public void SetSelected(bool selected)
    {
        if (targetGraphic == null)
            return;

        targetGraphic.color = selected ? selectedColor : normalColor;
    }
}
