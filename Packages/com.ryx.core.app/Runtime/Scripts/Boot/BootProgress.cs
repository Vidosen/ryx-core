using System;

namespace Ryx.Core.App
{
    /// <summary>Snapshot of boot progress. Value type: safe to hand to presentation.</summary>
    public readonly struct BootProgress : IEquatable<BootProgress>
    {
        public BootProgress(int completedSteps, int totalSteps, float normalized, string description)
        {
            CompletedSteps = completedSteps;
            TotalSteps = totalSteps;
            Normalized = normalized;
            Description = description ?? string.Empty;
        }

        public int CompletedSteps { get; }
        public int TotalSteps { get; }

        /// <summary>Weighted completion in the 0..1 range.</summary>
        public float Normalized { get; }

        /// <summary>Description of the step currently running, or the last one that finished.</summary>
        public string Description { get; }

        public bool IsComplete => TotalSteps > 0 && CompletedSteps >= TotalSteps;

        public bool Equals(BootProgress other) =>
            CompletedSteps == other.CompletedSteps &&
            TotalSteps == other.TotalSteps &&
            Math.Abs(Normalized - other.Normalized) < 0.0001f &&
            string.Equals(Description, other.Description, StringComparison.Ordinal);

        public override bool Equals(object obj) => obj is BootProgress other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(CompletedSteps, TotalSteps, Normalized, Description);
        public override string ToString() => $"{Description} ({CompletedSteps}/{TotalSteps}, {Normalized:P0})";
    }

    public sealed class BootFailedException : Exception
    {
        public BootFailedException(string stepDescription, Exception inner)
            : base($"Boot step '{stepDescription}' failed: {inner?.Message}", inner)
        {
            StepDescription = stepDescription;
        }

        public string StepDescription { get; }
    }
}
