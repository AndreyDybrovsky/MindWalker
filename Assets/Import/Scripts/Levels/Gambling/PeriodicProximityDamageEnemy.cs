using System;
using UnityEngine;

/// <summary>
/// Враг без стрельбы: наносит урон игроку с интервалом, пока видит его (EnemyVision).
/// </summary>
[DisallowMultipleComponent]
public class PeriodicProximityDamageEnemy : MonoBehaviour
{
    /// <summary>Вызывается в момент удара (особая атака Homeless). Используется EnemyAnimatorDriver.</summary>
    public event Action OnAttack;

    [Header("Обнаружение")]
    [SerializeField] private EnemyVision vision;

    [Header("Урон")]
    [SerializeField] private float damageAmount = 12f;
    [SerializeField] private float damageInterval = 1.25f;
    [SerializeField] private float firstHitDelay = 0.35f;
    [Tooltip("Макс. дистанция удара (рукопашка). 0 = без лимита (только зрение).")]
    [SerializeField] private float maxMeleeRange = 2.4f;
    [Tooltip("После потери зрения урон продолжается, пока игрок в радиусе удара.")]
    [SerializeField] private float meleeEngageGrace = 0.5f;

    private EnemyController _enemyController;
    private bool _playerEngaged;
    private bool _playerInVision;
    private float _nextDamageTime;
    private float _meleeGraceUntil;

    private void Awake()
    {
        if (vision == null)
            vision = GetComponentInChildren<EnemyVision>(true);

        _enemyController = GetComponent<EnemyController>();
        if (_enemyController == null)
            _enemyController = GetComponentInParent<EnemyController>();
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
        _playerEngaged = false;
    }

    private void Update()
    {
        if (vision == null)
            return;

        if (_playerEngaged && !_playerInVision)
        {
            // После потери зрения: отключаем, если игрок вышел из радиуса удара и grace истёк
            if (!IsPlayerWithinMeleeRange() && Time.time >= _meleeGraceUntil)
                _playerEngaged = false;
        }

        if (!_playerEngaged)
            return;

        if (Time.time < _nextDamageTime)
            return;

        if (!TryDamagePlayer())
            return;

        _nextDamageTime = Time.time + damageInterval;
    }

    private void OnPlayerDetected(Transform _)
    {
        _playerEngaged = true;
        _playerInVision = true;
        _nextDamageTime = Time.time + firstHitDelay;
    }

    private void OnPlayerLost()
    {
        _playerInVision = false;
        _meleeGraceUntil = Time.time + meleeEngageGrace;
    }

    private bool IsPlayerWithinMeleeRange()
    {
        if (maxMeleeRange <= 0.01f)
            return true;

        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

        Vector3 toPlayer = body.position - transform.position;
        toPlayer.y = 0f;
        return toPlayer.sqrMagnitude <= maxMeleeRange * maxMeleeRange;
    }

    private bool TryDamagePlayer()
    {
        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

        PlayerHealth health = body.GetComponentInParent<PlayerHealth>();
        if (health == null)
            health = body.GetComponentInChildren<PlayerHealth>(true);

        if (health == null || health.IsDead)
            return false;

        if (maxMeleeRange > 0.01f)
        {
            Vector3 toPlayer = body.position - transform.position;
            toPlayer.y = 0f;
            if (toPlayer.sqrMagnitude > maxMeleeRange * maxMeleeRange)
                return false;
        }

        health.TakeDamage(damageAmount);
        _enemyController?.PlayAttackSfx();
        OnAttack?.Invoke();
        return true;
    }
}
