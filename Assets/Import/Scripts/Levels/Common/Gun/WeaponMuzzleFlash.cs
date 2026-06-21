using UnityEngine;

/// <summary>
/// Дульная вспышка: при выстреле кратко загорается Point Light у ствола.
/// Без ассетов. Вешается на вьюмодель оружия; <see cref="WeaponHandler"/> находит
/// компонент автоматически и вызывает <see cref="Flash"/>.
/// </summary>
[AddComponentMenu("Player System/Weapon Muzzle Flash")]
public class WeaponMuzzleFlash : MonoBehaviour
{
    [SerializeField] private Light flashLight;
    [SerializeField] private Vector3 localOffset = new Vector3(0f, 0f, 0.35f);
    [SerializeField] private float intensity = 3.5f;
    [SerializeField] private float range = 6f;
    [SerializeField] private float duration = 0.05f;
    [SerializeField] private Color color = new Color(1f, 0.85f, 0.5f);

    private float _t;

    private void Awake()
    {
        EnsureLight();
    }

    private void EnsureLight()
    {
        if (flashLight == null)
        {
            GameObject go = new GameObject("MuzzleFlashLight");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            flashLight = go.AddComponent<Light>();
            flashLight.type = LightType.Point;
            flashLight.shadows = LightShadows.None;
        }

        flashLight.range = range;
        flashLight.color = color;
        flashLight.enabled = false;
    }

    public void Flash()
    {
        if (flashLight == null)
            EnsureLight();

        _t = duration;
        flashLight.enabled = true;
        flashLight.intensity = intensity;
    }

    private void Update()
    {
        if (_t <= 0f)
            return;

        _t -= Time.deltaTime;
        if (_t <= 0f)
        {
            flashLight.enabled = false;
            return;
        }

        flashLight.intensity = intensity * Mathf.Clamp01(_t / duration);
    }
}
