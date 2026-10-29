namespace EarlyYearsFoundationRecovery.Domain.Training;

public sealed class AssessmentAttempt
{
    private readonly List<AssessmentAnswer> _answers = [];

    private AssessmentAttempt()
    {
    }

    public AssessmentAttempt(long userAccountId, string moduleName, DateTimeOffset startedAt)
    {
        UserAccountId = userAccountId;
        ModuleName = string.IsNullOrWhiteSpace(moduleName)
            ? throw new ArgumentException("A module name is required.", nameof(moduleName))
            : moduleName;
        StartedAt = startedAt;
    }

    public long Id { get; private set; }

    public long UserAccountId { get; private set; }

    public string ModuleName { get; private set; } = string.Empty;

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public decimal? Score { get; private set; }

    public bool? Passed { get; private set; }

    public IReadOnlyCollection<AssessmentAnswer> Answers => _answers.AsReadOnly();

    public void AddOrReplaceAnswer(string questionName, IEnumerable<string> selectedAnswerIds, bool correct)
    {
        if (CompletedAt is not null)
        {
            throw new InvalidOperationException("A completed assessment cannot be changed.");
        }

        if (string.IsNullOrWhiteSpace(questionName))
        {
            throw new ArgumentException("A question name is required.", nameof(questionName));
        }

        _answers.RemoveAll(answer => answer.QuestionName == questionName);
        _answers.Add(new AssessmentAnswer(questionName, selectedAnswerIds, correct));
    }

    public decimal Grade(int totalQuestions, DateTimeOffset completedAt)
    {
        if (totalQuestions <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(totalQuestions), "An assessment must have at least one question.");
        }

        Score ??= decimal.Round(
            (decimal)_answers.Count(answer => answer.IsCorrect) / totalQuestions * 100m,
            2,
            MidpointRounding.AwayFromZero);
        Passed ??= Score >= AssessmentGrader.PassThreshold;
        CompletedAt ??= completedAt;
        return Score.Value;
    }
}

public sealed class AssessmentAnswer
{
    private readonly List<string> _selectedAnswerIds = [];

    private AssessmentAnswer()
    {
    }

    internal AssessmentAnswer(string questionName, IEnumerable<string> selectedAnswerIds, bool isCorrect)
    {
        QuestionName = questionName;
        _selectedAnswerIds = selectedAnswerIds
            .Where(answerId => !string.IsNullOrWhiteSpace(answerId))
            .Distinct(StringComparer.Ordinal)
            .ToList();
        IsCorrect = isCorrect;
    }

    public string QuestionName { get; private set; } = string.Empty;

    public IReadOnlyCollection<string> SelectedAnswerIds => _selectedAnswerIds.AsReadOnly();

    public bool IsCorrect { get; private set; }
}
