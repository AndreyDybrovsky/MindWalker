using System.Collections;
using UnityEngine;

/// <summary>
/// Триггер-зона для смены ambient-музыки в Bipolar.
/// При входе игрока — плавный переход на свой клип, при выходе — возврат к defaultClip контроллера.
/// </summary>
[RequireComponent(typeof(Collider))]
public class BipolarAmbientZone : MonoBehaviour
{
    [SerializeField] private AudioClip zoneClip;
    [SerializeField] private float crossfadeDuration = 1.8f;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!IsPlayer(other)) return;
        BipolarAmbientController.Instance?.CrossfadeTo(zoneClip, crossfadeDuration);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsPlayer(other)) return;
        BipolarAmbientController.Instance?.CrossfadeToDefault(crossfadeDuration);
    }

    private static bool IsPlayer(Collider col) =>
        col.CompareTag("Player") || col.transform.root.CompareTag("Player");

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.25f);
        Collider col = GetComponent<Collider>();
        if (col != null)
            Gizmos.DrawCube(col.bounds.center, col.bounds.size);
        Gizmos.color = new Color(0.4f, 0.8f, 1f, 0.8f);
        if (col != null)
            Gizmos.DrawWireCube(col.bounds.center, col.bounds.size);
    }
#endif
}
