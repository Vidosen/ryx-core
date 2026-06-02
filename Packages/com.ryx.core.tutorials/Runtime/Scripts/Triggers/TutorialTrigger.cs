using System;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Ryx.Core.Tutorials
{

public abstract class TutorialTrigger : MonoBehaviour
{
    [Header("Events")]
    [Tooltip("Event invoked when tutorial condition is met")]
    public UnityEvent onTriggerActivated;

    private TutorialManager _ownerManager;
    private bool _isInitialized;
    private bool _isArmed;
    private bool _isTriggered;

    public event Action Triggered;

    public bool IsArmed => _isArmed;
    protected TutorialManager OwnerManager => _ownerManager;

    protected virtual void Update()
    {
        if (!_isArmed || _isTriggered)
            return;

        DetectTriggerCompletion();
    }

    protected virtual void OnDisable()
    {
        Disarm();
    }

    public virtual void Arm(TutorialManager ownerManager)
    {
        _ownerManager = ownerManager;
        EnsureInitialized();
        ResetTrigger();
        _isArmed = true;
        OnArmed();
    }

    public virtual void Disarm()
    {
        if (!_isArmed)
            return;

        _isArmed = false;
        OnDisarmed();
        _ownerManager = null;
    }

    protected void EnsureInitialized()
    {
        if (_isInitialized)
        {
            return;
        }

        _isInitialized = true;
        OnInitialize();
    }

    protected virtual void OnInitialize()
    {
    }

    protected virtual void OnArmed()
    {
    }

    protected virtual void OnDisarmed()
    {
    }

    protected virtual void DetectTriggerCompletion()
    {
    }

    protected bool IsPointerOverUI()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null)
            return false;

        if (TutorialPointerInput.TryGetPrimaryTouch(out var touchPointer))
            return TutorialPointerInput.IsPointerOverUI(eventSystem, touchPointer);

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
            return eventSystem.IsPointerOverGameObject(mouse.deviceId > 0 ? mouse.deviceId : -1);

        return false;
    }

    public virtual void TriggerComplete()
    {
        if (!_isArmed || _isTriggered)
            return;

        _isTriggered = true;
        onTriggerActivated?.Invoke();
        Triggered?.Invoke();
    }

    public virtual void ResetTrigger()
    {
        _isTriggered = false;
    }
}

}
