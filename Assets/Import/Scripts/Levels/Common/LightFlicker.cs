using UnityEngine;

/// <summary>
/// Случайное мерцание Light через Perlin Noise.
/// Опционально: только в Nightmare-режиме Bipolar (nightmareOnly = true).
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [SerializeField] private float minIntensity = 0.4f;
    [SerializeField] private float maxIntensity = 1.6f;
    [SerializeField] private float noiseSpeed   = 5f;

    [Tooltip("Мерцать только в Nightmare-режиме Bipolar. Отключать себя в Meadow.")]
    [SerializeField] private bool nightmareOnly = false;

    private Light  _light;
    private float  _noiseOffset;
    private float  _baseIntensity;
    private BipolarMindscapeController _controller;

    private void Awake()
    {
        _light         = GetComponent<Light>();
        _baseIntensity = _light.intensity;
        _noiseOffset   = Random.Range(0f, 100f);
    }

    private void Start()
    {
        if (!nightmareOnly) return;

        _controller = FindFirstObjectByType<BipolarMindscapeController>();
        if (_controller != null)
            _controller.OnModeChanged += OnModeChanged;

        bool inNightmare = _controller != null &&
                           _controller.CurrentMode == BipolarMindscapeMode.Nightmare;
        enabled = inNightmare;
    }

    private void OnDestroy()
    {
        if (_controller != null)
            _controller.OnModeChanged -= OnModeChanged;
    }

    private void OnModeChanged(BipolarMindscapeMode mode)
    {
        enabled = (mode == BipolarMindscapeMode.Nightmare);
        if (!enabled)
            _light.intensity = _baseIntensity;
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * noiseSpeed + _noiseOffset, 0f);
        _light.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }

    private void OnDisable()
    {
        if (_light != null)
            _light.intensity = _baseIntensity;
    }
}
