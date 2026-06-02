using System;
using System.Collections.Generic;

namespace Ryx.Core.Tutorials
{

public sealed class TutorialProgressData
{
    public TutorialProgressData(
        bool isCompleted,
        string chapterId,
        string stepId,
        bool isIntroCompleted = false,
        int introPageIndex = 0,
        IReadOnlyCollection<string> completedChapterIds = null)
    {
        IsCompleted = isCompleted;
        ChapterId = chapterId ?? string.Empty;
        StepId = stepId ?? string.Empty;
        IsIntroCompleted = isIntroCompleted;
        IntroPageIndex = Math.Max(0, introPageIndex);
        CompletedChapterIds = completedChapterIds ?? Array.Empty<string>();
    }

    public bool IsCompleted { get; }
    public string ChapterId { get; }
    public string StepId { get; }
    public bool IsIntroCompleted { get; }
    public int IntroPageIndex { get; }
    public IReadOnlyCollection<string> CompletedChapterIds { get; }
}

public interface ITutorialProgressStore
{
    TutorialProgressData Load();
    void SaveProgress(string chapterId, string stepId, bool isIntroCompleted, int introPageIndex);
    void MarkChapterCompleted(string chapterId);
    void MarkCompleted();
    void Reset();
}

public sealed class NullTutorialProgressStore : ITutorialProgressStore
{
    public static readonly NullTutorialProgressStore Instance = new();
    private static readonly TutorialProgressData Empty = new(false, string.Empty, string.Empty);

    private NullTutorialProgressStore()
    {
    }

    public TutorialProgressData Load() => Empty;
    public void SaveProgress(string chapterId, string stepId, bool isIntroCompleted, int introPageIndex) { }
    public void MarkChapterCompleted(string chapterId) { }
    public void MarkCompleted() { }
    public void Reset() { }
}

public enum TutorialProgressionStatus
{
    Start,
    Complete
}

public interface ITutorialAnalyticsSink
{
    void LogEvent(string eventName);
    void LogStep(TutorialProgressionStatus status, string chapterId, string stepId);
}

public sealed class NullTutorialAnalyticsSink : ITutorialAnalyticsSink
{
    public static readonly NullTutorialAnalyticsSink Instance = new();

    private NullTutorialAnalyticsSink()
    {
    }

    public void LogEvent(string eventName) { }
    public void LogStep(TutorialProgressionStatus status, string chapterId, string stepId) { }
}

}
