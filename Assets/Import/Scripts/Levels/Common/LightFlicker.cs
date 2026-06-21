using UnityEngine;

/// <summary>
/// Случайное мерцание Light через Perlin Noise.
/// </summary>
[RequireComponent(typeof(Light))]
public class LightFlicker : MonoBehaviour
{
    [SerializeField] private float minIntensity = 0.4f;
    [SerializeField] private float maxIntensity = 1.6f;
    [SerializeField] private float noiseSpeed   = 5f;

    private Light _light;
    private float _noiseOffset;

    private void Awake()
    {
        _light = GetComponent<Light>();
        _noiseOffset = Random.Range(0f, 100f);
    }

    private void Update()
    {
        float noise = Mathf.PerlinNoise(Time.time * noiseSpeed + _noiseOffset, 0f);
        _light.intensity = Mathf.Lerp(minIntensity, maxIntensity, noise);
    }
}
