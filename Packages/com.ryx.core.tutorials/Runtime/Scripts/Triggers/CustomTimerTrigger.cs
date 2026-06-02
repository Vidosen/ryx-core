using UnityEngine;

namespace Ryx.Core.Tutorials
{

public class CustomTimerTrigger : TutorialTrigger
{
    [Header("Timer Settings")]
    [Tooltip("Time in seconds before trigger completes automatically")]
    public float triggerDelay = 5.0f;

    private float timer = 0f;
    private bool timerActive = false;

    protected override void OnArmed()
    {
        timer = 0f;
        timerActive = true;
    }

    protected override void DetectTriggerCompletion()
    {
        if (timerActive)
        {
            timer += Time.deltaTime;
            if (timer >= triggerDelay)
            {
                TriggerComplete();
                timerActive = false;
            }
        }
    }

    public override void ResetTrigger()
    {
        base.ResetTrigger();
        timer = 0f;
        timerActive = false;
    }
}

}
