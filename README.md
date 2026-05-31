# Ryx Core

Reusable Unity 6 packages shared by Ryx projects.

## Packages

- `com.ryx.core.fps`: first-person movement and input adapters.
- `com.ryx.core.hands`: tag-based hand, grab, and tool-grab runtime.
- `com.ryx.core.interaction`: interaction state and optional UGUI presentation.
- `com.ryx.core.ui`: reusable UGUI controls and effects.

R3 and UniTask are baseline dependencies. Runtime packages do not depend on a
specific DI container. Consumers compose them from project-local installers.

## Development

Open `UnityProjects/SmokeTest` to compile all packages through local `file:`
references. `UnityProjects/StarterTemplate` is the neutral starting point for
new Ryx games.
