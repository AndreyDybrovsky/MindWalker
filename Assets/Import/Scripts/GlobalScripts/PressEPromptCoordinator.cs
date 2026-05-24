using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Одна подсказка PressEText на сцену — объединяет вклад всех триггеров и зон E.
/// </summary>
public static class PressEPromptCoordinator
{
    private static readonly List<IPressEPromptContributor> Contributors = new();

    public static void Register(IPressEPromptContributor contributor)
    {
        if (contributor != null && !Contributors.Contains(contributor))
            Contributors.Add(contributor);
    }

    public static void Unregister(IPressEPromptContributor contributor)
    {
        Contributors.Remove(contributor);
    }

    public static void Refresh()
    {
        PressEPromptView view = PressEPromptUtility.AcquireSharedPrompt();
        if (view == null)
            return;

        float maxReveal = 0f;
        bool anyVisible = false;

        for (int i = Contributors.Count - 1; i >= 0; i--)
        {
            IPressEPromptContributor contributor = Contributors[i];
            if (contributor == null)
            {
                Contributors.RemoveAt(i);
                continue;
            }

            if (!contributor.IsPressPromptVisible)
                continue;

            anyVisible = true;
            maxReveal = Mathf.Max(maxReveal, contributor.PressPromptReveal);
        }

        if (anyVisible)
        {
            if (!view.gameObject.activeSelf)
                view.gameObject.SetActive(true);

            view.SetReveal(maxReveal);
        }
        else
        {
            view.SetReveal(0f);
            if (view.gameObject.activeSelf)
                view.gameObject.SetActive(false);
        }
    }
}

public interface IPressEPromptContributor
{
    float PressPromptReveal { get; }
    bool IsPressPromptVisible { get; }
}
