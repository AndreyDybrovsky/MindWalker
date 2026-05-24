using UnityEngine;

/// <summary>
/// Поиск тела игрока (CharacterController) и телепорт с учётом иерархии префаба.
/// </summary>
public static class PlayerTeleportUtility
{
    public static bool TryGetPlayerBody(out Transform body)
    {
        body = null;
        GameObject[] tagged = GameObject.FindGameObjectsWithTag("Player");
        if (tagged == null || tagged.Length == 0)
            return false;

        for (int i = 0; i < tagged.Length; i++)
        {
            if (tagged[i] != null && tagged[i].TryGetComponent(out CharacterController controller))
            {
                body = controller.transform;
                return true;
            }
        }

        for (int i = 0; i < tagged.Length; i++)
        {
            if (tagged[i] == null)
                continue;

            CharacterController inParent = tagged[i].GetComponentInParent<CharacterController>();
            if (inParent != null)
            {
                body = inParent.transform;
                return true;
            }
        }

        for (int i = 0; i < tagged.Length; i++)
        {
            if (tagged[i] == null)
                continue;

            CharacterController inChild = tagged[i].GetComponentInChildren<CharacterController>(true);
            if (inChild != null)
            {
                body = inChild.transform;
                return true;
            }
        }

        return false;
    }

    public static void TeleportTo(Transform destination, bool matchRotation, float heightOffset = 0f)
    {
        if (destination == null || !TryGetPlayerBody(out Transform body))
        {
            Debug.LogWarning("PlayerTeleportUtility: не найден игрок с CharacterController для телепорта.");
            return;
        }

        CharacterController controller = body.GetComponent<CharacterController>();
        Transform moveRoot = body.parent != null ? body.parent : body;

        Vector3 targetPosition = destination.position + destination.up * heightOffset;
        if (controller != null)
            targetPosition.y += controller.height * 0.5f;

        if (controller != null)
            controller.enabled = false;

        Vector3 delta = targetPosition - body.position;
        moveRoot.position += delta;

        if (matchRotation)
            moveRoot.rotation = destination.rotation;

        if (controller != null)
        {
            controller.enabled = true;
            controller.Move(Vector3.zero);
        }

        Physics.SyncTransforms();
    }
}
