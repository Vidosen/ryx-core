# Ryx Core App

Application lifecycle for Ryx games: an ordered async boot sequence and scene flow with
transitions. Container-agnostic — construct the services in your composition root, exactly
like the rest of Ryx Core.

## What is in here

| Type | Layer | Role |
| --- | --- | --- |
| `IBootStep` / `BootStep` | Application | One unit of startup work. `BootStep.FromAction` / `FromAsync` for lambda wiring. |
| `BootSequence` | Application | Runs steps in order once, publishes weighted `BootProgress` over R3. |
| `ISceneLoader` | Contract | Scene loading port. Keeps `SceneManager` at the boundary. |
| `UnitySceneLoader` | Infrastructure | The only type that touches Unity scene APIs. |
| `ISceneTransition` | Contract | Cover / reveal around a swap, plus progress readout. |
| `ISceneFlowService` / `SceneFlowService` | Application | Orchestrates cover → load → reveal, owns `CurrentScene`. |
| `FadeSceneTransition` | Presentation | `CanvasGroup` fade + optional progress fill. |
| `AppBootstrapper` | Presentation | Scene-side entry point. Runs boot, then goes to the first scene. |

`SceneId` replaces bare scene-name strings; `BootProgress` is a value type safe to hand
straight to a view.

## Composition

```csharp
var sceneLoader = new UnitySceneLoader();
var sceneFlow   = new SceneFlowService(sceneLoader, fadeTransition, initialScene: new SceneId("Boot"));

var boot = new BootSequence(
    BootStep.FromAction("Applying settings", () => Application.targetFrameRate = 60),
    BootStep.FromAsync("Loading save data", ct => saveService.LoadAsync(ProfileKey, ct: ct).AsUniTask(), weight: 3f),
    BootStep.FromAction("Warming pools", pools.Prewarm));

bootstrapper.Configure(boot, sceneFlow, firstScene: new SceneId("Station"));
```

`AppBootstrapper` deliberately does **not** find its dependencies itself. If nothing calls
`Configure`, it logs an error and stops rather than reaching for a service locator.

## Boot progress

`BootSequence.Progress` is an R3 `ReadOnlyReactiveProperty<BootProgress>` — returned
directly, not re-wrapped per get, so subscribing allocates nothing extra.

```csharp
bootstrapper.Progress
    .Subscribe(p => label.text = $"{p.Description}… {p.Normalized:P0}")
    .AddTo(this);
```

Weight steps that dominate boot time (`weight: 3f`) so the bar moves at a believable rate.

## Failure handling

A throwing step is wrapped in `BootFailedException` carrying the step description, and the
sequence stops there — later steps never run against a half-initialised app. Cancellation
propagates as `OperationCanceledException` and is not wrapped.

## Scene flow

`GoToAsync` rejects re-entrant calls with `InvalidOperationException` rather than
interleaving two transitions. `LoadOverlayAsync` is idempotent, so calling it for an
already-loaded additive scene is a no-op.
