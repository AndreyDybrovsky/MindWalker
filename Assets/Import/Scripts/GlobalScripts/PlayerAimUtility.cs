using UnityEngine;

/// <summary>
/// Единая точка прицеливания по телу игрока (CharacterController), не по камере.
/// </summary>
public static class PlayerAimUtility
{
    public const float DefaultHeightFallback = 1.05f;

  /// <summary>Доля высоты капсулы ниже центра — грудь, не голова.</summary>
    public const float ChestOffsetBelowCenter = 0.35f;

    public static bool TryGetPlayerAimPoint(out Vector3 aimPoint)
    {
        aimPoint = default;
        if (!PlayerTeleportUtility.TryGetPlayerBody(out Transform body))
            return false;

        aimPoint = GetAimPoint(body);
        return true;
    }

    public static Vector3 GetAimPoint(Transform body)
    {
        if (body == null)
            return Vector3.zero;

        if (body.TryGetComponent(out CharacterController controller))
            return GetAimPointFromController(body, controller);

        CharacterController child = body.GetComponentInChildren<CharacterController>(true);
        if (child != null)
            return GetAimPointFromController(child.transform, child);

        return body.position + Vector3.up * DefaultHeightFallback;
    }

    private static Vector3 GetAimPointFromController(Transform body, CharacterController controller)
    {
        Vector3 centerWorld = body.TransformPoint(controller.center);
        float drop = Mathf.Clamp(controller.height * ChestOffsetBelowCenter, 0.15f, controller.height * 0.45f);
        return centerWorld - Vector3.up * drop;
    }
}
