using UnityEngine;

namespace Ryx.Core.Tutorials
{

public class TouchTutorialTrigger : TutorialTrigger
{
    protected override void DetectTriggerCompletion()
    {
        if (TutorialPointerInput.TryGetPrimaryTouchPressThisFrame(out _) && !IsPointerOverUI())
        {
            TriggerComplete();
        }
    }
}

}
