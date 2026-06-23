using UnityEngine;

/// <summary>
/// Временно уменьшает радиус CharacterController пока игрок находится внутри триггера.
/// Используется для узких дверных проёмов, через которые игрок иначе не проходит.
/// Повесить на BoxCollider (isTrigger), охватывающий дверной проём.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DoorwayPassthrough : MonoBehaviour
{
    [Tooltip("Радиус CharacterController внутри проёма (должен быть меньше половины ширины проёма).")]
    [SerializeField] private float narrowRadius = 0.18f;

    private float _originalRadius;
    private CharacterController _cc;
    private bool _isInside;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;

        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_isInside || !IsPlayer(other)) return;

        _cc = other.GetComponentInParent<CharacterController>()
            ?? FindFirstObjectByType<CharacterController>(FindObjectsInactive.Exclude);
        if (_cc == null) return;

        _originalRadius = _cc.radius;
        _cc.radius = narrowRadius;
        _isInside = true;
    }

    private void OnTriggerExit(Collider other)
    {
        if (!_isInside || !IsPlayer(other)) return;
        Restore();
    }

    private void OnDisable()
    {
        if (_isInside) Restore();
    }

    private void Restore()
    {
        if (_cc != null)
            _cc.radius = _originalRadius;
        _cc = null;
        _isInside = false;
    }

    private static bool IsPlayer(Collider col)
    {
        if (col.CompareTag("Player")) return true;
        return col.transform.root.CompareTag("Player");
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.4f);
        Collider c = GetComponent<Collider>();
        if (c != null)
            Gizmos.DrawWireCube(c.bounds.center, c.bounds.size);
    }
#endif
}
