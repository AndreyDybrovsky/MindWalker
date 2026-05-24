using UnityEngine;

/// <summary>
/// Камера под углом (изометрия ~55°) с плавным следованием за персонажем.
/// </summary>
[DefaultExecutionOrder(100)]
public class TopDownCameraFollow : MonoBehaviour
{
    [Header("Цель")]
    [SerializeField] private Transform target;
    [SerializeField] private float focusHeight = 1.15f;

    [Header("Ракурс (как в изометрии / twin-stick)")]
    [Tooltip("Угол наклона вниз от горизонта. 50–60° — типичный диапазон.")]
    [SerializeField] private float pitchAngle = 55f;
    [SerializeField] private float yawDegrees;
    [Tooltip("Дистанция от точки фокуса до камеры по направлению взгляда.")]
    [SerializeField] private float followDistance = 14f;

    [Header("Плавность")]
    [SerializeField] private float positionSmoothTime = 0.48f;
    [SerializeField] private float maxFollowSpeed = 28f;
    [SerializeField] private float rotationSmoothSpeed = 9f;
    [SerializeField] private bool useUnscaledTime = true;

    [Header("Проекция")]
    [SerializeField] private bool useOrthographic;
    [SerializeField] private float orthographicSize = 8f;

    private Vector3 _positionVelocity;
    private Camera _camera;

    public Transform Target
    {
        get => target;
        set => target = value;
    }

    private float DeltaTime => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

    private void Awake()
    {
        _camera = GetComponent<Camera>();
        if (_camera == null)
            _camera = gameObject.AddComponent<Camera>();

        _camera.orthographic = useOrthographic;
        if (useOrthographic)
            _camera.orthographicSize = orthographicSize;
    }

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector3 focusPoint = target.position + Vector3.up * focusHeight;
        Quaternion orbit = Quaternion.Euler(pitchAngle, yawDegrees, 0f);
        Vector3 desiredPosition = focusPoint + orbit * (Vector3.back * followDistance);

        float dt = Mathf.Max(DeltaTime, 0.0001f);
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref _positionVelocity,
            positionSmoothTime,
            maxFollowSpeed,
            dt);

        Vector3 lookDirection = focusPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
        {
            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection, Vector3.up);
            float rotationT = 1f - Mathf.Exp(-rotationSmoothSpeed * dt);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationT);
        }
    }

    public void ApplySceneDefaults()
    {
        focusHeight = 1.15f;
        pitchAngle = 55f;
        yawDegrees = 0f;
        followDistance = 14f;
        positionSmoothTime = 0.48f;
        maxFollowSpeed = 28f;
        rotationSmoothSpeed = 9f;
        useOrthographic = false;
        useUnscaledTime = true;
    }

    /// <summary>
    /// Ставит камеру сразу в целевую позу (без рывка на первом кадре после телепорта).
    /// </summary>
    public void SnapToTarget()
    {
        if (target == null)
            return;

        Vector3 focusPoint = target.position + Vector3.up * focusHeight;
        Quaternion orbit = Quaternion.Euler(pitchAngle, yawDegrees, 0f);
        transform.position = focusPoint + orbit * (Vector3.back * followDistance);

        Vector3 lookDirection = focusPoint - transform.position;
        if (lookDirection.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(lookDirection, Vector3.up);

        _positionVelocity = Vector3.zero;
    }
}
