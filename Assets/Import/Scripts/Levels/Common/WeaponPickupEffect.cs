using UnityEngine;

public class WeaponPickupEffect : MonoBehaviour
{
    [Header("Левитация")]
    [SerializeField] private float bobHeight = 0.18f;
    [SerializeField] private float bobSpeed  = 1.6f;

    [Header("Вращение")]
    [SerializeField] private Vector3 spinAxis  = Vector3.up;
    [SerializeField] private float   spinSpeed = 60f;

    private Vector3 _startPos;

    private void Start()
    {
        _startPos = transform.localPosition;
    }

    private void Update()
    {
        if (bobHeight > 0f)
        {
            Vector3 p = _startPos;
            p.y += Mathf.Sin(Time.time * bobSpeed) * bobHeight;
            transform.localPosition = p;
        }

        transform.Rotate(spinAxis, spinSpeed * Time.deltaTime, Space.World);
    }

    public void StopEffect()
    {
        enabled = false;
    }
}
