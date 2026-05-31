using UnityEngine;

namespace Ryx.Core.Interaction
{
	public interface IInteractionSelectionTarget
	{
		Transform SelectionRoot { get; }
		bool IgnoreOcclusion { get; }
		bool CanParticipateInSelection();
	}
}
