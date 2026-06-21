using UnityEngine;

/// <summary>
/// Ставится на вход в Location3. При первом заходе игрока показывает задание сбора таблеток.
/// </summary>
[RequireComponent(typeof(Collider))]
public class PillQuestActivateTrigger : MonoBehaviour
{
    private bool _activated;

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
        if (_activated) return;
        if (!other.CompareTag("Player") && !other.transform.root.CompareTag("Player")) return;

        _activated = true;
        PillCollectionManager.ShowQuest();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.3f);
        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif
}
