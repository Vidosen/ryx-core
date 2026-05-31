# Ryx Shared FPS

First-person movement controller with desktop and touch look.

## Assembly
- `Ryx.Core.FPS`

## Dependencies
- `com.unity.inputsystem`
- `com.neuecc.unirx` (for touch delta stream; optional)

## Components
- `FPSController`
  - CharacterController-based movement
  - Input actions: `Player/Move`, `Player/Look`, `Player/Jump`, `Player/MouseRightButton`
  - Mobile: optional `CameraRotationControl`
- `CameraRotationControl`
  - Emits drag deltas for touch-look

## Setup
1) Add `PlayerInput` to the Player GameObject.
2) Assign actions as above.
3) Add `FPSController` and set `playerCamera` and `eyeSight`.
4) For mobile, add `CameraRotationControl` and assign to `FPSController`.

## Inspector Reference (FPSController)
- Movement Settings
  - `moveSpeed`: meters/second on flat ground
  - `jumpForce`: initial upward velocity applied on jump
  - `gravity`: gravity acceleration (negative)
  - `acceleration`: how quickly velocity approaches the desired direction
  - `deceleration`: how quickly velocity returns to zero when no input
- Mobile Controls
  - `enableTouchLook`: if true, adds touch-look delta from `CameraRotationControl`
  - `lookControl`: reference to the `CameraRotationControl` UI component
- Mouse Look Settings
  - `mouseSensitivity`: scales both mouse and touch deltas
  - `playerCamera`: the Camera to move (usually child camera)
  - `eyeSight`: a Transform used as the camera target pose (e.g., head)

## Input Actions (suggested bindings)
- Map `Player/Move` (Vector2):
  - Keyboard: WASD / Arrow keys (composite 2D Vector)
  - Gamepad: left stick
- Map `Player/Look` (Vector2):
  - Mouse delta
  - Gamepad: right stick
- Map `Player/Jump` (Button):
  - Keyboard: Space
  - Gamepad: South button (A / Cross)
- Map `Player/MouseRightButton` (Button):
  - Mouse: Right Button

## Example: Player Prefab Structure
```
Player (GameObject)
├─ CharacterController
├─ PlayerInput (maps: Player)
├─ FPSController (playerCamera, eyeSight)
└─ Camera (child, tagged MainCamera)
```

Optional mobile UI:
```
Canvas
└─ TouchArea (RectTransform)
   └─ CameraRotationControl
```
Assign `TouchArea/CameraRotationControl` to `FPSController.lookControl` and enable `enableTouchLook`.

## Code Snippets
- Accessing InputAction callbacks (if using Send Messages or C# events on PlayerInput):
```csharp
public void OnLook(InputAction.CallbackContext ctx) => _controller.OnLook(ctx);
public void OnJump(InputAction.CallbackContext ctx) => _controller.OnJump(ctx);
public void OnMouseRightButton(InputAction.CallbackContext ctx) => _controller.OnMouseRightButton(ctx);
```

- Programmatically setting up the PlayerInput (if needed):
```csharp
var playerInput = gameObject.AddComponent<PlayerInput>();
playerInput.actions = yourInputActionAsset;
playerInput.defaultActionMap = "Player";
```

## Behavior Details
- Movement
  - Desired velocity is calculated from camera-aligned `transform.right/forward` and input vector.
  - Acceleration/deceleration are applied via `Vector3.Lerp` for smooth start/stop.
  - CharacterController handles collision; gravity is integrated with a grounded snap (`-2f` when grounded).
- Look
  - Desktop: look updates only while RMB is held (prevents accidental look when interacting with UI).
  - Mobile: touch deltas from `CameraRotationControl` are always considered when `enableTouchLook` is true.
  - Pitch is clamped to [-90°, 90°] and applied to `eyeSight` local rotation; yaw rotates `transform`.
- Camera Sync
  - In `LateUpdate`, the camera’s transform is aligned to `eyeSight` pose for clean timing with animations.

## Migration Notes
- If you previously used a different controller:
  - Keep your `PlayerInput` asset and map the actions listed above.
  - Replace your controller component with `FPSController` and wire `playerCamera`/`eyeSight`.
  - If you used a mobile joystick for movement, keep it feeding `Player/Move`; `FPSController` reads the action’s value.
- If you had unrestricted mouse look, set `OnMouseRightButton` binding to an always-on action or add an option to bypass RMB.

## Troubleshooting
- No movement
  - Verify `PlayerInput` is active and `Player/Move` reads non-zero values in Play mode.
  - Check CharacterController center/height so the capsule isn’t stuck inside colliders.
- No look
  - Ensure `Player/MouseRightButton` is pressed (desktop) or `enableTouchLook` is true with a valid `lookControl` (mobile).
  - Confirm `Player/Look` is bound to Mouse Delta, not Mouse Position.
- Camera not updating
  - `playerCamera` and `eyeSight` must be assigned. Tag the camera as MainCamera if other systems rely on it.
- Cursor lock issues
  - The controller locks the cursor while RMB is pressed and restores it on release, warping back to the last position.
  - On mobile (`Application.isMobilePlatform`), cursor locking is disabled.

## Performance Notes
- Uses `CharacterController.Move` per frame; prefer fixed-size colliders and avoid extremely small `skinWidth`.
- Avoid very high `acceleration/deceleration` that can cause stutter; tweak in the 8–20 range.

## Extensibility
- To add head-bob or FOV kick: hook after `LateUpdate` camera sync and animate `playerCamera.localPosition/FOV`.
- To allow look without RMB: gate `OnLook` with a serialized flag, or map a different binding.

## Known Limitations
- Jump is event-based; if you need buffered jumps or coyote time, extend `OnJump` to include those features.
- Rigidbody-driven physics interactions are not handled; use kinematic triggers or hybrid approaches if needed.
