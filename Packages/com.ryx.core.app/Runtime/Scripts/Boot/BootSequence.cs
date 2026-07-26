using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Ryx.Core.App
{
    /// <summary>
    /// Runs <see cref="IBootStep"/>s in registration order exactly once, publishing weighted
    /// progress. Plain C#: construct it in the composition root, no <c>MonoBehaviour</c>,
    /// no container coupling.
    /// </summary>
    public sealed class BootSequence : IDisposable
    {
        private readonly IReadOnlyList<IBootStep> _steps;
        private readonly ReactiveProperty<BootProgress> _progress;
        private readonly float _totalWeight;

        private bool _disposed;

        public BootSequence(IEnumerable<IBootStep> steps)
        {
            if (steps == null) throw new ArgumentNullException(nameof(steps));

            _steps = steps.Where(step => step != null).ToArray();
            _totalWeight = _steps.Sum(step => Math.Max(step.Weight, 0f));
            if (_totalWeight <= 0f) _totalWeight = Math.Max(_steps.Count, 1);

            _progress = new ReactiveProperty<BootProgress>(
                new BootProgress(0, _steps.Count, 0f, _steps.Count > 0 ? _steps[0].Description : "Ready"));
        }

        public BootSequence(params IBootStep[] steps) : this((IEnumerable<IBootStep>)steps)
        {
        }

        public ReadOnlyReactiveProperty<BootProgress> Progress => _progress;

        public bool HasRun { get; private set; }

        public int StepCount => _steps.Count;

        /// <summary>
        /// Executes every step in order. Throws <see cref="BootFailedException"/> wrapping the
        /// first failure; cancellation propagates as <see cref="OperationCanceledException"/>.
        /// </summary>
        public async UniTask RunAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(BootSequence));
            if (HasRun) throw new InvalidOperationException("BootSequence has already run.");

            HasRun = true;
            var accumulatedWeight = 0f;

            for (var index = 0; index < _steps.Count; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var step = _steps[index];
                Publish(index, accumulatedWeight, step.Description);

                try
                {
                    await step.ExecuteAsync(cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception error)
                {
                    throw new BootFailedException(step.Description, error);
                }

                accumulatedWeight += Math.Max(step.Weight, 0f);
                Publish(index + 1, accumulatedWeight, step.Description);
            }

            Publish(_steps.Count, _totalWeight, "Ready");
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _progress.Dispose();
        }

        private void Publish(int completedSteps, float accumulatedWeight, string description)
        {
            var normalized = Math.Clamp(accumulatedWeight / _totalWeight, 0f, 1f);
            _progress.Value = new BootProgress(completedSteps, _steps.Count, normalized, description);
        }
    }
}
