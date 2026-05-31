# Ryx Core Hands

Reusable dual-hand grab, tool interaction and spline movement package.

## Assembly
- `Ryx.Core.Hands`

## Item Model
Items use project-defined `ItemTagSO` assets instead of a shared enum. This keeps core
contracts stable while each game owns its item taxonomy.

```csharp
ItemTagSO IGrabItem.ItemTag { get; }
bool IToolGrabItem.CanGrabItemTag(ItemTagSO targetTag);
bool IConditionalGrabItem.CanBeGrabbedByTool(ItemTagSO toolTag);
```

Create tag assets from `Create > Ryx Core > Hands > Item Tag`, then assign them to
`GrabItem`, `ToolGrabItem`, `ConditionalGrabItem` and `GrabLocationsData`.

## R3 And UniTask API
The public reactive contracts are R3-native:

```csharp
ReadOnlyReactiveProperty<HandState> IPlayerHand.HandStateChanged { get; }
Observable<GrabItem> IPlayerHand.OnGrabbed { get; }
Observable<GrabItem> IPlayerHand.OnDropped { get; }
Observable<GrabItem> IPlayerHand.OnToolGrabbed { get; }
Observable<GrabItem> IPlayerHand.OnToolDropped { get; }
Observable<bool> IGrabService.HasGrabbedByHandAsObservable(HandSide side, ItemTagSO tag);
```

Movement and grab commands are awaitable:

```csharp
UniTask TryGrabAsync(HandSide side, GrabItem item);
UniTask TryToolGrabAsync(HandSide side, GrabItem targetItem);
UniTask<bool> MoveTo(HandSide side, Transform target);
UniTask<bool> MoveAlongSplinePath(HandSide side, Transform target);
UniTask<bool> MoveToIdle(HandSide side);
```

## Composition
The package does not depend on a DI container. Construct `GrabService` in the
application composition root with the left and right `PlayerHand` instances and their
`GrabLocationsData`.

## Dependencies
- `com.cysharp.r3`
- `com.cysharp.unitask`
- `com.unity.splines`
- `com.unity.mathematics`
- `com.unity.animation.rigging`
- `com.unity.formats.fbx`

FBX export support is currently shipped as an editor dependency of the hands package.
