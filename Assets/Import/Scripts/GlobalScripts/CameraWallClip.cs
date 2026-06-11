using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Уменьшает nearClipPlane камеры когда она близко к геометрии,
/// предотвращая просвечивание стен и моделей насквозь.
/// Авто-устанавливается на Camera.main в игровых сценах.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraWallClip : MonoBehaviour
{
    [SerializeField] private float nearDefault = 0.08f;
    [SerializeField] private float nearWall    = 0.01f;
    [SerializeField] private float checkRadius = 0.12f;
    [SerializeField] private LayerMask wallMask = ~0;

    private Camera _cam;

    private static readonly string[] MenuScenes = { "MainMenu", "Victory", "TrueVictory" };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoInstall()
    {
        string sceneName = SceneManager.GetActiveScene().name;
        foreach (string s in MenuScenes)
            if (sceneName == s) return;

        Camera main = Camera.main;
        if (main != null && main.GetComponent<CameraWallClip>() == null)
            main.gameObject.AddComponent<CameraWallClip>();
    }

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        _cam.nearClipPlane = nearDefault;
    }

    private void LateUpdate()
    {
        bool near = Physics.CheckSphere(transform.position, checkRadius, wallMask, QueryTriggerInteraction.Ignore);
        _cam.nearClipPlane = near ? nearWall : nearDefault;
    }
}
