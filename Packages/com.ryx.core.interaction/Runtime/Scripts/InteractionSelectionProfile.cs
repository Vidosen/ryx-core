using UnityEngine;

namespace Ryx.Core.Interaction
{
	[CreateAssetMenu(fileName = "InteractionSelectionProfile", menuName = "Ryx/Interaction/Selection Profile")]
	public class InteractionSelectionProfile : ScriptableObject
	{
		[SerializeField, Min(0.1f)] private float maxSelectionDistance = 6f;
		[SerializeField] private LayerMask interactableMask;
		[SerializeField] private LayerMask occlusionMask;
		[SerializeField] private bool requireLineOfSight = true;
		[SerializeField] private bool debugLogging;

		public float MaxSelectionDistance => maxSelectionDistance;
		public LayerMask InteractableMask => interactableMask;
		public LayerMask OcclusionMask => occlusionMask;
		public bool RequireLineOfSight => requireLineOfSight;
		public bool DebugLogging => debugLogging;

		private void OnEnable()
		{
			EnsureMasks();
		}

		private void Reset()
		{
			EnsureMasks();
		}

		public static InteractionSelectionProfile CreateRuntimeDefault()
		{
			var profile = CreateInstance<InteractionSelectionProfile>();
			profile.hideFlags = HideFlags.HideAndDontSave;
			profile.EnsureMasks();
			return profile;
		}

		private void EnsureMasks()
		{
			if (interactableMask.value == 0)
			{
				interactableMask = LayerMask.GetMask("Interactable");
			}
		}
	}
}
