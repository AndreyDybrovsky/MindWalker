using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// При касании с игроком плавно переключает веса трёх Post-Processing Volume.
/// Используй 3 глобальных Volume с разными профилями в сцене и назначь их в поля ниже.
/// </summary>
public class PostProcessingTrigger : MonoBehaviour
{
    [Header("Три Post-Processing Volume")]
    [SerializeField] private Volume volume1;
    [SerializeField] private Volume volume2;
    [SerializeField] private Volume volume3;

    [Header("Целевые веса при входе (0–1)")]
    [Range(0f, 1f)] [SerializeField] private float enterWeight1 = 1f;
    [Range(0f, 1f)] [SerializeField] private float enterWeight2 = 0f;
    [Range(0f, 1f)] [SerializeField] private float enterWeight3 = 0f;

    [Header("Переход")]
    [SerializeField] private float transitionDuration = 1.5f;
    [Tooltip("Вернуть веса к исходным при выходе из триггера")]
    [SerializeField] private bool reverseOnExit = true;
    [SerializeField] private string playerTag = "Player";

    private Volume[] _vols;
    private float[]  _originalWeights = new float[3];
    private Coroutine _active;

    private void Awake()
    {
        _vols = new[] { volume1, volume2, volume3 };
        for (int i = 0; i < 3; i++)
            _originalWeights[i] = _vols[i] != null ? _vols[i].weight : 0f;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(playerTag)) return;
        Transition(enterWeight1, enterWeight2, enterWeight3);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!reverseOnExit || !other.CompareTag(playerTag)) return;
        Transition(_originalWeights[0], _originalWeights[1], _originalWeights[2]);
    }

    private void Transition(float w1, float w2, float w3)
    {
        if (_active != null) StopCoroutine(_active);
        _active = StartCoroutine(DoTransition(w1, w2, w3));
    }

    private IEnumerator DoTransition(float w1, float w2, float w3)
    {
        float[] from = new float[3];
        float[] to   = { w1, w2, w3 };

        for (int i = 0; i < 3; i++)
            from[i] = _vols[i] != null ? _vols[i].weight : 0f;

        float elapsed = 0f;
        while (elapsed < transitionDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / transitionDuration));
            for (int i = 0; i < 3; i++)
                if (_vols[i] != null) _vols[i].weight = Mathf.Lerp(from[i], to[i], t);
            yield return null;
        }

        for (int i = 0; i < 3; i++)
            if (_vols[i] != null) _vols[i].weight = to[i];
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        var col = GetComponent<Collider>();
        if (col == null) return;
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.25f);
        Gizmos.matrix = transform.localToWorldMatrix;
        if (col is BoxCollider box)
            Gizmos.DrawCube(box.center, box.size);
        else if (col is SphereCollider sphere)
            Gizmos.DrawSphere(sphere.center, sphere.radius);
        Gizmos.color = new Color(0.2f, 0.8f, 1f, 0.8f);
        if (col is BoxCollider b2)
            Gizmos.DrawWireCube(b2.center, b2.size);
    }
#endif
}
