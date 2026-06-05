using UnityEngine;

public class EnemyShooting : MonoBehaviour
{
    [Header("Настройки стрельбы")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 1f;
    [SerializeField] private float bulletSpeed = 15f;
    [SerializeField] private float damage = 10f;

    [Header("Настройки прицеливания")]
    [SerializeField] private float aimOffset = 0.08f;
    [SerializeField] private float aimHeight = 1.05f;
    [SerializeField] private bool alignWeaponBeforeShot = true;
    [SerializeField] private bool aimOnlyOnYAxis = true;
    [SerializeField] private float maxShootDistance = 50f;

    [Header("Звук выстрела (SFX)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip shootSound;
    [SerializeField, Range(0f, 1f)] private float shootSoundVolume = 0.75f;

    [Header("Отладка")]
    [SerializeField] private bool debugMode = false;

    private Transform target;
    private float nextFireTime = 0f;
    private bool canShoot = false;

    private void Awake()
    {
        if (firePoint == null)
        {
            GameObject firePointObj = new GameObject("FirePoint");
            firePointObj.transform.SetParent(transform);
            firePointObj.transform.localPosition = Vector3.forward * 1f;
            firePoint = firePointObj.transform;

            if (debugMode)
                Debug.Log($"EnemyShooting: Автоматически создан firePoint для {gameObject.name}");
        }

        if (shootSound == null)
            shootSound = Resources.Load<AudioClip>("Sounds/Shoot");

        EnsureAudioSource();
    }

    private void Start()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError($"EnemyShooting: Префаб пули не назначен для {gameObject.name}!");
            enabled = false;
        }
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
    }

    private void OnSettingsApplied()
    {
        ApplySfxVolume();
    }

    private void Update()
    {
        if (!canShoot || target == null)
            return;

        float distanceToTarget = Vector3.Distance(firePoint.position, GetTargetAimPoint());
        if (distanceToTarget > maxShootDistance)
            return;

        if (alignWeaponBeforeShot)
            AlignWeaponTowardTarget();

        if (Time.time >= nextFireTime)
        {
            Shoot();
            nextFireTime = Time.time + (1f / fireRate);
        }
    }

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
        canShoot = target != null;
    }

    private Vector3 GetTargetAimPoint()
    {
        if (target == null)
            return Vector3.zero;

        Vector3 aim = PlayerAimUtility.GetAimPoint(target);
        if (aim == Vector3.zero)
            return target.position + Vector3.up * aimHeight;

        return aim;
    }

    private void AlignWeaponTowardTarget()
    {
        Vector3 aimPoint = GetTargetAimPoint();
        Vector3 direction = aimPoint - firePoint.position;
        if (aimOnlyOnYAxis)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
    }

    private void Shoot()
    {
        if (bulletPrefab == null || firePoint == null || target == null)
            return;

        Vector3 targetPosition = GetTargetAimPoint();
        Vector3 randomOffset = new Vector3(
            Random.Range(-aimOffset, aimOffset),
            Random.Range(-aimOffset * 0.25f, aimOffset * 0.25f),
            Random.Range(-aimOffset, aimOffset));
        targetPosition += randomOffset;

        Vector3 direction = targetPosition - firePoint.position;
        if (aimOnlyOnYAxis)
            direction.y = 0f;

        if (direction.sqrMagnitude < 0.0001f)
            return;

        direction.Normalize();

        GameObject bullet = Instantiate(bulletPrefab, firePoint.position, Quaternion.LookRotation(direction));
        if (bullet == null)
            return;

        EnemyBullet bulletScript = bullet.GetComponent<EnemyBullet>();
        if (bulletScript != null)
        {
            bulletScript.SetEnemyOwner(transform.root.gameObject);
            bulletScript.SetDamage(damage);
            bulletScript.SetSpeed(bulletSpeed);

            Rigidbody bulletRb = bullet.GetComponent<Rigidbody>();
            if (bulletRb != null && bulletRb.linearVelocity.magnitude < 0.1f)
                bulletRb.linearVelocity = direction * bulletSpeed;
        }
        else if (bullet.TryGetComponent(out Rigidbody rb))
        {
            rb.linearVelocity = direction * bulletSpeed;
        }

        PlayShootSound();
    }

    private void EnsureAudioSource()
    {
        if (audioSource != null)
        {
            ConfigureShootAudioSource(audioSource);
            return;
        }

        Transform shootRoot = transform.Find("ShootAudio");
        if (shootRoot != null)
            shootRoot.TryGetComponent(out audioSource);

        if (audioSource == null)
        {
            GameObject shootGo = new GameObject("ShootAudio");
            shootGo.transform.SetParent(transform, false);
            audioSource = shootGo.AddComponent<AudioSource>();
        }

        ConfigureShootAudioSource(audioSource);
    }

    private void ConfigureShootAudioSource(AudioSource source)
    {
        if (source == null)
            return;

        audioSource = source;
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 1f;
        audioSource.minDistance = 2f;
        audioSource.maxDistance = 28f;
        AudioMixerRoutingUtility.BindSourceToSfx(audioSource);
        ApplySfxVolume();
    }

    private void ApplySfxVolume()
    {
        if (audioSource == null)
            return;

        float sfx = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().sfxVolume
            : 1f;

        audioSource.volume = shootSoundVolume * Mathf.Clamp01(sfx);
        audioSource.mute = sfx <= 0.001f;
    }

    private void PlayShootSound()
    {
        if (shootSound == null)
            return;

        EnsureAudioSource();
        ApplySfxVolume();

        float sfx = SettingsManager.Instance != null
            ? SettingsManager.Instance.GetCurrentSettings().sfxVolume
            : 1f;

        if (sfx <= 0.001f)
            return;

        audioSource.PlayOneShot(shootSound, shootSoundVolume * Mathf.Clamp01(sfx));
    }
}
