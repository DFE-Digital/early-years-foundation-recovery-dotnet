namespace EarlyYearsFoundationRecovery.Domain.Training;

public sealed class ConfidenceCheckProgress
{
    private ConfidenceCheckProgress()
    {
    }

    public ConfidenceCheckProgress(long userAccountId, string moduleName, string checkType)
    {
        UserAccountId = userAccountId;
        ModuleName = Required(moduleName, nameof(moduleName));
        CheckType = checkType is "pre" or "post"
            ? checkType
            : throw new ArgumentOutOfRangeException(nameof(checkType), "Check type must be pre or post.");
    }

    public long Id { get; private set; }

    public long UserAccountId { get; private set; }

    public string ModuleName { get; private set; } = string.Empty;

    public string CheckType { get; private set; } = string.Empty;

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public DateTimeOffset? SkippedAt { get; private set; }

    public void RecordStart(DateTimeOffset at) =>
        StartedAt ??= at;

    public void RecordCompletion(DateTimeOffset at)
    {
        RecordStart(at);
        CompletedAt ??= at;
    }

    public void RecordSkip(DateTimeOffset at)
    {
        if (CheckType == "pre" && StartedAt is null && CompletedAt is null)
        {
            SkippedAt ??= at;
        }
    }

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A value is required.", parameterName)
            : value;
}
