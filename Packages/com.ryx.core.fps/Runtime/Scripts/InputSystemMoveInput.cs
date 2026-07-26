using UnityEngine;
using UnityEngine.InputSystem;

namespace Ryx.Core.FPS
{
    public class InputSystemMoveInput : MoveInputBase
    {
        [SerializeField] private string _moveActionName = "Player/Move";

        public override Vector2 MoveDelta => _moveDelta;

        private Vector2 _moveDelta;
        private InputAction _moveAction;
        private PlayerInput _playerInput;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            if (_playerInput == null) return;
            _moveAction = _playerInput.actions.FindAction(_moveActionName, false);
            if (_moveAction == null)
            {
                Debug.LogWarning($"[Ryx.Core.FPS] Move action '{_moveActionName}' not found on {name}.", this);
                return;
            }

            _moveAction.performed += OnMove;
            _moveAction.canceled += OnMoveCanceled;
        }

        private void OnDisable()
        {
            if (_moveAction == null) return;
            _moveAction.performed -= OnMove;
            _moveAction.canceled -= OnMoveCanceled;
            _moveAction = null;
            _moveDelta = Vector2.zero;
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
}
