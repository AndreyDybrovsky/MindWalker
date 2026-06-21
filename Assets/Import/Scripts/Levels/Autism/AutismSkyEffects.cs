using UnityEngine;

/// <summary>
/// Атмосферные эффекты неба уровня Autism: ветровые частицы и туман.
/// </summary>
[DisallowMultipleComponent]
public class AutismSkyEffects : MonoBehaviour
{
    [Header("Ветровые частицы")]
    [SerializeField] private bool enableWindDust = true;

    [Header("Звук ветра (назначить AudioClip вручную)")]
    [SerializeField] private AudioClip windSound;
    [SerializeField, Range(0f, 1f)] private float windVolume = 0.12f;

    private void Start()
    {
        if (enableWindDust) CreateWindDust();
        SetupFog();
        if (windSound != null) SetupWindAudio();
    }

    // ─── Ветровые частицы ───────────────────────────────────────────────────

    private void CreateWindDust()
    {
        var go = new GameObject("WindDust");
        go.transform.SetParent(transform, false);
        go.transform.position = new Vector3(-40f, -14f, 15f);

        var ps  = go.AddComponent<ParticleSystem>();
        var psr = go.GetComponent<ParticleSystemRenderer>();

        psr.material   = CreateParticleMat(new Color(0.9f, 0.95f, 1f, 0.5f));
        psr.renderMode = ParticleSystemRenderMode.Billboard;

        var main = ps.main;
        main.loop            = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.maxParticles    = 400;
        main.startLifetime   = new ParticleSystem.MinMaxCurve(8f, 16f);
        main.startSpeed      = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
        main.startSize       = new ParticleSystem.MinMaxCurve(0.06f, 0.22f);
        main.startColor      = new ParticleSystem.MinMaxGradient(
            new Color(1f, 1f, 1f, 0.15f), new Color(0.9f, 0.95f, 1f, 0.5f));
        main.gravityModifier = -0.025f;

        var em = ps.emission;
        em.rateOverTime = 20f;

        var sh = ps.shape;
        sh.shapeType = ParticleSystemShapeType.Box;
        sh.scale     = new Vector3(220f, 30f, 220f);

        var vel = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.x = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        vel.y = new ParticleSystem.MinMaxCurve(0.04f, 0.18f);
        vel.z = new ParticleSystem.MinMaxCurve(-0.1f,  0.1f);

        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f),   new GradientAlphaKey(1f, 0.12f),
                    new GradientAlphaKey(1f, 0.88f), new GradientAlphaKey(0f, 1f) });
        col.color = new ParticleSystem.MinMaxGradient(grad);

        ps.Play();
    }

    // ─── Материал частиц ────────────────────────────────────────────────────

    private static Material CreateParticleMat(Color color)
    {
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                  ?? Shader.Find("Particles/Standard Unlit")
                  ?? Shader.Find("Mobile/Particles/Alpha Blended")
                  ?? Shader.Find("Sprites/Default");

        var mat = new Material(shader);

        if (mat.HasProperty("_Surface"))   mat.SetFloat("_Surface",   1f);
        if (mat.HasProperty("_Blend"))     mat.SetFloat("_Blend",     0f);
        if (mat.HasProperty("_BlendMode")) mat.SetFloat("_BlendMode", 0f);
        if (mat.HasProperty("_SrcBlend"))  mat.SetFloat("_SrcBlend",  5f);
        if (mat.HasProperty("_DstBlend"))  mat.SetFloat("_DstBlend", 10f);
        if (mat.HasProperty("_ZWrite"))    mat.SetFloat("_ZWrite",    0f);
        if (mat.HasProperty("_ColorMode")) mat.SetFloat("_ColorMode", 0f);

        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     color);

        return mat;
    }

    // ─── Туман высоты ───────────────────────────────────────────────────────

    private static void SetupFog()
    {
        RenderSettings.fog        = true;
        RenderSettings.fogMode    = FogMode.ExponentialSquared;
        RenderSettings.fogColor   = new Color(0.72f, 0.86f, 1.00f);
        RenderSettings.fogDensity = 0.004f;
    }

    // ─── Звук ветра ─────────────────────────────────────────────────────────

    private void SetupWindAudio()
    {
        var go  = new GameObject("WindAmbient");
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.clip         = windSound;
        src.loop         = true;
        src.volume       = windVolume;
        src.spatialBlend = 0f;
        src.playOnAwake  = true;
        AudioMixerRoutingUtility.BindSourceToSfx(src);
        src.Play();
    }
}
