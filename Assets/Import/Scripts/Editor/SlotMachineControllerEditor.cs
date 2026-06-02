#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(SlotMachineController))]
public class SlotMachineControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        EditorGUILayout.HelpBox(
            "Настройка:\n" +
            "1) Tools → Gambling → Настроить префаб SlotMachine.\n" +
            "2) В префабе под Object есть PlayerPoint (переименуется в PlayerStandPoint) — перетащите перед экраном автомата.\n" +
            "3) LureZone — оранжевый бокс в Gizmos (зона входа).\n" +
            "4) Буквы QTE — на экране по центру (не Canvas на префабе).\n" +
            "5) Если героя уносит далеко — PlayerPoint стоит не там или дальше Max Lure Distance.",
            MessageType.Info);

        DrawDefaultInspector();

        SlotMachineController controller = (SlotMachineController)target;
        EditorGUILayout.Space(8f);

        if (GUILayout.Button("Пересобрать якоря (как в меню Tools)"))
        {
            Undo.RecordObject(controller, "Rebuild SlotMachine anchors");
            controller.ApplyEditorPrefabLayout();
            EditorUtility.SetDirty(controller);
        }
    }
}
#endif
