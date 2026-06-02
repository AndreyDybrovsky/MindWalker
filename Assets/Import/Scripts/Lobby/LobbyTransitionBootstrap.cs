using TMPro;
using UnityEngine;

/// <summary>
<<<<<<< HEAD
/// Подключает общие UI подсказки и затемнение ко всем <see cref="SceneTransitionTrigger"/> в лобби.
/// </summary>
[DisallowMultipleComponent]
public class LobbyTransitionBootstrap : MonoBehaviour
{
=======
/// Подключает общие UI подсказки, затемнение и счётчик прогресса ко всем <see cref="SceneTransitionTrigger"/> в лобби.
/// </summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-200)]
public class LobbyTransitionBootstrap : MonoBehaviour
{
    private static readonly string[] DefaultLevelSceneNames =
    {
        "Autism",
        "PTSD",
        "OCD",
        "Bipolar",
        "Depression",
        "Gambling disease",
    };

>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
    [SerializeField] private GameObject sharedPromptUi;
    [SerializeField] private TextMeshProUGUI sharedPromptText;
    [SerializeField] private CanvasGroup sharedFadeCanvasGroup;

<<<<<<< HEAD
=======
    [Header("Счётчик «Спаси их: X/6»")]
    [SerializeField] private TextMeshProUGUI sharedProgressText;
    [Tooltip("Имя объекта в иерархии (например QuestText или Quest на LobbyCanvas).")]
    [SerializeField] private string progressTextObjectName = "QuestText";

>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
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

<<<<<<< HEAD
        if (sharedFadeCanvasGroup == null)
=======
        if (sharedFadeCanvasGroup == null || IsInvalidFadeReference(sharedFadeCanvasGroup))
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
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
<<<<<<< HEAD
            triggers[i].ApplySharedUi(sharedPromptUi, sharedPromptText, sharedFadeCanvasGroup);
            if (sharedBoard != null)
                triggers[i].ApplyDefaultPatientBoardIfMissing(sharedBoard);
        }

        PressEPromptCoordinator.Refresh();
    }

=======
            if (triggers[i] == null)
                continue;

            triggers[i].ApplySharedUi(sharedPromptUi, sharedPromptText, sharedFadeCanvasGroup);
            if (sharedBoard != null)
                triggers[i].ApplyDefaultPatientBoardIfMissing(sharedBoard);
            triggers[i].NotifyLobbyBootstrapComplete();
        }

        EnsureLobbyProgressUi();
        PressEPromptCoordinator.Refresh();
    }

    private void Start()
    {
        StartCoroutine(RevalidatePromptAfterPlayerSpawn());
    }

    private System.Collections.IEnumerator RevalidatePromptAfterPlayerSpawn()
    {
        yield return null;
        yield return null;
        PressEPromptUtility.HideNonSharedPressEPrompts();

        SceneTransitionTrigger[] triggers = FindObjectsByType<SceneTransitionTrigger>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < triggers.Length; i++)
        {
            if (triggers[i] != null)
                triggers[i].NotifyLobbyBootstrapComplete();
        }

        EnsureLobbyProgressUi();
        PressEPromptCoordinator.Refresh();
    }

    private void EnsureLobbyProgressUi()
    {
        GlobalProgressTracker tracker = GlobalProgressTracker.Instance;
        tracker.EnsureDefaultLevelScenes(DefaultLevelSceneNames);

        if (sharedProgressText == null)
            sharedProgressText = FindQuestProgressText();

        if (sharedProgressText != null)
            tracker.SetProgressTextUI(sharedProgressText);
        else
            Debug.LogWarning(
                $"{nameof(LobbyTransitionBootstrap)}: не найден UI для прогресса (ожидались объекты QuestText или Quest с TMP).",
                this);
    }

    private TextMeshProUGUI FindQuestProgressText()
    {
        if (!string.IsNullOrWhiteSpace(progressTextObjectName))
        {
            TextMeshProUGUI byName = FindTextMeshProByObjectName(progressTextObjectName);
            if (byName != null)
                return byName;
        }

        TextMeshProUGUI questText = FindTextMeshProByObjectName("QuestText");
        if (questText != null)
            return questText;

        TextMeshProUGUI quest = FindTextMeshProByObjectName("Quest");
        if (quest != null)
            return quest;

        return FindTextMeshProContaining("Спаси их");
    }

    private static TextMeshProUGUI FindTextMeshProByObjectName(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
            return null;

        GameObject go = GameObject.Find(objectName);
        if (go != null && go.TryGetComponent(out TextMeshProUGUI direct))
            return direct;

        TextMeshProUGUI[] all = FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            if (all[i] != null && all[i].gameObject.name == objectName)
                return all[i];
        }

        return null;
    }

    private static TextMeshProUGUI FindTextMeshProContaining(string fragment)
    {
        if (string.IsNullOrWhiteSpace(fragment))
            return null;

        TextMeshProUGUI[] all = FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            TextMeshProUGUI label = all[i];
            if (label == null || string.IsNullOrEmpty(label.text))
                continue;

            if (label.text.IndexOf(fragment, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return label;
        }

        return null;
    }

    private static bool IsInvalidFadeReference(CanvasGroup group)
    {
        if (group == null)
            return true;

        Transform node = group.transform;
        while (node != null)
        {
            string name = node.name;
            if (name.IndexOf("Menu", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("LobbyCanvas", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("ElseCanvas", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            node = node.parent;
        }

        Transform root = group.transform.root;
        return root != null && root.name.IndexOf("Player", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
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
