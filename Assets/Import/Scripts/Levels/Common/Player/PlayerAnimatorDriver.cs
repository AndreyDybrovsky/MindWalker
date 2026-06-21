using UnityEngine;
using ElmanGameDevTools.PlayerSystem;

/// <summary>
/// Связывает First Person контроллер игрока (PlayerController) с Animator модели.
/// Читает состояние контроллера — реальную скорость, землю, присед, отрыв в прыжок —
/// и гонит параметры аниматора. Root motion НЕ используется: перемещением
/// управляет CharacterController, анимация лишь проигрывает цикл «на месте».
///
/// Параметры контроллера аниматора:
///   Speed    (float 0..1) — нормализованная горизонтальная скорость (0 = стоит, 1 = бег)
///   Grounded (bool)       — на земле
///   Crouch   (bool)       — присел
///   Jump     (trigger)    — момент отрыва от земли (прыжок/падение)
/// </summary>
[RequireComponent(typeof(Animator))]
[AddComponentMenu("Player System/Player Animator Driver")]
public class PlayerAnimatorDriver : MonoBehaviour
{
    [Header("Ссылки (заполняются автоматически)")]
    [SerializeField] private Animator animator;
    [SerializeField] private PlayerController controller;
    [SerializeField] private CharacterController characterController;

    [Header("Настройка")]
    [Tooltip("Сглаживание параметра Speed. Больше — резче реакция ног на разгон/торможение.")]
    [SerializeField] private float speedSmoothing = 10f;

    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int CrouchHash = Animator.StringToHash("Crouch");
    private static readonly int JumpHash = Animator.StringToHash("Jump");

    private float _speedSmoothed;
    private bool _wasGrounded = true;
    private Vector3 _prevPosition;

    private void Reset()
    {
        animator = GetComponent<Animator>();
        controller = GetComponentInParent<PlayerController>();
        if (controller != null)
            characterController = controller.controller;
    }

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (controller == null) controller = GetComponentInParent<PlayerController>();
        if (characterController == null && controller != null)
            characterController = controller.controller;
        if (characterController == null && controller != null)
            characterController = controller.GetComponent<CharacterController>();
    }

    private void Start()
    {
        _prevPosition = characterController != null
            ? characterController.transform.position
            : transform.position;
    }

    private void Update()
    {
        if (animator == null || controller == null)
            return;

        // --- Нормализованная скорость через дельту позиции ---
        // CharacterController.Move() вызывается дважды за кадр (XZ + gravity),
        // поэтому velocity после второго вызова теряет горизонтальную составляющую.
        // Дельта позиции корневого объекта — единственный надёжный источник.
        float targetSpeed = 0f;
        var root = characterController != null ? characterController.transform : transform.parent ?? transform;
        if (Time.deltaTime > 0f)
        {
            Vector3 delta = root.position - _prevPosition;
            delta.y = 0f;
            float runSpeed = Mathf.Max(0.01f, controller.runSpeed);
            targetSpeed = Mathf.Clamp01(delta.magnitude / (Time.deltaTime * runSpeed));
        }
        _prevPosition = root.position;

        _speedSmoothed = Mathf.Lerp(_speedSmoothed, targetSpeed, Time.deltaTime * speedSmoothing);
        animator.SetFloat(SpeedHash, _speedSmoothed);

        // --- Земля / присед ---
        bool grounded = controller.IsGrounded;
        animator.SetBool(GroundedHash, grounded);
        animator.SetBool(CrouchHash, controller.IsCrouching);

        // --- Jump параметр не используется в GGMain — не триггерим ---
        _wasGrounded = grounded;
    }
}
