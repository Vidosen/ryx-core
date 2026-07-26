using System;
using System.Threading;
using Cysharp.Threading.Tasks;

namespace Ryx.Core.App
{
    /// <summary>
    /// Scene loading port. Keeps <c>UnityEngine.SceneManagement</c> at the boundary so flow
    /// orchestration stays testable without entering play mode.
    /// </summary>
    public interface ISceneLoader
    {
        UniTask LoadSingleAsync(SceneId scene, IProgress<float> progress = null, CancellationToken cancellationToken = default);

        UniTask LoadAdditiveAsync(SceneId scene, IProgress<float> progress = null, CancellationToken cancellationToken = default);

        UniTask UnloadAsync(SceneId scene, CancellationToken cancellationToken = default);

        bool IsLoaded(SceneId scene);
    }

    /// <summary>
    /// Covers and reveals the screen around a scene swap. Implemented by presentation
    /// (a fade canvas, a loading screen); defaulted away with <see cref="NullSceneTransition"/>.
    /// </summary>
    public interface ISceneTransition
    {
        /// <summary>Covers the screen. Awaited before the outgoing scene is unloaded.</summary>
        UniTask CoverAsync(CancellationToken cancellationToken = default);

        /// <summary>Reveals the screen. Awaited after the incoming scene is active.</summary>
        UniTask RevealAsync(CancellationToken cancellationToken = default);

        /// <summary>Called while the incoming scene loads, with 0..1 progress.</summary>
        void ReportProgress(float normalized);
    }

    public sealed class NullSceneTransition : ISceneTransition
    {
        public static readonly NullSceneTransition Instance = new();
        private NullSceneTransition() { }

        public UniTask CoverAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
        public UniTask RevealAsync(CancellationToken cancellationToken = default) => UniTask.CompletedTask;
        public void ReportProgress(float normalized) { }
    }
}
