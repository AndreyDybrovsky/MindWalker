using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

/// <summary>
/// Патруль по нескольким точкам, конусное зрение с обрезкой по стенам.
/// </summary>
[DisallowMultipleComponent]
public class PatrolConeGuardEnemy : MonoBehaviour
{
    [Header("Патруль")]
    [SerializeField] private Transform[] patrolPoints;
    [FormerlySerializedAs("patrolPointA")]
    [SerializeField] private Transform patrolPointA;
    [FormerlySerializedAs("patrolPointB")]
    [SerializeField] private Transform patrolPointB;
    [SerializeField] private bool pingPongRoute;
    [SerializeField] private float moveSpeed = 2.2f;
    [SerializeField] private float waitAtPointSeconds = 1.5f;
    [SerializeField] private float turnSpeed = 6f;

    [Header("Зрение")]
    [SerializeField] private float viewDistance = 14f;
    [SerializeField] private float viewAngle = 70f;
    [SerializeField] private float proximityRadius = 2.75f;
    [SerializeField] private float detectHoldSeconds = 0.5f;
    [SerializeField] private LayerMask obstacleLayer;
    [SerializeField] private ConeVisionVisualizer coneVisualizer;
    [SerializeField] private Transform visionOrigin;

    [Header("Обнаружение")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip spottedSound;
    [SerializeField] private bool lockPlayerOnCatch = true;

    private Transform _moveTarget;
    private int _currentPointIndex;
    private int _patrolStep = 1;
    private float _waitTimer;
    private float _visibleTimer;
    private bool _caughtTriggered;

    private void Awake()
    {
        if (visionOrigin == null)
            visionOrigin = transform;

        if (audioSource == null)
            audioSource = GetComponent<AudioSource>();

        EnsureConeVisualizer();
        ResolvePatrolPoints();
        RefreshConeVisualizer();
    }

    private void Start()
    {
        RefreshConeVisualizer();

        if (!HasValidPatrolRoute())
        {
            Debug.LogWarning(
                $"PatrolConeGuardEnemy ({name}): нужно минимум 2 точки в Patrol Points. " +
                "Tools → PTSD → Настроить префаб EnemyPTSDInDanger",
                this);
            enabled = false;
            return;
        }

        ResetToPatrolStart();
    }

    /// <summary>
    /// Сброс на первую точку патруля (после загрузки сейва — не восстанавливать transform из файла).
    /// </summary>
    public void ResetToPatrolStart()
    {
        ResolvePatrolPoints();
        if (!HasValidPatrolRoute())
            return;

        _currentPointIndex = 0;
        _patrolStep = 1;
        _waitTimer = 0f;
        _visibleTimer = 0f;
        _caughtTriggered = false;
        transform.position = patrolPoints[0].position;
        _moveTarget = patrolPoints[Mathf.Min(1, patrolPoints.Length - 1)];
        RefreshConeVisualizer();
    }

    private void Update()
    {
        if (_caughtTriggered)
            return;

        UpdatePatrol();
        UpdateDetection();
    }

    private void ResolvePatrolPoints()
    {
        if (patrolPoints != null && patrolPoints.Length >= 2)
            return;

        if (patrolPointA != null && patrolPointB != null)
            patrolPoints = new[] { patrolPointA, patrolPointB };
    }

    private bool HasValidPatrolRoute()
    {
        ResolvePatrolPoints();
        return patrolPoints != null && patrolPoints.Length >= 2 && patrolPoints[0] != null;
    }

    private void AdvancePatrolTarget()
    {
        _waitTimer = waitAtPointSeconds;

        if (!pingPongRoute)
        {
            _currentPointIndex = (_currentPointIndex + 1) % patrolPoints.Length;
        }
        else
        {
            int nextIndex = _currentPointIndex + _patrolStep;
            if (nextIndex >= patrolPoints.Length - 1)
            {
                nextIndex = patrolPoints.Length - 1;
                _patrolStep = -1;
            }
            else if (nextIndex <= 0)
            {
                nextIndex = 0;
                _patrolStep = 1;
            }

            _currentPointIndex = nextIndex;
        }

        _moveTarget = patrolPoints[_currentPointIndex];
    }

    private void UpdatePatrol()
    {
        if (_moveTarget == null)
            return;

        if (_waitTimer > 0f)
        {
            _waitTimer -= Time.deltaTime;
            return;
        }

        Vector3 toTarget = _moveTarget.position - transform.position;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.04f)
        {
            AdvancePatrolTarget();
            return;
        }

        Vector3 step = toTarget.normalized * (moveSpeed * Time.deltaTime);
        transform.position += step;

        Quaternion look = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, turnSpeed * Time.deltaTime);
    }

    private void EnsureConeVisualizer()
    {
        if (coneVisualizer == null)
            coneVisualizer = GetComponentInChildren<ConeVisionVisualizer>(true);

        if (coneVisualizer != null)
        {
            visionOrigin = coneVisualizer.transform;
            return;
        }

        GameObject visionGo = new GameObject("VisionCone");
        visionGo.transform.SetParent(transform, false);
        visionGo.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        visionGo.transform.localRotation = Quaternion.identity;
        visionGo.transform.localRotation = Quaternion.identity;

        coneVisualizer = visionGo.AddComponent<ConeVisionVisualizer>();
        visionOrigin = visionGo.transform;

        Debug.Log($"PatrolConeGuardEnemy ({name}): создан дочерний VisionCone — назначьте Patrol Points.", this);
    }

    private void RefreshConeVisualizer()
    {
        EnsureConeVisualizer();

        if (coneVisualizer == null)
        {
            Debug.LogWarning($"PatrolConeGuardEnemy ({name}): Cone Vision Visualizer не найден.", this);
            return;
        }

        Transform origin = visionOrigin != null ? visionOrigin : coneVisualizer.transform;
        coneVisualizer.Configure(viewAngle, viewDistance, origin, obstacleLayer, true, proximityRadius);
    }

    private void UpdateDetection()
    {
        Transform playerBody = PlayerInteractionZone.GetPlayerBodyTransform();
        if (playerBody == null)
        {
            _visibleTimer = 0f;
            return;
        }

        if (!IsPlayerDetected(playerBody))
        {
            _visibleTimer = 0f;
            return;
        }

        _visibleTimer += Time.deltaTime;
        if (_visibleTimer >= detectHoldSeconds)
            StartCoroutine(CatchPlayerSequence());
    }

    private bool IsPlayerDetected(Transform playerBody)
    {
        if (proximityRadius > 0.05f && IsPlayerInProximity(playerBody))
            return HasLineOfSight(playerBody);

        return IsPlayerInCone(playerBody);
    }

    private bool IsPlayerInProximity(Transform playerBody)
    {
        Vector3 toPlayer = playerBody.position - visionOrigin.position;
        toPlayer.y = 0f;
        return toPlayer.sqrMagnitude <= proximityRadius * proximityRadius;
    }

    private bool IsPlayerInCone(Transform playerBody)
    {
        Vector3 toPlayer = playerBody.position - visionOrigin.position;
        toPlayer.y = 0f;

        float distSqr = toPlayer.sqrMagnitude;
        if (distSqr > viewDistance * viewDistance || distSqr < 0.01f)
            return false;

        float angle = Vector3.Angle(ConeVisionVisualizer.GetFlatForward(visionOrigin), toPlayer.normalized);
        if (angle > viewAngle * 0.5f)
            return false;

        return HasLineOfSight(playerBody);
    }

    private bool HasLineOfSight(Transform playerBody)
    {
        Vector3 origin = visionOrigin.position + Vector3.up * 1.4f;
        Vector3 target = PlayerAimUtility.GetAimPoint(playerBody);

        Vector3 direction = target - origin;
        float distance = direction.magnitude;
        if (distance <= 0.05f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, distance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Collider col = hits[i].collider;
            if (col == null)
                continue;

            if (col.CompareTag("Player") || col.transform.root.CompareTag("Player"))
                return true;

            if (IsObstacle(col))
                return false;
        }

        return obstacleLayer.value == 0;
    }

    private bool IsObstacle(Collider col)
    {
        if (obstacleLayer.value != 0)
            return ((1 << col.gameObject.layer) & obstacleLayer.value) != 0;

        if (col.isTrigger)
            return false;

        if (col.CompareTag("Player") || col.transform.root.CompareTag("Player"))
            return false;

        return true;
    }

    private IEnumerator CatchPlayerSequence()
    {
        if (_caughtTriggered)
            yield break;

        _caughtTriggered = true;

        if (lockPlayerOnCatch)
            PlayerInteractionZone.SetPlayerControlLocked(true);

        if (spottedSound != null && audioSource != null)
            audioSource.PlayOneShot(spottedSound);

        yield return null;

        GameOverManager gameOver = FindFirstObjectByType<GameOverManager>();
        if (gameOver != null)
            gameOver.StartGameOver();
        else
            Debug.LogWarning("PatrolConeGuardEnemy: GameOverManager не найден в сцене.");
    }

    private void OnValidate()
    {
        ResolvePatrolPoints();

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            EnsureConeVisualizer();
            RefreshConeVisualizer();
        }
#endif
    }

    private void OnDrawGizmosSelected()
    {
        ResolvePatrolPoints();
        if (patrolPoints == null || patrolPoints.Length == 0)
            return;

        Gizmos.color = Color.cyan;
        for (int i = 0; i < patrolPoints.Length; i++)
        {
            if (patrolPoints[i] == null)
                continue;

            Gizmos.DrawSphere(patrolPoints[i].position, 0.2f);
        }

        if (pingPongRoute)
        {
            for (int i = 0; i < patrolPoints.Length - 1; i++)
            {
                if (patrolPoints[i] != null && patrolPoints[i + 1] != null)
                    Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[i + 1].position);
            }
        }
        else
        {
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                int next = (i + 1) % patrolPoints.Length;
                if (patrolPoints[i] != null && patrolPoints[next] != null)
                    Gizmos.DrawLine(patrolPoints[i].position, patrolPoints[next].position);
            }
        }
    }
}
