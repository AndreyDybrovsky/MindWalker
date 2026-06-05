using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// После загрузки уровня снимает чёрный экран с глобального FadeCanvas (остаётся после перехода из лобби).
/// </summary>
[DefaultExecutionOrder(-150)]
public class LevelEntryScreenFade : MonoBehaviour
{
    private static LevelEntryScreenFade _instance;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        GameObject host = new GameObject(nameof(LevelEntryScreenFade));
        _instance = host.AddComponent<LevelEntryScreenFade>();
        DontDestroyOnLoad(host);
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FadeStart.ResetFadeGate();
        ClearCanvasFadeIfNotFromTransition();
        CollectableDocumentProgress.EnsureLoadedFromActiveSave();
        CollectableDocumentProgress.RefreshSceneDocuments();
        StartCoroutine(FadeInIfNeeded());
    }

    private static void ClearCanvasFadeIfNotFromTransition()
    {
        CanvasGroup globalFade = ScreenFadeUtility.EnsureFadeCanvasGroup();
        bool fromTransition = globalFade != null && globalFade.alpha > 0.5f;
        if (fromTransition)
            return;

        GameObject canvasFade = GameObject.Find("CanvasFade");
        if (canvasFade != null && canvasFade.TryGetComponent(out CanvasGroup pauseFade))
        {
            pauseFade.alpha = 0f;
            pauseFade.blocksRaycasts = false;
        }
    }

    private IEnumerator FadeInIfNeeded()
    {
        yield return null;

        string sceneName = SceneManager.GetActiveScene().name;
        if (IsMenuScene(sceneName))
        {
            ClearFadeLayer(ScreenFadeUtility.EnsureFadeCanvasGroup());
            yield break;
        }

        CanvasGroup globalFade = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (globalFade != null && globalFade.alpha > 0.02f)
            yield return ScreenFadeRunner.FadeFromBlack(1.2f, globalFade);

        FadeStart[] fadeStarts = Object.FindObjectsByType<FadeStart>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < fadeStarts.Length; i++)
        {
            FadeStart starter = fadeStarts[i];
            if (starter == null)
                continue;

            starter.ForceFadeOutIfNeeded();
        }
    }

    private static void ClearFadeLayer(CanvasGroup fade)
    {
        if (fade == null)
            return;

        fade.alpha = 0f;
        fade.blocksRaycasts = false;
    }

    private static bool IsMenuScene(string sceneName)
    {
        return sceneName == "MainMenu"
               || sceneName == "Victory"
               || sceneName == "TrueVictory";
    }
}
