# Ryx Core

Reusable Unity 6 packages shared by Ryx projects.

## Packages

- `com.ryx.core`: base package and the `Ryx Package Manager` editor window.
- `com.ryx.core.app`: boot sequence, scene flow, transitions.
- `com.ryx.core.fps`: first-person movement and input adapters.
- `com.ryx.core.hands`: tag-based hand, grab, and tool-grab runtime.
- `com.ryx.core.interaction`: interaction state and optional UGUI presentation.
- `com.ryx.core.save`: versioned save documents, migrations, pluggable stores.
- `com.ryx.core.tutorials`: FTUE chapters, steps, triggers, with Localization support.
- `com.ryx.core.ui`: reusable UGUI controls, context-gated views, and effects.

R3 and UniTask are baseline dependencies. Runtime packages do not depend on a
specific DI container. Consumers compose them from project-local installers.

### Dependency graph

Only two edges exist between packages: `interaction -> ui`, and nothing depends
on `core`. Keep it that way — a package that needs another package's types is
usually a sign the shared part wants extracting instead.

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

## Remote Package Catalog

`Ryx Package Manager` discovers available Ryx packages from the remote
`ryx-core` repository. This flow must work without a local checkout of the
repository on the consumer machine.

### Tag Contract

- Every published package version must have a git tag in the format
  `<package-id>/<version>`, for example `com.ryx.core.ui/0.2.0`.
- `<package-id>` must match the `name` field in
  `Packages/<package-id>/package.json`.
- `<version>` must match the package version being published for that package.

### Repository Layout Contract

- Each package must live at `Packages/<package-id>/`.
- The canonical metadata source for the package catalog is
  `Packages/<package-id>/package.json`.
- `displayName`, `description`, `author`, and package dependency metadata are
  read from that `package.json`.

### Package Manager Behavior

- `Ryx Package Manager` builds the full package catalog from remote git tags.
- Installed state is resolved locally through Unity Package Manager.
- Remote package metadata is fetched through a minimal local git cache with
  shallow fetches of specific refs; a full clone of `ryx-core` is not required.
- The package manager requires normal git access to the remote repository for
  both tag discovery and remote manifest reads.
- A package may appear in the catalog even when it is not installed in the
  current Unity project.
- The canonical manifest lookup path remains `Packages/<package-id>/package.json`.
  The package manager may verify or recover that path through git tree lookup
  when reading metadata for a fetched ref.
- If a package has valid release tags but its remote `package.json` cannot be
  fetched, the window must degrade gracefully and still show the package by its
  `package-id`.
