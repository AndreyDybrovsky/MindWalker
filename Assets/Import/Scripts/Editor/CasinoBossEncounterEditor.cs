#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

/// <summary>
/// Помощник настройки финальной встречи с боссом на уровне Gambling disease.
/// </summary>
public static class CasinoBossEncounterEditor
{
    [MenuItem("Tools/Gambling/Создать триггер босса казино")]
    public static void CreateEncounterTrigger()
    {
        GameObject root = new GameObject("CasinoBossEncounter");
        BoxCollider box = root.AddComponent<BoxCollider>();
        box.isTrigger = true;
        box.size = new Vector3(6f, 4f, 6f);
        box.center = new Vector3(0f, 2f, 0f);

        root.AddComponent<Rigidbody>().isKinematic = true;
        root.AddComponent<CasinoChoicePresenter>();
        CasinoBossEncounterTrigger trigger = root.AddComponent<CasinoBossEncounterTrigger>();

        Selection.activeGameObject = root;
        EditorGUIUtility.PingObject(root);

        Debug.Log(
            "Создан CasinoBossEncounter. Назначьте в инспекторе:\n" +
            "• Boss Look Target — точка на модели босса (BossModels/Boss)\n" +
            "• Fortune Wheel — колесо с FortuneWheelController\n" +
            "• Wheel Spin Zone — триггер E на колесе (FortuneWheelSpinZone)\n" +
            "• Boss Spawn Manager — на Player/Managers (BossSpawnManager)\n" +
            "• Scene Boss Prop — декоративная модель босса до боя\n" +
            "• На Player: UI/CasinoButton с кнопками Yes и No (или будет запасной UI).",
            trigger);
    }
}
#endif
