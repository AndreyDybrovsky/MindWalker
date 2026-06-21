using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// «Исправь всё»: точки FixAll, любой порядок, появляются с Дня 1.
/// После сбора всех активирует финальный <see cref="OCDMomentTrigger"/>.
/// </summary>
public class OCDFixAllCollectibleManager : MonoBehaviour
{
    [SerializeField] private OCDFixAllCollectiblePoint[] points;
    [Tooltip("Момент с нижним текстом после сбора всех точек (Require Press E или авто-зона).")]
    [SerializeField] private OCDMomentTrigger completionMoment;
    [SerializeField] private bool playCompletionOnce = true;

    [Header("Прогресс (цель сверху)")]
    [Tooltip("Ключ локализации для счётчика вида 'Исправь всё ({0}/{1})'. Пусто — прогресс не показывается.")]
    [SerializeField] private string progressObjectiveKey = "scene.ocd.fixall.progress";

    private readonly HashSet<OCDFixAllCollectiblePoint> _collectedPoints = new();
    private bool _completionActivated;

    private void Awake()
    {
        if (points == null || points.Length == 0)
        {
            // Ищем все точки в сцене — в том числе появившиеся в Day1.
            points = Object.FindObjectsByType<OCDFixAllCollectiblePoint>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
        }

        SanitizePoints();
        TryResolveCompletionMoment();
    }

    public void RegisterCollected(OCDFixAllCollectiblePoint point)
    {
        if (point == null || !_collectedPoints.Add(point))
            return;

        int required = points != null && points.Length > 0 ? points.Length : _collectedPoints.Count;
        UpdateProgressUI(_collectedPoints.Count, required);

        if (_collectedPoints.Count < required)
            return;

        ActivateCompletionMoment();
    }

    private void UpdateProgressUI(int collected, int total)
    {
        if (string.IsNullOrEmpty(progressObjectiveKey))
            return;

        OCDObjectiveUI ui = OCDObjectiveUI.GetShared();
        if (ui == null)
            return;

        ui.ShowCurrent(
            progressObjectiveKey,
            new[] { collected.ToString(), total.ToString() },
            $"Исправь всё ({collected}/{total})");
    }

    private void SanitizePoints()
    {
        if (points == null)
            return;

        for (int i = 0; i < points.Length; i++)
        {
            OCDFixAllCollectiblePoint point = points[i];
            if (point == null)
                continue;

            if (point.Manager == null)
                point.BindManager(this);

            OCDMomentTrigger[] momentTriggers = point.GetComponents<OCDMomentTrigger>();
            for (int t = 0; t < momentTriggers.Length; t++)
            {
                if (momentTriggers[t] != null && momentTriggers[t] != completionMoment)
                    momentTriggers[t].enabled = false;
            }
        }
    }

    private void TryResolveCompletionMoment()
    {
        if (completionMoment != null)
            return;

        OCDMomentTrigger[] triggers = GetComponentsInChildren<OCDMomentTrigger>(true);
        for (int i = 0; i < triggers.Length; i++)
        {
            OCDMomentTrigger trigger = triggers[i];
            if (trigger == null)
                continue;

            if (trigger.GetComponent<OCDFixAllCollectiblePoint>() != null)
                continue;

            completionMoment = trigger;
            return;
        }
    }

    private void ActivateCompletionMoment()
    {
        if (_completionActivated && playCompletionOnce)
            return;

        _completionActivated = true;

        if (completionMoment == null)
        {
            Debug.LogWarning($"OCDFixAllCollectibleManager '{name}': все точки собраны, но Completion Moment не задан.", this);
            return;
        }

        // Разблокируем и сразу проигрываем момент: надпись появляется автоматически
        // после сбора последней точки (без нажатия E).
        completionMoment.ActivateInSequence();
        completionMoment.BeginMomentSequence();
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (points == null || points.Length == 0)
            points = GetComponentsInChildren<OCDFixAllCollectiblePoint>(true);
    }
#endif
}
