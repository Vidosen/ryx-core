using UnityEngine;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Ryx.Core.Tutorials
{

public class SwipeTutorialTrigger : TutorialTrigger
{
    [Header("Swipe Detection")]
    [Tooltip("Minimum distance required to detect a swipe")]
    public float minSwipeDistance = 50f;

    [Tooltip("Direction required for swipe detection")]
    public Vector2 swipeDirection = Vector2.right;

    [Tooltip("Tolerance for swipe angle (0-1)")]
    [Range(0f, 1f)]
    public float directionTolerance = 0.8f;

    private Vector2 touchStartPosition;
    private bool isTouching = false;

    protected override void DetectTriggerCompletion()
    {
        if (TutorialPointerInput.TryGetPrimaryTouch(out var touchPointer))
            DetectSwipe(touchPointer);
    }

    private void DetectSwipe(TutorialPointerState touchPointer)
    {
        if (!touchPointer.TouchPhase.HasValue)
            return;

        switch (touchPointer.TouchPhase.Value)
        {
            case InputTouchPhase.Began:
                if (IsPointerOverUI())
                {
                    isTouching = false;
                    return;
                }

                touchStartPosition = touchPointer.ScreenPosition;
                isTouching = true;
                break;

            case InputTouchPhase.Ended:
                if (isTouching)
                {
                    Vector2 swipeVector = touchPointer.ScreenPosition - touchStartPosition;
                    float swipeLength = swipeVector.magnitude;

                    if (swipeLength >= minSwipeDistance)
                    {
                        Vector2 swipeNormalized = swipeVector.normalized;
                        float dotProduct = Vector2.Dot(swipeNormalized, swipeDirection.normalized);

                        if (dotProduct >= directionTolerance)
                        {
                            TriggerComplete();
                        }
                    }
                }
                isTouching = false;
                break;

            case InputTouchPhase.Canceled:
                isTouching = false;
                break;
        }
    }
}

}
