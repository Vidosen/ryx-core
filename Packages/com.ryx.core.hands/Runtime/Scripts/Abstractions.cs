using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Ryx.Core.Hands
{
	public interface IGrabItem
	{
		ItemTagSO ItemTag { get; }
		Transform Transform { get; }
		bool IsAlive { get; }
		bool CanBeGrabbedDirectly { get; }
		string GetGrabAnimationName();
		Matrix4x4[] GetPathPointsFor(Transform from);
	}

	public interface IToolGrabItem : IGrabItem
	{
		bool CanGrabItemTag(ItemTagSO targetTag);
	}

	public interface IConditionalGrabItem : IGrabItem
	{
		bool CanBeGrabbedByTool(ItemTagSO toolTag);
	}

	public enum HandState { Idle, Grabbing, Grabbed, UsingTool }

	public interface IPlayerHand
	{
		HandState HandState { get; }
		ReadOnlyReactiveProperty<HandState> HandStateChanged { get; }
		GrabItem GrabbedItem { get; }
		GrabItem ToolGrabbedItem { get; }
		Observable<GrabItem> OnGrabbed { get; }
		Observable<GrabItem> OnDropped { get; }
		Observable<GrabItem> OnToolGrabbed { get; }
		Observable<GrabItem> OnToolDropped { get; }

		UniTask TryGrabAsync(GrabItem item);
		UniTask TryToolGrabAsync(GrabItem targetItem);
		UniTask<bool> MoveTo(Transform target);
		UniTask<bool> MoveAlongSplinePath(Transform target);
		GrabItem Drop();
		void DropToolGrabbedItem();
		GrabItem DropTool();
	}

	public interface IGrabService
	{
		PlayerHand LeftHand { get; }
		PlayerHand RightHand { get; }
		UniTask TryGrabAsync(HandSide side, GrabItem item);
		UniTask TryToolGrabAsync(HandSide side, GrabItem targetItem);
		UniTask<bool> MoveTo(HandSide handSide, Transform target);
		UniTask<bool> MoveAlongSplinePath(HandSide handSide, Transform target);
		UniTask<bool> MoveToIdle(HandSide side);
		bool HasGrabbedByHand(HandSide side, ItemTagSO tag);
		bool HasToolGrabbedByHand(HandSide side, ItemTagSO tag);
		bool IsUsingTool(HandSide side);
		bool IsHoldingTool(HandSide side, ItemTagSO toolTag);
		Observable<bool> HasGrabbedByHandAsObservable(HandSide handSide, ItemTagSO tag);
		Observable<bool> HasToolGrabbedByHandAsObservable(HandSide handSide, ItemTagSO tag);
		void Drop(HandSide handSide);
		void DropToolGrabbedItem(HandSide handSide);
		void DropTool(HandSide handSide);
	}
}


