# Ryx Core Interaction

Reusable interaction-mode input, state and UI coordination for Unity projects.

## Assembly
- `Ryx.Core.Interaction`

## Dependencies
- `com.cysharp.r3`
- `com.cysharp.unitask`
- `com.ryx.core.ui`
- `com.unity.inputsystem`
- `com.unity.ugui`

The package does not depend on a DI container. Create one `FPSInteractionService` in the
application composition root and pass it to consumers. For `InteractionUIManager`, call
`Initialize(FPSInteractionService)`.

## R3 API
```csharp
ReadOnlyReactiveProperty<InteractionType> FPSInteractionService.CurrentMode { get; }
Observable<Unit> InteractiveModeInput.InteractPressed { get; }
```

`InteractiveModeInput` reads `${context}/Interact` and `${context}/Position` actions from
the attached `PlayerInput`.

## Typical Usage
```csharp
var service = new FPSInteractionService();
interactionUIManager.Initialize(service);

input.InteractPressed
    .Where(_ => service.IsInMode(InteractionType.Fridge))
    .Subscribe(_ => HandleFridgeInteract());
```

Dispose the service from the application lifetime owner.
