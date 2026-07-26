using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using R3;
using UnityEngine;

namespace Ryx.Core.App
{
    /// <summary>
    /// Scene-side entry point. Runs the <see cref="BootSequence"/>, then hands control to the
    /// first gameplay scene.
    /// </summary>
    /// <remarks>
    /// Container-agnostic by design, matching the rest of Ryx Core: the composition root calls
    /// <see cref="Configure"/> (from a Reflex installer, a Zenject installer, or plain code)
    /// before <c>Start</c> runs. If nothing configures it, it logs and does nothing rather than
    /// reaching out to a service locator.
    /// </remarks>
    [DefaultExecutionOrder(-100)]
    public sealed class AppBootstrapper : MonoBehaviour
    {
        [SerializeField] private SceneId _firstScene;
        [SerializeField] private bool _runOnStart = true;

        private BootSequence _bootSequence;
        private ISceneFlowService _sceneFlow;
        private CancellationTokenSource _cts;
        private bool _configured;

        /// <summary>Raised for each progress update while booting. Subscribe from a boot screen view.</summary>
        public Observable<BootProgress> Progress =>
            _bootSequence != null ? _bootSequence.Progress : Observable.Empty<BootProgress>();

        public void Configure(BootSequence bootSequence, ISceneFlowService sceneFlow, SceneId? firstScene = null)
        {
            _bootSequence = bootSequence ?? throw new ArgumentNullException(nameof(bootSequence));
            _sceneFlow = sceneFlow ?? throw new ArgumentNullException(nameof(sceneFlow));

            if (firstScene.HasValue)
            {
                _firstScene = firstScene.Value;
            }

            _configured = true;
        }

        private void Start()
        {
            if (_runOnStart)
            {
                RunAsync().Forget();
            }
        }

        public async UniTask RunAsync()
        {
            if (!_configured)
            {
                Debug.LogError(
                    $"[Ryx.Core.App] {nameof(AppBootstrapper)} on '{name}' was never configured. " +
                    "Call Configure(bootSequence, sceneFlow) from your composition root.", this);
                return;
            }

            if (!_firstScene.IsValid)
            {
                Debug.LogError($"[Ryx.Core.App] {nameof(AppBootstrapper)} has no first scene assigned.", this);
                return;
            }

            // Guard re-entry. A second call would leak the first CTS and then hit
            // BootSequence's "already run" guard as an unhandled exception through Forget().
            if (_cts != null)
            {
                Debug.LogWarning($"[Ryx.Core.App] {nameof(AppBootstrapper)} is already running; ignoring.", this);
                return;
            }

            _cts = CancellationTokenSource.CreateLinkedTokenSource(destroyCancellationToken);

            try
            {
                await _bootSequence.RunAsync(_cts.Token);
                await _sceneFlow.GoToAsync(_firstScene, _cts.Token);
            }
            catch (OperationCanceledException)
            {
                // Application is shutting down or the object was destroyed mid-boot.
            }
            catch (BootFailedException error)
            {
                Debug.LogException(error, this);
            }
        }

        private void OnDestroy()
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = null;
        }
    }
}
