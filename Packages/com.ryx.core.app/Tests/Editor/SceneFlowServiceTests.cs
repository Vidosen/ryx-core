using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
            public readonly List<string> Calls;
            private readonly HashSet<SceneId> _loaded = new();

            public FakeSceneLoader(List<string> calls = null) => Calls = calls ?? new List<string>();

            public UniTaskCompletionSource LoadGate { get; set; }

            public UniTask LoadSingleAsync(SceneId scene, IProgress<float> progress = null, CancellationToken cancellationToken = default)
            {
                Calls.Add($"load-single:{scene}");
                progress?.Report(0.5f);
                progress?.Report(1f);
                _loaded.Clear();
                _loaded.Add(scene);
                return LoadGate?.Task ?? UniTask.CompletedTask;
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
            private readonly List<string> _calls;
            public readonly List<float> Progress = new();

            public RecordingTransition(List<string> calls = null) => _calls = calls ?? new List<string>();

            public UniTask CoverAsync(CancellationToken cancellationToken = default)
            {
                _calls.Add("cover");
                return UniTask.CompletedTask;
            }

            public UniTask RevealAsync(CancellationToken cancellationToken = default)
            {
                _calls.Add("reveal");
                return UniTask.CompletedTask;
            }

            public void ReportProgress(float normalized) => Progress.Add(normalized);
        }

        [Test]
        public async Task GoToAsync_CoversLoadsThenReveals()
        {
            var calls = new List<string>();
            var gate = new UniTaskCompletionSource();
            var loader = new FakeSceneLoader(calls) { LoadGate = gate };
            var transition = new RecordingTransition(calls);
            using var flow = new SceneFlowService(loader, transition, Boot);

            var move = flow.GoToAsync(Station).AsTask();
            var whileLoading = calls.ToArray();
            gate.TrySetResult();
            await move;

            CollectionAssert.AreEqual(new[] { "cover", "load-single:Station" }, whileLoading);
            CollectionAssert.AreEqual(new[] { "cover", "load-single:Station", "reveal" }, calls);
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

        [TestCase(false)]
        [TestCase(true)]
        public async Task GoToAsync_ResetsTransitionFlagAfterLoading(bool fail)
        {
            var gate = new UniTaskCompletionSource();
            using var flow = new SceneFlowService(new FakeSceneLoader { LoadGate = gate }, null, Boot);

            var move = flow.GoToAsync(Station).AsTask();
            var transitioning = flow.IsTransitioning.CurrentValue;
            if (fail)
            {
                gate.TrySetException(new InvalidOperationException("load failed"));
                Assert.ThrowsAsync<InvalidOperationException>(async () => await move);
            }
            else
            {
                gate.TrySetResult();
                await move;
            }

            Assert.IsTrue(transitioning);
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
