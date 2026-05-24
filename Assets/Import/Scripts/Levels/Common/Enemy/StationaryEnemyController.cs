using UnityEngine;

[DisallowMultipleComponent]
public class StationaryEnemyController : MonoBehaviour
{
    [Header("Компоненты")]
    [SerializeField] private EnemyVision vision;
    [SerializeField] private EnemyShooting shooting;
    [SerializeField] private Transform rotationPivot;

    [Header("Прицеливание")]
    [SerializeField] private float rotationSpeed = 8f;
    [SerializeField] private bool rotateOnlyOnYAxis = true;

    private Transform _aimTarget;
    private bool _isEngaging;

    private void Awake()
    {
        if (vision == null)
            vision = GetComponentInChildren<EnemyVision>();

        if (shooting == null)
            shooting = ResolveShootingComponent();

        if (rotationPivot == null)
        {
            if (shooting != null && shooting.transform != transform)
                rotationPivot = shooting.transform;
            else
                rotationPivot = transform;
        }
    }

    private void OnEnable()
    {
        if (vision == null)
            return;

        vision.OnPlayerDetected.AddListener(OnPlayerDetected);
        vision.OnPlayerLost.AddListener(OnPlayerLost);
    }

    private void OnDisable()
    {
        if (vision == null)
            return;

        vision.OnPlayerDetected.RemoveListener(OnPlayerDetected);
        vision.OnPlayerLost.RemoveListener(OnPlayerLost);
        StopEngage();
    }

    private void Update()
    {
        if (!_isEngaging)
            return;

        if (vision != null && vision.PlayerAimTransform != null)
            _aimTarget = vision.PlayerAimTransform;

        if (_aimTarget == null)
            return;

        RotateTowardTarget(PlayerAimUtility.GetAimPoint(_aimTarget));

        if (shooting != null)
            shooting.SetTarget(_aimTarget);
    }

    private EnemyShooting ResolveShootingComponent()
    {
        EnemyShooting[] shootings = GetComponentsInChildren<EnemyShooting>(true);
        if (shootings == null || shootings.Length == 0)
            return null;

        for (int i = 0; i < shootings.Length; i++)
        {
            if (shootings[i].transform != transform)
                return shootings[i];
        }

        return shootings[0];
    }

    private void OnPlayerDetected(Transform player)
    {
        if (player == null)
            return;

        _aimTarget = vision != null && vision.PlayerAimTransform != null ? vision.PlayerAimTransform : player;
        _isEngaging = true;
    }

    private void OnPlayerLost()
    {
        StopEngage();
    }

    private void StopEngage()
    {
        _isEngaging = false;
        _aimTarget = null;

        if (shooting != null)
            shooting.SetTarget(null);
    }

    private void RotateTowardTarget(Vector3 targetPosition)
    {
        Vector3 direction = targetPosition - rotationPivot.position;
        if (rotateOnlyOnYAxis)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction.normalized);
        rotationPivot.rotation = Quaternion.Slerp(rotationPivot.rotation, targetRotation, rotationSpeed * Time.deltaTime);
    }
}
