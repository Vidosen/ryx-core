using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ryx.Core.App
{
    /// <summary>
    /// One unit of startup work. Steps run in the order they were registered and must be
    /// idempotent enough to survive a retry after a recoverable failure.
    /// </summary>
    public interface IBootStep
    {
        /// <summary>Player-facing label, e.g. "Loading save data". Shown on the boot screen.</summary>
        string Description { get; }

        /// <summary>
        /// Relative cost of this step, used to weight progress. Default to 1 unless a step is
        /// known to dominate boot time.
        /// </summary>
        float Weight => 1f;

        UniTask ExecuteAsync(CancellationToken cancellationToken);
    }

    /// <summary>Lambda-backed steps, for wiring that does not deserve its own type.</summary>
    public static class BootStep
    {
        public static IBootStep FromAction(string description, Action action, float weight = 1f)
        {
            if (action == null) throw new ArgumentNullException(nameof(action));
            return new DelegateBootStep(description, weight, _ =>
            {
                action();
                return UniTask.CompletedTask;
            });
        }

        public static IBootStep FromAsync(string description, Func<CancellationToken, UniTask> work, float weight = 1f)
        {
            if (work == null) throw new ArgumentNullException(nameof(work));
            return new DelegateBootStep(description, weight, work);
        }

        private sealed class DelegateBootStep : IBootStep
        {
            private readonly Func<CancellationToken, UniTask> _work;

            public DelegateBootStep(string description, float weight, Func<CancellationToken, UniTask> work)
            {
                Description = string.IsNullOrWhiteSpace(description) ? "Working" : description;
                Weight = weight <= 0f ? 1f : weight;
                _work = work;
            }

            public string Description { get; }
            public float Weight { get; }
            public UniTask ExecuteAsync(CancellationToken cancellationToken) => _work(cancellationToken);
        }
    }
}
