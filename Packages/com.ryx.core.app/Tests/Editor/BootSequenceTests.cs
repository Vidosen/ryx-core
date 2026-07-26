using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;

namespace Ryx.Core.App.Tests
{
    public sealed class BootSequenceTests
    {
        [Test]
        public void RunAsync_ExecutesStepsInRegistrationOrder()
        {
            var order = new List<string>();
            using var sequence = new BootSequence(
                BootStep.FromAction("first", () => order.Add("first")),
                BootStep.FromAction("second", () => order.Add("second")),
                BootStep.FromAction("third", () => order.Add("third")));

            sequence.RunAsync().GetAwaiter().GetResult();

            CollectionAssert.AreEqual(new[] { "first", "second", "third" }, order);
        }

        [Test]
        public void RunAsync_CompletesProgressAtOne()
        {
            using var sequence = new BootSequence(
                BootStep.FromAction("a", () => { }),
                BootStep.FromAction("b", () => { }));

            sequence.RunAsync().GetAwaiter().GetResult();

            Assert.AreEqual(1f, sequence.Progress.CurrentValue.Normalized, 0.0001f);
            Assert.AreEqual(2, sequence.Progress.CurrentValue.CompletedSteps);
            Assert.IsTrue(sequence.Progress.CurrentValue.IsComplete);
        }

        [Test]
        public void RunAsync_WeightsProgressByStepWeight()
        {
            var samples = new List<float>();
            using var sequence = new BootSequence(
                BootStep.FromAction("light", () => { }, weight: 1f),
                BootStep.FromAction("heavy", () => { }, weight: 3f));

            using var subscription = sequence.Progress.Subscribe(p => samples.Add(p.Normalized));
            sequence.RunAsync().GetAwaiter().GetResult();

            // After the light step the weighted progress is 1/4, not 1/2.
            CollectionAssert.Contains(samples.ConvertAll(v => (float)Math.Round(v, 2)), 0.25f);
        }

        [Test]
        public void RunAsync_WrapsStepFailure()
        {
            using var sequence = new BootSequence(
                BootStep.FromAction("ok", () => { }),
                BootStep.FromAction("explodes", () => throw new InvalidOperationException("nope")));

            var error = Assert.Throws<BootFailedException>(() => sequence.RunAsync().GetAwaiter().GetResult());

            Assert.AreEqual("explodes", error.StepDescription);
            Assert.IsInstanceOf<InvalidOperationException>(error.InnerException);
        }

        [Test]
        public void RunAsync_StopsAtFirstFailure()
        {
            var reached = false;
            using var sequence = new BootSequence(
                BootStep.FromAction("explodes", () => throw new InvalidOperationException("nope")),
                BootStep.FromAction("never", () => reached = true));

            Assert.Throws<BootFailedException>(() => sequence.RunAsync().GetAwaiter().GetResult());
            Assert.IsFalse(reached);
        }

        [Test]
        public void RunAsync_TwiceThrows()
        {
            using var sequence = new BootSequence(BootStep.FromAction("a", () => { }));
            sequence.RunAsync().GetAwaiter().GetResult();

            Assert.Throws<InvalidOperationException>(() => sequence.RunAsync().GetAwaiter().GetResult());
        }

        [Test]
        public void RunAsync_HonoursCancellation()
        {
            using var cts = new CancellationTokenSource();
            using var sequence = new BootSequence(
                BootStep.FromAction("cancels", () => cts.Cancel()),
                BootStep.FromAction("never", () => Assert.Fail("should not run")));

            Assert.Throws<OperationCanceledException>(() =>
                sequence.RunAsync(cts.Token).GetAwaiter().GetResult());
        }

        [Test]
        public void RunAsync_WithNoSteps_Completes()
        {
            using var sequence = new BootSequence(Array.Empty<IBootStep>());
            Assert.DoesNotThrow(() => sequence.RunAsync().GetAwaiter().GetResult());
            Assert.AreEqual(0, sequence.StepCount);
        }

        /// <remarks>
        /// Must be a <see cref="UnityTest"/>: <c>UniTask.Yield</c> resumes on the player loop,
        /// which an EditMode <c>[Test]</c> body never pumps — blocking on it would throw
        /// "Not yet completed" rather than wait.
        /// </remarks>
        [UnityTest]
        public IEnumerator RunAsync_AwaitsAsynchronousSteps() => UniTask.ToCoroutine(async () =>
        {
            var completed = false;
            using var sequence = new BootSequence(
                BootStep.FromAsync("async", async ct =>
                {
                    await UniTask.Yield(ct);
                    completed = true;
                }));

            await sequence.RunAsync();

            Assert.IsTrue(completed);
        });
    }
}
