using EarlyYearsFoundationRecovery.Domain.Training;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class AssessmentGraderTests
{
    [Fact]
    public void IsCorrect_RequiresExactSetOfAnswers()
    {
        Assert.True(AssessmentGrader.IsCorrect(["a", "b"], ["b", "a", "a"]));
        Assert.False(AssessmentGrader.IsCorrect(["a", "b"], ["a"]));
        Assert.False(AssessmentGrader.IsCorrect(["a"], ["a", "b"]));
    }

    [Theory]
    [InlineData(70, true)]
    [InlineData(69.99, false)]
    public void Passed_UsesSeventyPercentThreshold(decimal score, bool expected)
    {
        Assert.Equal(expected, AssessmentGrader.Passed(score));
    }
}

