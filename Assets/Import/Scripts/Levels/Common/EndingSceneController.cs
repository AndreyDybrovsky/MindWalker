using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Управляет экраном одной из четырёх концовок.
/// Назначь <see cref="endingType"/> в инспекторе соответствующей сцены.
/// Для BadEnding: включи <see cref="isBadEnding"/> — таймер отсчитает и затемнит экран.
/// Тексты берутся из локализации: ключи ending.{type}.title / ending.{type}.body.
/// </summary>
public class EndingSceneController : MonoBehaviour
{
    [Header("Тип этой сцены")]
    [SerializeField] private EndingType endingType = EndingType.None;

    [Header("UI")]
    [SerializeField] private TextMeshProUGUI titleText;
    [SerializeField] private TextMeshProUGUI bodyText;
    [SerializeField] private CanvasGroup     fadeGroup;
    [SerializeField] private Button          mainMenuButton;

    [Header("Плохая концовка (BadEnding)")]
    [SerializeField] private bool            isBadEnding;
    [SerializeField] private float           badEndingDuration = 30f;
    [SerializeField] private TextMeshProUGUI badEndingTimerText;

    [Header("Переходы")]
    [SerializeField] private float fadeInDuration  = 1.5f;
    [SerializeField] private float fadeOutDuration = 1.5f;

    private static readonly string[] TypeKeys =
        { "true", "false", "bad", "failure" };  // индекс = (EndingType - 1)

    private void Awake()
    {
        if (fadeGroup != null)
            fadeGroup.alpha = 0f;
    }

    private IEnumerator Start()
    {
        if (mainMenuButton != null)
            mainMenuButton.onClick.AddListener(GoToMainMenu);

        ApplyTexts();

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += _ => ApplyTexts();

        yield return FadeTo(1f, fadeInDuration);

        if (isBadEnding)
            yield return RunBadEndingTimer();
    }

    private void ApplyTexts()
    {
        int idx = (int)endingType - 1;
        if (idx < 0 || idx >= TypeKeys.Length)
            return;

        string key = TypeKeys[idx];

        string title = T($"ending.{key}.title");
        string body  = T($"ending.{key}.body");

        if (titleText != null) titleText.text = title;
        if (bodyText  != null) bodyText.text  = body;

        if (mainMenuButton != null)
        {
            var label = mainMenuButton.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null) label.text = T("ending.main_menu");
        }
    }

    private static string T(string key)
    {
        if (LocalizationManager.Instance != null)
            return LocalizationManager.Instance.T(key);
        return key;
    }

    private IEnumerator RunBadEndingTimer()
    {
        float remaining = badEndingDuration;

        while (remaining > 0f)
        {
            remaining -= Time.deltaTime;
            if (badEndingTimerText != null)
                badEndingTimerText.text = Mathf.CeilToInt(Mathf.Max(0f, remaining)).ToString();
            yield return null;
        }

        // Финальное сообщение
        if (titleText != null) titleText.text = T("ending.bad.title");
        if (bodyText  != null) bodyText.text  = "";

        yield return new WaitForSecondsRealtime(2f);
        yield return FadeTo(0f, fadeOutDuration);
        SceneManager.LoadScene("Main");
    }

    private void GoToMainMenu()
    {
        StartCoroutine(FadeOutAndLoad("Main"));
    }

    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        yield return FadeTo(0f, fadeOutDuration);
        SceneManager.LoadScene(sceneName);
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        if (fadeGroup == null) yield break;
        float start   = fadeGroup.alpha;
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed        += Time.unscaledDeltaTime;
            fadeGroup.alpha = Mathf.Lerp(start, target, elapsed / duration);
            yield return null;
        }
        fadeGroup.alpha = target;
    }
}
