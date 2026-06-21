using UnityEngine;

/// <summary>
/// Пуля игрока, наносит урон врагам при попадании
/// </summary>
public class PlayerBullet : MonoBehaviour
{
    [Header("Настройки пули")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float speed = 20f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Настройки коллизий")]
    [SerializeField] private float ignoreObstacleTime = 0.1f; // Время игнорирования коллизий с препятствиями

    private Rigidbody rb;
    private bool hasHit = false;
    private float spawnTime;
    private GameObject playerOwner;

    private void Awake()
    {
        spawnTime = Time.time;
        
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        
        // Убеждаемся, что Collider настроен как триггер
        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 0.1f;
        }
    }

    private void Start()
    {
        // Устанавливаем скорость, если она еще не установлена
        if (rb != null && rb.linearVelocity.magnitude < 0.1f && speed > 0)
        {
            rb.linearVelocity = transform.forward * speed;
        }
        
        // Уничтожаем пулю через определенное время
        Destroy(gameObject, lifetime);
    }

    public void SetDamage(float newDamage)
    {
        damage = newDamage;
    }

    public void SetSpeed(float newSpeed)
    {
        speed = newSpeed;
        if (rb != null)
        {
            rb.linearVelocity = transform.forward * speed;
        }
    }

    public void SetPlayerOwner(GameObject owner)
    {
        playerOwner = owner;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (hasHit) return;

        // Игнорируем коллизии с препятствиями в течение короткого времени после создания
        if (Time.time - spawnTime < ignoreObstacleTime)
        {
            if (!other.CompareTag(enemyTag))
            {
                return; // Игнорируем коллизию с препятствием сразу после создания
            }
        }

        if (IsEnemyCollider(other))
        {
            hasHit = true;
            
            EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = other.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = other.transform.root.GetComponentInChildren<EnemyHealth>(true);
            
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                GameStatsTracker.Instance?.RecordDamageDealt(damage);
                Debug.Log($"PlayerBullet: Нанесен урон {damage} врагу {other.name}. HP: {enemyHealth.CurrentHealth}");
            }
            else
            {
                // Проверяем фантома босса — показываем эффект попадания без урона
                BossPhantomMark phantom = other.GetComponentInParent<BossPhantomMark>()
                    ?? other.transform.root.GetComponentInChildren<BossPhantomMark>(true);
                phantom?.OnBulletHit();
            }
            
            SpawnImpact(true);

            // Останавливаем физику пули перед уничтожением
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            Destroy(gameObject);
        }
        else if (!other.isTrigger && !other.CompareTag("Player") && !other.CompareTag("PlayerBullet"))
        {
            // Попадание в препятствие (не триггер, не игрок, не другая пуля игрока)
            if (Time.time - spawnTime < ignoreObstacleTime)
            {
                return; // Игнорируем коллизию с препятствием сразу после создания
            }

            hasHit = true;
            SpawnImpact(false);
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            Destroy(gameObject);
        }
    }

    private void SpawnImpact(bool enemyHit)
    {
        if (enemyHit)
        {
            // По врагу — только хитмаркер (без точечного света, иначе он «светится»
            // шаром внутри врага, особенно на тёмных сценах вроде босса Депрессии).
            HitmarkerUI.Show();
        }
        else
        {
            // По препятствию — короткая искра.
            JuiceFx.SpawnFlash(transform.position, new Color(1f, 0.9f, 0.6f), 1.2f, 1.6f, 0.05f);
        }
    }

    private bool IsEnemyCollider(Component other)
    {
        if (other == null)
            return false;

        if (other.CompareTag(enemyTag))
            return true;

        return other.transform.root.CompareTag(enemyTag);
    }

    private bool IsEnemyCollider(GameObject other)
    {
        if (other == null)
            return false;

        if (other.CompareTag(enemyTag))
            return true;

        return other.transform.root.CompareTag(enemyTag);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        GameObject other = collision.gameObject;

        // Игнорируем коллизии с препятствиями в течение короткого времени после создания
        if (Time.time - spawnTime < ignoreObstacleTime)
        {
            if (!other.CompareTag(enemyTag))
            {
                return;
            }
        }

        if (IsEnemyCollider(other))
        {
            hasHit = true;
            
            EnemyHealth enemyHealth = other.GetComponent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = other.GetComponentInParent<EnemyHealth>();
            if (enemyHealth == null)
                enemyHealth = other.transform.root.GetComponentInChildren<EnemyHealth>(true);
            
            if (enemyHealth != null)
            {
                enemyHealth.TakeDamage(damage);
                Debug.Log($"PlayerBullet: Нанесен урон {damage} врагу (через OnCollisionEnter). HP: {enemyHealth.CurrentHealth}");
            }
            else
            {
                BossPhantomMark phantom = other.GetComponent<BossPhantomMark>()
                    ?? other.GetComponentInParent<BossPhantomMark>()
                    ?? other.transform.root.GetComponentInChildren<BossPhantomMark>(true);
                phantom?.OnBulletHit();
            }

            SpawnImpact(true);

            if (rb != null)
            {
                rb.isKinematic = true;
            }

            Destroy(gameObject);
        }
        else if (!other.CompareTag("Player") && !other.CompareTag("PlayerBullet"))
        {
            if (Time.time - spawnTime < ignoreObstacleTime)
            {
                return;
            }

            hasHit = true;
            SpawnImpact(false);
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            Destroy(gameObject);
        }
    }
}
