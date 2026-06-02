using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using InputTouchPhase = UnityEngine.InputSystem.TouchPhase;

namespace Ryx.Core.Tutorials
{

internal readonly struct TutorialPointerState
{
    public TutorialPointerState(Vector2 screenPosition, int pointerId, InputTouchPhase? touchPhase = null)
    {
        ScreenPosition = screenPosition;
        PointerId = pointerId;
        TouchPhase = touchPhase;
    }

    public Vector2 ScreenPosition { get; }
    public int PointerId { get; }
    public InputTouchPhase? TouchPhase { get; }
}

internal static class TutorialPointerInput
{
    public static bool TryGetPrimaryTouch(out TutorialPointerState pointerState)
    {
        var touch = Touchscreen.current?.primaryTouch;
        if (touch == null)
        {
            pointerState = default;
            return false;
        }

        var press = touch.press;
        if (!press.isPressed && !press.wasPressedThisFrame && !press.wasReleasedThisFrame)
        {
            pointerState = default;
            return false;
        }

        pointerState = new TutorialPointerState(
            touch.position.ReadValue(),
            touch.touchId.ReadValue(),
            touch.phase.ReadValue());
        return true;
    }

    public static bool TryGetPrimaryTouchPressThisFrame(out TutorialPointerState pointerState)
    {
        if (TryGetPrimaryTouch(out pointerState) &&
            pointerState.TouchPhase == InputTouchPhase.Began &&
            Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
        {
            return true;
        }

        pointerState = default;
        return false;
    }

    public static bool TryGetPointerPressThisFrame(out TutorialPointerState pointerState)
    {
        if (TryGetPrimaryTouchPressThisFrame(out pointerState))
            return true;

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame)
        {
            pointerState = new TutorialPointerState(
                mouse.position.ReadValue(),
                mouse.deviceId > 0 ? mouse.deviceId : -1);
            return true;
        }

        pointerState = default;
        return false;
    }

    public static bool IsPointerOverUI(EventSystem eventSystem, TutorialPointerState pointerState)
    {
        if (eventSystem == null)
            return false;

        return eventSystem.IsPointerOverGameObject(pointerState.PointerId);
    }
}

}
