using System;
using System.Collections.Generic;

[Serializable]
public class GameStatsData
{
    public float totalPlayTimeSeconds;
    public float damageReceived;
    public float damageDealt;
    public int enemiesKilled;
    public int slotMachineCaught;
    public int patientCalmedCount;

    // Параллельные списки: имя сцены → время прохождения (секунды)
    public List<string> levelNames      = new List<string>();
    public List<float>  levelTimesSec   = new List<float>();
}
