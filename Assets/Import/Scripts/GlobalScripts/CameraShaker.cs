using UnityEngine;

/// <summary>
/// Лёгкая процедурная тряска камеры (game feel). Добавляет затухающее вращательное
/// смещение поверх того, что выставил FPS-контроллер (в LateUpdate, поэтому ничего
/// не ломает). Вызывать <see cref="Shake"/> при выстреле, уроне, смерти врага и т.п.
/// Сам находит активную камеру — настройка сцен не нужна.
/// </summary>
[DefaultExecutionOrder(10000)] // LateUpdate после FPS-контроллера, иначе тряску перетрут
public class CameraShaker : MonoBehaviour
{
    [SerializeField] private float maxAngle = 2.5f;
    [SerializeField] private float frequency = 26f;
    [SerializeField] private float decayPerSecond = 1.8f;

    private static CameraShaker _instance;

    private float _trauma;
    private float _seed;

    private void Awake()
    {
        _instance = this;
        _seed = Random.value * 100f;
    }

    /// <summary>amount 0..1 — добавочная «травма». 0.15 выстрел, 0.3 урон, 0.5 смерть врага.</summary>
    public static void Shake(float amount)
    {
        if (_instance == null)
            TryAttach();
        if (_instance != null)
            _instance._trauma = Mathf.Clamp01(_instance._trauma + amount);
    }

    private static void TryAttach()
    {
        Camera cam = Camera.main;
        if (cam == null)
            return;

        _instance = cam.GetComponent<CameraShaker>();
        if (_instance == null)
            _instance = cam.gameObject.AddComponent<CameraShaker>();
    }

    private void LateUpdate()
    {
        if (_trauma <= 0f)
            return;

        float shake = _trauma * _trauma;
        float t = Time.unscaledTime * frequency;

        float ax = (Mathf.PerlinNoise(_seed, t) * 2f - 1f) * maxAngle * shake;
        float ay = (Mathf.PerlinNoise(_seed + 11f, t) * 2f - 1f) * maxAngle * shake;
        float az = (Mathf.PerlinNoise(_seed + 23f, t) * 2f - 1f) * maxAngle * shake;

        transform.localRotation *= Quaternion.Euler(ax, ay, az);

        _trauma = Mathf.Max(0f, _trauma - decayPerSecond * Time.unscaledDeltaTime);
    }
}
