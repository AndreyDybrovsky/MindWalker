using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Триггер входа в зону перехода: подсказка с локализацией, 3D-доска пациента у камеры, звуки входа/выхода, E — затемнение и загрузка сцены.
/// </summary>
[DefaultExecutionOrder(100)]
public class SceneTransitionTrigger : MonoBehaviour, IPressEPromptContributor
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
    private bool _playerNearby;
    private bool _isTransitioning;
    private Collider _triggerCollider;
    private float _reveal;
    private float _boardReveal;

    private GameObject _spawnedBoard;
    private PatientInfoBoardView _boardView;

    private Color _promptTextBaseColor = Color.white;
    private Color _prompt3DBaseColor = Color.white;
    private bool _promptColorCached;
    private Vector3 _promptUiOriginalLocalScale = Vector3.one;
    private bool _usesSharedPressPrompt;

    public float PressPromptReveal => _reveal;
    public bool IsPressPromptVisible =>
        _playerInZone && !_isTransitioning;

    public void ApplySharedPressPromptText(PressEPromptView view)
    {
        if (view == null)
            return;

        view.SetText(ResolvePromptText());
    }

    private string ResolvePromptText()
    {
        return PressEPromptUtility.ResolveLocalizedText(promptLocalizationKey, promptFallbackText);
    }

    public void ApplySharedUi(GameObject prompt, TextMeshProUGUI text, CanvasGroup fade)
    {
        if (promptUI == null && prompt != null)
            promptUI = prompt;

        if (promptText == null && text != null)
            promptText = text;

        if (IsInvalidFadeCanvasGroup(fadeCanvasGroup))
            fadeCanvasGroup = null;

        if (fade != null && !IsInvalidFadeCanvasGroup(fade))
            fadeCanvasGroup = fade;
    }

    public bool TryGetPatientBoardPrefab(out GameObject prefab)
    {
        prefab = patientBoardPrefab;
        return prefab != null;
    }

    public void ApplyDefaultPatientBoardIfMissing(GameObject fallbackPrefab)
    {
        if (patientBoardPrefab == null && fallbackPrefab != null)
            patientBoardPrefab = fallbackPrefab;
    }

  /// <summary>Вызывается из <see cref="LobbyTransitionBootstrap"/> после привязки общих UI.</summary>
    public void NotifyLobbyBootstrapComplete()
    {
        ResolveSharedReferences();
        RefreshSharedPromptBinding();
        PressEPromptCoordinator.Refresh();
    }

    private void Awake()
    {
        _triggerCollider = GetComponent<Collider>();
        if (_triggerCollider != null)
            _triggerCollider.isTrigger = true;

        EnsureTriggerRigidbody();
    }

    private void EnsureTriggerRigidbody()
    {
        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void ResolveSharedReferences()
    {
        PressEPromptView sharedView = PressEPromptUtility.AcquireSharedPrompt();
        if (sharedView != null)
        {
            if (promptUI == null)
                promptUI = sharedView.gameObject;

            if (promptText == null)
                promptText = sharedView.Label;

            if (promptCanvasGroup == null)
                promptCanvasGroup = sharedView.CanvasGroup;

            _usesSharedPressPrompt = true;
        }
        else if (promptUI == null || promptText == null)
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

        EnsureValidFadeCanvasGroup();
    }

    private void EnsureValidFadeCanvasGroup()
    {
        if (fadeCanvasGroup != null && promptCanvasGroup != null && fadeCanvasGroup == promptCanvasGroup)
            fadeCanvasGroup = null;

        if (fadeCanvasGroup != null && IsInvalidFadeCanvasGroup(fadeCanvasGroup))
            fadeCanvasGroup = null;

        if (fadeCanvasGroup != null)
            return;

        fadeCanvasGroup = ScreenFadeUtility.EnsureFadeCanvasGroup();
        if (fadeCanvasGroup != null && fadeCanvasGroup.alpha > 0.99f)
            fadeCanvasGroup.alpha = 0f;
    }

    private static bool IsInvalidFadeCanvasGroup(CanvasGroup group)
    {
        if (group == null)
            return true;

        if (HasInvalidFadeAncestor(group.transform))
            return true;

        Transform root = group.transform.root;
        if (root != null)
        {
            string rootName = root.name;
            if (rootName.IndexOf("Player", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }

        return false;
    }

    private static bool HasInvalidFadeAncestor(Transform node)
    {
        while (node != null)
        {
            string name = node.name;
            if (name.IndexOf("Menu", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("LobbyCanvas", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
            if (name.IndexOf("ElseCanvas", StringComparison.OrdinalIgnoreCase) >= 0)
                return true;

            node = node.parent;
        }

        return false;
    }

    private void RefreshSharedPromptBinding()
    {
        PressEPromptView sharedView = PressEPromptUtility.AcquireSharedPrompt();
        _usesSharedPressPrompt = sharedView != null
            && promptUI != null
            && promptUI == sharedView.gameObject;
    }

    private void Start()
    {
        if (_triggerCollider == null)
            _triggerCollider = GetComponent<Collider>();

        ResolveSharedReferences();
        RefreshSharedPromptBinding();
        CheckAndUpdateTeleportState();
        SanitizeFadeAndPromptCanvasGroups();
        EnsureValidFadeCanvasGroup();

        if (promptUI != null && !_usesSharedPressPrompt)
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
        else if (_usesSharedPressPrompt)
        {
            EnsurePromptCanvasGroup();
        }

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
        }

        AudioMixerRoutingUtility.BindSourceToSfx(audioSource);

        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;

        ApplyLocalizedPrompt(forceFallbackIfNeeded: true);
        RefreshSharedPromptBinding();
        PressEPromptCoordinator.Register(this);
        PressEPromptUtility.HideNonSharedPressEPrompts();
        RefreshSharedPromptBinding();
        PressEPromptCoordinator.Refresh();
        StartCoroutine(CheckPlayerSpawnedInsideTriggerNextFrame());
    }

    private IEnumerator CheckPlayerSpawnedInsideTriggerNextFrame()
    {
        yield return null;
        PressEPromptUtility.HideNonSharedPressEPrompts();
        RefreshSharedPromptBinding();
        PressEPromptCoordinator.Refresh();
        TryRegisterPlayerIfAlreadyInsideTrigger(playEnterSound: false);
    }

    /// <summary>Игрок уже в коллайдере при загрузке сцены — OnTriggerEnter не приходит, включаем UI вручную.</summary>
    private void TryRegisterPlayerIfAlreadyInsideTrigger(bool playEnterSound)
    {
        if (_isTransitioning || _triggerCollider == null)
            return;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return;

        Vector3 pt = body.position;
        Vector3 closest = _triggerCollider.ClosestPoint(pt);
        if ((closest - pt).sqrMagnitude > 0.0004f)
            return;

        _playerNearby = true;

        if (!IsLevelEntryBlocked())
            RegisterPlayerInZone(playEnterSound);
    }

    private void RegisterPlayerInZone(bool playEnterSound)
    {
        if (IsLevelEntryBlocked())
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
        PressEPromptCoordinator.Refresh();
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
        PressEPromptCoordinator.Unregister(this);
        PressEPromptCoordinator.Refresh();

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
        SyncPlayerZoneState();

        bool wantShow = _playerInZone && !_isTransitioning;
        float targetReveal = wantShow ? 1f : 0f;
        _reveal = Mathf.MoveTowards(_reveal, targetReveal, revealSpeed * Time.deltaTime);

        float boardTarget = (_playerNearby && !_isTransitioning) ? 1f : 0f;
        _boardReveal = Mathf.MoveTowards(_boardReveal, boardTarget, revealSpeed * Time.deltaTime);

        if (_usesSharedPressPrompt)
        {
            PressEPromptCoordinator.Refresh();
        }
        else if (promptCanvasGroup != null)
        {
            promptCanvasGroup.alpha = _reveal;
            promptCanvasGroup.blocksRaycasts = _reveal > 0.99f;
        }

        CachePromptColorsOnce();
        if (!_usesSharedPressPrompt)
            ApplyPromptTextAlpha();

        if (wantShow && promptUI != null && !promptUI.activeSelf && !_usesSharedPressPrompt)
            EnsurePromptUiActive();

        bool boardWantShow = _playerNearby && !_isTransitioning && patientBoardPrefab != null;
        if (boardWantShow)
        {
            Camera viewCamera = ResolveViewCamera();
            if (viewCamera != null)
            {
                if (_spawnedBoard == null)
                    SpawnBoardUnderCamera(viewCamera);
                else if (_boardView == null)
                {
                    _boardView = _spawnedBoard.GetComponentInChildren<PatientInfoBoardView>(true);
                    if (_boardView != null)
                        _boardView.BindViewCamera(viewCamera);
                }

                if (_boardView != null)
                    _boardView.SetVisualReveal(_boardReveal);
            }
        }
        else
        {
            if (_boardView != null)
                _boardView.SetVisualReveal(_boardReveal);

            if (!_playerNearby && _boardReveal <= 0.001f && _spawnedBoard != null)
            {
                Destroy(_spawnedBoard);
                _spawnedBoard = null;
                _boardView = null;
            }
        }

        if (!_usesSharedPressPrompt && promptUI != null && !wantShow && _reveal <= 0.001f && promptUI.activeSelf)
            promptUI.SetActive(false);

        if (_playerInZone && !_isTransitioning && Input.GetKeyDown(KeyCode.E))
            StartTransition();
    }

    private void SyncPlayerZoneState()
    {
        if (_triggerCollider == null)
            return;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
        {
            bool changed = _playerInZone || _playerNearby;
            _playerInZone = false;
            _playerNearby = false;
            if (changed) PressEPromptCoordinator.Refresh();
            return;
        }

        Vector3 closest = _triggerCollider.ClosestPoint(body.position);
        bool inside = (closest - body.position).sqrMagnitude <= 0.25f;

        _playerNearby = inside;

        if (_isTransitioning || IsLevelEntryBlocked())
        {
            if (_playerInZone)
            {
                _playerInZone = false;
                PressEPromptCoordinator.Refresh();
            }
            return;
        }

        if (inside && !_playerInZone)
            RegisterPlayerInZone(playEnterSound: true);
        else if (!inside && _playerInZone)
        {
            _playerInZone = false;
            PressEPromptCoordinator.Refresh();
        }
    }

    private static Camera ResolveViewCamera()
    {
        if (PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
        {
            Camera playerCam = body.GetComponentInChildren<Camera>(true);
            if (IsUsableViewCamera(playerCam))
                return playerCam;
        }

        Camera main = Camera.main;
        if (IsUsableViewCamera(main))
            return main;

        Camera[] cameras = FindObjectsByType<Camera>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        for (int i = 0; i < cameras.Length; i++)
        {
            if (IsUsableViewCamera(cameras[i]))
                return cameras[i];
        }

        return null;
    }

    private static bool IsUsableViewCamera(Camera cam)
    {
        return cam != null && cam.enabled && cam.gameObject.activeInHierarchy;
    }

    private void SpawnBoardUnderCamera(Camera cam)
    {
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
            _boardView.BindViewCamera(cam);

            if (!string.IsNullOrEmpty(targetSceneName))
                _boardView.SetLevelSceneKey(targetSceneName);

            _boardView.SetVisualReveal(0f);
            _boardView.RefreshLocalizedTexts();
        }
        else
        {
            Debug.LogWarning(
                $"{nameof(SceneTransitionTrigger)} «{gameObject.name}»: на префабе доски нет {nameof(PatientInfoBoardView)} — карточка пациента не появится.",
                patientBoardPrefab);
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
        string resolved = ResolvePromptText();

        if (_usesSharedPressPrompt)
        {
            PressEPromptView shared = PressEPromptUtility.AcquireSharedPrompt();
            if (shared != null)
                shared.SetText(resolved);
        }
        else
        {
            if (promptText != null)
                promptText.text = resolved;
            if (promptText3D != null)
                promptText3D.text = resolved;
        }

        if (forceFallbackIfNeeded)
            _promptColorCached = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerBodyCollider(other))
            return;

        _playerNearby = true;

        if (IsLevelEntryBlocked())
        {
            _playerInZone = false;
            return;
        }

        RegisterPlayerInZone(playEnterSound: true);
    }

    private void OnTriggerStay(Collider other)
    {
        if (!IsPlayerBodyCollider(other))
            return;

        _playerNearby = true;

        if (_isTransitioning || IsLevelEntryBlocked())
            return;

        if (!_playerInZone)
            RegisterPlayerInZone(playEnterSound: true);
        else
            EnsurePromptUiActive();
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayerBodyCollider(other))
            return;

        _playerNearby = false;
        _playerInZone = false;
        PressEPromptCoordinator.Refresh();

        if (!_isTransitioning && exitZonePaperSound != null && audioSource != null)
            audioSource.PlayOneShot(exitZonePaperSound);
    }

    private void StartTransition()
    {
        if (_isTransitioning)
            return;

        if (IsLevelEntryBlocked())
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

    private bool IsLevelEntryBlocked()
    {
        return LevelSceneProgress.IsEntryBlocked(targetSceneName);
    }

    private void CheckAndUpdateTeleportState()
    {
        if (_triggerCollider != null && !_triggerCollider.enabled)
            _triggerCollider.enabled = true;

        if (IsLevelEntryBlocked())
        {
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

    private static bool IsPlayerBodyCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.TryGetComponent(out CharacterController _))
            return true;

        return other.GetComponentInParent<CharacterController>() != null;
    }
}
