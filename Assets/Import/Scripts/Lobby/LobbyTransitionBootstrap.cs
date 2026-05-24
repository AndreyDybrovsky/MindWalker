using TMPro;
using UnityEngine;

/// <summary>
/// Подключает общие UI подсказки и затемнение ко всем <see cref="SceneTransitionTrigger"/> в лобби.
/// </summary>
[DisallowMultipleComponent]
public class LobbyTransitionBootstrap : MonoBehaviour
{
    [SerializeField] private GameObject sharedPromptUi;
    [SerializeField] private TextMeshProUGUI sharedPromptText;
    [SerializeField] private CanvasGroup sharedFadeCanvasGroup;

    private void Awake()
    {
        if (sharedPromptUi == null)
        {
            PressEPromptView sharedView = PressEPromptUtility.AcquireSharedPrompt();
            if (sharedView != null)
            {
                sharedPromptUi = sharedView.gameObject;
                sharedPromptText = sharedView.Label;
            }
            else
            {
                TextMeshProUGUI found = FindPromptText();
                if (found != null)
                {
                    sharedPromptUi = found.gameObject;
                    sharedPromptText = found;
                }
            }
        }

        if (sharedFadeCanvasGroup == null)
        {
            sharedFadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
            if (sharedFadeCanvasGroup != null)
                sharedFadeCanvasGroup.alpha = 0f;
        }

        SceneTransitionTrigger[] triggers = FindObjectsByType<SceneTransitionTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        GameObject sharedBoard = ResolveSharedPatientBoard(triggers);

        for (int i = 0; i < triggers.Length; i++)
        {
            triggers[i].ApplySharedUi(sharedPromptUi, sharedPromptText, sharedFadeCanvasGroup);
            if (sharedBoard != null)
                triggers[i].ApplyDefaultPatientBoardIfMissing(sharedBoard);
        }

        PressEPromptCoordinator.Refresh();
    }

    private static GameObject ResolveSharedPatientBoard(SceneTransitionTrigger[] triggers)
    {
        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] != null && triggers[i].TryGetPatientBoardPrefab(out GameObject prefab))
                return prefab;
        }

        return null;
    }

    private static TextMeshProUGUI FindPromptText()
    {
        GameObject pressE = GameObject.Find("PressEText");
        if (pressE != null && pressE.TryGetComponent(out TextMeshProUGUI text))
            return text;

        TextMeshProUGUI[] all = FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.name == "PressEText")
                return all[i];
        }

        return null;
    }
}
