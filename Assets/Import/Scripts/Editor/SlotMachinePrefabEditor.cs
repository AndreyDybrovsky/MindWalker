#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Настройка префаба SlotMachine: якоря, триггер, сброс позиции корня.
/// </summary>
public static class SlotMachinePrefabEditor
{
    private const string PrefabPath = "Assets/Import/Prefabs/SlotMachine.prefab";

    [MenuItem("Tools/Gambling/Настроить префаб SlotMachine")]
    public static void SetupSlotMachinePrefab()
    {
        GameObject root = PrefabUtility.LoadPrefabContents(PrefabPath);
        if (root == null)
        {
            EditorUtility.DisplayDialog("SlotMachine", $"Не найден префаб:\n{PrefabPath}", "OK");
            return;
        }

        SlotMachineController controller = root.GetComponent<SlotMachineController>();
        if (controller == null)
            controller = root.AddComponent<SlotMachineController>();

        controller.ApplyEditorPrefabLayout();

        PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        PrefabUtility.UnloadPrefabContents(root);

        EditorUtility.DisplayDialog(
            "SlotMachine",
            "Готово.\n\n" +
            "• Корень префаба: позиция (0,0,0)\n" +
            "• MachineFront — «лицо» автомата\n" +
            "• PlayerStandPoint — куда подводится игрок\n" +
            "• LureZone — оранжевая зона в Scene (Gizmos)\n\n" +
            "На сцене Gambling disease: перетащите префаб, поверните к игроку, подстройте Lure Box / Stand Local Offset при необходимости.",
            "OK");
    }
}
#endif
