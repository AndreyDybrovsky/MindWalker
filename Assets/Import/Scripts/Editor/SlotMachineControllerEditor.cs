#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SlotMachineController))]
public class SlotMachineControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Добавьте компонент на корень префаба или нажмите «Пересобрать якоря».\n" +
            "• MachineFront — лицо автомата; PlayerStandPoint — куда подводится игрок.\n" +
            "• LureZone — оранжевый бокс (Gizmos): зона входа.\n" +
            "• Буквы QTE — общий UI на экране, не Canvas на префабе.\n" +
            "• Героя уносит далеко — сдвиньте PlayerStandPoint или увеличьте Max Lure Distance.",
            MessageType.Info);

        DrawDefaultInspector();

        SlotMachineController controller = (SlotMachineController)target;
        EditorGUILayout.Space(8f);

        if (GUILayout.Button("Пересобрать якоря"))
        {
            Undo.RecordObject(controller, "Rebuild SlotMachine anchors");
            controller.ApplyEditorPrefabLayout();
            EditorUtility.SetDirty(controller);
        }
    }
}
#endif
