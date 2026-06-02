using System;
using UnityEngine;

/// <summary>
/// Состояние <see cref="DepressionPanicMomentZone"/> для слота сохранения.
/// </summary>
[Serializable]
public class DepressionPanicSaveData
{
    public bool firstEncounterCompleted;
    public bool zoneLoopActive;
    public bool patientActive;
    public Vector3 patientPosition;
    public Quaternion patientRotation;
    /// <summary>0 — нет, 1 — поиск, 2 — успокоение.</summary>
    public int phase;
    public bool countdownActive;
    public float countdownRemaining;
    public string countdownLocalizationKey;
    public string countdownFallback;
    public bool waitingRespawn;
    public float respawnWaitRemaining;
}
