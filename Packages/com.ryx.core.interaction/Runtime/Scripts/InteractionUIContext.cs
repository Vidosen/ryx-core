using UnityEngine;

namespace Ryx.Core.Interaction
{
	/// <summary>
	/// Defines a UI context that controls which interaction buttons are visible.
	/// When a context is active, only buttons valid for that context (or global buttons) are shown.
	/// </summary>
	[CreateAssetMenu(fileName = "UIContext", menuName = "Ryx/Interaction/UI Context")]
	public class InteractionUIContext : ScriptableObject
	{
		[SerializeField] private string _contextId;
		[SerializeField] private bool _isExclusive = true;
		[SerializeField] private InteractionType _linkedMode = InteractionType.None;

		/// <summary>
		/// Unique identifier for this context. Defaults to asset name if not specified.
		/// </summary>
		public string ContextId => string.IsNullOrEmpty(_contextId) ? name : _contextId;

		/// <summary>
		/// When true, activating this context hides buttons not explicitly valid for it.
		/// When false, this context is additive (buttons from other contexts remain visible).
		/// </summary>
		public bool IsExclusive => _isExclusive;

		/// <summary>
		/// Optional linked InteractionType for auto-mapping.
		/// When FPSInteractionService enters this mode, this context can be auto-pushed.
		/// </summary>
		public InteractionType LinkedMode => _linkedMode;

		/// <summary>
		/// Checks if this context has a linked mode for auto-mapping.
		/// </summary>
		public bool HasLinkedMode => _linkedMode != InteractionType.None;

		public override string ToString() => ContextId;
	}
}
