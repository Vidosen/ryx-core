using System;
using R3;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Ryx.Core.Interaction
{
	public class InteractiveModeInput : MonoBehaviour
	{
		[SerializeField] private string context = "Fridge";
		private PlayerInput _playerInput;
		private InputAction _interactAction;
		private InputAction _cursorPositionAction;
		private readonly Subject<Unit> _interactPressed = new();

		public Observable<Unit> InteractPressed => _interactPressed;
		public Vector2 CursorPosition => _cursorPositionAction != null ? _cursorPositionAction.ReadValue<Vector2>() : Vector2.zero;

		private void Awake()
		{
			_playerInput = GetComponent<PlayerInput>();
			if (_playerInput)
			{
				_interactAction = _playerInput.actions[$"{context}/Interact"];
				_cursorPositionAction = _playerInput.actions[$"{context}/Position"];
				_interactAction.performed += OnPerformed;
			}
		}

		private void OnPerformed(InputAction.CallbackContext _)
		{
			_interactPressed.OnNext(Unit.Default);
		}

		private void OnDestroy()
		{
			if (_playerInput)
			{
				_interactAction.performed -= OnPerformed;
				_interactPressed.Dispose();
			}
		}
	}
}



