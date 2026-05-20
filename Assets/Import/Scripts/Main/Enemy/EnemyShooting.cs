using UnityEngine;

public class EnemyShooting : MonoBehaviour
{
    [Header("Настройки стрельбы")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 1f; // Выстрелов в секунду
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float damage = 10f;

    [Header("Настройки прицеливания")]
    [SerializeField] private float aimOffset = 0.5f; // Смещение прицеливания (для реалистичности)
    [SerializeField] private float maxShootDistance = 50f; // Максимальная дистанция стрельбы

    [Header("Отладка")]
    [SerializeField] private bool debugMode = false;

    private Transform target;
    private float nextFireTime = 0f;
    private bool canShoot = false;

    private void Awake()
    {
        // Автоматически создаем firePoint, если он не назначен
        if (firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            // Размещаем firePoint достаточно далеко от противника, чтобы пули не сталкивались сразу
            firePointObj.transform.localPosition = Vector3.forward * 1f; // 1 единица впереди противника
            firePoint = firePointObj.transform;
            
            if (debugMode)
                Debug.Log($"EnemyShooting: Автоматически создан firePoint для {gameObject.name} на позиции {firePoint.localPosition}");
        }
    }

    private void Start()
    {
        // Проверяем наличие префаба пули
        if (bulletPrefab == null)
        {
            Debug.LogError($"EnemyShooting: Префаб пули не назначен для {gameObject.name}!");
            enabled = false;
            return;
        }

        // Проверяем, что у префаба есть необходимые компоненты
        if (bulletPrefab.GetComponent<EnemyBullet>() == null && bulletPrefab.GetComponent<Rigidbody>() == null)
        {
            Debug.LogWarning($"EnemyShooting: Префаб пули {bulletPrefab.name} не имеет компонента EnemyBullet или Rigidbody!");
        }
    }

    private void Update()
    {
        if (!canShoot || target == null)
            return;

        // Проверяем дистанцию до цели
        float distanceToTarget = Vector3.Distance(transform.position, target.position);
        if (distanceToTarget > maxShootDistance)
        {
            if (debugMode)
                Debug.Log($"EnemyShooting: Цель слишком далеко ({distanceToTarget:F1} > {maxShootDistance})");
            return;
        }

        // Проверяем, можно ли стрелять
        if (Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + (1f / fireRate);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        canShoot = (target != null);
        
        if (debugMode)
        {
            if (canShoot)
                Debug.Log($"EnemyShooting: Цель установлена: {target.name}");
            else
                Debug.Log("EnemyShooting: Цель сброшена");
        }
    }

    private void Shoot()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError($"EnemyShooting: Префаб пули не назначен для {gameObject.name}!");
            return;
        }

        if (firePoint == null)
        {
            Debug.LogError($"EnemyShooting: FirePoint не назначен для {gameObject.name}!");
            return;
        }

        if (target == null)
        {
            if (debugMode)
                Debug.LogWarning("EnemyShooting: Попытка стрельбы без цели!");
            return;
        }

        // Вычисляем направление к цели с небольшим случайным смещением
        Vector3 targetPosition = target.position;
        
        // Добавляем случайное смещение для реалистичности
        Vector3 randomOffset = new Vector3(
            Random.Range(-aimOffset, aimOffset),
            Random.Range(-aimOffset, aimOffset),
            Random.Range(-aimOffset, aimOffset)
        );
        
        targetPosition += randomOffset;

        Vector3 direction = (targetPosition - firePoint.position).normalized;

        if (direction == Vector3.zero)
        {
            Debug.LogWarning("EnemyShooting: Направление равно нулю!");
            return;
        }

        // Создаем пулю
        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));
        
        if (bullet == null)
        {
            Debug.LogError("EnemyShooting: Не удалось создать пулю!");
            return;
        }

        // Настраиваем пулю
        EnemyBullet bulletScript = bullet.GetComponent<EnemyBullet>();
        if (bulletScript != null)
        {
            // Устанавливаем владельца пули для игнорирования коллизий
            bulletScript.SetEnemyOwner(gameObject);
            
            // Устанавливаем параметры пули
            bulletScript.SetDamage(damage);
            bulletScript.SetSpeed(bulletSpeed);
            
            // Убеждаемся, что скорость установлена сразу
            Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
            if (bulletRb != null && bulletRb.linearVelocity.magnitude < 0.1f)
            {
                bulletRb.linearVelocity = direction * bulletSpeed;
            }
            
            if (debugMode)
                Debug.Log($"EnemyShooting: Выстрел произведен! Урон: {damage}, Скорость: {bulletSpeed}, Позиция: {firePoint.position}");
        }
        else
        {
            // Если компонент отсутствует, используем Rigidbody напрямую
            Rigidbody rb = bullet.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.linearVelocity = direction * bulletSpeed;
                
                if (debugMode)
                    Debug.Log($"EnemyShooting: Выстрел произведен через Rigidbody! Скорость: {bulletSpeed}, Позиция: {firePoint.position}");
            }
            else
            {
                Debug.LogWarning($"EnemyShooting: Пуля {bullet.name} не имеет компонента EnemyBullet или Rigidbody!");
            }
        }
        
        // Дополнительная проверка: убеждаемся, что пуля действительно создана и видна
        if (debugMode && bullet != null)
        {
            Debug.Log($"EnemyShooting: Пуля создана: {bullet.name}, Активна: {bullet.activeSelf}, Позиция: {bullet.transform.position}");
        }
    }
}
