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
<<<<<<< HEAD
=======
        IPressEPromptContributor bestContributor = null;
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)

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
<<<<<<< HEAD
            maxReveal = Mathf.Max(maxReveal, contributor.PressPromptReveal);
=======
            if (contributor.PressPromptReveal >= maxReveal)
            {
                maxReveal = contributor.PressPromptReveal;
                bestContributor = contributor;
            }
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
        }

        if (anyVisible)
        {
            if (!view.gameObject.activeSelf)
                view.gameObject.SetActive(true);

<<<<<<< HEAD
=======
            bestContributor?.ApplySharedPressPromptText(view);
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
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
<<<<<<< HEAD
=======
    void ApplySharedPressPromptText(PressEPromptView view);
>>>>>>> 1d5712d3 (Чистый коммит без громадного файла)
}
