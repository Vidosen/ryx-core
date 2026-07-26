# Changelog

## Unreleased

### Added

- `com.ryx.core.app` 0.1.0 — ordered async boot sequence with weighted progress, scene flow
  with cover/reveal transitions, `AppBootstrapper`, `FadeSceneTransition`. Container-agnostic.
- `com.ryx.core.save` 0.1.0 — `ISaveStore` / `ISaveSerializer` ports, versioned `SaveEnvelope`,
  eagerly-validated `SaveMigrationPipeline`, atomic `FileSaveStore`, `InMemorySaveStore`.

### Fixed

- `com.ryx.core.ui` 0.2.1 and `com.ryx.core.interaction` 0.4.3: added the missing `Editor`
  assembly definitions. `UIEffectHandlerDrawer`, `InteractionIdDrawer` and
  `InteractionTypeDrawer` were never being compiled.
- `com.ryx.core.fps` 0.2.0 (**breaking**): `LookInputBase`, `MoveInputBase`,
  `InputSystemLookInput` and `InputSystemMoveInput` moved out of the global namespace into
  `Ryx.Core.FPS`. Prefab references survive (they bind by script GUID), but `using` directives
  in consuming code must be updated. Both input adapters now warn instead of throwing when the
  configured action is missing, and unsubscribe safely.
- `com.ryx.core.tutorials` 0.1.1: resuming saved progress restored neither the completed chapters
  nor the exact step. A player who quit mid-chapter came back to the start of that chapter with
  every earlier chapter forgotten, so triggers already satisfied fired again. `TryResumeFromSaved`
  now takes the whole `TutorialProgressData`, replays `CompletedChapterIds`, and resumes at the
  saved step when it still exists — falling back to the chapter start only when it does not.
- `UnityProjects/StarterTemplate`: manifest was missing `com.ryx.core`,
  `com.ryx.core.tutorials` and `com.unity.localization`; the lockfile pinned a stale
  `com.ryx.core.ui` version. Reset the lockfile so Unity re-resolves.

## 0.1.0

- Extract FPS, Hands, Interaction, and UI packages from the original game repositories.
- Replace shared game-specific item enums with `ItemTagSO`.
- Make R3 part of the public package API.
- Remove runtime DI-container coupling and DOTween dependencies.
