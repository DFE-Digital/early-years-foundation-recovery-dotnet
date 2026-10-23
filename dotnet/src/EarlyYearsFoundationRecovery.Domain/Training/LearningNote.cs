namespace EarlyYearsFoundationRecovery.Domain.Training;

public sealed class LearningNote
{
    private LearningNote()
    {
    }

    public LearningNote(long userAccountId, string moduleName)
    {
        UserAccountId = userAccountId;
        ModuleName = string.IsNullOrWhiteSpace(moduleName)
            ? throw new ArgumentException("A module name is required.", nameof(moduleName))
            : moduleName;
    }

    public long Id { get; private set; }

    public long UserAccountId { get; private set; }

    public string ModuleName { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string content, DateTimeOffset updatedAt)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        UpdatedAt = updatedAt;
    }
}
