using System;
using System.Collections.Generic;

[Serializable]
public class OCDCheckpointSaveData
{
    public int activeDayIndex = 1;
    public List<string> playedMomentIds = new List<string>();
    public string currentUnlockedMomentId = "";
}
