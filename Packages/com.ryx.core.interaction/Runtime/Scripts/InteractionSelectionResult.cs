namespace Ryx.Core.Interaction
{
	public struct InteractionSelectionResult<TTarget>
		where TTarget : UnityEngine.Component, IInteractionSelectionTarget
	{
		public TTarget BestTarget;
		public float BestScore;
		public float BestHitDistance;
		public int CandidateCount;
		public int EvaluatedCount;
		public int OccludedCount;
	}
}
