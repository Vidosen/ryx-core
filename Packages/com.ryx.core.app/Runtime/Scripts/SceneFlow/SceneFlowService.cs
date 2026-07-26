using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;

namespace Ryx.Core.App
{
    /// <summary>
    /// Orchestrates "go to scene X": cover the screen, swap scenes, reveal. Owns the notion
    /// of which scene the game is currently in.
    /// </summary>
    public interface ISceneFlowService
    {
        ReadOnlyReactiveProperty<SceneId> CurrentScene { get; }

        /// <summary>True while a transition is in flight. Further requests are rejected.</summary>
        ReadOnlyReactiveProperty<bool> IsTransitioning { get; }

        UniTask GoToAsync(SceneId scene, CancellationToken cancellationToken = default);

        UniTask LoadOverlayAsync(SceneId scene, CancellationToken cancellationToken = default);

        UniTask UnloadOverlayAsync(SceneId scene, CancellationToken cancellationToken = default);
    }

    public sealed class SceneFlowService : ISceneFlowService, IDisposable
    {
        private readonly ISceneLoader _loader;
        private readonly ISceneTransition _transition;
        private readonly ReactiveProperty<SceneId> _currentScene;
        private readonly ReactiveProperty<bool> _isTransitioning = new(false);

        private bool _disposed;

        public SceneFlowService(ISceneLoader loader, ISceneTransition transition = null, SceneId initialScene = default)
        {
            _loader = loader ?? throw new ArgumentNullException(nameof(loader));
            _transition = transition ?? NullSceneTransition.Instance;
            _currentScene = new ReactiveProperty<SceneId>(initialScene);
        }

        public ReadOnlyReactiveProperty<SceneId> CurrentScene => _currentScene;
        public ReadOnlyReactiveProperty<bool> IsTransitioning => _isTransitioning;

        public async UniTask GoToAsync(SceneId scene, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();

            if (!scene.IsValid)
            {
                throw new ArgumentException("Scene id is not valid.", nameof(scene));
            }

            if (_isTransitioning.Value)
            {
                throw new InvalidOperationException(
                    $"A scene transition is already running; cannot go to '{scene}'.");
            }

            _isTransitioning.Value = true;
            try
            {
                await _transition.CoverAsync(cancellationToken);

                // Fully qualified: `Progress` is ambiguous between Cysharp's static helper
                // and System.Progress<T> once `using System;` is in scope.
                var progress = Cysharp.Threading.Tasks.Progress.CreateOnlyValueChanged<float>(_transition.ReportProgress);
                await _loader.LoadSingleAsync(scene, progress, cancellationToken);

                _currentScene.Value = scene;

                await _transition.RevealAsync(cancellationToken);
            }
            finally
            {
                _isTransitioning.Value = false;
            }
        }

        public UniTask LoadOverlayAsync(SceneId scene, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _loader.IsLoaded(scene)
                ? UniTask.CompletedTask
                : _loader.LoadAdditiveAsync(scene, null, cancellationToken);
        }

        public UniTask UnloadOverlayAsync(SceneId scene, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            return _loader.UnloadAsync(scene, cancellationToken);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _currentScene.Dispose();
            _isTransitioning.Dispose();
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(SceneFlowService));
        }
    }
}
