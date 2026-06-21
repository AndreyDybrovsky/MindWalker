using UnityEngine;

/// <summary>
/// Оживляет вьюмодель оружия от первого лица:
///  • «дыхание» в покое — оружие мягко покачивается, будто его держит живой человек;
///  • инерция (sway) при повороте камеры — оружие чуть запаздывает за обзором;
///  • покачивание (bob) при ходьбе/беге;
///  • отдача (recoil) при выстреле — резкий толчок назад-вверх с возвратом.
///
/// Вешается на трансформ ВИДИМОЙ модели оружия (дочерний к Main Camera, например «Tools»
/// или сам объект пистолета). Перемещение игрока им не управляется — только локальная
/// анимация относительно стартовой позы.
/// <see cref="WeaponHandler"/> автоматически находит компонент и вызывает <see cref="AddRecoil"/>.
/// </summary>
[AddComponentMenu("Player System/Weapon Viewmodel Motion")]
public class WeaponViewmodelMotion : MonoBehaviour
{
    [Header("Дыхание (покой)")]
    [SerializeField] private float breatheAmplitude = 0.0045f;
    [SerializeField] private float breatheSpeed = 1.6f;

    [Header("Инерция от поворота камеры (sway)")]
    [SerializeField] private float swayPositionAmount = 0.02f;
    [SerializeField] private float swayRotationAmount = 3.5f;
    [SerializeField] private float swayMaxOffset = 0.06f;
    [SerializeField] private float swaySmooth = 9f;

    [Header("Покачивание при ходьбе (bob)")]
    [SerializeField] private float bobAmount = 0.012f;
    [SerializeField] private float bobSpeed = 9f;
    [SerializeField] private float bobSmooth = 8f;
    [SerializeField] private float movingSpeedThreshold = 0.6f;

    [Header("Отдача (выстрел)")]
    [SerializeField] private float recoilKickBack = 0.045f;
    [SerializeField] private float recoilKickUp = 6f;
    [SerializeField] private float recoilRandomYaw = 2f;
    [SerializeField] private float recoilReturnSpeed = 9f;
    [SerializeField] private float recoilSnappiness = 14f;

    private Vector3 _initialLocalPos;
    private Quaternion _initialLocalRot;

    private Vector3 _swayPosOffset;
    private Vector3 _bobPosOffset;
    private float _bobTimer;

    private Vector3 _recoilPosCurrent, _recoilPosTarget;
    private Vector3 _recoilRotCurrent, _recoilRotTarget;

    private CharacterController _playerController;

    private void Awake()
    {
        _initialLocalPos = transform.localPosition;
        _initialLocalRot = transform.localRotation;
        _playerController = GetComponentInParent<CharacterController>();
    }

    /// <summary>Толчок отдачи — вызывается из <see cref="WeaponHandler"/> при выстреле.</summary>
    public void AddRecoil()
    {
        _recoilPosTarget += new Vector3(0f, 0f, -recoilKickBack);
        _recoilRotTarget += new Vector3(-recoilKickUp, Random.Range(-recoilRandomYaw, recoilRandomYaw), 0f);
    }

    private void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f)
            return;

        // --- Дыхание ---
        Vector3 breathe = new Vector3(
            Mathf.Sin(Time.time * breatheSpeed * 0.8f) * breatheAmplitude,
            Mathf.Sin(Time.time * breatheSpeed) * breatheAmplitude,
            0f);

        // --- Sway от мыши (только когда игрок управляет обзором) ---
        Quaternion swayRot = Quaternion.identity;
        Vector3 targetSwayPos = Vector3.zero;
        if (!GameplayInputBlocker.IsBlocked)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            targetSwayPos = new Vector3(
                Mathf.Clamp(-mouseX * swayPositionAmount, -swayMaxOffset, swayMaxOffset),
                Mathf.Clamp(-mouseY * swayPositionAmount, -swayMaxOffset, swayMaxOffset),
                0f);

            swayRot = Quaternion.Euler(
                mouseY * swayRotationAmount,
                -mouseX * swayRotationAmount,
                -mouseX * swayRotationAmount * 0.5f);
        }
        _swayPosOffset = Vector3.Lerp(_swayPosOffset, targetSwayPos, dt * swaySmooth);

        // --- Bob при движении ---
        Vector3 targetBob = Vector3.zero;
        float horizontalSpeed = 0f;
        if (_playerController != null)
        {
            Vector3 v = _playerController.velocity;
            v.y = 0f;
            horizontalSpeed = v.magnitude;
        }

        bool moving = horizontalSpeed > movingSpeedThreshold
                      && (_playerController == null || _playerController.isGrounded)
                      && !GameplayInputBlocker.IsBlocked;
        if (moving)
        {
            _bobTimer += dt * bobSpeed;
            targetBob = new Vector3(
                Mathf.Cos(_bobTimer) * bobAmount,
                Mathf.Abs(Mathf.Sin(_bobTimer)) * bobAmount,
                0f);
        }
        else
        {
            _bobTimer = 0f;
        }
        _bobPosOffset = Vector3.Lerp(_bobPosOffset, targetBob, dt * bobSmooth);

        // --- Отдача: target плавно возвращается к нулю, current «догоняет» target ---
        _recoilPosTarget = Vector3.Lerp(_recoilPosTarget, Vector3.zero, dt * recoilReturnSpeed);
        _recoilPosCurrent = Vector3.Lerp(_recoilPosCurrent, _recoilPosTarget, dt * recoilSnappiness);
        _recoilRotTarget = Vector3.Lerp(_recoilRotTarget, Vector3.zero, dt * recoilReturnSpeed);
        _recoilRotCurrent = Vector3.Lerp(_recoilRotCurrent, _recoilRotTarget, dt * recoilSnappiness);

        // --- Применяем относительно стартовой позы ---
        transform.localPosition = _initialLocalPos + breathe + _swayPosOffset + _bobPosOffset + _recoilPosCurrent;
        transform.localRotation = _initialLocalRot * swayRot * Quaternion.Euler(_recoilRotCurrent);
    }
}
