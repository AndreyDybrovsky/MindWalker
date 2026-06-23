using UnityEngine;

/// <summary>
/// Создаёт системы плавающих частиц пыли для Meadow и Nightmare.
/// Следует за игроком. Переключается при смене режима Bipolar.
/// Добавить на тот же GO, что и BipolarMindscapeController, или на любой другой.
/// </summary>
public class BipolarAtmosphereSpawner : MonoBehaviour
{
    [Header("Meadow (тёплая пыль)")]
    [SerializeField] private int   meadowParticles  = 55;
    [SerializeField] private float meadowEmission   = 7f;
    [SerializeField] private float meadowRadius     = 14f;

    [Header("Nightmare (холодные частицы)")]
    [SerializeField] private int   nightmareParticles = 90;
    [SerializeField] private float nightmareEmission  = 14f;
    [SerializeField] private float nightmareRadius    = 12f;

    private ParticleSystem _meadow;
    private ParticleSystem _nightmare;
    private BipolarMindscapeController _controller;
    private Transform _player;

    private void Start()
    {
        _meadow    = CreateDust(true);
        _nightmare = CreateDust(false);

        _controller = FindFirstObjectByType<BipolarMindscapeController>();
        if (_controller != null)
        {
            _controller.OnModeChanged += OnModeChanged;
            OnModeChanged(_controller.CurrentMode);
        }
        else
        {
            // нет контроллера — запускаем оба, meadow активен
            _meadow.Play();
        }
    }

    private void OnDestroy()
    {
        if (_controller != null)
            _controller.OnModeChanged -= OnModeChanged;
    }

    private void Update()
    {
        if (_player == null)
        {
            _player = PlayerInteractionZone.GetPlayerBodyTransform();
            if (_player == null) return;
        }

        Vector3 pos = _player.position;
        if (_meadow    != null) _meadow.transform.position    = pos;
        if (_nightmare != null) _nightmare.transform.position = pos;
    }

    private void OnModeChanged(BipolarMindscapeMode mode)
    {
        bool isMeadow = mode == BipolarMindscapeMode.Meadow;

        if (_meadow != null)
        {
            if (isMeadow && !_meadow.isPlaying)  _meadow.Play();
            if (!isMeadow && _meadow.isPlaying)  _meadow.Stop();
        }
        if (_nightmare != null)
        {
            if (!isMeadow && !_nightmare.isPlaying) _nightmare.Play();
            if (isMeadow  && _nightmare.isPlaying)  _nightmare.Stop();
        }
    }

    private ParticleSystem CreateDust(bool meadow)
    {
        var go = new GameObject(meadow ? "AtmosphereMeadow" : "AtmosphereNightmare");
        go.transform.SetParent(transform, false);

        var ps   = go.AddComponent<ParticleSystem>();

        // Main
        var main = ps.main;
        main.loop             = true;
        main.maxParticles     = meadow ? meadowParticles : nightmareParticles;
        main.simulationSpace  = ParticleSystemSimulationSpace.World;
        main.startLifetime    = new ParticleSystem.MinMaxCurve(5f, 9f);
        main.startSpeed       = new ParticleSystem.MinMaxCurve(meadow ? 0.02f : 0.06f, meadow ? 0.08f : 0.18f);
        main.startSize        = new ParticleSystem.MinMaxCurve(0.015f, 0.055f);
        // тёплый кремовый для луга, холодный сине-фиолетовый для кошмара
        Color col = meadow
            ? new Color(0.95f, 0.88f, 0.72f, 0.22f)
            : new Color(0.50f, 0.42f, 0.70f, 0.30f);
        main.startColor = col;

        // Emission
        var emission = ps.emission;
        emission.rateOverTime = meadow ? meadowEmission : nightmareEmission;

        // Shape: сфера вокруг игрока
        var shape        = ps.shape;
        shape.shapeType  = ParticleSystemShapeType.Sphere;
        shape.radius     = meadow ? meadowRadius : nightmareRadius;
        shape.radiusThickness = 1f;

        // Velocity over lifetime: лёгкий дрейф вверх (луг) или хаотичный (кошмар)
        var vel   = ps.velocityOverLifetime;
        vel.enabled = true;
        vel.space   = ParticleSystemSimulationSpace.World;
        vel.y = new ParticleSystem.MinMaxCurve(meadow ? 0.01f : -0.03f, meadow ? 0.05f : 0.03f);
        if (!meadow)
        {
            vel.x = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
            vel.z = new ParticleSystem.MinMaxCurve(-0.04f, 0.04f);
        }

        // Size over lifetime: плавное появление и угасание
        var sizeOL    = ps.sizeOverLifetime;
        sizeOL.enabled = true;
        var sizeCurve  = AnimationCurve.EaseInOut(0f, 0f, 1f, 0f);
        sizeCurve.AddKey(new Keyframe(0.15f, 1f));
        sizeCurve.AddKey(new Keyframe(0.85f, 1f));
        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

        // Color over lifetime: fade in/out по альфе
        var colorOL    = ps.colorOverLifetime;
        colorOL.enabled = true;
        var grad        = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(col, 0f), new GradientColorKey(col, 1f) },
            new[]
            {
                new GradientAlphaKey(0f,   0f),
                new GradientAlphaKey(1f,   0.15f),
                new GradientAlphaKey(1f,   0.85f),
                new GradientAlphaKey(0f,   1f)
            });
        colorOL.color = grad;

        // Renderer
        var rend = ps.GetComponent<ParticleSystemRenderer>();
        rend.renderMode = ParticleSystemRenderMode.Billboard;

        Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        if (sh == null) sh = Shader.Find("Sprites/Default");
        if (sh != null)
        {
            var mat = new Material(sh);
            mat.SetFloat("_Surface", 1f);   // Transparent
            mat.renderQueue = 3000;
            rend.material = mat;
        }

        ps.Stop();
        return ps;
    }
}
