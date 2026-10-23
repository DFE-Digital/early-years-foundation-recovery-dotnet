using System.Text.Json;

namespace EarlyYearsFoundationRecovery.Domain.Training;

public sealed class TrainingResponse
{
    private TrainingResponse()
    {
    }

    public TrainingResponse(
        long userAccountId,
        string moduleName,
        string questionName,
        string responseType,
        IEnumerable<string> selectedAnswerIds,
        bool correct,
        DateTimeOffset createdAt)
    {
        UserAccountId = userAccountId;
        ModuleName = Required(moduleName, nameof(moduleName));
        QuestionName = Required(questionName, nameof(questionName));
        ResponseType = Required(responseType, nameof(responseType));
        SelectedAnswerIdsJson = JsonSerializer.Serialize(selectedAnswerIds.Distinct(StringComparer.Ordinal));
        Correct = correct;
        CreatedAt = createdAt;
    }

    public long Id { get; private set; }

    public long UserAccountId { get; private set; }

    public string ModuleName { get; private set; } = string.Empty;

    public string QuestionName { get; private set; } = string.Empty;

    public string ResponseType { get; private set; } = string.Empty;

    public string SelectedAnswerIdsJson { get; private set; } = "[]";

    public bool Correct { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public IReadOnlyList<string> SelectedAnswerIds =>
        JsonSerializer.Deserialize<string[]>(SelectedAnswerIdsJson) ?? [];

    private static string Required(string value, string parameterName) =>
        string.IsNullOrWhiteSpace(value)
            ? throw new ArgumentException("A value is required.", parameterName)
            : value;
}
