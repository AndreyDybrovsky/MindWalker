using ElmanGameDevTools.PlayerSystem;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

/// <summary>
/// Настройка сцены «PTSD in Danger»: вид сверху, плавная камера, управление без инверсии.
/// </summary>
public class PTSDInDangerSceneConfigurator : MonoBehaviour
{
    private const string SceneName = "PTSD in Danger";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (SceneManager.GetActiveScene().name != SceneName)
            return;

        GameObject host = new GameObject(nameof(PTSDInDangerSceneConfigurator));
        host.AddComponent<PTSDInDangerSceneConfigurator>();
    }

    private void Start()
    {
        Configure();
        Destroy(gameObject);
    }

    private static void Configure()
    {
        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform playerBody))
        {
            Debug.LogWarning("PTSDInDangerSceneConfigurator: не найден игрок с CharacterController.");
            return;
        }

        DisableFirstPersonControl(playerBody);

        Camera topDownCamera = FindOrCreateTopDownCamera(playerBody);
        if (topDownCamera == null)
            return;

        SetupCameraFollow(topDownCamera, playerBody);
        SetupTopDownMovement(playerBody, topDownCamera.transform);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private static void DisableFirstPersonControl(Transform playerBody)
    {
        PlayerController fpsController = playerBody.GetComponent<PlayerController>();
        if (fpsController != null)
            fpsController.enabled = false;

        Camera[] childCameras = playerBody.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < childCameras.Length; i++)
        {
            if (childCameras[i].gameObject.name == "Main Camera")
                childCameras[i].gameObject.SetActive(false);
        }
    }

    private static Camera FindOrCreateTopDownCamera(Transform playerBody)
    {
        // Камера может быть прямым потомком Player root, а не Player_Object
        Transform embedded = playerBody.Find("Camera")
            ?? playerBody.root.Find("Camera");
        Camera camera;

        if (embedded != null)
        {
            camera = embedded.GetComponent<Camera>();
            if (camera == null)
                camera = embedded.gameObject.AddComponent<Camera>();

            if (embedded.parent != null)
                embedded.SetParent(null, true);

            embedded.gameObject.SetActive(true);
            camera.enabled = true;
        }
        else
        {
            GameObject cameraObject = new GameObject("TopDownCamera");
            camera = cameraObject.AddComponent<Camera>();
            cameraObject.AddComponent<AudioListener>();
        }

        // Убеждаемся что постобработка включена на камере
        var urpData = camera.GetComponent<UniversalAdditionalCameraData>();
        if (urpData == null)
            urpData = camera.gameObject.AddComponent<UniversalAdditionalCameraData>();
        urpData.renderPostProcessing = true;

        camera.tag = "MainCamera";

        Camera[] allCameras = Object.FindObjectsByType<Camera>(FindObjectsSortMode.None);
        for (int i = 0; i < allCameras.Length; i++)
        {
            if (allCameras[i] == camera)
                continue;

            if (allCameras[i].CompareTag("MainCamera"))
                allCameras[i].tag = "Untagged";

            allCameras[i].enabled = false;
        }

        AudioListener listener = camera.GetComponent<AudioListener>();
        if (listener == null)
            camera.gameObject.AddComponent<AudioListener>();

        AudioListener[] listeners = Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None);
        for (int i = 0; i < listeners.Length; i++)
        {
            if (listeners[i].gameObject != camera.gameObject)
                listeners[i].enabled = false;
        }

        return camera;
    }

    private static void SetupCameraFollow(Camera camera, Transform playerBody)
    {
        TopDownCameraFollow follow = camera.GetComponent<TopDownCameraFollow>();
        if (follow == null)
            follow = camera.gameObject.AddComponent<TopDownCameraFollow>();

        follow.Target = playerBody;
        follow.ApplySceneDefaults();
        follow.SnapToTarget();
    }

    private static void SetupTopDownMovement(Transform playerBody, Transform cameraTransform)
    {
        TopDownPlayerMovement movement = playerBody.GetComponent<TopDownPlayerMovement>();
        if (movement == null)
            movement = playerBody.gameObject.AddComponent<TopDownPlayerMovement>();

        movement.CameraTransform = cameraTransform;
    }
}
