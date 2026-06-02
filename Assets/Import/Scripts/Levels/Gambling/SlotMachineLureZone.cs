using UnityEngine;

/// <summary>
/// Триггер перед автоматом — запускает притяжение игрока.
/// </summary>
[RequireComponent(typeof(Collider))]
public class SlotMachineLureZone : MonoBehaviour
{
    [SerializeField] private SlotMachineController machine;

    private void Awake()
    {
        if (machine == null)
            machine = GetComponentInParent<SlotMachineController>();

        Collider zoneCollider = GetComponent<Collider>();
        if (zoneCollider != null)
            zoneCollider.isTrigger = true;

        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (machine == null || !IsPlayerCollider(other))
            return;

        machine.TryStartLure(other.transform);
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        return other.transform.root.CompareTag("Player");
    }
}
