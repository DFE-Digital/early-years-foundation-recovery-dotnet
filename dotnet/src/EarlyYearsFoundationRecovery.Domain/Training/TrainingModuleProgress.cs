namespace EarlyYearsFoundationRecovery.Domain.Training;

public sealed class TrainingModuleProgress
{
    private readonly List<VisitedPage> _visitedPages = [];

    private TrainingModuleProgress()
    {
    }

    public TrainingModuleProgress(long userAccountId, string moduleName)
    {
        UserAccountId = userAccountId;
        ModuleName = string.IsNullOrWhiteSpace(moduleName)
            ? throw new ArgumentException("A module name is required.", nameof(moduleName))
            : moduleName;
    }

    public long Id { get; private set; }

    public long UserAccountId { get; private set; }

    public string ModuleName { get; private set; } = string.Empty;

    public DateTimeOffset? StartedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public string? LastPageName { get; private set; }

    public IReadOnlyCollection<VisitedPage> VisitedPages => _visitedPages.AsReadOnly();

    public int VisitedPageCount => _visitedPages.Count;

    public void RecordStart(DateTimeOffset at) =>
        StartedAt ??= at;

    public void RecordPageView(string pageName, DateTimeOffset at)
    {
        if (string.IsNullOrWhiteSpace(pageName))
        {
            throw new ArgumentException("A page name is required.", nameof(pageName));
        }

        RecordStart(at);
        if (_visitedPages.All(page => page.Name != pageName))
        {
            _visitedPages.Add(new VisitedPage(pageName, at));
        }

        LastPageName = pageName;
    }

    public void RecordCompletion(DateTimeOffset at)
    {
        RecordStart(at);
        CompletedAt ??= at;
    }

    public bool HasVisited(string pageName) =>
        _visitedPages.Any(page => page.Name == pageName);
}

public sealed class VisitedPage
{
    private VisitedPage()
    {
    }

    internal VisitedPage(string name, DateTimeOffset visitedAt)
    {
        Name = name;
        VisitedAt = visitedAt;
    }

    public string Name { get; private set; } = string.Empty;

    public DateTimeOffset VisitedAt { get; private set; }
}
