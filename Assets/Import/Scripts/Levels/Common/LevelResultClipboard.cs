using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Показывает доску ClipBoard с печатью после завершения или провала уровня.
/// Разместите компонент в каждой уровневой сцене.
/// </summary>
[AddComponentMenu("Level/Level Result Clipboard")]
public class LevelResultClipboard : MonoBehaviour
{
    [Header("Префаб доски")]
    [SerializeField] private GameObject clipBoardPrefab;

    [Header("Позиция перед камерой")]
    [SerializeField] private float distanceFromCamera = 1.8f;
    [SerializeField] private Vector3 positionOffset = new Vector3(0.1f, -0.25f, 0f);

    [Header("Анимация")]
    [SerializeField] private float revealDuration = 0.7f;
    [SerializeField] private float delayBeforeInput = 1.0f;

    [Header("Подсказка (экранная)")]
    [Tooltip("Ключ локализации для подсказки. Если пусто — используется Hint Fallback Text.")]
    [SerializeField] private string hintLocalizationKey = "scene.hint_press_e_continue";
    [SerializeField] private string hintFallbackText = "Нажмите E / Enter / Space";

    [Header("Звук")]
    [SerializeField] private AudioClip showSoundSuccess;
    [SerializeField] private AudioClip showSoundFail;
    [SerializeField] [Range(0f, 1f)] private float soundVolume = 0.8f;

    public static LevelResultClipboard Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>
    /// Показывает клипборд с нужной печатью. Когда игрок нажимает E/Enter/Space — вызывает onContinue.
    /// </summary>
    public void Show(bool success, Action onContinue)
    {
        if (clipBoardPrefab == null)
        {
            onContinue?.Invoke();
            return;
        }
        StartCoroutine(ShowRoutine(success, onContinue));
    }

    private IEnumerator ShowRoutine(bool success, Action onContinue)
    {
        GameplayInputBlocker.SetBlocked(true);
        GameplayInputBlocker.UnlockCursorForMenu();

        Camera cam = Camera.main ?? FindFirstObjectByType<Camera>();
        if (cam == null)
        {
            GameplayInputBlocker.SetBlocked(false);
            onContinue?.Invoke();
            yield break;
        }

        GameObject instance = null;
        GameObject hintCanvas = null;

        try
        {
            Transform camT = cam.transform;
            Vector3 worldOffset = camT.right * positionOffset.x + camT.up * positionOffset.y;
            Vector3 spawnPos = camT.position + camT.forward * distanceFromCamera + worldOffset;
            Quaternion spawnRot = Quaternion.LookRotation(spawnPos - camT.position) * Quaternion.Euler(0f, 180f, 0f);

            instance = Instantiate(clipBoardPrefab, spawnPos, spawnRot);
            DontDestroyOnLoad(instance);

            PatientInfoBoardView boardView = instance.GetComponentInChildren<PatientInfoBoardView>(true);
            if (boardView != null)
            {
                boardView.BindViewCamera(cam);
                boardView.ForceStamp(success);
            }

            AudioClip clip = success ? showSoundSuccess : showSoundFail;
            if (clip != null)
                AudioSource.PlayClipAtPoint(clip, spawnPos, soundVolume);
        }
        catch (Exception e)
        {
            Debug.LogError($"[LevelResultClipboard] Ошибка при создании клипборда: {e}");
            if (instance != null) Destroy(instance);
            GameplayInputBlocker.SetBlocked(false);
            onContinue?.Invoke();
            yield break;
        }

        PatientInfoBoardView revealView = instance.GetComponentInChildren<PatientInfoBoardView>(true);

        float elapsed = 0f;
        while (elapsed < revealDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / revealDuration));
            revealView?.SetVisualReveal(t);
            yield return null;
        }
        revealView?.SetVisualReveal(1f);

        yield return new WaitForSecondsRealtime(delayBeforeInput);

        hintCanvas = BuildHintCanvas();

        while (!Input.GetKeyDown(KeyCode.E) &&
               !Input.GetKeyDown(KeyCode.Return) &&
               !Input.GetKeyDown(KeyCode.Space))
            yield return null;

        GameplayInputBlocker.SetBlocked(false);
        if (hintCanvas != null) Destroy(hintCanvas);
        Destroy(instance);
        onContinue?.Invoke();
    }

    private GameObject BuildHintCanvas()
    {
        string text = ResolveHintText();

        GameObject canvasGo = new GameObject("ClipboardHintCanvas");
        DontDestroyOnLoad(canvasGo);

        Canvas canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 300;

        CanvasScaler scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject textGo = new GameObject("HintText");
        textGo.transform.SetParent(canvasGo.transform, false);

        TextMeshProUGUI tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = 28f;
        tmp.alignment = TextAlignmentOptions.Bottom;
        tmp.color = new Color(1f, 1f, 1f, 0.88f);

        RectTransform rt = textGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 0.18f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        return canvasGo;
    }

    private string ResolveHintText()
    {
        if (!string.IsNullOrEmpty(hintLocalizationKey) && LocalizationManager.Instance != null)
        {
            string localized = LocalizationManager.Instance.T(hintLocalizationKey);
            if (!string.IsNullOrEmpty(localized) && localized != hintLocalizationKey)
                return localized;
        }
        return string.IsNullOrEmpty(hintFallbackText) ? "Нажмите E / Enter / Space" : hintFallbackText;
    }
}
