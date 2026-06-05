using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Большая зона под картой: при падении игрок перезагружает текущую сцену.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Collider))]
public class FallRecoveryZone : MonoBehaviour
{
    [Tooltip("Пусто = перезагрузить активную сцену.")]
    [SerializeField] private string sceneNameOverride = "";
    [SerializeField] private float reloadCooldown = 1.25f;

    private float _lastReloadTime = -10f;
    private Collider _zoneCollider;

    private void Awake()
    {
        _zoneCollider = GetComponent<Collider>();
        if (_zoneCollider != null)
            _zoneCollider.isTrigger = true;

        EnsureTriggerRigidbody();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsPlayerCollider(other))
            TryReloadScene();
    }

    private void OnTriggerStay(Collider other)
    {
        if (IsPlayerCollider(other))
            TryReloadScene();
    }

    private void TryReloadScene()
    {
        if (Time.unscaledTime - _lastReloadTime < reloadCooldown)
            return;

        _lastReloadTime = Time.unscaledTime;
        Time.timeScale = 1f;
        Time.fixedDeltaTime = 0.02f;

        string sceneToLoad = string.IsNullOrWhiteSpace(sceneNameOverride)
            ? SceneManager.GetActiveScene().name
            : sceneNameOverride;

        SceneManager.LoadScene(sceneToLoad);
    }

    private static bool IsPlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        return other.transform.root.CompareTag("Player");
    }

    private void EnsureTriggerRigidbody()
    {
        if (!TryGetComponent(out Rigidbody rb))
            rb = gameObject.AddComponent<Rigidbody>();

        rb.isKinematic = true;
        rb.useGravity = false;
    }
}
