using UnityEngine;

/// <summary>
/// Движение относительно камеры сверху (WASD / стики) без FPS-мыши.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class TopDownPlayerMovement : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float walkSpeed = 5.5f;
    [SerializeField] private float runSpeed = 8f;
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private bool rotateBodyToMoveDirection = true;
    [SerializeField] private float bodyTurnSpeed = 12f;
    [SerializeField] private bool invertHorizontal;
    [SerializeField] private bool invertVertical;

    [Header("Анимация")]
    [SerializeField] private Animator animator;
    [SerializeField] private float speedSmoothing = 10f;

    private CharacterController _controller;
    private float _verticalVelocity;

    private bool _hasSpeedParam;
    private bool _hasGroundedParam;
    private float _animSpeed;
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");

    public Transform CameraTransform
    {
        get => cameraTransform;
        set => cameraTransform = value;
    }

    /// <summary>Подброс вверх на заданную высоту (метры до вершины дуги).</summary>
    public void LaunchToHeight(float height)
    {
        if (height <= 0f)
            return;

        _verticalVelocity = Mathf.Sqrt(height * -2f * gravity);
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
        SetupAnimator();
    }

    private void SetupAnimator()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        // В top-down FPS-контроллер выключен, поэтому PlayerAnimatorDriver не сможет
        // считать скорость — отключаем его и гоним аниматор сами.
        PlayerAnimatorDriver fpsDriver = GetComponentInChildren<PlayerAnimatorDriver>();
        if (fpsDriver != null)
            fpsDriver.enabled = false;

        if (animator == null)
            return;

        foreach (AnimatorControllerParameter p in animator.parameters)
        {
            if (p.type == AnimatorControllerParameterType.Float && p.name == "Speed")
                _hasSpeedParam = true;
            if (p.type == AnimatorControllerParameterType.Bool && p.name == "Grounded")
                _hasGroundedParam = true;
        }
    }

    private void Update()
    {
        if (_controller == null || !_controller.enabled)
            return;

        if (GameplayInputBlocker.IsBlocked)
        {
            UpdateAnimator(0f);
            return;
        }

        float horizontal = Input.GetAxisRaw("Horizontal");
        float vertical = Input.GetAxisRaw("Vertical");

        if (invertHorizontal)
            horizontal = -horizontal;
        if (invertVertical)
            vertical = -vertical;

        Vector3 move = BuildCameraRelativeMove(horizontal, vertical);
        float speed = Input.GetKey(runKey) ? runSpeed : walkSpeed;

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        _controller.Move(move * speed * Time.deltaTime);

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        _verticalVelocity += gravity * Time.deltaTime;
        _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));

        if (rotateBodyToMoveDirection && move.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, bodyTurnSpeed * Time.deltaTime);
        }

        // Нормализованная скорость для аниматора (0 — стоит, 1 — бег).
        float target01 = Mathf.Clamp01(move.magnitude * speed / Mathf.Max(0.01f, runSpeed));
        UpdateAnimator(target01);
    }

    private void UpdateAnimator(float target01)
    {
        if (animator == null)
            return;

        _animSpeed = Mathf.Lerp(_animSpeed, target01, Time.deltaTime * speedSmoothing);

        if (_hasSpeedParam)
            animator.SetFloat(SpeedHash, _animSpeed);
        if (_hasGroundedParam)
            animator.SetBool(GroundedHash, true);
    }

    private Vector3 BuildCameraRelativeMove(float horizontal, float vertical)
    {
        if (cameraTransform == null)
            return new Vector3(horizontal, 0f, vertical);

        // Для наклонной камеры (~55°): «вперёд» — проекция forward на пол; при почти вертикальном виде — up.
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;

        if (forward.sqrMagnitude < 0.15f)
        {
            forward = cameraTransform.up;
            forward.y = 0f;
        }

        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        else
            forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        return right * horizontal + forward * vertical;
    }
}
