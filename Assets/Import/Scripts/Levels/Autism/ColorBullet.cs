using UnityEngine;

/// <summary>
/// Цветная пуля игрока (уровень Autism). Хранит свой ElementColor.
/// При попадании во врага: совпадение цвета → урон, несовпадение → враг получает бафф.
/// Если цвет не задан явно — берётся текущий цвет оружия из <see cref="ColorManager"/>.
/// </summary>
public class ColorBullet : MonoBehaviour
{
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

    [Header("Настройки пули")]
    [SerializeField] private float damage = 25f;
    [SerializeField] private float speed = 22f;
    [SerializeField] private float lifetime = 5f;
    [SerializeField] private string enemyTag = "Enemy";

    [Header("Цвет")]
    [Tooltip("Если выключено — цвет берётся из ColorManager.CurrentColor при спавне.")]
    [SerializeField] private bool overrideColor = false;
    [SerializeField] private ElementColor color = ElementColor.Red;

    [Header("Коллизии")]
    [SerializeField] private float ignoreObstacleTime = 0.1f;

    private Rigidbody _rb;
    private bool _hasHit;
    private float _spawnTime;
    private bool _colorResolved;

    private void Awake()
    {
        _spawnTime = Time.time;

        _rb = GetComponent<Rigidbody>();
        if (_rb == null)
            _rb = gameObject.AddComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.isKinematic = false;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            col.isTrigger = true;
        }
        else
        {
            SphereCollider sphere = gameObject.AddComponent<SphereCollider>();
            sphere.isTrigger = true;
            sphere.radius = 0.12f;
        }
    }

    private void Start()
    {
        ResolveColor();
        ApplyVisualColor();

        if (_rb != null && _rb.linearVelocity.magnitude < 0.1f && speed > 0f)
            _rb.linearVelocity = transform.forward * speed;

        Destroy(gameObject, lifetime);
    }

    // ── Конфигурация (для оружия) ───────────────────────────────────────────

    public void SetDamage(float value) => damage = value;

    public void SetSpeed(float value)
    {
        speed = value;
        if (_rb != null)
            _rb.linearVelocity = transform.forward * speed;
    }

    public void SetColor(ElementColor value)
    {
        color = value;
        overrideColor = true;
        _colorResolved = true;
        ApplyVisualColor();
    }

    public void SetPlayerOwner(GameObject owner) { /* совместимость с WeaponHandler-подобными вызовами */ }

    // ── Логика ───────────────────────────────────────────────────────────────

    private void ResolveColor()
    {
        if (_colorResolved)
            return;

        if (!overrideColor)
            color = ColorManager.CurrentColor;

        _colorResolved = true;
    }

    private void ApplyVisualColor()
    {
        Color c = ColorManager.ToColor(color);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r == null || r.material == null)
                continue;

            Material mat = r.material;
            if (mat.HasProperty(BaseColorId))
                mat.SetColor(BaseColorId, c);
            if (mat.HasProperty(ColorId))
                mat.SetColor(ColorId, c);
            if (mat.HasProperty(EmissionColorId))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor(EmissionColorId, c * 2f);
            }
        }
    }

    private void OnTriggerEnter(Collider other) => HandleContact(other);

    private void OnCollisionEnter(Collision collision) => HandleContact(collision.collider);

    private void HandleContact(Collider other)
    {
        if (_hasHit || other == null)
            return;

        if (IsEnemy(other))
        {
            _hasHit = true;
            ResolveColor();

            ColorEnemy colorEnemy = other.GetComponentInParent<ColorEnemy>();
            if (colorEnemy == null)
                colorEnemy = other.transform.root.GetComponentInChildren<ColorEnemy>(true);

            if (colorEnemy != null)
            {
                colorEnemy.HandleHit(color, damage);
            }
            else
            {
                // Фолбэк: если ColorEnemy нет — обычный урон.
                EnemyHealth health = other.GetComponentInParent<EnemyHealth>();
                if (health == null)
                    health = other.transform.root.GetComponentInChildren<EnemyHealth>(true);
                if (health != null)
                    health.TakeDamage(damage);
            }

            // «Сок» попадания по врагу — хитмаркер (без точечного света на враге).
            HitmarkerUI.Show();

            DestroyBullet();
            return;
        }

        // Препятствие (не игрок, не пуля, не триггер) — уничтожаемся.
        if (!other.isTrigger && !other.CompareTag("Player") &&
            !other.CompareTag("PlayerBullet") && !other.CompareTag("Player"))
        {
            if (Time.time - _spawnTime < ignoreObstacleTime)
                return;

            _hasHit = true;
            DestroyBullet();
        }
    }

    private bool IsEnemy(Component other)
    {
        if (other == null)
            return false;
        if (other.CompareTag(enemyTag))
            return true;
        return other.transform.root.CompareTag(enemyTag);
    }

    private void DestroyBullet()
    {
        if (_rb != null)
        {
            _rb.linearVelocity = Vector3.zero;
            _rb.isKinematic = true;
        }
        Destroy(gameObject);
    }
}
