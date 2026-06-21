using System.Collections;
using UnityEngine;

/// <summary>
/// «Сочность» батута: при отскоке игрока — squash/stretch визуала, вспышка света,
/// всплеск частиц. Подписывается на событие <see cref="TrampolinePad.Bounced"/>.
///
/// Вешается на тот же объект, что и TrampolinePad (или на родителя).
/// Визуал (squashTarget) по умолчанию — родительский корень батута.
/// </summary>
public class TrampolineJuice : MonoBehaviour
{
    [Header("Цель деформации (пусто → родительский корень)")]
    [SerializeField] private Transform squashTarget;

    [Header("Squash / Stretch")]
    [Tooltip("Насколько приплюснуть по высоте в момент удара (0.6 = 60% высоты).")]
    [SerializeField] private float squashScaleY = 0.6f;
    [Tooltip("Насколько растянуть в стороны при сжатии.")]
    [SerializeField] private float squashScaleXZ = 1.25f;
    [Tooltip("Длительность всей анимации пружины.")]
    [SerializeField] private float duration = 0.45f;

    [Header("Вспышка света (создаётся, если пусто)")]
    [SerializeField] private Light flashLight;
    [SerializeField] private float flashIntensity = 4f;
    [SerializeField] private float flashRange = 6f;
    [SerializeField] private Color flashColor = new Color(0.6f, 1f, 0.8f);

    [Header("Частицы (создаются, если пусто)")]
    [SerializeField] private ParticleSystem burst;
    [SerializeField] private Color particleColor = new Color(0.6f, 1f, 0.8f);

    private TrampolinePad _pad;
    private Vector3 _baseScale;
    private Coroutine _routine;

    private void Awake()
    {
        _pad = GetComponent<TrampolinePad>();
        if (_pad == null) _pad = GetComponentInParent<TrampolinePad>();

        if (squashTarget == null)
            squashTarget = transform.parent != null ? transform.parent : transform;

        _baseScale = squashTarget.localScale;

        EnsureLight();
        EnsureParticles();
    }

    private void OnEnable()
    {
        if (_pad != null) _pad.Bounced += OnBounced;
    }

    private void OnDisable()
    {
        if (_pad != null) _pad.Bounced -= OnBounced;
    }

    private void OnBounced()
    {
        if (_routine != null) StopCoroutine(_routine);
        _routine = StartCoroutine(SquashRoutine());

        if (burst != null) burst.Play();

        if (flashLight != null)
            StartCoroutine(FlashRoutine());
    }

    private IEnumerator SquashRoutine()
    {
        // Фаза 1: мгновенное сжатие → 2: пружинистый возврат с лёгким перелётом.
        Vector3 squashed = new Vector3(
            _baseScale.x * squashScaleXZ,
            _baseScale.y * squashScaleY,
            _baseScale.z * squashScaleXZ);

        float t = 0f;
        float squashPhase = duration * 0.25f;
        while (t < squashPhase)
        {
            t += Time.deltaTime;
            squashTarget.localScale = Vector3.Lerp(_baseScale, squashed, t / squashPhase);
            yield return null;
        }

        // Возврат с overshoot (упругая пружина).
        float reboundPhase = duration * 0.75f;
        t = 0f;
        while (t < reboundPhase)
        {
            t += Time.deltaTime;
            float p = t / reboundPhase;
            // затухающая синусоида для эффекта пружины
            float spring = 1f + Mathf.Sin(p * Mathf.PI * 2f) * 0.12f * (1f - p);
            squashTarget.localScale = Vector3.Lerp(squashed, _baseScale * spring, p);
            yield return null;
        }

        squashTarget.localScale = _baseScale;
        _routine = null;
    }

    private IEnumerator FlashRoutine()
    {
        flashLight.enabled = true;
        float t = 0f;
        const float flashDur = 0.25f;
        while (t < flashDur)
        {
            t += Time.deltaTime;
            flashLight.intensity = Mathf.Lerp(flashIntensity, 0f, t / flashDur);
            yield return null;
        }
        flashLight.intensity = 0f;
        flashLight.enabled = false;
    }

    private void EnsureLight()
    {
        if (flashLight != null) return;

        var go = new GameObject("BounceFlash");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.3f;

        flashLight = go.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = flashColor;
        flashLight.range = flashRange;
        flashLight.intensity = 0f;
        flashLight.enabled = false;
    }

    private void EnsureParticles()
    {
        if (burst != null) return;

        var go = new GameObject("BounceBurst");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = Vector3.up * 0.2f;
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // вверх

        burst = go.AddComponent<ParticleSystem>();
        burst.Stop();

        var main = burst.main;
        main.duration = 0.6f;
        main.loop = false;
        main.playOnAwake = false;
        main.startLifetime = 0.6f;
        main.startSpeed = 5f;
        main.startSize = 0.18f;
        main.startColor = particleColor;
        main.gravityModifier = 0.8f;
        main.maxParticles = 60;

        var emission = burst.emission;
        emission.enabled = true;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 24) });

        var shape = burst.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.4f;

        var colorOverLife = burst.colorOverLifetime;
        colorOverLife.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new[] { new GradientColorKey(particleColor, 0f), new GradientColorKey(particleColor, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colorOverLife.color = grad;

        // URP-совместимый материал для рендера частиц.
        var renderer = burst.GetComponent<ParticleSystemRenderer>();
        if (renderer != null)
        {
            Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (sh == null) sh = Shader.Find("Sprites/Default");
            if (sh != null) renderer.material = new Material(sh);
        }
    }
}
