using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ryx.Core.Interaction
{
	public static class InteractionTargetSelector
	{
		private static bool _warnedEmptyInteractableMask;

		private static void WarnEmptyInteractableMaskOnce()
		{
			if (_warnedEmptyInteractableMask) return;
			_warnedEmptyInteractableMask = true;
			Debug.LogWarning(
				"[InteractionTargetSelector] InteractionSelectionProfile.InteractableMask is empty (Nothing). " +
				"Selection falls back to Physics.DefaultRaycastLayers and will be blocked by the nearest world " +
				"collider, so interaction prompts may silently fail to appear. Assign a dedicated 'Interactable' " +
				"layer to the profile's InteractableMask and to interaction-point colliders.");
		}

		private static class Buffers<TTarget>
			where TTarget : Component, IInteractionSelectionTarget
		{
			public static readonly RaycastHit[] Hits = new RaycastHit[64];
			public static readonly HashSet<TTarget> Candidates = new(64);
			public static readonly HashSet<TTarget> AddedTargets = new(16);
			public static readonly List<RaycastHit> SortedHits = new(64);
			public static readonly List<TTarget> SingleTargetOutput = new(1);
		}

		public static InteractionSelectionResult<TTarget> SelectBest<TTarget>(
			Camera camera,
			IReadOnlyList<TTarget> candidates,
			InteractionSelectionProfile profile)
			where TTarget : Component, IInteractionSelectionTarget
		{
			var output = Buffers<TTarget>.SingleTargetOutput;
			output.Clear();
			return SelectTopTargets(camera, candidates, profile, 1, output);
		}

		public static InteractionSelectionResult<TTarget> SelectTopTargets<TTarget>(
			Camera camera,
			IReadOnlyList<TTarget> candidates,
			InteractionSelectionProfile profile,
			int maxTargets,
			List<TTarget> output)
			where TTarget : Component, IInteractionSelectionTarget
		{
			if (output == null) throw new ArgumentNullException(nameof(output));
			output.Clear();

			var result = new InteractionSelectionResult<TTarget>
			{
				BestScore = float.MinValue,
				BestHitDistance = float.PositiveInfinity,
				CandidateCount = candidates?.Count ?? 0
			};

			if (!camera || candidates == null || candidates.Count == 0 || !profile)
			{
				return result;
			}

			var candidateSet = Buffers<TTarget>.Candidates;
			var addedTargets = Buffers<TTarget>.AddedTargets;
			candidateSet.Clear();
			addedTargets.Clear();
			for (var i = 0; i < candidates.Count; i++)
			{
				var target = candidates[i];
				if (target && target.CanParticipateInSelection())
				{
					candidateSet.Add(target);
				}
			}

			if (candidateSet.Count == 0) return result;

			var ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
			var maxDistance = Mathf.Max(profile.MaxSelectionDistance, 0.1f);
			var takeCount = Mathf.Clamp(maxTargets, 1, candidateSet.Count);
			var hitCount = BuildSortedRayHitList<TTarget>(ray, maxDistance, profile, out var usedFallbackMask);
			for (var i = 0; i < hitCount; i++)
			{
				var hit = Buffers<TTarget>.SortedHits[i];
				if (!hit.collider) continue;

				var target = hit.collider.GetComponentInParent<TTarget>();
				if (!target)
				{
					if (usedFallbackMask) break;
					continue;
				}

				if (!candidateSet.Contains(target) || !target.CanParticipateInSelection()) continue;
				if (profile.RequireLineOfSight && !target.IgnoreOcclusion &&
				    IsOccluded(ray.origin, ray.direction, hit.distance, target, profile.OcclusionMask))
				{
					result.OccludedCount++;
					continue;
				}

				result.EvaluatedCount++;
				if (!addedTargets.Add(target)) continue;

				output.Add(target);
				if (!result.BestTarget)
				{
					result.BestTarget = target;
					result.BestHitDistance = hit.distance;
					result.BestScore = Mathf.Clamp01(1f - hit.distance / maxDistance);
				}

				if (output.Count >= takeCount) break;
			}

			return result;
		}

		private static int BuildSortedRayHitList<TTarget>(
			Ray ray,
			float maxDistance,
			InteractionSelectionProfile profile,
			out bool usedFallbackMask)
			where TTarget : Component, IInteractionSelectionTarget
		{
			usedFallbackMask = false;
			if (profile.InteractableMask.value == 0)
			{
				WarnEmptyInteractableMaskOnce();
			}

			var hitCount = CollectAndSortRayHits<TTarget>(ray, maxDistance, profile.InteractableMask);
			if (hitCount > 0) return hitCount;

			// Fallback only runs when InteractableMask yields nothing (usually because the mask is
			// misconfigured/empty). It scans every default layer, so any world collider that sits in
			// front of an interaction point will block the selection (see the usedFallbackMask break in
			// SelectTopTargets). Assigning a dedicated interactable layer keeps us on the primary path.
			usedFallbackMask = true;
			return CollectAndSortRayHits<TTarget>(ray, maxDistance, Physics.DefaultRaycastLayers);
		}

		private static int CollectAndSortRayHits<TTarget>(Ray ray, float maxDistance, LayerMask mask)
			where TTarget : Component, IInteractionSelectionTarget
		{
			var sortedHits = Buffers<TTarget>.SortedHits;
			sortedHits.Clear();
			var hitCount = Physics.RaycastNonAlloc(
				ray,
				Buffers<TTarget>.Hits,
				maxDistance,
				mask,
				QueryTriggerInteraction.Collide);

			for (var i = 0; i < hitCount; i++)
			{
				var hit = Buffers<TTarget>.Hits[i];
				if (!hit.collider) continue;
				InsertRayHitByDistance(sortedHits, hit);
			}

			return sortedHits.Count;
		}

		private static void InsertRayHitByDistance(List<RaycastHit> sortedHits, RaycastHit hit)
		{
			for (var i = 0; i < sortedHits.Count; i++)
			{
				if (hit.distance >= sortedHits[i].distance) continue;
				sortedHits.Insert(i, hit);
				return;
			}

			sortedHits.Add(hit);
		}

		private static bool IsOccluded<TTarget>(
			Vector3 origin,
			Vector3 direction,
			float distance,
			TTarget target,
			LayerMask occlusionMask)
			where TTarget : Component, IInteractionSelectionTarget
		{
			if (distance <= 0.001f) return false;
			if (!Physics.Raycast(origin, direction, out var hit, distance, occlusionMask, QueryTriggerInteraction.Ignore))
			{
				return false;
			}

			var root = target.SelectionRoot ? target.SelectionRoot : target.transform;
			return !hit.collider.transform.IsChildOf(root);
		}
	}
}
