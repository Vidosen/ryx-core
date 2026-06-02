using NUnit.Framework;
using UnityEngine;

namespace Ryx.Core.Tutorials.Tests
{
    public sealed class TutorialContractsTests
    {
        [Test]
        public void ProgressData_PreservesIntroResumeState()
        {
            var progress = new TutorialProgressData(
                false,
                "chapter",
                "step",
                true,
                1,
                new[] { "completed" });

            Assert.That(progress.IsIntroCompleted, Is.True);
            Assert.That(progress.IntroPageIndex, Is.EqualTo(1));
            Assert.That(progress.CompletedChapterIds, Is.EqualTo(new[] { "completed" }));
        }

        [Test]
        public void NullAdapters_AreCallable()
        {
            Assert.DoesNotThrow(() =>
            {
                NullTutorialProgressStore.Instance.SaveProgress("chapter", "step", true, 1);
                NullTutorialProgressStore.Instance.MarkChapterCompleted("chapter");
                NullTutorialProgressStore.Instance.MarkCompleted();
                NullTutorialAnalyticsSink.Instance.LogEvent("event");
                NullTutorialAnalyticsSink.Instance.LogStep(TutorialProgressionStatus.Start, "chapter", "step");
            });
        }

        [Test]
        public void Trigger_ArmsAndDisarms()
        {
            var gameObject = new GameObject("TutorialTrigger");
            var manager = gameObject.AddComponent<TutorialManager>();
            var trigger = gameObject.AddComponent<TestTutorialTrigger>();

            trigger.Arm(manager);
            Assert.That(trigger.IsArmed, Is.True);

            trigger.Disarm();
            Assert.That(trigger.IsArmed, Is.False);
            Object.DestroyImmediate(gameObject);
        }
    }

    public sealed class TestTutorialTrigger : TutorialTrigger
    {
    }
}
