using UnityEngine;

namespace Ryx.Core.Hands
{
	public class GrabItem : MonoBehaviour, IGrabItem
	{
		[SerializeField] private string _itemID;
		[SerializeField] private ItemTagSO _itemTag;
		[SerializeField] private Transform _grabPoint;
		[SerializeField] private bool _enableCustomGrabMaxSpeed;
		[SerializeField] private float _customGrabMaxSpeed;
		[SerializeField] private bool _returnToOrigin = false;

		private RigidbodySnapshot? _rigidbodySnapshot;

		public Transform Transform => _grabPoint;
		public ItemTagSO ItemTag => _itemTag;
		public string ItemID => _itemID;
		public bool IsAlive => gameObject;
		public bool ReturnToOrigin => _returnToOrigin;
		public bool EnableCustomGrabMaxSpeed => _enableCustomGrabMaxSpeed;
		public float CustomGrabMaxSpeed => _customGrabMaxSpeed;

		public virtual bool CanBeGrabbedDirectly => true;

		public void OnGrabbed()
		{
			var rb = GetComponent<Rigidbody>();
			_rigidbodySnapshot = RigidbodySnapshot.Capture(rb);
			if (rb)
				Destroy(rb);
		}

		public void OnDropped()
		{
			if (_rigidbodySnapshot.HasValue)
			{
				var rb = gameObject.AddComponent<Rigidbody>();
				_rigidbodySnapshot.Value.ApplyTo(rb);
				_rigidbodySnapshot = null;
			}
		}

		public void SetPositionAndRotation(Vector3 position, Quaternion rotation)
		{
			transform.SetPositionAndRotation(position, rotation);
		}
		
		public Matrix4x4[] GetPathPointsFor(Transform tm)
		{
			return new[]
			{
				Matrix4x4.TRS(tm.position, tm.rotation, Vector3.one),
				Matrix4x4.TRS(_grabPoint.position, _grabPoint.rotation, Vector3.one)
			};
		}
		public void CopyFrom(GrabItem grabItem, ItemTagSO itemTag)
		{
			_itemTag = itemTag;
			_grabPoint = grabItem._grabPoint;
			_returnToOrigin = grabItem._returnToOrigin;
			_enableCustomGrabMaxSpeed = grabItem._enableCustomGrabMaxSpeed;
			_customGrabMaxSpeed = grabItem._customGrabMaxSpeed;
			_itemID = grabItem._itemID;
		}

		public string GetGrabAnimationName()
		{
			return "Grab_" + _itemID;
		}
	}
}

