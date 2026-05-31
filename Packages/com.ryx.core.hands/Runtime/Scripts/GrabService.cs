using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Ryx.Core.Hands
{
	public class GrabService : IGrabService
	{
		private readonly Transform _idleLeftHandPosition;
		private readonly Transform _idleRightHandPosition;
		private readonly Dictionary<string, Transform> _itemLeftHandGrabLocations = new();
		private readonly Dictionary<string, Transform> _itemRightHandGrabLocations = new();

		public GrabService(PlayerHand leftHand, PlayerHand rightHand,
			GrabLocationsData leftHandData, GrabLocationsData rightHandData)
		{
			LeftHand = leftHand;
			RightHand = rightHand;
			_idleLeftHandPosition = leftHandData.defaultLocation;
			_idleRightHandPosition = rightHandData.defaultLocation;

			foreach (var itemGrabLocation in leftHandData.itemGrabLocations)
			foreach (var itemID in itemGrabLocation.itemIDs)
				_itemLeftHandGrabLocations[itemID] = itemGrabLocation.location;

			foreach (var itemGrabLocation in rightHandData.itemGrabLocations)
			foreach (var itemID in itemGrabLocation.itemIDs)
				_itemRightHandGrabLocations[itemID] = itemGrabLocation.location;
		}

		public PlayerHand LeftHand { get; }
		public PlayerHand RightHand { get; }

		public UniTask TryGrabAsync(HandSide side, GrabItem item)
			=> GetHand(side).TryGrabAsync(item);

		public UniTask TryToolGrabAsync(HandSide side, GrabItem targetItem)
			=> GetHand(side).TryToolGrabAsync(targetItem);

		private PlayerHand GetHand(HandSide side) => side == HandSide.Left ? LeftHand : RightHand;

		public UniTask<bool> MoveTo(HandSide handSide, Transform target) => GetHand(handSide).MoveTo(target);
		public UniTask<bool> MoveAlongSplinePath(HandSide handSide, Transform target) => GetHand(handSide).MoveAlongSplinePath(target);

		public UniTask<bool> MoveToIdle(HandSide side)
		{
			var grabLocation = side == HandSide.Left ? _itemLeftHandGrabLocations : _itemRightHandGrabLocations;
			var idlePosition = side == HandSide.Left ? _idleLeftHandPosition : _idleRightHandPosition;
			return MoveHandTo(GetHand(side), grabLocation, idlePosition);
		}

		private static UniTask<bool> MoveHandTo(PlayerHand hand, Dictionary<string, Transform> itemPositions, Transform idlePosition)
		{
			var handItemIfAny = hand.GrabbedItem;
			if (handItemIfAny)
			{
				var handItemPosition = itemPositions[handItemIfAny.ItemID];
				return hand.MoveTo(handItemPosition);
			}
			return hand.MoveTo(idlePosition);
		}
		
		public bool HasGrabbedByHand(HandSide side, ItemTagSO itemTag)
        {
            var hand = GetHand(side);
            return (hand.HandState == HandState.Grabbed && hand.GrabbedItem && hand.GrabbedItem.ItemTag == itemTag) ||
                   (hand.HandState == HandState.UsingTool && hand.ToolGrabbedItem && hand.ToolGrabbedItem.ItemTag == itemTag);
        }
        
        public bool HasToolGrabbedByHand(HandSide side, ItemTagSO itemTag)
        {
            var hand = GetHand(side);
            return hand.HandState == HandState.UsingTool && 
                   hand.ToolGrabbedItem && 
                   hand.ToolGrabbedItem.ItemTag == itemTag;
        }
        
        public bool IsUsingTool(HandSide side)
        {
            return GetHand(side).HandState == HandState.UsingTool;
        }
        
        public bool IsHoldingTool(HandSide side, ItemTagSO toolTag)
        {
            var hand = GetHand(side);
            return hand.HandState is HandState.Grabbed or HandState.UsingTool && 
                   hand.GrabbedItem && 
                   hand.GrabbedItem.ItemTag == toolTag && 
                   hand.GrabbedItem is ToolGrabItem;
        }
        
        public Observable<bool> HasGrabbedByHandAsObservable(HandSide handSide, ItemTagSO itemTag)
        {
            var hand = GetHand(handSide);
            return Observable.Merge(
                hand.OnGrabbed.Select(item => item && item.ItemTag == itemTag),
                hand.OnToolGrabbed.Select(item => item && item.ItemTag == itemTag),
                hand.OnDropped.Select(_ => false),
                hand.OnToolDropped.Select(_ => false)
            );
        }
        
        public Observable<bool> HasToolGrabbedByHandAsObservable(HandSide handSide, ItemTagSO itemTag)
        {
            var hand = GetHand(handSide);
            return Observable.Merge(
                hand.OnToolGrabbed.Select(item => item && item.ItemTag == itemTag),
                hand.OnToolDropped.Select(_ => false),
                hand.OnDropped.Select(_ => false)
            );
        }

		
		
		// Drop everything (tool and item grabbed by tool)
		public void Drop(HandSide handSide)
		{
			GetHand(handSide).Drop();
			MoveToIdle(handSide);
		}
        
		// Drop only the tool-grabbed item, keep holding the tool
		public void DropToolGrabbedItem(HandSide handSide)
		{
			GetHand(handSide).DropToolGrabbedItem();
			MoveToIdle(handSide);
		}
        
		// Drop the tool (and anything it's holding)
		public void DropTool(HandSide handSide)
		{
			GetHand(handSide).DropTool();
			MoveToIdle(handSide);
		}
	}

	[Serializable]
	public class ItemGrabLocationsData
	{
		public Transform location;
		public string[] itemIDs;
	}

	[Serializable]
	public class GrabLocationsData
	{
		public Transform defaultLocation;
		public ItemGrabLocationsData[] itemGrabLocations;
	}
}

