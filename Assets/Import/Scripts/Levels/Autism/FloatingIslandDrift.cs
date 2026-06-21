using UnityEngine;

/// <summary>
/// Лёгкое «дыхание» летающего острова: покачивание по Y, медленный поворот
/// и едва заметный дрейф в стороны. Параметры рандомизируются от позиции объекта,
/// чтобы каждый остров двигался по-своему и они не качались синхронно.
/// </summary>
public class FloatingIslandDrift : MonoBehaviour
{
    [Header("Покачивание по высоте")]
    [SerializeField] private float bobAmplitude = 0.6f;
    [SerializeField] private float bobSpeed = 0.5f;

    [Header("Поворот вокруг оси Y")]
    [SerializeField] private float rotationSpeed = 2.5f;

    [Header("Горизонтальный дрейф")]
    [SerializeField] private float swayAmplitude = 0.35f;
    [SerializeField] private float swaySpeed = 0.3f;

    [Header("Разброс (рандомизация фаз)")]
    [SerializeField] private bool randomizePerInstance = true;

    private Vector3 _basePos;
    private float _phaseBob;
    private float _phaseSwayX;
    private float _phaseSwayZ;
    private float _rotDir = 1f;

    private void Start()
    {
        _basePos = transform.position;

        if (randomizePerInstance)
        {
            // Стабильный «случайный» сдвиг на основе позиции — не зависит от порядка загрузки.
            float seed = _basePos.x * 12.9898f + _basePos.z * 78.233f;
            _phaseBob   = Frac(Mathf.Sin(seed) * 43758.5453f) * Mathf.PI * 2f;
            _phaseSwayX = Frac(Mathf.Sin(seed + 1.7f) * 43758.5453f) * Mathf.PI * 2f;
            _phaseSwayZ = Frac(Mathf.Sin(seed + 3.3f) * 43758.5453f) * Mathf.PI * 2f;
            _rotDir     = Frac(Mathf.Sin(seed + 5.1f) * 43758.5453f) > 0.5f ? 1f : -1f;

            // лёгкий разброс скоростей ±20%
            float spd = 0.8f + Frac(Mathf.Sin(seed + 7.7f) * 43758.5453f) * 0.4f;
            bobSpeed  *= spd;
            swaySpeed *= spd;
        }
    }

    private void Update()
    {
        float t = Time.time;

        Vector3 offset = new Vector3(
            Mathf.Sin(t * swaySpeed + _phaseSwayX) * swayAmplitude,
            Mathf.Sin(t * bobSpeed + _phaseBob) * bobAmplitude,
            Mathf.Cos(t * swaySpeed + _phaseSwayZ) * swayAmplitude);

        transform.position = _basePos + offset;

        if (rotationSpeed != 0f)
            transform.Rotate(Vector3.up, rotationSpeed * _rotDir * Time.deltaTime, Space.World);
    }

    private static float Frac(float v) => v - Mathf.Floor(v);
}
