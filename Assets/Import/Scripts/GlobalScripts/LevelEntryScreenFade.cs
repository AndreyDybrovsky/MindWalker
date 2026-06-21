using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

[DefaultExecutionOrder(-150)]
public class LevelEntryScreenFade : MonoBehaviour
{
    private static LevelEntryScreenFade _instance;
    private static bool _isFading;

    public static bool IsFading => _isFading;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (_instance != null)
            return;

        GameObject host = new GameObject(nameof(LevelEntryScreenFade));
        _instance = host.AddComponent<LevelEntryScreenFade>();
        DontDestroyOnLoad(host);

        // AfterSceneLoad срабатывает после события sceneLoaded, поэтому
        // для начальной сцены вызываем обработчик вручную.
        _instance.HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
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
        HandleSceneLoaded(scene, mode);
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        FadeStart.ResetFadeGate();
        ClearCanvasFadeIfNotFromTransition();
        CollectableDocumentProgress.EnsureLoadedFromActiveSave();
        CollectableDocumentProgress.RefreshSceneDocuments();

        if (!IsMenuScene(scene.name))
        {
            CanvasGroup fg = ScreenFadeUtility.EnsureFadeCanvasGroup();
            ScreenFadeUtility.PrepareForFade(fg);
            if (fg != null) { fg.alpha = 1f; fg.blocksRaycasts = true; }
            GameplayInputBlocker.SetBlocked(true);
            _isFading = true;
        }

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
            _isFading = false;
            yield break;
        }

        CanvasGroup globalFade = ScreenFadeUtility.EnsureFadeCanvasGroup();
        yield return ScreenFadeRunner.FadeFromBlack(1.5f, globalFade);

        GameplayInputBlocker.SetBlocked(false);
        _isFading = false;

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
        // Только главное меню имеет собственную анимированную заставку и не должно
        // проявляться из чёрного. Концовки (включая TrueVictory/Victory) проявляются
        // как обычные сцены — чтобы переход был единообразным во всех сценах.
        return sceneName == "MainMenu";
    }
}
