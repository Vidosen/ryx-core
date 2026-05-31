using UnityEngine;
using UnityEngine.InputSystem;

public class InputSystemMoveInput : MoveInputBase
{
    [SerializeField] private string _moveActionName = "Player/Move";
	public override Vector2 MoveDelta => _moveDelta;
	private Vector2 _moveDelta;
    private InputAction _moveAction;
    private PlayerInput _playerInput;
    private void Awake(){
        _playerInput = GetComponent<PlayerInput>();
    }
    private void OnEnable()
    {
        _moveAction = _playerInput.actions.FindAction(_moveActionName, false);
        _moveAction.performed += OnMove;
        _moveAction.canceled += OnMoveCanceled;
    }
    private void OnDisable()
    {
        _moveAction.performed -= OnMove;
        _moveAction.canceled -= OnMoveCanceled;
    }

	private void OnMove(InputAction.CallbackContext context)
	{
		_moveDelta = context.ReadValue<Vector2>();
	}
    private void OnMoveCanceled(InputAction.CallbackContext context)
    {
        _moveDelta = Vector2.zero;
    }
}


