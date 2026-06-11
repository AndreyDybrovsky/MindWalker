using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 3D-доска пациента (World Space UI + меш). Тексты — из LocalizationManager по ключам.
/// Статус — из LobbyPatientProgress / GlobalProgressTracker по linkedLevelSceneName.
/// </summary>
public class PatientInfoBoardView : MonoBehaviour
{
    public enum PatientLobbyStatus
    {
        Sick = 0,
        Healthy = 1,
        Lost = 2
    }

    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [Header("Локация (уровень для статуса)")]
    [SerializeField] private string linkedLevelSceneName;

    [Header("Модель Clipboard")]
    [SerializeField] private Transform clipboardModelRoot;

    [Header("Появление")]
    [SerializeField] private bool useScaleReveal = true;
    [SerializeField] private Vector3 hiddenLocalScale = new Vector3(0.02f, 0.02f, 0.02f);
    [SerializeField] private CanvasGroup worldUiCanvasGroup;
    [SerializeField] private bool fadeMeshMaterials;
    [SerializeField] private Renderer[] meshRenderersForFade;

    [Header("Портрет")]
    [SerializeField] private Sprite portraitSprite;

    [Header("Ключи локализации (strings_*.json / LocalizationBase)")]
    [SerializeField] private string locKeyStatusSick = "patient.status_sick";
    [SerializeField] private string locKeyStatusHealthy = "patient.status_healthy";
    [SerializeField] private string locKeyStatusLost = "patient.status_lost";
    [SerializeField] private string locKeyPatientAge = "patient.age_years";
    [SerializeField] private string locKeyProfileLine = "patient.profile_line";
    [SerializeField] private string locKeyPatientName;
    [SerializeField] private string locKeyDiagnosis;
    [SerializeField] private string locKeyNotes;
    [Tooltip("Число лет, например ключ patient.ptsd.age со значением \"18\".")]
    [SerializeField] private string locKeyPatientAgeNumber;

    [Header("Печать (спрайты)")]
    [SerializeField] private Sprite stampHealthy;
    [SerializeField] private Sprite stampLost;

    [Header("UI на модели")]
    [SerializeField] private Image portraitImage;
    [SerializeField] private TMP_Text patientNameText;
    [SerializeField] private TMP_Text patientAgeText;
    [SerializeField] private TMP_Text diagnosisText;
    [SerializeField] private TMP_Text notesText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private Image stampImage;

    private float _visibility;
    private Vector3 _shownLocalScale;
    private MaterialPropertyBlock _mpb;
    private Color[] _rendererBaseColors;
    private bool[] _rendererUsesBaseColor;

    private string _linkedLevelOverride;

    private void Awake()
    {
        if (clipboardModelRoot == null)
            clipboardModelRoot = transform;

        _shownLocalScale = clipboardModelRoot.localScale;

        if (worldUiCanvasGroup == null)
            worldUiCanvasGroup = clipboardModelRoot.GetComponentInChildren<CanvasGroup>(true);

        if (worldUiCanvasGroup != null)
        {
            worldUiCanvasGroup.alpha = 0f;
            worldUiCanvasGroup.blocksRaycasts = false;
            worldUiCanvasGroup.interactable = false;
        }

        if (fadeMeshMaterials && meshRenderersForFade != null && meshRenderersForFade.Length > 0)
            CacheRendererColors();

        ApplyHiddenVisualState();
        RefreshLocalizedTexts();
    }

    private void OnEnable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged += OnLanguageChanged;
    }

    private void OnDisable()
    {
        if (LocalizationManager.Instance != null)
            LocalizationManager.Instance.OnLanguageChanged -= OnLanguageChanged;
    }

    private void OnLanguageChanged(GameLanguage _)
    {
        RefreshLocalizedTexts();
    }

    public void SetLevelSceneKey(string sceneName)
    {
        _linkedLevelOverride = sceneName;
        RefreshLocalizedTexts();
    }

    /// <summary>Привязать World Space Canvas к камере игрока (нужно после спавна под камерой).</summary>
    public void BindViewCamera(Camera cam)
    {
        if (cam == null)
            return;

        Canvas[] canvases = GetComponentsInChildren<Canvas>(true);
        for (int i = 0; i < canvases.Length; i++)
        {
            Canvas canvas = canvases[i];
            if (canvas != null && canvas.renderMode == RenderMode.WorldSpace)
                canvas.worldCamera = cam;
        }
    }

    public void SetVisualReveal(float alpha)
    {
        _visibility = Mathf.Clamp01(alpha);
        ApplyVisibilityToModel();
    }

    private void CacheRendererColors()
    {
        _mpb = new MaterialPropertyBlock();
        int n = meshRenderersForFade.Length;
        _rendererBaseColors = new Color[n];
        _rendererUsesBaseColor = new bool[n];

        for (int i = 0; i < n; i++)
        {
            Renderer r = meshRenderersForFade[i];
            if (r == null)
                continue;

            Material m = r.sharedMaterial;
            if (m == null)
                continue;

            if (m.HasProperty(BaseColorId))
            {
                _rendererBaseColors[i] = m.GetColor(BaseColorId);
                _rendererUsesBaseColor[i] = true;
            }
            else if (m.HasProperty(ColorId))
            {
                _rendererBaseColors[i] = m.GetColor(ColorId);
                _rendererUsesBaseColor[i] = false;
            }
            else
            {
                _rendererBaseColors[i] = Color.white;
                _rendererUsesBaseColor[i] = true;
            }
        }
    }

    private void ApplyHiddenVisualState()
    {
        if (useScaleReveal)
            clipboardModelRoot.localScale = hiddenLocalScale;
    }

    private void ApplyVisibilityToModel()
    {
        if (useScaleReveal)
        {
            clipboardModelRoot.localScale = Vector3.Lerp(hiddenLocalScale, _shownLocalScale,
                Mathf.SmoothStep(0f, 1f, _visibility));
        }

        if (worldUiCanvasGroup != null)
            worldUiCanvasGroup.alpha = _visibility;

        if (!fadeMeshMaterials || meshRenderersForFade == null || _rendererBaseColors == null)
            return;

        for (int i = 0; i < meshRenderersForFade.Length; i++)
        {
            Renderer r = meshRenderersForFade[i];
            if (r == null)
                continue;

            Color c = _rendererBaseColors[i];
            c.a *= _visibility;
            r.GetPropertyBlock(_mpb);
            if (_rendererUsesBaseColor[i])
                _mpb.SetColor(BaseColorId, c);
            else
                _mpb.SetColor(ColorId, c);
            r.SetPropertyBlock(_mpb);
        }
    }

    private string EffectiveLevelName()
    {
        return !string.IsNullOrEmpty(_linkedLevelOverride) ? _linkedLevelOverride : linkedLevelSceneName;
    }

    private PatientLobbyStatus ResolveStatus()
    {
        string level = EffectiveLevelName();
        if (LobbyPatientProgress.IsLost(level))
            return PatientLobbyStatus.Lost;

        if (GlobalProgressTracker.Instance != null &&
            GlobalProgressTracker.Instance.IsLevelCompleted(level))
            return PatientLobbyStatus.Healthy;

        return PatientLobbyStatus.Sick;
    }

    public void RefreshLocalizedTexts()
    {
        PatientLobbyStatus status = ResolveStatus();
        ApplyPortrait();
        ApplyLocalizedPatientFields();
        RefreshStatusVisuals(status);
    }

    private void ApplyPortrait()
    {
        if (portraitImage != null && portraitSprite != null)
            portraitImage.sprite = portraitSprite;
    }

    private void ApplyLocalizedPatientFields()
    {
        string displayName = Loc(locKeyPatientName);
        string localizedDiagnosis = Loc(locKeyDiagnosis);
        string localizedNotes = Loc(locKeyNotes);
        int age = ResolvePatientAge();

        if (patientNameText != null && patientAgeText != null)
        {
            patientNameText.text = displayName;
            patientAgeText.text = FormatLocalizedAge(age);
        }
        else if (patientNameText != null)
        {
            patientNameText.text = BuildCombinedProfileLine(displayName, age);
        }
        else if (patientAgeText != null)
        {
            patientAgeText.text = FormatLocalizedAge(age);
        }

        if (diagnosisText != null)
            diagnosisText.text = localizedDiagnosis;

        if (notesText != null)
            notesText.text = localizedNotes;
    }

    private int ResolvePatientAge()
    {
        if (string.IsNullOrEmpty(locKeyPatientAgeNumber))
            return 0;

        string raw = Loc(locKeyPatientAgeNumber);
        return int.TryParse(raw, out int age) ? age : 0;
    }

    private string FormatLocalizedAge(int age)
    {
        string fmt = Loc(locKeyPatientAge);
        if (string.IsNullOrEmpty(fmt))
            fmt = "{0}";

        try
        {
            return string.Format(fmt, age);
        }
        catch
        {
            return age.ToString();
        }
    }

    private string BuildCombinedProfileLine(string displayName, int age)
    {
        if (!string.IsNullOrEmpty(locKeyProfileLine))
        {
            string fmt = Loc(locKeyProfileLine);
            if (!string.IsNullOrEmpty(fmt))
            {
                try
                {
                    return string.Format(fmt, displayName, age);
                }
                catch
                {
                    return $"{displayName}, {age}";
                }
            }
        }

        return $"{displayName}, {age}";
    }

    private static string Loc(string key)
    {
        if (LocalizationManager.Instance == null || string.IsNullOrEmpty(key))
            return string.Empty;

        string t = LocalizationManager.Instance.T(key);
        return string.IsNullOrEmpty(t) || t == key ? string.Empty : t;
    }

    /// <summary>
    /// Принудительно показывает нужную печать без проверки прогресса уровня.
    /// Используется экраном результата уровня.
    /// </summary>
    public void ForceStamp(bool isSuccess)
    {
        if (stampImage == null)
            return;

        Sprite stamp = isSuccess ? stampHealthy : stampLost;
        stampImage.sprite = stamp;
        stampImage.enabled = stamp != null;

        if (statusText != null)
        {
            string key = isSuccess ? locKeyStatusHealthy : locKeyStatusLost;
            string label = Loc(key);
            if (string.IsNullOrEmpty(label))
                label = isSuccess ? "Здоров" : "Потерян";
            statusText.text = label;
        }
    }

    private void RefreshStatusVisuals(PatientLobbyStatus status)
    {
        string sick = Loc(locKeyStatusSick);
        string healthy = Loc(locKeyStatusHealthy);
        string lost = Loc(locKeyStatusLost);

        if (string.IsNullOrEmpty(sick)) sick = "Болен";
        if (string.IsNullOrEmpty(healthy)) healthy = "Здоров";
        if (string.IsNullOrEmpty(lost)) lost = "Потерян";

        string label = status switch
        {
            PatientLobbyStatus.Healthy => healthy,
            PatientLobbyStatus.Lost => lost,
            _ => sick
        };

        if (statusText != null)
            statusText.text = label;

        if (stampImage == null)
            return;

        if (status == PatientLobbyStatus.Sick)
        {
            stampImage.enabled = false;
            return;
        }

        Sprite stamp = status == PatientLobbyStatus.Healthy ? stampHealthy : stampLost;
        stampImage.sprite = stamp;
        stampImage.enabled = stamp != null;
    }
}
