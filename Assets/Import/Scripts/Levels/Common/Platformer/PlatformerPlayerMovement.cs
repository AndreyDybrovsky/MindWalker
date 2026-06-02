using UnityEngine;

/// <summary>
/// Лёгкое 3D-движение для платформера: WASD относительно камеры, прыжок, гравитация.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class PlatformerPlayerMovement : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float walkSpeed = 6f;
    [SerializeField] private float runSpeed = 9f;
    [SerializeField] private KeyCode runKey = KeyCode.LeftShift;
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float gravity = -22f;
    [SerializeField] private bool rotateBodyToMoveDirection = true;
    [SerializeField] private float bodyTurnSpeed = 14f;

    private CharacterController _controller;
    private float _verticalVelocity;

    public float Gravity => gravity;

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
        Vector3 move = BuildCameraRelativeMove(horizontal, vertical);
        float speed = Input.GetKey(runKey) ? runSpeed : walkSpeed;

        if (move.sqrMagnitude > 1f)
            move.Normalize();

        _controller.Move(move * speed * Time.deltaTime);

        if (_controller.isGrounded && _verticalVelocity < 0f)
            _verticalVelocity = -2f;

        if (Input.GetButtonDown("Jump") && _controller.isGrounded)
            _verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        _verticalVelocity += gravity * Time.deltaTime;
        _controller.Move(Vector3.up * (_verticalVelocity * Time.deltaTime));

        if (rotateBodyToMoveDirection && move.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, bodyTurnSpeed * Time.deltaTime);
        }
    }

    /// <summary>Подброс вверх на заданную высоту (метры до вершины дуги).</summary>
    public void LaunchToHeight(float height)
    {
        if (height <= 0f)
            return;

        _verticalVelocity = Mathf.Sqrt(height * -2f * gravity);
    }

    private Vector3 BuildCameraRelativeMove(float horizontal, float vertical)
    {
        if (cameraTransform == null)
            return new Vector3(horizontal, 0f, vertical);

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
