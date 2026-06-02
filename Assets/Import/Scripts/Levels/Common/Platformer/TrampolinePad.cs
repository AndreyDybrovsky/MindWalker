using ElmanGameDevTools.PlayerSystem;
using UnityEngine;

/// <summary>
/// Батут: при касании игрока подбрасывает на заданную высоту.
/// Collider должен быть Is Trigger; на объекте — kinematic Rigidbody (для надёжных триггеров).
/// </summary>
[RequireComponent(typeof(Collider))]
public class TrampolinePad : MonoBehaviour
{
    [Header("Подброс")]
    [Tooltip("Высота подъёма в метрах (от точки отталкивания до вершины дуги).")]
    [SerializeField] private float bounceHeight = 4f;
    [Tooltip("Пауза перед повторным подбросом тем же игроком.")]
    [SerializeField] private float cooldown = 0.35f;
    [Tooltip("Подбрасывать только если игрок падает вниз.")]
    [SerializeField] private bool onlyWhenFalling = true;

    [Header("Гравитация (если нет контроллера движения)")]
    [SerializeField] private float fallbackGravity = -22f;

    [Header("Звук (опционально)")]
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip bounceSound;
    [SerializeField, Range(0f, 1f)] private float soundVolume = 0.85f;

    private float _lastBounceTime = -999f;

    private void Awake()
    {
        Collider col = GetComponent<Collider>();
        if (col != null)
            col.isTrigger = true;

        EnsureTriggerRigidbody();

        if (audioSource == null)
            TryGetComponent(out audioSource);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayerCollider(other))
            return;

        if (Time.time - _lastBounceTime < cooldown)
            return;

        if (!TryGetPlayerBody(other, out Transform body))
            return;

        if (onlyWhenFalling && !IsMovingDownward(body))
            return;

        if (!PlayerLaunchUtility.TryLaunch(body, bounceHeight, fallbackGravity))
            return;

        _lastBounceTime = Time.time;

        if (bounceSound != null && audioSource != null)
            audioSource.PlayOneShot(bounceSound, soundVolume);
    }

    private static bool IsMovingDownward(Transform body)
    {
        if (body.TryGetComponent(out CharacterController controller))
            return controller.velocity.y <= 0.05f;

        return true;
    }

    private static bool TryGetPlayerBody(Collider other, out Transform body)
    {
        body = null;

        if (other.TryGetComponent(out CharacterController onSelf))
        {
            body = onSelf.transform;
            return true;
        }

        CharacterController inParent = other.GetComponentInParent<CharacterController>();
        if (inParent != null)
        {
            body = inParent.transform;
            return true;
        }

        return false;
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        return other.transform.root.CompareTag("Player");
    }

    private void EnsureTriggerRigidbody()
    {
        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.9f, 0.5f, 0.85f);
        Vector3 top = transform.position + Vector3.up * Mathf.Max(0.5f, bounceHeight);
        Gizmos.DrawLine(transform.position, top);
        Gizmos.DrawWireSphere(top, 0.25f);
    }
#endif
}

/// <summary>
/// Единая точка подброса для разных контроллеров игрока.
/// </summary>
public static class PlayerLaunchUtility
{
    public static bool TryLaunch(Transform body, float height, float fallbackGravity)
    {
        if (body == null || height <= 0f)
            return false;

        if (body.TryGetComponent(out PlatformerPlayerMovement platformer))
        {
            platformer.LaunchToHeight(height);
            return true;
        }

        if (body.TryGetComponent(out TopDownPlayerMovement topDown))
        {
            topDown.LaunchToHeight(height);
            return true;
        }

        if (body.TryGetComponent(out PlayerController fps))
        {
            fps.LaunchToHeight(height);
            return true;
        }

        if (!body.TryGetComponent(out PlayerVerticalLaunch launch))
            launch = body.gameObject.AddComponent<PlayerVerticalLaunch>();

        launch.LaunchToHeight(height, fallbackGravity);
        return true;
    }
}

/// <summary>
/// Запасной подброс, если на игроке нет известного контроллера движения.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlayerVerticalLaunch : MonoBehaviour
{
    private CharacterController _controller;
    private float _verticalVelocity;
    private float _gravity = -22f;

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    public void LaunchToHeight(float height, float gravity)
    {
        if (height <= 0f)
            return;

        _gravity = gravity;
        _verticalVelocity = Mathf.Sqrt(height * -2f * _gravity);
    }

    private void LateUpdate()
    {
        if (_controller == null || !_controller.enabled)
            return;

        if (GetComponent<PlatformerPlayerMovement>() != null
            || GetComponent<TopDownPlayerMovement>() != null
            || GetComponent<PlayerController>() != null)
            return;

        if (GameplayInputBlocker.IsBlocked)
            return;

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += _gravity * Time.deltaTime;
        _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));
    }
}
