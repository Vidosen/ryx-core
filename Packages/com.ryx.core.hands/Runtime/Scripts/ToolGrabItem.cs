using System.Collections.Generic;
using UnityEngine;

namespace Ryx.Core.Hands
{
	public class ToolGrabItem : GrabItem, IToolGrabItem
	{
		[SerializeField] private List<ItemTagSO> _compatibleItemTags = new();
		[SerializeField] private Transform _toolGrabPoint;
		[SerializeField] private float _toolGrabOffset = 0.1f;
		[SerializeField] private string _toolGrabAnimationPrefix = "ToolGrab_";
		[SerializeField] private Animator _toolAnimator;
		[SerializeField] private float _toolAnimationSmoothTime = 0.2f;
		
		public IReadOnlyList<ItemTagSO> CompatibleItemTags => _compatibleItemTags;
		public Transform ToolGrabPoint => _toolGrabPoint;
		public float ToolGrabOffset => _toolGrabOffset;
		public Animator ToolAnimator => _toolAnimator;
		public float ToolAnimationSmoothTime => _toolAnimationSmoothTime;
		
		public bool CanGrabItemTag(ItemTagSO itemTag) => _compatibleItemTags.Contains(itemTag);
		
		public Matrix4x4[] GetToolGrabPathPointsFor(Transform tm, GrabItem targetItem)
		{
			return new Matrix4x4[] {
				Matrix4x4.TRS(_toolGrabPoint.position, _toolGrabPoint.rotation, Vector3.one),
				Matrix4x4.TRS(Vector3.Lerp(_toolGrabPoint.position, targetItem.Transform.position, 0.4f), Quaternion.identity, Vector3.one),
				Matrix4x4.TRS(targetItem.Transform.position, Quaternion.identity, Vector3.one)
			};
		}
		
		public string GetToolGrabAnimationName(ItemTagSO itemTag) => $"{_toolGrabAnimationPrefix}{itemTag.name}";
		
		public void PlayGrabAnimation(string animationName, float smoothTime)
		{
			if (_toolAnimator != null)
				_toolAnimator.CrossFade(animationName, smoothTime);
		}
		
		public bool IsAnimationComplete(int animationHash)
		{
			if (_toolAnimator == null) return true;
			return _toolAnimator.GetCurrentAnimatorStateInfo(0).shortNameHash == animationHash &&
			       _toolAnimator.GetCurrentAnimatorStateInfo(0).normalizedTime >= 1;
		}
	}
}

