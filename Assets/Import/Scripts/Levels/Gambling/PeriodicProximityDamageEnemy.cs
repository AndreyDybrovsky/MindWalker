using UnityEngine;

/// <summary>
/// Враг без стрельбы: наносит урон игроку с интервалом, пока видит его (EnemyVision).
/// </summary>
[DisallowMultipleComponent]
public class PeriodicProximityDamageEnemy : MonoBehaviour
{
    [Header("Обнаружение")]
    [SerializeField] private EnemyVision vision;

    [Header("Урон")]
    [SerializeField] private float damageAmount = 12f;
    [SerializeField] private float damageInterval = 1.25f;
    [SerializeField] private float firstHitDelay = 0.35f;

    private bool _playerEngaged;
    private float _nextDamageTime;

    private void Awake()
    {
        if (vision == null)
            vision = GetComponentInChildren<EnemyVision>(true);
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
        if (!_playerEngaged || vision == null)
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
        _nextDamageTime = Time.time + firstHitDelay;
    }

    private void OnPlayerLost()
    {
        _playerEngaged = false;
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

        health.TakeDamage(damageAmount);
        return true;
    }
}
