using UnityEngine;

/// <summary>
/// «Врата взросления»: при первом проходе игрока плавно переводит
/// <see cref="AutismMaturationController"/> на следующий (или заданный) день.
///
/// Расставь их вдоль пути уровня — тогда само путешествие по летающим островам
/// становится взрослением: с каждым отрезком мир теряет сказочность и становится
/// всё более реальным.
/// </summary>
[RequireComponent(typeof(Collider))]
public class AutismMaturationGate : MonoBehaviour
{
    [Tooltip("Если > 0 — перейти именно к этому дню. Иначе просто +1 день.")]
    [SerializeField] private int targetDay = 0;
    [SerializeField] private string playerTag = "Player";

    private bool _used;

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (_used) return;
        if (!other.CompareTag(playerTag) && !other.transform.root.CompareTag(playerTag))
            return;

        _used = true;

        var ctrl = AutismMaturationController.Instance;
        if (ctrl == null) return;

        if (targetDay > 0)
            ctrl.SetDaySmooth(targetDay);
        else
            ctrl.AdvanceDay();
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider box)
        {
            Gizmos.DrawCube(box.center, box.size);
            Gizmos.color = new Color(0.6f, 0.4f, 1f, 0.9f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
#endif
}
