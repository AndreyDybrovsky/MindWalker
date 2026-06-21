using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Делает HUD-текст «QuestText» (левый верх экрана) читаемым во всех сценах:
/// белый цвет лица + тёмная обводка. Работает поверх любого, кто пишет в этот
/// объект (например <see cref="GlobalProgressTracker"/>), а так как
/// <c>OCDObjectiveUI</c> копирует визуальный стиль именно с QuestText, цели ОКР
/// тоже автоматически становятся читаемыми.
/// </summary>
public static class HudQuestLabelStyler
{
    private static readonly Color FaceColor = Color.white;
    private static readonly Color OutlineColor = new Color(0f, 0f, 0f, 1f);
    private const float OutlineWidth = 0.2f;

    private sealed class Runner : MonoBehaviour { }
    private static Runner _runner;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_runner == null)
        {
            GameObject host = new GameObject(nameof(HudQuestLabelStyler));
            Object.DontDestroyOnLoad(host);
            _runner = host.AddComponent<Runner>();
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        // Стилизуем сразу — до Start других систем, чтобы OCD скопировал уже
        // читаемый стиль с QuestText, а не исходный тёмно-зелёный.
        StyleNow();
    }

    private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StyleNow();
        if (_runner != null)
            _runner.StartCoroutine(StyleNextFrame());
    }

    private static IEnumerator StyleNextFrame()
    {
        yield return null;
        StyleNow();
    }

    private static void StyleNow()
    {
        TextMeshProUGUI[] all = Object.FindObjectsByType<TextMeshProUGUI>(
            FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < all.Length; i++)
        {
            TextMeshProUGUI label = all[i];
            if (label == null)
                continue;

            string objectName = label.gameObject.name;
            if (objectName == "QuestText" || objectName == "Quest")
                ApplyReadable(label);
        }
    }

    /// <summary>Белый текст + тёмная обводка на экземплярном материале (без утечки в общий).</summary>
    public static void ApplyReadable(TMP_Text label)
    {
        if (label == null)
            return;

        label.color = FaceColor;

        Material instanceMaterial = label.fontMaterial; // экземплярная копия материала
        if (instanceMaterial != null)
        {
            instanceMaterial.SetColor(ShaderUtilities.ID_OutlineColor, OutlineColor);
            instanceMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, OutlineWidth);
            label.UpdateMeshPadding();
        }
    }
}
