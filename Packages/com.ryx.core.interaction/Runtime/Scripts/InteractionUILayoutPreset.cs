using UnityEngine;

namespace Ryx.Core.Interaction
{
	public enum InteractionUILayoutType
	{
		Snapped,
		Fixed
	}

	[CreateAssetMenu(fileName = "InteractionUILayoutPreset", menuName = "Ryx/Interaction/UI Layout Preset")]
	public class InteractionUILayoutPreset : ScriptableObject
	{
		[SerializeField] private InteractionUILayoutType _layoutType;
		[SerializeField] private Vector3 _worldOffset = Vector3.zero;
		[SerializeField] private string _fixedAnchorID = "";

		public InteractionUILayoutType LayoutType => _layoutType;
		public Vector3 WorldOffset => _worldOffset;
		public string FixedAnchorID => _fixedAnchorID;
	}
}

