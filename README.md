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

Open `UnityProjects/SmokeTest` to verify packages. This project uses symbolic links to reference local packages in the root `Packages` directory.

### Windows Compatibility

To ensure symbolic links work correctly on Windows:

1. **Enable Developer Mode**: Go to *Settings -> Update & Security -> For developers* and turn on **Developer Mode**.
2. **Configure Git**: Run the following command in your terminal (with administrator privileges if Developer Mode is off):
   ```bash
   git config core.symlinks true
   ```
3. **If you already cloned the repo**: Run these commands to restore symlinks:
   ```bash
   git config core.symlinks true
   git reset --hard HEAD
   ```

`UnityProjects/StarterTemplate` is the neutral starting point for
new Ryx games.
