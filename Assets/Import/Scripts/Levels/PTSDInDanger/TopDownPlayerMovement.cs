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

    private CharacterController _controller;
    private float _verticalVelocity;

    public Transform CameraTransform
    {
        get => cameraTransform;
        set => cameraTransform = value;
    }

    private void Awake()
    {
        _controller = GetComponent<CharacterController>();
    }

    private void Update()
    {
        if (_controller == null || !_controller.enabled)
            return;

        if (GameplayInputBlocker.IsBlocked)
            return;

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
