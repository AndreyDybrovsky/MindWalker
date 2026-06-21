using UnityEngine;

/// <summary>
/// Обработчик стрельбы игрока
/// </summary>
public class WeaponHandler : MonoBehaviour
{
    [Header("Настройки стрельбы")]
    [SerializeField] private Transform bulletSpawnPoint;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private float bulletSpeed = 20f;
    [SerializeField] private float damage = 10f;
    [SerializeField] private KeyCode shootKey = KeyCode.Mouse0; // Левая кнопка мыши по умолчанию
    [SerializeField] private float fireRate = 0.5f; // Минимальное время между выстрелами

    [Header("Звук выстрела")]
    [Tooltip("Один или несколько клипов; при каждом выстреле выбирается случайный непустой.")]
    [SerializeField] private AudioClip[] shootSounds;
    [SerializeField, Range(0f, 1f)] private float shootVolume = 1f;
    [Tooltip("Если не задан — AudioSource на этом объекте, иначе PlayClipAtPoint у точки спавна.")]
    [SerializeField] private AudioSource shootAudioSource;

    [Header("Отдача / вспышка вьюмодели")]
    [Tooltip("Если не задан — ищется автоматически в детях/родителе.")]
    [SerializeField] private WeaponViewmodelMotion viewmodelMotion;
    [SerializeField] private WeaponMuzzleFlash muzzleFlash;
    [SerializeField, Range(0f, 1f)] private float shootShake = 0.12f;

    private float nextFireTime = 0f;
    private GameObject playerOwner;

    private void Awake()
    {
        if (shootAudioSource == null)
            shootAudioSource = GetComponent<AudioSource>();

        if (viewmodelMotion == null)
            viewmodelMotion = GetComponentInChildren<WeaponViewmodelMotion>();
        if (viewmodelMotion == null)
            viewmodelMotion = GetComponentInParent<WeaponViewmodelMotion>();

        if (muzzleFlash == null)
            muzzleFlash = GetComponentInChildren<WeaponMuzzleFlash>();
        if (muzzleFlash == null)
            muzzleFlash = GetComponentInParent<WeaponMuzzleFlash>();
    }

    private void Start()
    {
        playerOwner = gameObject;
        
        // Автоматически находим точку спавна пуль, если не назначена
        if (bulletSpawnPoint == null)
        {
            Camera playerCamera = GetComponentInChildren<Camera>();
            if (playerCamera == null)
            {
                playerCamera = Camera.main;
            }
            
            if (playerCamera != null)
            {
                // Создаем точку спавна перед камерой
                GameObject spawnPointObj = new GameObject("BulletSpawnPoint");
                spawnPointObj.transform.SetParent(playerCamera.transform);
                spawnPointObj.transform.localPosition = new Vector3(0f, 0f, 0.5f);
                bulletSpawnPoint = spawnPointObj.transform;
            }
        }
    }

    private void Update()
    {
        HandleInput();
    }

    private void HandleInput()
    {
        if (GameplayInputBlocker.IsBlocked)
            return;

        if (Input.GetKey(shootKey) && Time.time >= nextFireTime)
        {
            FireBullet();
            nextFireTime = Time.time + fireRate;
        }
    }

    private void FireBullet()
    {
        if (bulletPrefab == null)
        {
            Debug.LogWarning("WeaponHandler: Префаб пули не назначен!");
            return;
        }

        if (bulletSpawnPoint == null)
        {
            Debug.LogWarning("WeaponHandler: Точка спавна пуль не назначена!");
            return;
        }

        // Создаем пулю
        GameObject bullet = Instantiate(bulletPrefab, bulletSpawnPoint.position, bulletSpawnPoint.rotation);
        
        // Настраиваем пулю игрока
        PlayerBullet playerBullet = bullet.GetComponent<PlayerBullet>();
        if (playerBullet != null)
        {
            playerBullet.SetDamage(damage);
            playerBullet.SetSpeed(bulletSpeed);
            playerBullet.SetPlayerOwner(playerOwner);
        }
        else
        {
            // Если компонент PlayerBullet отсутствует, настраиваем Rigidbody напрямую
            Rigidbody bulletRigidbody = bullet.GetComponent<Rigidbody>();
            if (bulletRigidbody != null)
            {
                bulletRigidbody.linearVelocity = bulletSpawnPoint.forward * bulletSpeed;
            }
        }

        PlayRandomShootSound();
        viewmodelMotion?.AddRecoil();
        muzzleFlash?.Flash();
        CameraShaker.Shake(shootShake);
    }

    private void PlayRandomShootSound()
    {
        if (shootSounds == null || shootSounds.Length == 0)
            return;

        AudioClip clip = shootSounds[Random.Range(0, shootSounds.Length)];
        if (clip == null)
            return;

        if (shootAudioSource != null)
        {
            shootAudioSource.PlayOneShot(clip, shootVolume);
            return;
        }

        Vector3 pos = bulletSpawnPoint != null ? bulletSpawnPoint.position : transform.position;
        AudioSource.PlayClipAtPoint(clip, pos, shootVolume);
    }
}