using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;

namespace Ryx.Core.App.Tests
{
    public sealed class SceneFlowServiceTests
    {
        private static readonly SceneId Boot = new("Boot");
        private static readonly SceneId Station = new("Station");
        private static readonly SceneId Hud = new("Hud");

        private sealed class FakeSceneLoader : ISceneLoader
        {
            public readonly List<string> Calls = new();
            private readonly HashSet<SceneId> _loaded = new();

            public UniTask LoadSingleAsync(SceneId scene, IProgress<float> progress = null, CancellationToken cancellationToken = default)
            {
                Calls.Add($"load-single:{scene}");
                progress?.Report(0.5f);
                progress?.Report(1f);
                _loaded.Clear();
                _loaded.Add(scene);
                return UniTask.CompletedTask;
            }

            public UniTask LoadAdditiveAsync(SceneId scene, IProgress<float> progress = null, CancellationToken cancellationToken = default)
            {
                Calls.Add($"load-additive:{scene}");
                _loaded.Add(scene);
                return UniTask.CompletedTask;
            }

            public UniTask UnloadAsync(SceneId scene, CancellationToken cancellationToken = default)
            {
                Calls.Add($"unload:{scene}");
                _loaded.Remove(scene);
                return UniTask.CompletedTask;
            }

            public bool IsLoaded(SceneId scene) => _loaded.Contains(scene);
        }

        private sealed class RecordingTransition : ISceneTransition
        {
            public readonly List<string> Calls = new();
            public readonly List<float> Progress = new();

            public UniTask CoverAsync(CancellationToken cancellationToken = default)
            {
                Calls.Add("cover");
                return UniTask.CompletedTask;
            }

            public UniTask RevealAsync(CancellationToken cancellationToken = default)
            {
                Calls.Add("reveal");
                return UniTask.CompletedTask;
            }

            public void ReportProgress(float normalized) => Progress.Add(normalized);
        }

        [Test]
        public void GoToAsync_CoversLoadsThenReveals()
        {
            var loader = new FakeSceneLoader();
            var transition = new RecordingTransition();
            using var flow = new SceneFlowService(loader, transition, Boot);

            flow.GoToAsync(Station).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "cover", "reveal" }, transition.Calls);
            CollectionAssert.AreEqual(new[] { "load-single:Station" }, loader.Calls);
        }

        [Test]
        public void GoToAsync_UpdatesCurrentScene()
        {
            var loader = new FakeSceneLoader();
            using var flow = new SceneFlowService(loader, null, Boot);
            Assert.AreEqual(Boot, flow.CurrentScene.CurrentValue);

            flow.GoToAsync(Station).GetAwaiter().GetResult();

            Assert.AreEqual(Station, flow.CurrentScene.CurrentValue);
        }

        [Test]
        public void GoToAsync_ForwardsLoadProgressToTransition()
        {
            var transition = new RecordingTransition();
            using var flow = new SceneFlowService(new FakeSceneLoader(), transition, Boot);

            flow.GoToAsync(Station).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { 0.5f, 1f }, transition.Progress);
        }

        [Test]
        public void GoToAsync_ResetsTransitionFlagAfterCompletion()
        {
            using var flow = new SceneFlowService(new FakeSceneLoader(), null, Boot);

            flow.GoToAsync(Station).GetAwaiter().GetResult();

            Assert.IsFalse(flow.IsTransitioning.CurrentValue);
        }

        [Test]
        public void GoToAsync_InvalidScene_Throws()
        {
            using var flow = new SceneFlowService(new FakeSceneLoader(), null, Boot);

            Assert.Throws<ArgumentException>(() =>
                flow.GoToAsync(default).GetAwaiter().GetResult());
        }

        [Test]
        public void LoadOverlayAsync_IsIdempotent()
        {
            var loader = new FakeSceneLoader();
            using var flow = new SceneFlowService(loader, null, Boot);

            flow.LoadOverlayAsync(Hud).GetAwaiter().GetResult();
            flow.LoadOverlayAsync(Hud).GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "load-additive:Hud" }, loader.Calls);
        }

        [Test]
        public void UnloadOverlayAsync_DelegatesToLoader()
        {
            var loader = new FakeSceneLoader();
            using var flow = new SceneFlowService(loader, null, Boot);
            flow.LoadOverlayAsync(Hud).GetAwaiter().GetResult();

            flow.UnloadOverlayAsync(Hud).GetAwaiter().GetResult();

            CollectionAssert.Contains(loader.Calls, "unload:Hud");
        }

        [Test]
        public void Dispose_MakesFurtherCallsThrow()
        {
            var flow = new SceneFlowService(new FakeSceneLoader(), null, Boot);
            flow.Dispose();

            Assert.Throws<ObjectDisposedException>(() =>
                flow.GoToAsync(Station).GetAwaiter().GetResult());
        }
    }
}
