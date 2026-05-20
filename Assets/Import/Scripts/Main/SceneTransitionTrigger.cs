using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Триггер входа в зону перехода: подсказка с локализацией, 3D-доска пациента у камеры, звуки входа/выхода, E — затемнение и загрузка сцены.
/// </summary>
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("Сцена")]
    [SerializeField] private string targetSceneName;

    [Header("Подсказка (UI)")]
    [SerializeField] private GameObject promptUI;
    [SerializeField] private TMPro.TextMeshProUGUI promptText;
    [SerializeField] private TextMesh promptText3D;
    [Tooltip("Ключ в LocalizationBase / strings_*.json")]
    [SerializeField] private string promptLocalizationKey = "scene.hint_press_e";
    [Tooltip("Если локализация недоступна или ключа нет в таблице — показывается эта строка.")]
    [SerializeField] private string promptFallbackText = "Press E";
    [SerializeField] private CanvasGroup promptCanvasGroup;

    [Header("Доска пациента (префаб под камерой)")]
    [SerializeField] private GameObject patientBoardPrefab;
    [SerializeField] private Vector3 boardLocalPosition = new Vector3(0.42f, -0.22f, 0.72f);
    [SerializeField] private Vector3 boardLocalEulerAngles = new Vector3(0f, -18f, 0f);

    [Header("Плавность")]
    [SerializeField] private float revealSpeed = 4f;
    [SerializeField] private float fadeDuration = 1f;

    [Header("Звук")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip enterZonePaperSound;
    [SerializeField] private AudioClip exitZonePaperSound;
    [FormerlySerializedAs("buttonPressSound")]
    [SerializeField] private AudioClip transitionStartSound;

    [Header("Затемнение при переходе")]
    [SerializeField] private CanvasGroup fadeCanvasGroup;

    private bool _playerInZone;
    private bool _isTransitioning;
    private Collider _triggerCollider;
    private float _reveal;

    private GameObject _spawnedBoard;
    private PatientInfoBoardView _boardView;

    private Color _promptTextBaseColor = Color.white;
    private Color _prompt3DBaseColor = Color.white;
    private bool _promptColorCached;
    private Vector3 _promptUiOriginalLocalScale = Vector3.one;

    public void ApplySharedUi(GameObject prompt, TextMeshProUGUI text, CanvasGroup fade)
    {
        if (promptUI == null && prompt != null)
            promptUI = prompt;

        if (promptText == null && text != null)
            promptText = text;

        if (fadeCanvasGroup == null && fade != null)
            fadeCanvasGroup = fade;
    }

    private void ResolveSharedReferences()
    {
        if (promptUI == null || promptText == null)
        {
            GameObject pressE = GameObject.Find("PressEText");
            if (pressE != null)
            {
                if (promptUI == null)
                    promptUI = pressE;

                if (promptText == null)
                    pressE.TryGetComponent(out promptText);
            }
        }

        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
            if (fadeCanvasGroup != null && fadeCanvasGroup.alpha > 0.99f)
                fadeCanvasGroup.alpha = 0f;
        }
    }

    private void Start()
    {
        _triggerCollider = GetComponent<Collider>();
        ResolveSharedReferences();
        CheckAndUpdateTeleportState();
        SanitizeFadeAndPromptCanvasGroups();

        if (promptUI != null)
        {
            if (promptUI.TryGetComponent(out RectTransform promptRt))
                _promptUiOriginalLocalScale = promptRt.localScale.sqrMagnitude > 1e-8f
                    ? promptRt.localScale
                    : Vector3.one;

            EnsurePromptCanvasGroup();
            EnsurePromptRectTransformScale();
            promptCanvasGroup.alpha = 0f;
            promptCanvasGroup.blocksRaycasts = false;
            promptCanvasGroup.interactable = false;
            promptUI.SetActive(false);
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

        ApplyLocalizedPrompt(forceFallbackIfNeeded: true);
        StartCoroutine(CheckPlayerSpawnedInsideTriggerNextFrame());
    }

    private IEnumerator CheckPlayerSpawnedInsideTriggerNextFrame()
    {
        yield return null;
        TryRegisterPlayerIfAlreadyInsideTrigger(playEnterSound: false);
    }

    /// <summary>Игрок уже в коллайдере при загрузке сцены — OnTriggerEnter не приходит, включаем UI вручную.</summary>
    private void TryRegisterPlayerIfAlreadyInsideTrigger(bool playEnterSound)
    {
        if (_isTransitioning || IsTargetLevelCompleted() || _triggerCollider == null)
            return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player == null)
            return;

        Vector3 pt = player.transform.position;
        Vector3 closest = _triggerCollider.ClosestPoint(pt);
        if ((closest - pt).sqrMagnitude > 0.0004f)
            return;

        RegisterPlayerInZone(playEnterSound);
    }

    private void RegisterPlayerInZone(bool playEnterSound)
    {
        if (IsTargetLevelCompleted())
        {
            _playerInZone = false;
            return;
        }

        bool wasIn = _playerInZone;
        _playerInZone = true;

        if (playEnterSound && !wasIn && enterZonePaperSound != null && audioSource != null)
            audioSource.PlayOneShot(enterZonePaperSound);

        EnsurePromptUiActive();
        ApplyLocalizedPrompt(forceFallbackIfNeeded: true);
    }

    private void EnsurePromptUiActive()
    {
        if (promptUI == null)
            return;

        EnsurePromptCanvasGroup();
        EnsurePromptRectTransformScale();
        if (!promptUI.activeSelf)
            promptUI.SetActive(true);
    }

    private void SanitizeFadeAndPromptCanvasGroups()
    {
        if (promptCanvasGroup != null && fadeCanvasGroup == promptCanvasGroup)
        {
            Debug.LogWarning(
                $"{nameof(SceneTransitionTrigger)} на «{gameObject.name}»: {nameof(fadeCanvasGroup)} совпадает с подсказкой — затемнение перезаписало бы весь LobbyCanvas. Поле сброшено, будет создан/найден отдельный FadeCanvas.",
                this);
            fadeCanvasGroup = null;
        }
    }

    private void OnDestroy()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        _promptColorCached = false;
        ApplyLocalizedPrompt();
    }

    private void EnsurePromptCanvasGroup()
    {
        if (promptUI == null)
            return;

        if (promptCanvasGroup == null)
            promptCanvasGroup = promptUI.GetComponent<CanvasGroup>();
        if (promptCanvasGroup == null)
            promptCanvasGroup = promptUI.AddComponent<CanvasGroup>();
    }

    private void EnsurePromptRectTransformScale()
    {
        if (promptUI == null || !promptUI.TryGetComponent(out RectTransform rt))
            return;

        if (rt.localScale.sqrMagnitude < 1e-8f)
            rt.localScale = _promptUiOriginalLocalScale.sqrMagnitude > 1e-8f ? _promptUiOriginalLocalScale : Vector3.one;
    }

    private void Update()
    {
        bool wantShow = _playerInZone && !IsTargetLevelCompleted() && !_isTransitioning;
        float targetReveal = wantShow ? 1f : 0f;
        _reveal = Mathf.MoveTowards(_reveal, targetReveal, revealSpeed * Time.deltaTime);

        if (promptCanvasGroup != null)
        {
            promptCanvasGroup.alpha = _reveal;
            promptCanvasGroup.blocksRaycasts = _reveal > 0.99f;
        }

        CachePromptColorsOnce();
        ApplyPromptTextAlpha();

        if (wantShow && promptUI != null && !promptUI.activeSelf)
            EnsurePromptUiActive();

        if (wantShow && patientBoardPrefab != null && Camera.main != null)
        {
            if (_spawnedBoard == null)
                SpawnBoardUnderCamera();

            if (_boardView != null)
                _boardView.SetVisualReveal(_reveal);
        }
        else
        {
            if (_boardView != null)
                _boardView.SetVisualReveal(_reveal);

            if (!wantShow && _reveal <= 0.001f && _spawnedBoard != null)
            {
                Destroy(_spawnedBoard);
                _spawnedBoard = null;
                _boardView = null;
            }
        }

        if (promptUI != null && !wantShow && _reveal <= 0.001f && promptUI.activeSelf)
            promptUI.SetActive(false);

        if (_playerInZone && !_isTransitioning && Input.GetKeyDown(KeyCode.E))
            StartTransition();
    }

    private void SpawnBoardUnderCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || patientBoardPrefab == null)
            return;

        _spawnedBoard = Instantiate(patientBoardPrefab, cam.transform, false);
        _spawnedBoard.transform.localPosition = boardLocalPosition;
        _spawnedBoard.transform.localRotation = Quaternion.Euler(boardLocalEulerAngles);

        _boardView = _spawnedBoard.GetComponent<PatientInfoBoardView>();
        if (_boardView == null)
            _boardView = _spawnedBoard.GetComponentInChildren<PatientInfoBoardView>(true);

        if (_boardView != null)
        {
            if (!string.IsNullOrEmpty(targetSceneName))
                _boardView.SetLevelSceneKey(targetSceneName);
            _boardView.SetVisualReveal(0f);
            _boardView.RefreshLocalizedTexts();
        }
    }

    private void CachePromptColorsOnce()
    {
        if (_promptColorCached)
            return;

        if (promptText != null)
        {
            _promptTextBaseColor = promptText.color;
            _promptTextBaseColor.a = Mathf.Max(_promptTextBaseColor.a, 0.02f);
        }

        if (promptText3D != null)
        {
            _prompt3DBaseColor = promptText3D.color;
            _prompt3DBaseColor.a = Mathf.Max(_prompt3DBaseColor.a, 0.02f);
        }

        _promptColorCached = true;
    }

    private void ApplyPromptTextAlpha()
    {
        float a = _reveal;
        if (promptText != null)
        {
            Color c = _promptTextBaseColor;
            if (promptCanvasGroup == null)
                c.a *= a;
            promptText.color = c;
        }

        if (promptText3D != null)
        {
            Color c = _prompt3DBaseColor;
            if (promptCanvasGroup == null)
                c.a *= a;
            promptText3D.color = c;
        }
    }

    private void ApplyLocalizedPrompt(bool forceFallbackIfNeeded = false)
    {
        string resolved = promptFallbackText;

        if (!string.IsNullOrEmpty(promptLocalizationKey) && LocalizationManager.Instance != null)
        {
            string t = LocalizationManager.Instance.T(promptLocalizationKey);
            if (!string.IsNullOrEmpty(t) && t != promptLocalizationKey)
                resolved = t;
        }

        if (promptText != null)
            promptText.text = resolved;
        if (promptText3D != null)
            promptText3D.text = resolved;

        if (forceFallbackIfNeeded)
            _promptColorCached = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        if (IsTargetLevelCompleted())
        {
            _playerInZone = false;
            return;
        }

        RegisterPlayerInZone(playEnterSound: true);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!other.CompareTag("Player") || _isTransitioning || IsTargetLevelCompleted())
            return;

        if (!_playerInZone)
            RegisterPlayerInZone(playEnterSound: true);
        else
            EnsurePromptUiActive();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag("Player"))
            return;

        _playerInZone = false;

        if (!_isTransitioning && exitZonePaperSound != null && audioSource != null)
            audioSource.PlayOneShot(exitZonePaperSound);
    }

    private void StartTransition()
    {
        if (_isTransitioning)
            return;

        if (IsTargetLevelCompleted())
        {
            _isTransitioning = false;
            _playerInZone = false;
            return;
        }

        _isTransitioning = true;
        _reveal = 0f;
        if (promptCanvasGroup != null)
        {
            promptCanvasGroup.alpha = 0f;
            promptCanvasGroup.blocksRaycasts = false;
        }

        ApplyPromptTextAlpha();

        if (_boardView != null)
            _boardView.SetVisualReveal(0f);

        if (transitionStartSound != null && audioSource != null)
            audioSource.PlayOneShot(transitionStartSound);

        StartCoroutine(FadeAndTransition());
    }

    private IEnumerator FadeAndTransition()
    {
        if (fadeCanvasGroup == null)
            fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.gameObject.SetActive(true);
            fadeCanvasGroup.blocksRaycasts = true;
            float from = fadeCanvasGroup.alpha;
            float elapsedTime = 0f;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                fadeCanvasGroup.alpha = Mathf.Lerp(from, 1f, Mathf.Clamp01(elapsedTime / fadeDuration));
                yield return null;
            }

            fadeCanvasGroup.alpha = 1f;
        }

        yield return new WaitForSecondsRealtime(0.1f);

        if (_spawnedBoard != null)
        {
            Destroy(_spawnedBoard);
            _spawnedBoard = null;
            _boardView = null;
        }

        try
        {
            if (!string.IsNullOrEmpty(targetSceneName))
                SceneManager.LoadScene(targetSceneName);
        }
        finally
        {
            _isTransitioning = false;
        }
    }

    private bool IsTargetLevelCompleted()
    {
        if (string.IsNullOrEmpty(targetSceneName))
            return false;
        if (GlobalProgressTracker.Instance != null)
            return GlobalProgressTracker.Instance.IsLevelCompleted(targetSceneName);
        return false;
    }

    private void CheckAndUpdateTeleportState()
    {
        if (IsTargetLevelCompleted())
        {
            if (_triggerCollider != null)
                _triggerCollider.enabled = false;

            _reveal = 0f;
            if (promptCanvasGroup != null)
            {
                promptCanvasGroup.alpha = 0f;
                promptCanvasGroup.blocksRaycasts = false;
            }

            ApplyPromptTextAlpha();

            if (_spawnedBoard != null)
            {
                Destroy(_spawnedBoard);
                _spawnedBoard = null;
                _boardView = null;
            }

            if (promptText != null)
                promptText.color = Color.green;
            if (promptText3D != null)
                promptText3D.color = Color.green;
            _promptTextBaseColor = Color.green;
            _prompt3DBaseColor = Color.green;
        }
        else
        {
            if (promptText != null)
                promptText.color = Color.white;
            if (promptText3D != null)
                promptText3D.color = Color.white;
            _promptTextBaseColor = Color.white;
            _prompt3DBaseColor = Color.white;
        }
    }

    public void UpdateTeleportState()
    {
        CheckAndUpdateTeleportState();
    }
}
