using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;
using UnityEngine.Animations;

namespace Ryx.Core.Hands
{
	public enum PathFollowingState
	{
		None,
		Following,
		Completed
	}

	public class PlayerHand : MonoBehaviour, IPlayerHand
	{
		private static readonly int IdleAnimKey = Animator.StringToHash("Idle");
		[SerializeField] private float GrabSpeed = 1.5f;
		[SerializeField] private float GrabRotateSpeed = 30f;
		private const float DestinationReachedPositionThreshold = 0.001f;
		private const float DestinationReachedRotationThreshold = 0.1f;
		[SerializeField] private float IdleSmoothSpeed = 0.1f;
		[SerializeField] private float IdleMaxSpeed = 3f;
		[SerializeField] private Animator _handAnimator;
		private readonly ReactiveProperty<HandState> _currentStateRx = new(HandState.Idle);
		[SerializeField, Range(0f, 1f)] private float smoothTime = 0.5f;
		public SerializableReactiveProperty<Transform> Target = new();
		
		private CancellationTokenSource _grabCancellationToken;
		private GrabItem _grabbedItem;
		private Vector3 _moveSpeed;
		private readonly ReactiveCommand<GrabItem> _onGrabbedCommand = new();
		private readonly ReactiveCommand<GrabItem> _onDroppedCommand = new();
		private readonly ReactiveCommand<Transform> _onReachedTargetCommand = new();
		private readonly ReactiveCommand<GrabItem> _onToolGrabbedCommand = new();
		private readonly ReactiveCommand<GrabItem> _onToolDroppedCommand = new();
		
		private List<Matrix4x4> _currentPath = new List<Matrix4x4>();
		private int _currentPathIndex;
		private PathFollowingState _pathFollowingState = PathFollowingState.None;
		private Transform _pathTarget;
		private Quaternion _pathStartRotation;
		
		public HandState HandState => _currentStateRx.Value;
		public ReadOnlyReactiveProperty<HandState> HandStateChanged => _currentStateRx.ToReadOnlyReactiveProperty();
		public GrabItem GrabbedItem => _grabbedItem;
		public Observable<GrabItem> OnGrabbed => _onGrabbedCommand;
		public Observable<GrabItem> OnDropped => _onDroppedCommand;
		public Observable<GrabItem> OnToolGrabbed => _onToolGrabbedCommand;
		public Observable<GrabItem> OnToolDropped => _onToolDroppedCommand;
		private GrabItem _toolGrabbedItem;
		public GrabItem ToolGrabbedItem => _toolGrabbedItem;
		
		private void Awake()
		{
			_handAnimator = GetComponent<Animator>();
			if (TryGetComponent(out ParentConstraint parentConstraint))
				parentConstraint.enabled = false;
		}
		
		private void LateUpdate()
		{
			if ((_grabbedItem == null || !_grabbedItem.IsAlive) && _currentStateRx.Value is HandState.Grabbed or HandState.UsingTool)
			{
				Drop();
				return;
			}
			if (_currentStateRx.Value == HandState.UsingTool && (_toolGrabbedItem == null || !_toolGrabbedItem.IsAlive))
			{
				DropToolGrabbedItem();
				return;
			}
			if (_pathFollowingState == PathFollowingState.Following)
			{
				UpdatePathFollowing();
				return;
			}
			var target = Target.Value;
			if (target)
			{
				if (_currentStateRx.Value == HandState.Idle)
				{
					var reached = SmoothUpdateGrabObject(transform, target, IdleMaxSpeed);
					if (reached) _onReachedTargetCommand.Execute(target);
				}
				else if (_currentStateRx.Value is HandState.Grabbed or HandState.UsingTool)
				{
					var reached = SmoothUpdateGrabObject(_grabbedItem.transform, target, _grabbedItem.EnableCustomGrabMaxSpeed ? _grabbedItem.CustomGrabMaxSpeed : IdleMaxSpeed, _grabbedItem);
					if (reached) _onReachedTargetCommand.Execute(target);
				}
			}
			if (_currentStateRx.Value is HandState.Grabbed or HandState.UsingTool)
				transform.SetPositionAndRotation(_grabbedItem.Transform.position, _grabbedItem.Transform.rotation);
		}
		
		private bool SmoothUpdateGrabObject(Transform grabObj, Transform target, float maxSpeed, GrabItem grabItem = null)
		{
			var targetPosition = target.position;
			var targetRotation = target.rotation;
			var newPosition = Vector3.SmoothDamp(grabObj.position, targetPosition, ref _moveSpeed, IdleSmoothSpeed, maxSpeed);
			var newRotation = Quaternion.Slerp(grabObj.rotation, targetRotation, Time.deltaTime * maxSpeed * 2);
			if (grabItem)
				grabItem.SetPositionAndRotation(newPosition, newRotation);
			else
				grabObj.SetPositionAndRotation(newPosition, newRotation);
			if (Vector3.SqrMagnitude(newPosition - targetPosition) < DestinationReachedPositionThreshold * DestinationReachedPositionThreshold &&
			    Quaternion.Angle(newRotation, targetRotation) < DestinationReachedRotationThreshold)
				return true;
			return false;
		}
		
		private void UpdatePathFollowing()
		{
			if (_currentPath.Count == 0 || _currentPathIndex >= _currentPath.Count)
			{
				CompletePathFollowing();
				return;
			}
			Matrix4x4 targetPoint = _currentPath[_currentPathIndex];
			Transform transformToMove = transform;
			GrabItem itemToMove = null;
			if (_currentStateRx.Value is HandState.Grabbed or HandState.UsingTool && _grabbedItem != null)
			{
				transformToMove = _grabbedItem.transform;
				itemToMove = _grabbedItem;
			}
			float distanceToTarget = Vector3.Distance(transformToMove.position, targetPoint.GetPosition());
			if (distanceToTarget < DestinationReachedPositionThreshold)
			{
				_currentPathIndex++;
				if (_currentPathIndex >= _currentPath.Count)
				{
					if (itemToMove)
						itemToMove.SetPositionAndRotation(targetPoint.GetPosition(), targetPoint.rotation);
					else
						transformToMove.SetPositionAndRotation(targetPoint.GetPosition(), targetPoint.rotation);
					CompletePathFollowing();
					return;
				}
				targetPoint = _currentPath[_currentPathIndex];
			}
			var newPosition = Vector3.MoveTowards(
				transformToMove.position,
				targetPoint.GetPosition(),
				GrabSpeed * Time.deltaTime);
			var newRotation = Quaternion.Slerp(_pathStartRotation, targetPoint.rotation, Time.deltaTime * GrabRotateSpeed * 2);
			if (itemToMove)
				itemToMove.SetPositionAndRotation(newPosition, newRotation);
			else
				transformToMove.SetPositionAndRotation(newPosition, newRotation);
			if (_currentStateRx.Value is HandState.Grabbed or HandState.UsingTool && _grabbedItem != null)
				transform.SetPositionAndRotation(_grabbedItem.Transform.position, _grabbedItem.Transform.rotation);
		}
		
		private void CompletePathFollowing()
		{
			_pathFollowingState = PathFollowingState.Completed;
			_currentPath.Clear();
			_currentPathIndex = 0;
			if (_pathTarget != null)
			{
				_onReachedTargetCommand.Execute(_pathTarget);
				_pathTarget = null;
			}
		}
		
		private void StartPathFollowing(List<Matrix4x4> path, Transform target)
		{
			_currentPath = new List<Matrix4x4>(path);
			_currentPathIndex = 0;
			_pathFollowingState = PathFollowingState.Following;
			_pathTarget = target;
			if (_currentStateRx.Value is HandState.Grabbed or HandState.UsingTool && _grabbedItem != null)
				_pathStartRotation = _grabbedItem.transform.rotation;
			else
				_pathStartRotation = transform.rotation;
		}
		
		public async UniTask TryGrabAsync(GrabItem item)
		{
			if (!item.CanBeGrabbedDirectly)
			{
				Debug.Log($"Item {item.ItemTag} cannot be grabbed directly");
				return;
			}
			if (_currentStateRx.Value == HandState.Grabbed || !item.IsAlive) return;
			_grabCancellationToken?.Cancel();
			using var grabCancellationToken = new CancellationTokenSource();
			try
			{
				_grabCancellationToken = grabCancellationToken;
				await GrabAsync(item, grabCancellationToken.Token);
				if (_grabCancellationToken == grabCancellationToken)
					_grabCancellationToken = null;
			}
			catch (OperationCanceledException)
			{
				Debug.Log("Grab cancelled by a new grab");
			}
		}
		
		public async UniTask TryToolGrabAsync(GrabItem targetItem)
		{
			if (_currentStateRx.Value != HandState.Grabbed || !targetItem.IsAlive) return;
			var toolItem = _grabbedItem as ToolGrabItem;
			if (toolItem == null)
			{
				Debug.Log("Currently held item is not a tool");
				return;
			}
			var conditionalItem = targetItem as IConditionalGrabItem;
			if (conditionalItem != null)
			{
				if (!conditionalItem.CanBeGrabbedByTool(toolItem.ItemTag))
				{
					Debug.Log($"Item {targetItem.ItemTag} cannot be grabbed by tool {toolItem.ItemTag}");
					return;
				}
			}
			else if (!toolItem.CanGrabItemTag(targetItem.ItemTag))
			{
				Debug.Log($"Tool {toolItem.ItemTag} cannot grab item {targetItem.ItemTag}");
				return;
			}
			_grabCancellationToken?.Cancel();
			using var grabCancellationToken = new CancellationTokenSource();
			try
			{
				_grabCancellationToken = grabCancellationToken;
				await ToolGrabAsync(toolItem, targetItem, grabCancellationToken.Token);
				if (_grabCancellationToken == grabCancellationToken)
					_grabCancellationToken = null;
			}
			catch (OperationCanceledException)
			{
				Debug.Log("Tool grab cancelled by a new grab");
			}
		}
		
		private async UniTask ToolGrabAsync(ToolGrabItem toolItem, GrabItem targetItem, CancellationToken token)
		{
			var animationName = toolItem.GetToolGrabAnimationName(targetItem.ItemTag);
			var grabAnimationState = Animator.StringToHash(animationName);
			List<Matrix4x4> path = new List<Matrix4x4>();
			var spline = targetItem.GetComponent<SplinePathBuilder>();
			if (spline != null && spline.HasSplinePath)
			{
				path = new List<Matrix4x4>(spline.BuildPathFromTo(transform, targetItem.transform));
			}
			else
			{
				path = new List<Matrix4x4>(toolItem.GetToolGrabPathPointsFor(transform, targetItem));
			}
			StartPathFollowing(path, targetItem.transform);
			await UniTask.WaitUntil(() => _pathFollowingState == PathFollowingState.Completed, cancellationToken: token);
			if (!targetItem.IsAlive) throw new Exception("Target item is no longer alive");
			await UniTask.SwitchToMainThread(token);
			Debug.Log($"PlayerHand::ToolGrabAsync: Start tool grab animation `{grabAnimationState}`...");
			toolItem.PlayGrabAnimation(animationName, toolItem.ToolAnimationSmoothTime);
			await UniTask.WaitWhile(() => !toolItem.IsAnimationComplete(grabAnimationState), cancellationToken: token);
			if (!targetItem.IsAlive) throw new Exception("Target item is no longer alive");
			_currentStateRx.Value = HandState.UsingTool;
			_toolGrabbedItem = targetItem;
			_onToolGrabbedCommand.Execute(targetItem);
		}
		
		private async UniTask GrabAsync(GrabItem item, CancellationToken token)
		{
			_currentStateRx.Value = HandState.Grabbing;
			item.OnGrabbed();
			var animationName = item.GetGrabAnimationName();
			var grabAnimationState = Animator.StringToHash(animationName);
			List<Matrix4x4> path = new List<Matrix4x4>();
			var spline = item.GetComponent<SplinePathBuilder>();
			if (spline != null && spline.HasSplinePath)
				path = new List<Matrix4x4>(spline.BuildPathFromTo(transform, item.transform));
			else
				path = new List<Matrix4x4>(item.GetPathPointsFor(transform));
			StartPathFollowing(path, item.transform);
			await UniTask.WaitUntil(() => _pathFollowingState == PathFollowingState.Completed, cancellationToken: token);
			if (!item.IsAlive) throw new Exception("Item is dead");
			await UniTask.SwitchToMainThread(token);
			Debug.Log($"PlayerHand::GrabAsync: Start grab animation `{grabAnimationState}`...");
			_handAnimator.CrossFade(grabAnimationState, smoothTime);
			await UniTask.WaitWhile(() =>
				_handAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash != grabAnimationState ||
				_handAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime < 1,
				cancellationToken: token);
			if (!item.IsAlive) throw new Exception("Item is dead");
			_moveSpeed = Vector3.zero; // Reset velocity when transitioning to Grabbed state
			_currentStateRx.Value = HandState.Grabbed;
			_grabbedItem = item;
			_onGrabbedCommand.Execute(item);
			if (!item.ReturnToOrigin) ResetTarget();
		}
		
		public UniTask<bool> MoveTo(Transform target)
		{
			if (_currentStateRx.Value is HandState.Grabbing)
				return UniTask.FromResult(false);
			_moveSpeed = Vector3.zero; // Reset velocity when starting new movement
			Target.Value = target;
			var reachedObservable = _onReachedTargetCommand.Where(reachedTarget => reachedTarget == target).Select(_ => true);
			var canceledObservable = Target.Skip(1).Select(_ => false);
			return reachedObservable.Merge(canceledObservable).FirstAsync().AsUniTask();
		}
		
		public UniTask<bool> MoveAlongSplinePath(Transform target)
		{
			if (_currentStateRx.Value is HandState.Grabbing)
				return UniTask.FromResult(false);
			var spline = target.GetComponent<SplinePathBuilder>();
			if (spline != null && spline.HasSplinePath)
			{
				var waypoints = spline.BuildPathFromTo(transform, target);
				Target.Value = null;
				StartPathFollowing(new List<Matrix4x4>(waypoints), target);
			}
			else
			{
				Target.Value = target;
			}
			var reached = _onReachedTargetCommand.Where(reachedTarget => reachedTarget == target).Select(_ => true);
			var canceled = Target.Skip(1).Select(_ => false);
			return reached.Merge(canceled).FirstAsync().AsUniTask();
		}
		
		public void ResetTarget()
		{
			Target.Value = null;
			_pathFollowingState = PathFollowingState.None;
			_currentPath.Clear();
			_currentPathIndex = 0;
			_pathTarget = null;
		}
		
		public void DropToolGrabbedItem()
		{
			if (_currentStateRx.Value != HandState.UsingTool || _toolGrabbedItem == null) return;
			var droppedItem = _toolGrabbedItem;
			_toolGrabbedItem = null;
			_onToolDroppedCommand.Execute(droppedItem);
			_currentStateRx.Value = HandState.Grabbed;
		}
		
		public GrabItem DropTool()
		{
			if (_currentStateRx.Value != HandState.Grabbed && _currentStateRx.Value != HandState.UsingTool)
				return null;
			_grabCancellationToken?.Cancel();
			if (_currentStateRx.Value == HandState.UsingTool && _toolGrabbedItem != null)
			{
				var droppedToolItem = _toolGrabbedItem;
				_toolGrabbedItem = null;
				_onToolDroppedCommand.Execute(droppedToolItem);
			}
			var dropped = _grabbedItem;
			_grabbedItem = null;
			if (dropped != null) dropped.OnDropped();
			_onDroppedCommand.Execute(dropped);
			_handAnimator.CrossFade(IdleAnimKey, smoothTime);
			_moveSpeed = Vector3.zero; // Reset velocity when transitioning to Idle state
			_currentStateRx.Value = HandState.Idle;
			return dropped;
		}
		
		public GrabItem Drop()
		{
			if (_currentStateRx.Value == HandState.Idle)
				return null;
			_grabCancellationToken?.Cancel();
			_pathFollowingState = PathFollowingState.None;
			_currentPath.Clear();
			if (_currentStateRx.Value == HandState.Grabbing)
			{
				_handAnimator.CrossFade(IdleAnimKey, smoothTime);
				_moveSpeed = Vector3.zero;
				_currentStateRx.Value = HandState.Idle;
				return null;
			}
			if (_currentStateRx.Value == HandState.UsingTool && _toolGrabbedItem != null)
			{
				var droppedToolItem = _toolGrabbedItem;
				_toolGrabbedItem = null;
				_onToolDroppedCommand.Execute(droppedToolItem);
				_currentStateRx.Value = HandState.Grabbed;
				return droppedToolItem;
			}
			return DropTool();
		}
	}
}
