using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    [Header("Компоненты")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform patrolCenter;
    [SerializeField] private float patrolRadius = 10f;
    [SerializeField] private float minPatrolDistance = 2f;
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas; // Маска областей NavMesh для Unity 6

    [Header("Настройки патрулирования")]
    [SerializeField] private float patrolWaitTime = 2f;
    [SerializeField] private float patrolSpeed = 3f;

    [Header("Настройки преследования")]
    [SerializeField] private float chaseSpeed = 5f;
    [SerializeField] private float stoppingDistance = 2f;
    [SerializeField] private float rotationSpeed = 5f; // Скорость поворота к цели

    [Header("Область обнаружения")]
    [SerializeField] private EnemyVision vision;

    [Header("Стрельба")]
    [SerializeField] private EnemyShooting shooting;

    private Vector3 currentPatrolTarget;
    private bool isPatrolling = true;
    private bool isChasing = false;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private Transform playerTarget;
    private bool isAgentReady = false;

    private void Awake()
    {
        if (agent == null)
            agent = GetComponent<NavMeshAgent>();

        if (agent == null)
            agent = GetComponentInParent<NavMeshAgent>();

        DisableConflictingRigidbodies();

        if (patrolCenter == null)
            patrolCenter = transform;

        if (vision == null)
            vision = GetComponentInChildren<EnemyVision>();

        if (shooting == null)
            shooting = GetComponent<EnemyShooting>();
    }

    private void Start()
    {
        if (agent == null)
        {
            Debug.LogError("EnemyController: NavMeshAgent не найден!");
            enabled = false;
            return;
        }

        agent.speed = patrolSpeed;
        agent.stoppingDistance = stoppingDistance;
        agent.areaMask = navMeshAreaMask; // Устанавливаем маску областей для Unity 6
        
        // Небольшая задержка для инициализации NavMesh в Unity 6
        StartCoroutine(DelayedInitialization());
        
        if (vision != null)
        {
            vision.OnPlayerDetected.AddListener(StartChase);
            vision.OnPlayerLost.AddListener(StopChase);
        }
    }

    private System.Collections.IEnumerator DelayedInitialization()
    {
        // Ждем один кадр для полной инициализации NavMesh в Unity 6
        yield return null;
        
        // Проверяем, что агент находится на NavMesh
        CheckAgentOnNavMesh();
        
        if (isAgentReady)
        {
            SetNewPatrolTarget();
        }
        else
        {
            // Пытаемся еще раз через небольшую задержку
            yield return new WaitForSeconds(0.1f);
            CheckAgentOnNavMesh();
            if (isAgentReady)
            {
                SetNewPatrolTarget();
            }
        }
    }

    private void Update()
    {
        // Проверяем готовность агента каждый кадр (на случай, если он упал с NavMesh)
        if (!isAgentReady)
        {
            CheckAgentOnNavMesh();
            return;
        }

        if (isChasing && playerTarget != null)
        {
            ChasePlayer();
        }
        else if (isPatrolling)
        {
            Patrol();
        }
    }

    private void Patrol()
    {
        if (isWaiting)
        {
            waitTimer -= Time.deltaTime;
            if (waitTimer <= 0f)
            {
                isWaiting = false;
                SetNewPatrolTarget();
            }
            return;
        }

        // Улучшенная проверка достижения цели для патрулирования
        if (agent.hasPath && !agent.pathPending)
        {
            float distanceToTarget = Vector3.Distance(transform.position, currentPatrolTarget);
            
            // Проверяем, достиг ли агент цели или близок к ней
            if (distanceToTarget < stoppingDistance + 0.5f || 
                (agent.remainingDistance < stoppingDistance + 0.5f && agent.remainingDistance != Mathf.Infinity))
            {
                isWaiting = true;
                waitTimer = patrolWaitTime;
            }
        }
        else if (!agent.pathPending && !agent.hasPath)
        {
            // Если путь не найден, пытаемся установить новую цель
            SetNewPatrolTarget();
        }
    }

    private void SetNewPatrolTarget()
    {
        int attempts = 0;
        const int maxAttempts = 10;
        
        while (attempts < maxAttempts)
        {
            Vector3 randomDirection = Random.insideUnitSphere * patrolRadius;
            randomDirection.y = 0f; // Ограничиваем движение по горизонтали
            
            Vector3 targetPosition = patrolCenter.position + randomDirection;
            
            // Проверяем, что цель находится в пределах полусферы
            float distanceFromCenter = Vector3.Distance(targetPosition, patrolCenter.position);
            if (distanceFromCenter > patrolRadius)
            {
                targetPosition = patrolCenter.position + (targetPosition - patrolCenter.position).normalized * patrolRadius;
            }

            // Проверяем минимальное расстояние от текущей позиции
            float distanceToTarget = Vector3.Distance(targetPosition, transform.position);
            if (distanceToTarget >= minPatrolDistance)
            {
                // Проверяем, что точка доступна на NavMesh
                NavMeshHit hit;
                if (NavMesh.SamplePosition(targetPosition, out hit, 2f, navMeshAreaMask))
                {
                    currentPatrolTarget = hit.position;
                    SafeSetDestination(hit.position);
                    return;
                }
            }
            
            attempts++;
        }
        
        // Если не удалось найти подходящую точку, используем случайную точку рядом с центром
        Debug.LogWarning($"EnemyController: Не удалось найти подходящую точку патрулирования после {maxAttempts} попыток");
        Vector3 fallbackPosition = patrolCenter.position + Random.insideUnitSphere * (patrolRadius * 0.5f);
        fallbackPosition.y = transform.position.y;
        currentPatrolTarget = fallbackPosition;
        SafeSetDestination(fallbackPosition);
    }

    private void StartChase(Transform player)
    {
        playerTarget = player;
        isChasing = true;
        isPatrolling = false;
        isWaiting = false;
        agent.speed = chaseSpeed;
        
        if (shooting != null)
        {
            shooting.SetTarget(player);
            Debug.Log($"EnemyController: Начато преследование игрока {player.name}, стрельба активирована");
        }
        else
        {
            Debug.LogWarning($"EnemyController: Компонент EnemyShooting не найден на {gameObject.name}!");
        }
    }

    private void StopChase()
    {
        playerTarget = null;
        isChasing = false;
        isPatrolling = true;
        agent.speed = patrolSpeed;
        
        if (shooting != null)
            shooting.SetTarget(null);
        
        SetNewPatrolTarget();
    }

    private void ChasePlayer()
    {
        if (playerTarget == null) return;

        // Поворачиваем противника к игроку для стрельбы
        Vector3 directionToPlayer = (playerTarget.position - transform.position);
        directionToPlayer.y = 0f; // Ограничиваем поворот по горизонтали
        
        if (directionToPlayer != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(directionToPlayer);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // Ограничиваем движение в пределах полусферы патрулирования
        Vector3 targetPosition = playerTarget.position;
        float distanceFromCenter = Vector3.Distance(targetPosition, patrolCenter.position);
        
        if (distanceFromCenter > patrolRadius)
        {
            // Если игрок вне зоны, двигаемся к границе зоны
            Vector3 directionToBoundary = (targetPosition - patrolCenter.position).normalized;
            targetPosition = patrolCenter.position + directionToBoundary * patrolRadius;
        }

        SafeSetDestination(targetPosition);
    }

    private void CheckAgentOnNavMesh()
    {
        if (agent == null || !agent.isActiveAndEnabled)
        {
            isAgentReady = false;
            return;
        }

        // В Unity 6 используем улучшенную проверку
        // Сначала проверяем через isOnNavMesh
        if (agent.isOnNavMesh)
        {
            isAgentReady = true;
            return;
        }

        // Если агент не на NavMesh, пытаемся найти ближайшую точку
        NavMeshHit hit;
        float searchRadius = 5f; // Увеличиваем радиус поиска для Unity 6
        
        if (NavMesh.SamplePosition(transform.position, out hit, searchRadius, navMeshAreaMask))
        {
            isAgentReady = true;
            // Перемещаем агента на NavMesh
            if (agent.Warp(hit.position))
            {
                Debug.Log($"EnemyController: Агент {gameObject.name} перемещен на NavMesh");
            }
            else
            {
                // Если Warp не сработал, пробуем через enabled
                agent.enabled = false;
                transform.position = hit.position;
                agent.enabled = true;
            }
        }
        else
        {
            isAgentReady = false;
            Debug.LogWarning($"EnemyController: Агент {gameObject.name} не находится на NavMesh! Убедитесь, что NavMeshSurface запечен.");
        }
    }

    private void SafeSetDestination(Vector3 target)
    {
        if (!isAgentReady || agent == null || !agent.isActiveAndEnabled)
            return;

        // В Unity 6 проверяем состояние агента более тщательно
        if (!agent.isOnNavMesh)
        {
            CheckAgentOnNavMesh();
            if (!isAgentReady)
                return;
        }

        // Проверяем, что целевая точка находится на NavMesh (для Unity 6)
        NavMeshHit hit;
        float searchRadius = Mathf.Max(patrolRadius, 5f);
        
        if (NavMesh.SamplePosition(target, out hit, searchRadius, navMeshAreaMask))
        {
            // Убеждаемся, что агент все еще на NavMesh перед установкой цели
            if (agent.isOnNavMesh)
            {
                agent.SetDestination(hit.position);
            }
            else
            {
                // Если агент потерял NavMesh, пытаемся восстановить
                CheckAgentOnNavMesh();
            }
        }
        else
        {
            // Если точка недоступна, пытаемся найти ближайшую доступную точку
            if (NavMesh.FindClosestEdge(target, out hit, navMeshAreaMask))
            {
                if (agent.isOnNavMesh)
                {
                    agent.SetDestination(hit.position);
                }
            }
            else
            {
                // Если ничего не найдено, просто пытаемся установить цель напрямую
                // (в Unity 6 это может работать, если агент на NavMesh)
                if (agent.isOnNavMesh)
                {
                    try
                    {
                        agent.SetDestination(target);
                    }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning($"EnemyController: Не удалось установить цель: {e.Message}");
                    }
                }
            }
        }
    }

    private void DisableConflictingRigidbodies()
    {
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>(true);
        for (int i = 0; i < bodies.Length; i++)
        {
            Rigidbody rb = bodies[i];
            if (rb == null)
                continue;

            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    private void OnDrawGizmosSelected()
    {
        // Визуализация зоны патрулирования
        if (patrolCenter != null)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(patrolCenter.position, patrolRadius);
        }
    }
}
