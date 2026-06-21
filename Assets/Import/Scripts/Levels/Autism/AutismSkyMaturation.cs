using UnityEngine;

/// <summary>
/// Фиксированная атмосфера неба уровня Autism: тёплое золотое солнце, лазурный туман.
/// Система дней удалена — небо одинаково во всём уровне.
/// </summary>
public class AutismSkyMaturation : MonoBehaviour
{
    private static readonly int TintId       = Shader.PropertyToID("_Tint");
    private static readonly int SkyTintId    = Shader.PropertyToID("_SkyTint");
    private static readonly int GroundColorId = Shader.PropertyToID("_GroundColor");
    private static readonly int ExposureId   = Shader.PropertyToID("_Exposure");

    [Header("Солнце (пусто → ищется Directional Light)")]
    [SerializeField] private Light sun;

    [Header("Скайбокс-материал (пусто → RenderSettings.skybox, инстанс)")]
    [SerializeField] private Material skyboxMaterial;

    // Фиксированная палитра: тёплое золото + небесная лазурь
    private static readonly Color SunColor     = new Color(1.00f, 0.88f, 0.58f);
    private static readonly Color AmbientColor = new Color(0.48f, 0.62f, 0.80f);
    private static readonly Color FogColor     = new Color(0.76f, 0.88f, 1.00f);
    private static readonly Color SkyTint      = new Color(0.55f, 0.80f, 1.00f);
    private const float SkyExposure = 1.25f;

    private void Awake()
    {
        if (sun == null)
        {
            foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
        }

        if (skyboxMaterial == null && RenderSettings.skybox != null)
        {
            skyboxMaterial = new Material(RenderSettings.skybox);
            RenderSettings.skybox = skyboxMaterial;
        }
    }

    private void Start() => Apply();

    private void Apply()
    {
        if (sun != null) sun.color = SunColor;

        RenderSettings.ambientLight = AmbientColor;
        RenderSettings.fogColor     = FogColor;
        RenderSettings.fog          = true;
        RenderSettings.fogMode      = FogMode.ExponentialSquared;
        RenderSettings.fogDensity   = 0.004f;

        if (skyboxMaterial == null) return;

        if (skyboxMaterial.HasProperty(TintId))          skyboxMaterial.SetColor(TintId, SkyTint);
        if (skyboxMaterial.HasProperty(SkyTintId))       skyboxMaterial.SetColor(SkyTintId, SkyTint);
        if (skyboxMaterial.HasProperty(GroundColorId))   skyboxMaterial.SetColor(GroundColorId, SkyTint * 0.6f);
        if (skyboxMaterial.HasProperty(ExposureId))      skyboxMaterial.SetFloat(ExposureId, SkyExposure);
    }
}
