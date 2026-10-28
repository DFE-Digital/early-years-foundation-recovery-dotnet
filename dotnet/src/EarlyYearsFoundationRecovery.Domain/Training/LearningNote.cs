namespace EarlyYearsFoundationRecovery.Domain.Training;

public sealed class LearningNote
{
    private LearningNote()
    {
    }

    public LearningNote(long userAccountId, string moduleName, string pageName = "", string title = "")
    {
        UserAccountId = userAccountId;
        ModuleName = string.IsNullOrWhiteSpace(moduleName)
            ? throw new ArgumentException("A module name is required.", nameof(moduleName))
            : moduleName;
        PageName = pageName;
        Title = title;
    }

    public long Id { get; private set; }

    public long UserAccountId { get; private set; }

    public string ModuleName { get; private set; } = string.Empty;

    public string Content { get; private set; } = string.Empty;

    public string PageName { get; private set; } = string.Empty;

    public string Title { get; private set; } = string.Empty;

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Update(string content, DateTimeOffset updatedAt)
    {
        Content = content ?? throw new ArgumentNullException(nameof(content));
        UpdatedAt = updatedAt;
    }
}
