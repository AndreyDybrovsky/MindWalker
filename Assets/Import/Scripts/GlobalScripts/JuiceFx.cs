using UnityEngine;

/// <summary>
/// Одноразовые визуальные «вспышки» света (попадание пули, дульная вспышка-затычка).
/// Без ассетов — создаёт временный Point Light, который гаснет и самоуничтожается.
/// </summary>
public static class JuiceFx
{
    public static void SpawnFlash(Vector3 position, Color color, float intensity = 3f, float range = 4f, float duration = 0.08f)
    {
        GameObject go = new GameObject("ImpactFlash");
        go.transform.position = position;

        Light light = go.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = color;
        light.range = range;
        light.intensity = intensity;
        light.shadows = LightShadows.None;

        LightFlashFader fader = go.AddComponent<LightFlashFader>();
        fader.Init(intensity, duration);
    }

    private sealed class LightFlashFader : MonoBehaviour
    {
        private Light _light;
        private float _intensity;
        private float _duration;
        private float _t;

        public void Init(float intensity, float duration)
        {
            _light = GetComponent<Light>();
            _intensity = intensity;
            _duration = Mathf.Max(0.01f, duration);
            _t = _duration;
        }

        private void Update()
        {
            _t -= Time.deltaTime;
            if (_t <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            if (_light != null)
                _light.intensity = _intensity * (_t / _duration);
        }
    }
}
