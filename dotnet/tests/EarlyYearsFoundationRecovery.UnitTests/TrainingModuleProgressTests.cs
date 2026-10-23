using EarlyYearsFoundationRecovery.Domain.Training;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class TrainingModuleProgressTests
{
    [Fact]
    public void RecordPageView_IsIdempotentForCount_AndTracksLatestPage()
    {
        var progress = new TrainingModuleProgress(1, "module-one");
        var firstView = DateTimeOffset.Parse("2026-01-01T09:00:00Z");

        progress.RecordPageView("welcome", firstView);
        progress.RecordPageView("welcome", firstView.AddMinutes(1));
        progress.RecordPageView("activity", firstView.AddMinutes(2));

        Assert.Equal(firstView, progress.StartedAt);
        Assert.Equal(2, progress.VisitedPageCount);
        Assert.Equal("activity", progress.LastPageName);
    }

    [Fact]
    public void RecordCompletion_IsIdempotent()
    {
        var progress = new TrainingModuleProgress(1, "module-one");
        var completedAt = DateTimeOffset.Parse("2026-01-01T09:00:00Z");

        progress.RecordCompletion(completedAt);
        progress.RecordCompletion(completedAt.AddMinutes(1));

        Assert.Equal(completedAt, progress.StartedAt);
        Assert.Equal(completedAt, progress.CompletedAt);
    }
}
