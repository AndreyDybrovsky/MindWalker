using System.Collections;
using UnityEngine;

/// <summary>
/// Маркер фантома босса. Не имеет здоровья — мерцает при попаданиях.
/// </summary>
public class BossPhantomMark : MonoBehaviour
{
    [SerializeField] private float flickerDuration = 0.28f;
    [SerializeField] private int flickerCount = 5;

    private Renderer[] _renderers;
    private bool _isFlickering;

    private void Awake()
    {
        _renderers = GetComponentsInChildren<Renderer>(true);
    }

    public void OnBulletHit()
    {
        if (!_isFlickering)
            StartCoroutine(FlickerRoutine());
    }

    private IEnumerator FlickerRoutine()
    {
        _isFlickering = true;
        float interval = flickerDuration / (flickerCount * 2);
        for (int i = 0; i < flickerCount; i++)
        {
            SetRenderersEnabled(false);
            yield return new WaitForSeconds(interval);
            SetRenderersEnabled(true);
            yield return new WaitForSeconds(interval);
        }
        SetRenderersEnabled(true);
        _isFlickering = false;
    }

    private void SetRenderersEnabled(bool state)
    {
        foreach (var r in _renderers)
            if (r != null) r.enabled = state;
    }
}
