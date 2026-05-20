using UnityEngine;

public class EnemyBullet : MonoBehaviour
{
    [Header("Настройки пули")]
    [SerializeField] private float damage = 10f;
    [SerializeField] private float speed = 15f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private string playerTag = "Player";

    [Header("Настройки коллизий")]
    [SerializeField] private string enemyTag = "Enemy"; // Тег противника для игнорирования коллизий
    [SerializeField] private float ignoreCollisionTime = 0.2f; // Время игнорирования коллизий с противником
    [SerializeField] private float ignoreObstacleTime = 0.15f; // Время игнорирования коллизий с препятствиями (чтобы пуля не уничтожалась сразу после создания)

    private Rigidbody rb;
    private bool isInitialized = false;
    private bool hasHit = false; // Флаг для предотвращения множественных попаданий
    private float spawnTime;
    private GameObject enemyOwner; // Владелец пули (противник, который выстрелил)

    private void Awake()
    {
        spawnTime = Time.time;
        
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
        }
        rb.useGravity = false;
        
        // Настраиваем Rigidbody для триггерных столкновений
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
            // Если коллайдера нет, добавляем сферу
            SphereCollider sphereCol = gameObject.AddComponent<SphereCollider>();
            sphereCol.isTrigger = true;
            sphereCol.radius = 0.1f;
        }
    }

    private void Start()
    {
        // Убеждаемся, что скорость установлена
        if (!isInitialized && rb != null && speed > 0)
        {
            InitializeVelocity();
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
        InitializeVelocity();
    }

    public void SetEnemyOwner(GameObject owner)
    {
        enemyOwner = owner;
    }

    private void InitializeVelocity()
    {
        if (rb != null && !isInitialized)
        {
            rb.linearVelocity = transform.forward * speed;
            isInitialized = true;
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        // Предотвращаем множественные попадания
        if (hasHit) return;

        // Игнорируем коллизии с противником в течение короткого времени после создания
        if (Time.time - spawnTime < ignoreCollisionTime)
        {
            if (other.CompareTag(enemyTag) || other.gameObject == enemyOwner)
            {
                return; // Игнорируем коллизию с противником
            }
        }

        if (other.CompareTag(playerTag))
        {
            hasHit = true;
            
            // Останавливаем физику пули перед уничтожением
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            
            // Наносим урон игроку
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                Debug.Log($"EnemyBullet: Нанесен урон {damage} игроку. HP: {playerHealth.CurrentHealth}");
            }
            else
            {
                // Пытаемся найти PlayerHealth в дочерних объектах или родителе
                playerHealth = other.GetComponentInParent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(damage);
                    Debug.Log($"EnemyBullet: Нанесен урон {damage} игроку (найден в родителе). HP: {playerHealth.CurrentHealth}");
                }
                else
                {
                    Debug.LogWarning($"EnemyBullet: PlayerHealth не найден на объекте с тегом {playerTag}!");
                }
            }

            // Немедленно уничтожаем пулю
            Destroy(gameObject);
        }
        else if (!other.isTrigger && !other.CompareTag("Enemy") && !other.CompareTag("EnemyBullet"))
        {
            // Игнорируем коллизии с препятствиями в течение короткого времени после создания
            // Это предотвращает мгновенное уничтожение пули при создании
            if (Time.time - spawnTime < ignoreObstacleTime)
            {
                return; // Игнорируем коллизию с препятствием сразу после создания
            }
            
            // Если пуля попала в препятствие (не триггер, не враг, не другая пуля), уничтожаем её
            hasHit = true;
            if (rb != null)
            {
                rb.linearVelocity = Vector3.zero;
                rb.isKinematic = true;
            }
            Destroy(gameObject);
        }
    }

    // Дополнительная проверка через OnCollisionEnter на случай, если триггер не сработал
    private void OnCollisionEnter(Collision collision)
    {
        if (hasHit) return;

        GameObject other = collision.gameObject;

        // Игнорируем коллизии с противником в течение короткого времени после создания
        if (Time.time - spawnTime < ignoreCollisionTime)
        {
            if (other.CompareTag(enemyTag) || other == enemyOwner)
            {
                return; // Игнорируем коллизию с противником
            }
        }
        if (other.CompareTag(playerTag))
        {
            hasHit = true;
            
            // Останавливаем физику
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            
            // Наносим урон
            PlayerHealth playerHealth = other.GetComponent<PlayerHealth>();
            if (playerHealth == null)
            {
                playerHealth = other.GetComponentInParent<PlayerHealth>();
            }
            
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(damage);
                Debug.Log($"EnemyBullet: Нанесен урон {damage} игроку (через OnCollisionEnter). HP: {playerHealth.CurrentHealth}");
            }

            Destroy(gameObject);
        }
        else if (!other.CompareTag("Enemy") && !other.CompareTag("EnemyBullet"))
        {
            // Игнорируем коллизии с препятствиями в течение короткого времени после создания
            // Это предотвращает мгновенное уничтожение пули при создании
            if (Time.time - spawnTime < ignoreObstacleTime)
            {
                return; // Игнорируем коллизию с препятствием сразу после создания
            }
            
            // Попадание в препятствие
            hasHit = true;
            if (rb != null)
            {
                rb.isKinematic = true;
            }
            Destroy(gameObject);
        }
    }
}
