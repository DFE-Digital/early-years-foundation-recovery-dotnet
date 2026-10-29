namespace EarlyYearsFoundationRecovery.Domain.Training;

public static class AssessmentGrader
{
    public const decimal PassThreshold = 70m;

    public static bool IsCorrect(
        IEnumerable<string> expectedAnswerIds,
        IEnumerable<string> selectedAnswerIds)
    {
        ArgumentNullException.ThrowIfNull(expectedAnswerIds);
        ArgumentNullException.ThrowIfNull(selectedAnswerIds);

        var expected = expectedAnswerIds.ToHashSet(StringComparer.Ordinal);
        var selected = selectedAnswerIds.ToHashSet(StringComparer.Ordinal);
        return expected.SetEquals(selected);
    }

    public static bool Passed(decimal score) =>
        score >= PassThreshold;
}
