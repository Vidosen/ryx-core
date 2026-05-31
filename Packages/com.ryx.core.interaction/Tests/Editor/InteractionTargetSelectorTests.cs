using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace Ryx.Core.Interaction.Tests
{
	public class InteractionTargetSelectorTests
	{
		private const BindingFlags InstanceNonPublic = BindingFlags.Instance | BindingFlags.NonPublic;
		private readonly List<Object> _created = new();

		[TearDown]
		public void TearDown()
		{
			foreach (var item in _created)
			{
				if (item) Object.DestroyImmediate(item);
			}
			_created.Clear();
		}

		[Test]
		public void SelectBest_ReturnsNearestTargetOnCenterRay()
		{
			var camera = CreateCamera();
			var far = CreateTarget("Far", new Vector3(0f, 0f, 5f));
			var near = CreateTarget("Near", new Vector3(0f, 0f, 2f));
			var profile = CreateProfile();
			Physics.SyncTransforms();

			var result = InteractionTargetSelector.SelectBest(camera, new List<TestTarget> { far, near }, profile);

			Assert.That(result.BestTarget, Is.EqualTo(near));
		}

		[Test]
		public void SelectBest_IgnoresOffRayAndOutOfRangeTargets()
		{
			var camera = CreateCamera();
			var offRay = CreateTarget("OffRay", new Vector3(3f, 0f, 2f));
			var tooFar = CreateTarget("TooFar", new Vector3(0f, 0f, 8f));
			var profile = CreateProfile();
			SetProfileField(profile, "maxSelectionDistance", 4f);
			Physics.SyncTransforms();

			var result = InteractionTargetSelector.SelectBest(camera, new List<TestTarget> { offRay, tooFar }, profile);

			Assert.That(result.BestTarget, Is.Null);
		}

		[Test]
		public void SelectBest_FallbackMaskStopsAtBlockingCollider()
		{
			var camera = CreateCamera();
			var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
			blocker.transform.position = new Vector3(0f, 0f, 1f);
			_created.Add(blocker);
			var target = CreateTarget("BehindBlocker", new Vector3(0f, 0f, 3f));
			var profile = CreateProfile();
			SetProfileField(profile, "interactableMask", (LayerMask)(1 << 8));
			Physics.SyncTransforms();

			var result = InteractionTargetSelector.SelectBest(camera, new List<TestTarget> { target }, profile);

			Assert.That(result.BestTarget, Is.Null);
		}

		[Test]
		public void SelectBest_RespectsOcclusionUnlessTargetOptsOut()
		{
			var camera = CreateCamera();
			var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
			blocker.transform.position = new Vector3(0f, 0f, 1f);
			blocker.layer = 9;
			_created.Add(blocker);
			var target = CreateTarget("Target", new Vector3(0f, 0f, 3f));
			target.gameObject.layer = 8;
			var profile = CreateProfile();
			SetProfileField(profile, "interactableMask", (LayerMask)(1 << 8));
			SetProfileField(profile, "occlusionMask", (LayerMask)(1 << 9));
			Physics.SyncTransforms();

			var occluded = InteractionTargetSelector.SelectBest(camera, new List<TestTarget> { target }, profile);
			target.IgnoreOcclusionValue = true;
			var visible = InteractionTargetSelector.SelectBest(camera, new List<TestTarget> { target }, profile);

			Assert.That(occluded.BestTarget, Is.Null);
			Assert.That(occluded.OccludedCount, Is.EqualTo(1));
			Assert.That(visible.BestTarget, Is.EqualTo(target));
		}

		[Test]
		public void SelectTopTargets_ReturnsHitOrder()
		{
			var camera = CreateCamera();
			var first = CreateTarget("First", new Vector3(0f, 0f, 2f));
			var second = CreateTarget("Second", new Vector3(0f, 0f, 4f));
			var output = new List<TestTarget>();
			Physics.SyncTransforms();

			InteractionTargetSelector.SelectTopTargets(
				camera,
				new List<TestTarget> { second, first },
				CreateProfile(),
				2,
				output);

			Assert.That(output, Is.EqualTo(new[] { first, second }));
		}

		private Camera CreateCamera()
		{
			var go = new GameObject("Camera");
			_created.Add(go);
			return go.AddComponent<Camera>();
		}

		private TestTarget CreateTarget(string name, Vector3 position)
		{
			var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
			go.name = name;
			go.transform.position = position;
			_created.Add(go);
			return go.AddComponent<TestTarget>();
		}

		private InteractionSelectionProfile CreateProfile()
		{
			var profile = InteractionSelectionProfile.CreateRuntimeDefault();
			_created.Add(profile);
			return profile;
		}

		private static void SetProfileField(InteractionSelectionProfile profile, string fieldName, object value)
		{
			var field = typeof(InteractionSelectionProfile).GetField(fieldName, InstanceNonPublic);
			Assert.That(field, Is.Not.Null);
			field.SetValue(profile, value);
		}

		private sealed class TestTarget : MonoBehaviour, IInteractionSelectionTarget
		{
			public Transform SelectionRoot => transform;
			public bool IgnoreOcclusion => IgnoreOcclusionValue;
			public bool IgnoreOcclusionValue { get; set; }
			public bool CanParticipateInSelection() => true;
		}
	}
}
