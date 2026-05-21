using UnityEngine;

/// <summary>
/// Лёгкое покачивание камеры в меню — имитация едва заметного движения головой.
/// </summary>
[DisallowMultipleComponent]
public class MenuCameraHeadSway : MonoBehaviour
{
    [Header("Смещение позиции (метры)")]
    [SerializeField] private Vector3 positionAmplitude = new Vector3(0.028f, 0.022f, 0.018f);

    [Header("Поворот (градусы)")]
    [SerializeField] private Vector3 rotationAmplitudeEuler = new Vector3(0.35f, 0.55f, 0.2f);

    [Header("Скорость")]
    [SerializeField] private float positionFrequency = 0.32f;
    [SerializeField] private float rotationFrequency = 0.26f;

    [SerializeField] private float noiseSeed;

    private Vector3 _baseLocalPosition;
    private Quaternion _baseLocalRotation;

    private void Awake()
    {
        _baseLocalPosition = transform.localPosition;
        _baseLocalRotation = transform.localRotation;

        if (noiseSeed <= 0f)
            noiseSeed = Random.Range(1f, 500f);
    }

    private void LateUpdate()
    {
        float time = Time.unscaledTime;
        float px = noiseSeed;
        float py = noiseSeed * 0.73f;
        float pz = noiseSeed * 1.17f;

        Vector3 positionOffset = new Vector3(
            SampleSigned(px + time * positionFrequency, py) * positionAmplitude.x,
            SampleSigned(py + time * positionFrequency * 1.08f, pz) * positionAmplitude.y,
            SampleSigned(pz + time * positionFrequency * 0.92f, px) * positionAmplitude.z);

        Vector3 rotationOffset = new Vector3(
            SampleSigned(px + 40f, time * rotationFrequency) * rotationAmplitudeEuler.x,
            SampleSigned(py + 80f, time * rotationFrequency * 1.05f) * rotationAmplitudeEuler.y,
            SampleSigned(pz + 120f, time * rotationFrequency * 0.95f) * rotationAmplitudeEuler.z);

        transform.localPosition = _baseLocalPosition + positionOffset;
        transform.localRotation = _baseLocalRotation * Quaternion.Euler(rotationOffset);
    }

    private void OnDisable()
    {
        transform.localPosition = _baseLocalPosition;
        transform.localRotation = _baseLocalRotation;
    }

    private static float SampleSigned(float x, float y)
    {
        return (Mathf.PerlinNoise(x, y) - 0.5f) * 2f;
    }
}
