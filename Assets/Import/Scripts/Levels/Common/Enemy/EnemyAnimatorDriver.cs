using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Драйвер анимаций врага. Связывает ИИ-поведение (NavMeshAgent + EnemyShooting /
/// PeriodicProximityDamageEnemy + EnemyHealth) с Animator модели.
///
/// Движением управляет NavMeshAgent, поэтому root motion выключен — анимация лишь
/// проигрывает цикл «на месте». Скрипт сам находит компоненты и подписывается на их события,
/// поэтому достаточно повесить его на корень врага (рядом с EnemyController) и назначить
/// контроллер аниматора с нужными параметрами.
///
/// Параметры контроллера аниматора:
///   Speed  (float 0..1) — нормализованная горизонтальная скорость (0 = стоит, 1 = бег)
///   Shoot  (trigger)    — выстрел (стреляющие враги)
///   Attack (trigger)    — удар в ближнем бою (Homeless / мелее-враги)
///   Hit    (trigger)    — реакция на получение урона
///   Die    (trigger)    — смерть
/// Любого параметра может не быть в контроллере — драйвер это учитывает и не падает.
/// </summary>
[DisallowMultipleComponent]
[AddComponentMenu("Enemies/Enemy Animator Driver")]
public class EnemyAnimatorDriver : MonoBehaviour
{
    [Header("Ссылки (заполняются автоматически)")]
    [SerializeField] private Animator animator;
    [SerializeField] private NavMeshAgent agent;
    [SerializeField] private EnemyShooting shooting;
    [SerializeField] private PeriodicProximityDamageEnemy meleeAttacker;
    [SerializeField] private EnemyHealth health;

    [Header("Локомоция")]
    [Tooltip("Скорость, которой соответствует бег (Speed = 1). Меньшие скорости дают шаг/ходьбу.")]
    [SerializeField] private float runSpeedReference = 5f;
    [Tooltip("Сглаживание параметра Speed — больше значение, резче реакция ног.")]
    [SerializeField] private float speedSmoothing = 10f;

    [Header("Боевые анимации")]
    [Tooltip("Проигрывать реакцию на получение урона (триггер Hit). Может конфликтовать со стрельбой.")]
    [SerializeField] private bool playHitReaction = false;

    [Header("Имена параметров аниматора")]
    [SerializeField] private string speedParam = "Speed";
    [SerializeField] private string shootParam = "Shoot";
    [SerializeField] private string attackParam = "Attack";
    [SerializeField] private string hitParam = "Hit";
    [SerializeField] private string dieParam = "Die";

    private int _speedHash, _shootHash, _attackHash, _hitHash, _dieHash;
    private bool _hasSpeed, _hasShoot, _hasAttack, _hasHit, _hasDie;
    private float _speedSmoothed;
    private bool _isDead;
    private Vector3 _lastPos;
    private bool _hasLastPos;

    private void Reset()
    {
        ResolveReferences();
    }

    private void Awake()
    {
        ResolveReferences();

        if (animator != null)
        {
            animator.applyRootMotion = false;
            CacheParameters();
        }
    }

    private void OnEnable()
    {
        if (shooting != null)
            shooting.OnShoot += HandleShoot;

        if (meleeAttacker != null)
            meleeAttacker.OnAttack += HandleAttack;

        if (health != null)
        {
            health.OnEnemyDeath.AddListener(HandleDeath);
            health.OnDamaged += HandleDamaged;
        }
    }

    private void OnDisable()
    {
        if (shooting != null)
            shooting.OnShoot -= HandleShoot;

        if (meleeAttacker != null)
            meleeAttacker.OnAttack -= HandleAttack;

        if (health != null)
        {
            health.OnEnemyDeath.RemoveListener(HandleDeath);
            health.OnDamaged -= HandleDamaged;
        }
    }

    private void Update()
    {
        if (animator == null || _isDead || !_hasSpeed)
            return;

        float targetSpeed = 0f;
        float reference = Mathf.Max(0.01f, runSpeedReference);
        if (agent != null && agent.isActiveAndEnabled && agent.isOnNavMesh)
        {
            Vector3 v = agent.velocity;
            v.y = 0f;
            targetSpeed = Mathf.Clamp01(v.magnitude / reference);
        }
        else if (Time.deltaTime > 0.0001f)
        {
            // Враги без NavMeshAgent (например PatrolConeGuard) — скорость по перемещению transform.
            Vector3 pos = transform.position;
            if (_hasLastPos)
            {
                Vector3 delta = pos - _lastPos;
                delta.y = 0f;
                targetSpeed = Mathf.Clamp01((delta.magnitude / Time.deltaTime) / reference);
            }
            _lastPos = pos;
            _hasLastPos = true;
        }

        _speedSmoothed = Mathf.Lerp(_speedSmoothed, targetSpeed, Time.deltaTime * speedSmoothing);
        animator.SetFloat(_speedHash, _speedSmoothed);
    }

    private void ResolveReferences()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (agent == null)
            agent = GetComponentInParent<NavMeshAgent>();
        if (agent == null)
            agent = GetComponentInChildren<NavMeshAgent>();

        if (shooting == null)
            shooting = GetComponent<EnemyShooting>();
        if (shooting == null)
            shooting = GetComponentInChildren<EnemyShooting>();

        if (meleeAttacker == null)
            meleeAttacker = GetComponent<PeriodicProximityDamageEnemy>();

        if (health == null)
            health = GetComponent<EnemyHealth>();
        if (health == null)
            health = GetComponentInParent<EnemyHealth>();
    }

    private void CacheParameters()
    {
        _speedHash = Animator.StringToHash(speedParam);
        _shootHash = Animator.StringToHash(shootParam);
        _attackHash = Animator.StringToHash(attackParam);
        _hitHash = Animator.StringToHash(hitParam);
        _dieHash = Animator.StringToHash(dieParam);

        _hasSpeed = HasParameter(speedParam);
        _hasShoot = HasParameter(shootParam);
        _hasAttack = HasParameter(attackParam);
        _hasHit = HasParameter(hitParam);
        _hasDie = HasParameter(dieParam);
    }

    private bool HasParameter(string paramName)
    {
        if (animator == null || string.IsNullOrEmpty(paramName))
            return false;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.name == paramName)
                return true;
        }

        return false;
    }

    private void HandleShoot()
    {
        if (_isDead || animator == null || !_hasShoot)
            return;

        animator.SetTrigger(_shootHash);
    }

    private void HandleAttack()
    {
        if (_isDead || animator == null || !_hasAttack)
            return;

        animator.SetTrigger(_attackHash);
    }

    private void HandleDamaged()
    {
        if (_isDead || !playHitReaction || animator == null || !_hasHit)
            return;

        animator.SetTrigger(_hitHash);
    }

    private void HandleDeath()
    {
        _isDead = true;

        if (animator == null || !_hasDie)
            return;

        animator.SetTrigger(_dieHash);
    }
}
