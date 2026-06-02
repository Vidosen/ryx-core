# Ryx Core UI

Reusable UI views, contexts, snapping controls and async UI effects.

## Assembly
- `Ryx.Core.UI`

## Dependencies
- `com.cysharp.r3`
- `com.cysharp.unitask`
- `com.unity.ugui`

## UIButton
```csharp
Button Button { get; }
Observable<Unit> Click { get; }
UniTask Show(bool animate);
UniTask Hide(bool animate);
UniTask Enable(bool animate);
UniTask Disable(bool animate);
```

Assign a `UIEffectHandler` to define optional show, hide, enable and disable effects.

## UIView and UI contexts
`UIView` separates requested visibility from context-gated visibility. `UIContextService`
is a pure C# service that manages a stack of `UIContext` assets. Register scene views with
`UIContextViewBinding` or register dynamic views directly:

```csharp
var registration = contextService.Register(view);
var phoneContext = contextService.PushContext(phone);
phoneContext.Dispose();
registration.Dispose();
```

An empty allowed-context list means default-only. Global views remain visible in every context.

## UISnapper
`UISnapper` positions a `RectTransform` over a world-space target. `UISnappingButton`
combines it with `UIButton` for contextual interaction prompts.
