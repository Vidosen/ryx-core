using UnityEngine;
using UnityEngine.InputSystem;

public class InputSystemLookInput : LookInputBase
{
    public override Vector2 LookDelta => _lookDelta;
    private Vector2 _lookDelta;
    [SerializeField] private string _lookActionName = "Player/Look";
    private InputAction _lookAction;
    private PlayerInput _playerInput;
    private void Awake(){
        _playerInput = GetComponent<PlayerInput>();
    }
    private void OnEnable()
    {
        if (_playerInput == null) return;
        _lookAction = _playerInput.actions.FindAction(_lookActionName);
        _lookAction.performed += OnLook;
        _lookAction.canceled += OnLookCanceled;
    }
    private void OnDisable()
    {
        if (_playerInput == null) return;
        _lookAction.performed -= OnLook;
        _lookAction.canceled -= OnLookCanceled;
    }

    private void OnLook(InputAction.CallbackContext context)
    {
        _lookDelta = context.ReadValue<Vector2>();
    }
    private void OnLookCanceled(InputAction.CallbackContext context)
    {
        _lookDelta = Vector2.zero;
    }
}