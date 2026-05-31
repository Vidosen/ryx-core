# Ryx Core UI

Reusable UI primitives for snapping controls to world positions and playing async UI effects.

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

## UISnapper
`UISnapper` positions a `RectTransform` over a world-space target. `UISnappingButton`
combines it with `UIButton` for contextual interaction prompts.
