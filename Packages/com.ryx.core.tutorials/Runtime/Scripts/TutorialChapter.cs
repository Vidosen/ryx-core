using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Localization;

namespace Ryx.Core.Tutorials
{

[System.Serializable]
public class TutorialChapter
{
    [Tooltip("Unique identifier for the tutorial chapter")]
    public string chapterId;

    [Tooltip("Name of the tutorial chapter")]
    public LocalizedString chapterName;

    [Tooltip("Steps in this chapter")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    [Tooltip("Optional trigger that starts this chapter. If omitted, the chapter starts immediately.")]
    public TutorialTrigger startTrigger;

    [Tooltip("When true, this chapter competes with adjacent competitive chapters — all start triggers arm simultaneously and the first to fire wins.")]
    public bool competitiveStart;

    [Tooltip("Event triggered when all steps in this chapter are completed")]
    public UnityEngine.Events.UnityEvent onChapterCompleted;

    [HideInInspector]
    public bool isCompleted = false;

    [HideInInspector]
    public int currentStepIndex = -1;

    public void ResetProgress()
    {
        isCompleted = false;
        currentStepIndex = -1;
        foreach (var step in steps)
        {
            step.isCompleted = false;
        }
    }

    public bool AllStepsCompleted()
    {
        foreach (var step in steps)
        {
            if (!step.isCompleted)
                return false;
        }
        return true;
    }
}

}
