using UnityEngine;
using UnityEngine.InputSystem;

namespace Ryx.Core.FPS
{
    public class InputSystemLookInput : LookInputBase
    {
        [SerializeField] private string _lookActionName = "Player/Look";

        public override Vector2 LookDelta => _lookDelta;

        private Vector2 _lookDelta;
        private InputAction _lookAction;
        private PlayerInput _playerInput;

        private void Awake()
        {
            _playerInput = GetComponent<PlayerInput>();
        }

        private void OnEnable()
        {
            if (_playerInput == null) return;
            _lookAction = _playerInput.actions.FindAction(_lookActionName, false);
            if (_lookAction == null)
            {
                Debug.LogWarning($"[Ryx.Core.FPS] Look action '{_lookActionName}' not found on {name}.", this);
                return;
            }

            _lookAction.performed += OnLook;
            _lookAction.canceled += OnLookCanceled;
        }

        private void OnDisable()
        {
            if (_lookAction == null) return;
            _lookAction.performed -= OnLook;
            _lookAction.canceled -= OnLookCanceled;
            _lookAction = null;
            _lookDelta = Vector2.zero;
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
}
