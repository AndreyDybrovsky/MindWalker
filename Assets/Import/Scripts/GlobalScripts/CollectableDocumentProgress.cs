using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Прогресс по 6 документам (по одному на уровень). Хранится в customData сохранения.
/// </summary>
public static class CollectableDocumentProgress
{
    public const string SaveKey = "collectable_documents";
    public const int TotalDocuments = 6;

    private static readonly HashSet<string> Collected = new HashSet<string>();

    public static int CollectedCount => Collected.Count;

    public static bool AllCollected => Collected.Count >= TotalDocuments;

    public static bool IsCollected(string documentId)
    {
        return !string.IsNullOrWhiteSpace(documentId) && Collected.Contains(documentId);
    }

    public static void MarkCollected(string documentId)
    {
        if (string.IsNullOrWhiteSpace(documentId))
            return;

        if (!Collected.Add(documentId))
            return;

        WriteToActiveSave();
    }

    public static void LoadFromSave(GameSaveData saveData)
    {
        Collected.Clear();

        if (saveData == null)
            return;

        if (!SaveGameCustomDataUtility.TryRead(saveData, SaveKey, out CollectableDocumentSaveData data)
            || data.collectedIds == null)
        {
            return;
        }

        for (int i = 0; i < data.collectedIds.Count; i++)
        {
            string id = data.collectedIds[i];
            if (!string.IsNullOrWhiteSpace(id))
                Collected.Add(id);
        }
    }

    public static void WriteToSave(GameSaveData saveData)
    {
        if (saveData == null)
            return;

        var data = new CollectableDocumentSaveData
        {
            collectedIds = new List<string>(Collected)
        };

        SaveGameCustomDataUtility.Write(saveData, SaveKey, data);
    }

    public static void ClearForNewGame()
    {
        Collected.Clear();
    }

    public static void EnsureLoadedFromActiveSave()
    {
        if (SaveManager.Instance == null)
            return;

        int slot = SaveManager.Instance.GetCurrentSaveSlot();
        if (slot < 0)
            return;

        LoadFromSave(SaveManager.Instance.GetSaveData(slot));
    }

    public static void RefreshSceneDocuments()
    {
        CollectableDocument[] documents = Object.FindObjectsByType<CollectableDocument>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        for (int i = 0; i < documents.Length; i++)
        {
            CollectableDocument doc = documents[i];
            if (doc == null || !doc.isActiveAndEnabled)
                continue;

            if (IsCollected(doc.ResolveDocumentId()))
                doc.gameObject.SetActive(false);
        }
    }

    private static void WriteToActiveSave()
    {
        if (SaveManager.Instance == null)
            return;

        int slot = SaveManager.Instance.GetCurrentSaveSlot();
        if (slot < 0)
            return;

        GameSaveData saveData = SaveManager.Instance.GetSaveData(slot);
        if (saveData == null || saveData.IsEmpty())
            return;

        WriteToSave(saveData);
        SaveManager.Instance.SaveGame(slot, saveData);
    }
}
