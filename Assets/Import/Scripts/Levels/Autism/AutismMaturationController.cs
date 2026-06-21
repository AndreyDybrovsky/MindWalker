using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Единый глобальный цветовой грейдинг для уровня Autism.
/// Создаёт один Volume с фиксированными настройками (тёплое золото + небесная лазурь).
/// Система дней удалена — пост-обработка одинакова во всём уровне.
/// </summary>
public class AutismMaturationController : MonoBehaviour
{
    public static AutismMaturationController Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;

        BuildVolume();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    // Заглушки для AutismMaturationGate — больше ничего не делают.
    public void SetDayImmediate(int day) { }
    public void SetDaySmooth(int day)    { }
    public void AdvanceDay()             { }

    private void BuildVolume()
    {
        var go = new GameObject("Maturation_Fixed");
        go.transform.SetParent(transform, false);

        var vol = go.AddComponent<Volume>();
        vol.isGlobal = true;
        vol.priority = 21;
        vol.weight   = 1f;
        vol.profile  = BuildProfile();
    }

    private static VolumeProfile BuildProfile()
    {
        var profile = ScriptableObject.CreateInstance<VolumeProfile>();

        var ca = profile.Add<ColorAdjustments>(true);
        ca.active = true;
        ca.postExposure.Override(-0.2f);
        ca.contrast.Override(5f);
        ca.colorFilter.Override(new Color(1.00f, 0.97f, 0.90f));
        ca.saturation.Override(22f);
        ca.hueShift.Override(3f);

        var wb = profile.Add<WhiteBalance>(true);
        wb.active = true;
        wb.temperature.Override(8f);
        wb.tint.Override(0f);

        var st = profile.Add<SplitToning>(true);
        st.active = true;
        st.shadows.Override(new Color(0.28f, 0.52f, 0.88f));
        st.highlights.Override(new Color(1.00f, 0.90f, 0.65f));
        st.balance.Override(8f);

        var tm = profile.Add<Tonemapping>(true);
        tm.active = true;
        tm.mode.Override(TonemappingMode.Neutral);

        return profile;
    }
}
