using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;

namespace Ryx.Core.Hands
{
	public class SplinePathBuilder : MonoBehaviour
	{
		[SerializeField] private SplineContainer splineContainer;
		[SerializeField] private int pathPointsCount = 10;
		[SerializeField] private bool useLocalSpace = true;
		[SerializeField] private bool alignWithSourceDestination = true;
		[SerializeField] private bool returnPathOrientation = true;
		[SerializeField] private bool maintainPathHeight = true;
		[SerializeField] private bool inverseAlign = false;
		
		public bool HasSplinePath => splineContainer != null && splineContainer.Spline.Count > 0;
		
		private void Reset()
		{
			if (splineContainer == null)
			{
				splineContainer = GetComponent<SplineContainer>();
				if (splineContainer == null)
				{
					splineContainer = gameObject.AddComponent<SplineContainer>();
				}
			}
		}
		
		public Matrix4x4[] BuildPathFrom(Transform source)
		{
			return BuildPathFromTo(source, transform);
		}
		
		public Matrix4x4[] BuildPathFromTo(Transform source, Transform target)
		{
			if (!HasSplinePath)
			{
				return new[] { Matrix4x4.TRS(source.position, source.rotation, Vector3.one), Matrix4x4.TRS(target.position, target.rotation, Vector3.one) };
			}
			
			var sourcePosition = source.position;
			var targetPosition = target.position;
			var points = new List<Matrix4x4>(pathPointsCount);
			var originalRotation = transform.rotation;
			if (alignWithSourceDestination)
			{
				Vector3 sourceToDestXZ = targetPosition - sourcePosition;
				if (maintainPathHeight) sourceToDestXZ.y = 0;
				if (sourceToDestXZ.magnitude > 0.001f)
				{
					Quaternion targetRotation = Quaternion.LookRotation(inverseAlign ? -sourceToDestXZ.normalized : sourceToDestXZ.normalized);
					transform.rotation = targetRotation;
				}
			}
			for (int i = 0; i < pathPointsCount; i++)
			{
				float t = i / (float)(pathPointsCount - 1);
				Vector3 position;
				Quaternion rotation;
				if (useLocalSpace)
				{
					position = splineContainer.EvaluatePosition(t);
					position = splineContainer.transform.TransformPoint(position);
					rotation = Quaternion.LookRotation(splineContainer.EvaluateTangent(t), splineContainer.EvaluateUpVector(t));
				}
				else
				{
					position = splineContainer.EvaluatePosition(t);
					rotation = Quaternion.LookRotation(splineContainer.EvaluateTangent(t), splineContainer.EvaluateUpVector(t));
				}
				points.Add(Matrix4x4.TRS(position, rotation, Vector3.one));
			}
			if (alignWithSourceDestination && returnPathOrientation)
				transform.rotation = originalRotation;
			return points.ToArray();
		}
	}
}

