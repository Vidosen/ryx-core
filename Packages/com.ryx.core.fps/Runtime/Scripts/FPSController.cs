using UnityEngine;
using UnityEngine.InputSystem;

namespace Ryx.Core.FPS
{
    [RequireComponent(typeof(CharacterController))]
    public class FPSController : MonoBehaviour
    {
        [Header("Movement Settings")] public float moveSpeed = 5f;
        public float jumpForce = 5f;
        public float gravity = -9.81f;
        public float acceleration = 10f;
        public float deceleration = 10f;

        [SerializeField] private MoveInputBase[] moveControls;
        [SerializeField] private LookInputBase[] lookControls;

        [Header("Mouse Look Settings")] public float mouseSensitivity = 2f;
        public Transform playerCamera;
        public Transform eyeSight;

        private CharacterController _characterController;
        private float _verticalVelocity;
        private float _cameraPitch;
        private Vector3 _currentVelocity;

        private void Awake()
        {
            _characterController = GetComponent<CharacterController>();
        }



        public void OnJump(InputAction.CallbackContext context)
        {
            if (context.performed && _characterController.isGrounded)
                _verticalVelocity = jumpForce;
        }



        private void Update()
        {
            var totalMove = Vector2.zero;
            if (moveControls != null)
            {
                foreach (var control in moveControls)
                {
                    if (!control || !control.SupportsCurrentPlatform) continue;
                    totalMove += control.MoveDelta;
                }
            }

            if (totalMove.sqrMagnitude > 1f)
            {
                totalMove.Normalize();
            }

            HandleMovement(totalMove);
            HandleMouseLook();
        }

        private void LateUpdate()
        {
            if (playerCamera && eyeSight)
                playerCamera.SetPositionAndRotation(eyeSight.position, eyeSight.rotation);
        }

        private void HandleMovement(Vector2 direction)
        {
            var targetDirection = transform.right * direction.x + transform.forward * direction.y;
            targetDirection *= moveSpeed;
            _currentVelocity = targetDirection.magnitude > 0
                ? Vector3.Lerp(_currentVelocity, targetDirection, acceleration * Time.deltaTime)
                : Vector3.Lerp(_currentVelocity, Vector3.zero, deceleration * Time.deltaTime);

            if (_characterController.isGrounded && _verticalVelocity < 0) _verticalVelocity = -2f;
            _verticalVelocity += gravity * Time.deltaTime;

            var finalVelocity = _currentVelocity;
            finalVelocity.y = _verticalVelocity;
            _characterController.Move(finalVelocity * Time.deltaTime);
        }

        private void HandleMouseLook()
        {
            if (lookControls == null || lookControls.Length == 0)
                return;

            var totalDelta = Vector2.zero;
            foreach (var control in lookControls)
            {
                if (!control || !control.SupportsCurrentPlatform) continue;
                totalDelta += control.LookDelta;
            }
            if (totalDelta.sqrMagnitude < 0.01f)
                return;

            var mouseX = totalDelta.x * mouseSensitivity;
            var mouseY = totalDelta.y * mouseSensitivity;

            _cameraPitch -= mouseY;
            _cameraPitch = Mathf.Clamp(_cameraPitch, -90f, 90f);

            if (eyeSight) eyeSight.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
            transform.Rotate(Vector3.up * mouseX);
        }
    }
}

