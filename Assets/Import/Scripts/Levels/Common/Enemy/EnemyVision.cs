using UnityEngine;
using UnityEngine.Events;

public class EnemyVision : MonoBehaviour
{
    [Header("Настройки обнаружения")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private LayerMask playerLayer = 1 << 0;
    [SerializeField] private LayerMask obstacleLayer;

    [Header("События")]
    public UnityEvent<Transform> OnPlayerDetected;
    public UnityEvent OnPlayerLost;

    public Transform PlayerAimTransform => _playerBody != null ? _playerBody : _playerRoot;

    private Transform _playerRoot;
    private Transform _playerBody;
    private bool _playerInRange;
    private string _playerTag = "Player";

    private void Start()
    {
        CachePlayerReferences();
    }

    private void Update()
    {
        if (_playerRoot == null)
            CachePlayerReferences();

        CheckForPlayer();
    }

    private void CachePlayerReferences()
    {
        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return;

        _playerBody = body;
        _playerRoot = body.root;
    }

    private void CheckForPlayer()
    {
        bool foundPlayer = false;
        Transform foundTransform = null;

        if (TryDetectTaggedPlayer(out Transform taggedAim))
        {
            foundPlayer = true;
            foundTransform = taggedAim;
        }
        else
        {
            Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);
            foreach (Collider col in colliders)
            {
                if (!IsPlayerCollider(col))
                    continue;

                Transform aim = GetAimTransform(col.transform);
                if (!HasLineOfSight(aim))
                    continue;

                foundPlayer = true;
                foundTransform = aim;
                break;
            }
        }

        if (foundPlayer && !_playerInRange)
        {
            _playerInRange = true;
            OnPlayerDetected?.Invoke(foundTransform);
        }
        else if (!foundPlayer && _playerInRange)
        {
            _playerInRange = false;
            OnPlayerLost?.Invoke();
        }
    }

    private bool TryDetectTaggedPlayer(out Transform aim)
    {
        aim = null;
        if (_playerBody == null)
            return false;

        if (Vector3.Distance(transform.position, _playerBody.position) > detectionRadius)
            return false;

        if (!HasLineOfSight(_playerBody))
            return false;

        aim = _playerBody;
        return true;
    }

    private static bool IsPlayerCollider(Collider col)
    {
        if (col == null)
            return false;

        if (col.CompareTag("Player"))
            return true;

        return col.transform.root.CompareTag("Player");
    }

    private static Transform GetAimTransform(Transform fromCollider)
    {
        CharacterController controller = fromCollider.GetComponentInParent<CharacterController>();
        if (controller != null)
            return controller.transform;

        return fromCollider.root;
    }

    private bool HasLineOfSight(Transform target)
    {
        Vector3 origin = transform.position + Vector3.up * 1.4f;
        Vector3 targetPosition = PlayerAimUtility.GetAimPoint(target);

        Vector3 direction = targetPosition - origin;
        float distance = direction.magnitude;
        if (distance <= 0.05f)
            return true;

        RaycastHit[] hits = Physics.RaycastAll(origin, direction.normalized, distance);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTransform = hits[i].collider.transform;
            if (IsPlayerCollider(hits[i].collider))
                return true;

            if (IsObstacle(hits[i].collider))
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

        if (IsPlayerCollider(col))
            return false;

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = _playerInRange ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
