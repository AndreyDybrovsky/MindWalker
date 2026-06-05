using UnityEngine;

/// <summary>
/// Включает один корень дня (Day1, Day2…) на старте. При активации следующего момента включает нужный день.
/// </summary>
[DefaultExecutionOrder(-500)]
public class OCDMissionDayController : MonoBehaviour
{
    public static OCDMissionDayController Instance { get; private set; }

    [SerializeField] private GameObject[] dayRoots;
    [SerializeField, Min(1)] private int activeDayIndex = 1;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        TryAutoFindDayRoots();
        ApplyActiveDay(activeDayIndex);
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    public int GetActiveDayIndex() => activeDayIndex;

    public void SetActiveDayIndex(int index) => ApplyActiveDay(index);

    public void ActivateDayContaining(Component moment)
    {
        if (moment == null || dayRoots == null)
            return;

        Transform momentTransform = moment.transform;
        for (int i = 0; i < dayRoots.Length; i++)
        {
            GameObject root = dayRoots[i];
            if (root == null)
                continue;

            if (momentTransform == root.transform || momentTransform.IsChildOf(root.transform))
            {
                ApplyActiveDay(i + 1);
                return;
            }
        }
    }

    private void TryAutoFindDayRoots()
    {
        if (dayRoots != null && dayRoots.Length > 0)
            return;

        Transform mission = transform;
        if (!string.Equals(mission.name, "Mission", System.StringComparison.OrdinalIgnoreCase))
        {
            GameObject missionGo = GameObject.Find("Mission");
            if (missionGo != null)
                mission = missionGo.transform;
        }

        if (mission == null)
            return;

        var found = new System.Collections.Generic.List<GameObject>(4);
        for (int day = 1; day <= 4; day++)
        {
            Transform dayRoot = mission.Find($"Day{day}");
            if (dayRoot != null)
                found.Add(dayRoot.gameObject);
        }

        if (found.Count > 0)
            dayRoots = found.ToArray();
    }

    private void ApplyActiveDay(int dayIndex)
    {
        if (dayRoots == null || dayRoots.Length == 0)
            return;

        int index = Mathf.Clamp(dayIndex, 1, dayRoots.Length) - 1;
        activeDayIndex = index + 1;

        for (int i = 0; i < dayRoots.Length; i++)
        {
            if (dayRoots[i] != null)
                dayRoots[i].SetActive(i == index);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        TryAutoFindDayRoots();
    }
#endif
}
