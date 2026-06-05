using UnityEngine;
using UnityEngine.AI;

public class EnemyController : MonoBehaviour
{
    [Header("Компоненты")]
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private Transform patrolCenter;
    [SerializeField] private float patrolRadius = 10f;
    [SerializeField] private float minPatrolDistance = 2f;
    [Tooltip("Для патруля в узких коридорах — меньшая дистанция между точками.")]
    [SerializeField] private float narrowCorridorMinPatrolDistance = 0.75f;
    [SerializeField] private int navMeshAreaMask = NavMesh.AllAreas; // Маска областей NavMesh для Unity 6
    [Tooltip("Макс. дистанция поиска точки на NavMesh (большие значения «перекидывают» врага за стены на другой остров).")]
    [SerializeField] private float navMeshSampleRadius = 2f;
    [Tooltip("Макс. дистанция возврата на NavMesh, если агент сошёл с сетки.")]
    [SerializeField] private float navMeshRecoveryRadius = 2f;

    [Header("Коллизии зданий")]
    [Tooltip("Слои стен/домов. 0 = любой не-триггер (кроме игрока и врагов); иначе маска из EnemyVision.")]
    [SerializeField] private LayerMask buildingObstacleLayers;
    [SerializeField] private float wallCheckRadius = 0.42f;
    [SerializeField] private float wallCheckHeight = 1.65f;
    [SerializeField] private float wallProbeDistance = 0.85f;
    [SerializeField] private float wallBlockCooldown = 0.45f;
    [Tooltip("Ложные срабатывания на перепадах высот / склонах.")]
    [SerializeField] private bool blockMovementByBuildingWalls = true;

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

    [Header("Звук (SFX)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip patrolSound;
    [SerializeField] private AudioClip chaseSound;
    [SerializeField] private AudioClip deathSound;
    [SerializeField, Range(0f, 1f)] private float patrolSoundVolume = 0.35f;
    [SerializeField, Range(0f, 1f)] private float chaseSoundVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float deathSoundVolume = 0.85f;

    public AudioClip DeathSound => deathSound;
    public float DeathSoundVolume => deathSoundVolume;

    private Vector3 currentPatrolTarget;
    private bool isPatrolling = true;
    private bool isChasing = false;
    private bool isWaiting = false;
    private float waitTimer = 0f;
    private Transform playerTarget;
    private bool isAgentReady = false;
    private float _chaseDestinationTimer;
    private const float ChaseDestinationInterval = 0.25f;
    private NavMeshPath _navPathBuffer;
    private EnemyHealth _health;
    private AudioClip _activeLoopClip;
    private bool _isDead;
    private float _wallBlockCooldownTimer;
    private Collider[] _selfColliders;
    private float _lastPatrolWarnTime = -10f;
    private float _nextPatrolRetargetTime;
    private float _chaseStuckTimer;
    [SerializeField] private float chaseUnstickDelay = 1.1f;

    private void Awake()
    {
        _navPathBuffer = new NavMeshPath();

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

        ResolveDefaultSounds();
        EnsureAudioSource();
        CacheSelfColliders();
        ResolveBuildingObstacleLayers();
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
        agent.areaMask = navMeshAreaMask;
        agent.autoTraverseOffMeshLink = true;
        agent.autoBraking = true;
        agent.autoRepath = true;
        agent.updatePosition = true;
        agent.updateRotation = true;
        agent.acceleration = Mathf.Max(patrolSpeed * 3.5f, 6f);
        agent.angularSpeed = Mathf.Min(agent.angularSpeed, 180f);
        agent.obstacleAvoidanceType = ObstacleAvoidanceType.LowQualityObstacleAvoidance;

        TryGetComponent(out _health);
        if (_health != null)
            _health.OnEnemyDeath.AddListener(OnEnemyDied);
        
        // Небольшая задержка для инициализации NavMesh в Unity 6
        StartCoroutine(DelayedInitialization());
        
        if (vision != null)
        {
            vision.OnPlayerDetected.AddListener(StartChase);
            vision.OnPlayerLost.AddListener(StopChase);
        }
    }

    private void OnDestroy()
    {
        if (_health != null)
            _health.OnEnemyDeath.RemoveListener(OnEnemyDied);
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
            UpdateChaseUnstick();
        }
        else if (isPatrolling)
        {
            Patrol();
        }

        if (isChasing && blockMovementByBuildingWalls)
            EnforceBuildingCollision();

        UpdateBehaviorAudio();
    }

    private void UpdateChaseUnstick()
    {
        if (agent == null || !agent.isOnNavMesh || !agent.hasPath)
        {
            _chaseStuckTimer = 0f;
            return;
        }

        bool stuck = agent.velocity.sqrMagnitude < 0.04f
            && agent.remainingDistance > stoppingDistance + 0.35f;

        if (!stuck)
        {
            _chaseStuckTimer = 0f;
            return;
        }

        _chaseStuckTimer += Time.deltaTime;
        if (_chaseStuckTimer < chaseUnstickDelay)
            return;

        _chaseStuckTimer = 0f;
        Vector3 toTarget = playerTarget.position - transform.position;
        toTarget.y = 0f;
        if (toTarget.sqrMagnitude < 0.01f)
            return;

        Vector3 step = transform.position + toTarget.normalized * 1.25f;
        if (NavMesh.SamplePosition(step, out NavMeshHit hit, navMeshRecoveryRadius * 2f, navMeshAreaMask))
            agent.Warp(hit.position);

        _chaseDestinationTimer = 0f;
        SafeSetDestination(playerTarget.position);
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
        else if (!agent.pathPending && (!agent.hasPath || agent.pathStatus == NavMeshPathStatus.PathInvalid))
        {
            if (Time.time >= _nextPatrolRetargetTime)
                SetNewPatrolTarget();
        }
    }

    private void SetNewPatrolTarget()
    {
        _nextPatrolRetargetTime = Time.time + 1.25f;
        const int maxAttempts = 12;
        float effectiveMinDistance = Mathf.Min(minPatrolDistance, narrowCorridorMinPatrolDistance);

        for (int ring = 0; ring < 3; ring++)
        {
            float ringRadius = Mathf.Max(effectiveMinDistance, patrolRadius * (0.35f + ring * 0.25f));

            for (int attempts = 0; attempts < maxAttempts; attempts++)
            {
                Vector3 anchor = ring == 0 ? transform.position : patrolCenter.position;
                Vector3 randomDirection = Random.insideUnitSphere * ringRadius;
                randomDirection.y = 0f;

                Vector3 targetPosition = anchor + randomDirection;
                if (patrolCenter != null && ring > 0)
                {
                    float distanceFromCenter = Vector3.Distance(targetPosition, patrolCenter.position);
                    if (distanceFromCenter > patrolRadius)
                    {
                        targetPosition = patrolCenter.position
                            + (targetPosition - patrolCenter.position).normalized * patrolRadius;
                    }
                }

                float distanceToTarget = Vector3.Distance(targetPosition, transform.position);
                if (distanceToTarget < effectiveMinDistance)
                    continue;

                if (TryResolvePatrolDestination(targetPosition, out Vector3 resolved))
                {
                    currentPatrolTarget = resolved;
                    SafeSetDestination(resolved);
                    return;
                }
            }
        }

        if (Time.time - _lastPatrolWarnTime > 8f)
        {
            _lastPatrolWarnTime = Time.time;
            Debug.LogWarning(
                $"EnemyController ({name}): не найдена точка патруля — остаётся на месте.",
                this);
        }

        isWaiting = true;
        waitTimer = Mathf.Max(waitTimer, patrolWaitTime * 0.35f);
        agent.ResetPath();
    }

    private bool TryResolvePatrolDestination(Vector3 desiredWorldPosition, out Vector3 resolved)
    {
        if (TryResolveNavMeshDestination(desiredWorldPosition, out resolved, allowPartialPath: true))
            return true;

        if (NavMesh.SamplePosition(desiredWorldPosition, out NavMeshHit hit, navMeshSampleRadius, navMeshAreaMask)
            && IsWithinPatrolHorizon(hit.position))
        {
            resolved = hit.position;
            return true;
        }

        resolved = desiredWorldPosition;
        return false;
    }

    private void StartChase(Transform player)
    {
        playerTarget = player;
        isChasing = true;
        isPatrolling = false;
        isWaiting = false;
        agent.speed = chaseSpeed;
        
        if (shooting != null)
            shooting.SetTarget(player);
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

        _chaseDestinationTimer -= Time.deltaTime;

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

        if (_wallBlockCooldownTimer > 0f)
            return;

        if (_chaseDestinationTimer > 0f)
            return;

        _chaseDestinationTimer = ChaseDestinationInterval;
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
        float searchRadius = Mathf.Max(0.5f, navMeshRecoveryRadius);

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

        if (!agent.isOnNavMesh)
        {
            CheckAgentOnNavMesh();
            if (!isAgentReady)
                return;
        }

        // allowPartialPath: true — принимаем частичные пути, иначе в городских сценах с фрагментированным NavMesh
        // враг никогда не получает цель (PathPartial отвергается при isChasing=false)
        if (!TryResolveNavMeshDestination(target, out Vector3 resolved, allowPartialPath: true))
            return;

        if (agent.isOnNavMesh)
            agent.SetDestination(resolved);
    }

    private bool TryResolveNavMeshDestination(Vector3 desiredWorldPosition, out Vector3 resolved, bool allowPartialPath)
    {
        resolved = desiredWorldPosition;

        if (agent == null || !agent.isActiveAndEnabled)
            return false;

        float sampleRadius = Mathf.Max(0.5f, navMeshSampleRadius);
        if (!NavMesh.SamplePosition(desiredWorldPosition, out NavMeshHit sampleHit, sampleRadius, navMeshAreaMask))
            return false;

        resolved = sampleHit.position;

        if (!IsWithinPatrolHorizon(resolved))
            return false;

        Vector3 from = agent.isOnNavMesh ? agent.transform.position : transform.position;
        bool skipBuildingChecks = !isChasing;
        if (!HasReachableNavPath(from, resolved, allowPartialPath, skipBuildingChecks))
            return false;

        if (!skipBuildingChecks && IsSegmentBlockedByBuildings(from, resolved))
            return false;

        return true;
    }

    private void ResolveBuildingObstacleLayers()
    {
        if (buildingObstacleLayers.value != 0)
            return;

        if (vision != null && vision.ObstacleLayers.value != 0)
            buildingObstacleLayers = vision.ObstacleLayers;
    }

    private void CacheSelfColliders()
    {
        _selfColliders = GetComponentsInChildren<Collider>(true);
    }

    private void EnforceBuildingCollision()
    {
        if (agent == null || !agent.isOnNavMesh || !agent.hasPath)
            return;

        if (_wallBlockCooldownTimer > 0f)
        {
            _wallBlockCooldownTimer -= Time.deltaTime;
            return;
        }

        Vector3 moveDirection = agent.desiredVelocity;
        moveDirection.y = 0f;
        if (moveDirection.sqrMagnitude < 0.01f)
        {
            Vector3 toSteering = agent.steeringTarget - transform.position;
            toSteering.y = 0f;
            moveDirection = toSteering;
        }

        if (moveDirection.sqrMagnitude < 0.01f)
            return;

        moveDirection.Normalize();
        float probeDistance = Mathf.Max(wallProbeDistance, agent.speed * Time.deltaTime * 2.5f);

        if (!TryProbeBuildingWall(transform.position, moveDirection, probeDistance, out RaycastHit hit))
            return;

        HandleBlockedByBuilding(hit.normal);
    }

    private void HandleBlockedByBuilding(Vector3 wallNormal)
    {
        if (agent != null && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
            agent.isStopped = false;
        }

        Vector3 pushNormal = wallNormal;
        pushNormal.y = 0f;
        if (pushNormal.sqrMagnitude > 0.01f)
        {
            Vector3 pushFromWall = transform.position + pushNormal.normalized * 0.2f;
            if (NavMesh.SamplePosition(pushFromWall, out NavMeshHit navHit, navMeshRecoveryRadius, navMeshAreaMask))
                agent.Warp(navHit.position);
        }

        _wallBlockCooldownTimer = wallBlockCooldown;

        if (isPatrolling)
        {
            isWaiting = true;
            waitTimer = Mathf.Max(waitTimer, patrolWaitTime * 0.5f);
        }
        else if (isChasing)
        {
            _chaseDestinationTimer = ChaseDestinationInterval;
        }
    }

    private bool HasReachableNavPath(Vector3 from, Vector3 to, bool allowPartialPath, bool skipBuildingChecks = false)
    {
        if (_navPathBuffer == null)
            return false;

        if (!NavMesh.CalculatePath(from, to, navMeshAreaMask, _navPathBuffer))
            return false;

        bool pathOk = _navPathBuffer.status == NavMeshPathStatus.PathComplete;
        if (!pathOk)
        {
            pathOk = allowPartialPath
                     && _navPathBuffer.status == NavMeshPathStatus.PathPartial
                     && _navPathBuffer.corners != null
                     && _navPathBuffer.corners.Length >= 2;
        }

        if (!pathOk)
            return false;

        if (skipBuildingChecks)
            return true;

        return !IsNavPathBlockedByBuildings(_navPathBuffer);
    }

    private bool IsNavPathBlockedByBuildings(NavMeshPath path)
    {
        if (path == null || path.corners == null || path.corners.Length < 2)
            return false;

        for (int i = 0; i < path.corners.Length - 1; i++)
        {
            if (IsSegmentBlockedByBuildings(path.corners[i], path.corners[i + 1]))
                return true;
        }

        return false;
    }

    private bool IsSegmentBlockedByBuildings(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        delta.y = 0f;
        float distance = delta.magnitude;
        if (distance < 0.05f)
            return false;

        Vector3 direction = delta / distance;
        return TryProbeBuildingWall(from, direction, distance, out _);
    }

    private bool TryProbeBuildingWall(Vector3 feetPosition, Vector3 direction, float distance, out RaycastHit hit)
    {
        hit = default;
        if (distance <= 0.01f)
            return false;

        float radius = Mathf.Max(0.15f, wallCheckRadius);
        float height = Mathf.Max(radius * 2f, wallCheckHeight);
        Vector3 bottom = feetPosition + Vector3.up * radius;
        Vector3 top = feetPosition + Vector3.up * (height - radius);

        RaycastHit[] hits = Physics.CapsuleCastAll(
            bottom,
            top,
            radius,
            direction.normalized,
            distance,
            buildingObstacleLayers.value != 0 ? buildingObstacleLayers : Physics.AllLayers,
            QueryTriggerInteraction.Ignore);

        float closest = float.MaxValue;
        bool blocked = false;

        for (int i = 0; i < hits.Length; i++)
        {
            if (!IsBuildingObstacle(hits[i].collider))
                continue;

            if (hits[i].distance < closest)
            {
                closest = hits[i].distance;
                hit = hits[i];
                blocked = true;
            }
        }

        return blocked;
    }

    private bool IsBuildingObstacle(Collider col)
    {
        if (col == null || col.isTrigger)
            return false;

        if (IsSelfCollider(col))
            return false;

        if (col.CompareTag("Player") || col.transform.root.CompareTag("Player"))
            return false;

        if (col.CompareTag("Enemy") || col.CompareTag("EnemyBullet"))
            return false;

        if (buildingObstacleLayers.value != 0)
            return ((1 << col.gameObject.layer) & buildingObstacleLayers.value) != 0;

        return true;
    }

    private bool IsSelfCollider(Collider col)
    {
        if (col == null || _selfColliders == null)
            return false;

        for (int i = 0; i < _selfColliders.Length; i++)
        {
            if (_selfColliders[i] == col)
                return true;
        }

        return col.transform.IsChildOf(transform) || col.transform.root == transform.root;
    }

    private bool IsWithinPatrolHorizon(Vector3 worldPosition)
    {
        if (patrolCenter == null)
            return true;

        Vector3 flat = worldPosition - patrolCenter.position;
        flat.y = 0f;
        return flat.sqrMagnitude <= patrolRadius * patrolRadius + 0.25f;
    }

    private void ResolveDefaultSounds()
    {
        if (patrolSound == null)
            patrolSound = Resources.Load<AudioClip>("Sounds/Other/Ping");
        if (chaseSound == null)
            chaseSound = Resources.Load<AudioClip>("Sounds/Other/LongPing");
        if (deathSound == null)
            deathSound = Resources.Load<AudioClip>("Sounds/Ludomania/fail");
    }

    private void EnsureAudioSource()
    {
        if (audioSource == null)
            TryGetComponent(out audioSource);

        if (audioSource == null)
            audioSource = gameObject.AddComponent<AudioSource>();

        audioSource.playOnAwake = false;
        audioSource.loop = true;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 22f;
        AudioMixerRoutingUtility.BindSourceToSfx(audioSource);
        ApplySfxVolume();
    }

    private void OnSettingsApplied()
    {
        ApplySfxVolume();
    }

    private void ApplySfxVolume()
    {
        if (audioSource == null)
            return;

        float sfx = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().sfxVolume
            : 1f;

        audioSource.mute = sfx <= 0.001f;
        if (_activeLoopClip == patrolSound)
            audioSource.volume = patrolSoundVolume * Mathf.Clamp01(sfx);
        else if (_activeLoopClip == chaseSound)
            audioSource.volume = chaseSoundVolume * Mathf.Clamp01(sfx);
    }

    private void UpdateBehaviorAudio()
    {
        if (_isDead || audioSource == null)
            return;

        if (isChasing)
        {
            PlayLoop(chaseSound, chaseSoundVolume);
            return;
        }

        bool isMovingPatrol = isPatrolling
                              && !isWaiting
                              && agent != null
                              && agent.velocity.sqrMagnitude > 0.04f;

        if (isMovingPatrol)
            PlayLoop(patrolSound, patrolSoundVolume);
        else
            StopLoop();
    }

    private void PlayLoop(AudioClip clip, float volume)
    {
        if (clip == null || audioSource == null)
            return;

        ApplySfxVolume();

        if (_activeLoopClip == clip && audioSource.isPlaying)
            return;

        _activeLoopClip = clip;
        float sfx = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().sfxVolume
            : 1f;

        audioSource.clip = clip;
        audioSource.volume = volume * Mathf.Clamp01(sfx);
        audioSource.loop = true;
        audioSource.mute = sfx <= 0.001f;
        audioSource.Play();
    }

    private void StopLoop()
    {
        _activeLoopClip = null;
        if (audioSource != null && audioSource.isPlaying && audioSource.loop)
            audioSource.Stop();
    }

    private void OnEnemyDied()
    {
        _isDead = true;
        StopLoop();
    }

    private void OnEnable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsApplied += OnSettingsApplied;
    }

    private void OnDisable()
    {
        if (SettingsManager.Instance != null)
            SettingsManager.Instance.OnSettingsApplied -= OnSettingsApplied;
        StopLoop();
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
