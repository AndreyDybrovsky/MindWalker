using System.Collections;
using UnityEngine;

/// <summary>
/// Микро-заморозка времени (hit stop) для «веса» удара — например при смерти врага.
/// Кратковременно опускает Time.timeScale и возвращает обратно. Не срабатывает, если
/// игра уже на паузе (timeScale ≈ 0).
/// </summary>
public static class HitStopUtility
{
    private sealed class Runner : MonoBehaviour { }
    private static Runner _runner;
    private static bool _active;

    private static void Ensure()
    {
        if (_runner != null)
            return;

        GameObject go = new GameObject("HitStopRunner");
        Object.DontDestroyOnLoad(go);
        _runner = go.AddComponent<Runner>();
    }

    /// <param name="duration">Длительность в реальном времени (сек), напр. 0.05.</param>
    /// <param name="timeScale">Масштаб времени во время заморозки (0 — полная пауза).</param>
    public static void Do(float duration, float timeScale = 0f)
    {
        if (_active)
            return;
        if (Time.timeScale <= 0.01f) // игра на паузе — не трогаем
            return;

        Ensure();
        _runner.StartCoroutine(Routine(duration, Mathf.Clamp01(timeScale)));
    }

    private static IEnumerator Routine(float duration, float scale)
    {
        _active = true;
        Time.timeScale = scale;

        yield return new WaitForSecondsRealtime(duration);

        // Восстанавливаем только если масштаб всё ещё «наш» (паузу не трогаем).
        if (Time.timeScale <= scale + 0.001f)
            Time.timeScale = 1f;

        _active = false;
    }
}
