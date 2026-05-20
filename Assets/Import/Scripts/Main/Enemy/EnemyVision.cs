using UnityEngine;
using UnityEngine.Events;

public class EnemyVision : MonoBehaviour
{
    [Header("Настройки обнаружения")]
    [SerializeField] private float detectionRadius = 10f;
    [SerializeField] private LayerMask playerLayer = 1 << 0; // Слой игрока
    [SerializeField] private LayerMask obstacleLayer; // Слой препятствий для проверки видимости

    [Header("События")]
    public UnityEvent<Transform> OnPlayerDetected;
    public UnityEvent OnPlayerLost;

    private Transform detectedPlayer;
    private bool playerInRange = false;
    private string playerTag = "Player";

    private void Update()
    {
        CheckForPlayer();
    }

    private void CheckForPlayer()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, detectionRadius, playerLayer);

        bool foundPlayer = false;
        Transform foundPlayerTransform = null;

        foreach (Collider col in colliders)
        {
            if (col.CompareTag(playerTag))
            {
                // Проверяем видимость (нет препятствий между противником и игроком)
                if (HasLineOfSight(col.transform))
                {
                    foundPlayer = true;
                    foundPlayerTransform = col.transform;
                    break;
                }
            }
        }

        if (foundPlayer && !playerInRange)
        {
            // Игрок обнаружен
            playerInRange = true;
            detectedPlayer = foundPlayerTransform;
            OnPlayerDetected?.Invoke(detectedPlayer);
        }
        else if (!foundPlayer && playerInRange)
        {
            // Игрок потерян
            playerInRange = false;
            detectedPlayer = null;
            OnPlayerLost?.Invoke();
        }
    }

    private bool HasLineOfSight(Transform target)
    {
        Vector3 direction = target.position - transform.position;
        float distance = direction.magnitude;

        // Raycast для проверки препятствий
        if (obstacleLayer.value != 0)
        {
            RaycastHit hit;
            if (Physics.Raycast(transform.position, direction.normalized, out hit, distance, obstacleLayer))
            {
                // Если луч попал в препятствие, игрок не виден
                return false;
            }
        }

        return true;
    }

    private void OnDrawGizmosSelected()
    {
        // Визуализация области обнаружения
        Gizmos.color = playerInRange ? Color.red : Color.green;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}
