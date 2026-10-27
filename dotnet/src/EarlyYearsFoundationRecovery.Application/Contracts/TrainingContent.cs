namespace EarlyYearsFoundationRecovery.Application.Contracts;

public sealed record TrainingModule(
    string Name,
    string Title,
    int Position,
    bool IsDraft,
    IReadOnlyList<TrainingPage> Pages,
    string Criteria = "");

public sealed record TrainingPage(
    string Name,
    string Title,
    string PageType,
    string Body,
    IReadOnlyList<TrainingQuestionOption>? Options = null,
    IReadOnlyList<string>? CorrectAnswerIds = null,
    string? QuestionType = null,
    bool AllowsMultipleAnswers = false,
    TrainingVideo? Video = null);

public sealed record TrainingVideo(string? EmbedUrl, string Title, string Transcript);

public sealed record TrainingQuestionOption(string Id, string Label);

public sealed record RegistrationChoice(string Id, string Name, string? Group = null);

public sealed record RegistrationSetting(
    string Id,
    string Name,
    bool RequiresLocalAuthority,
    string RoleGroup);

public interface IRegistrationOptionsProvider
{
    IReadOnlyList<RegistrationChoice> GetCountries();

    IReadOnlyList<RegistrationChoice> GetLocalAuthorities();

    IReadOnlyList<RegistrationChoice> GetExperiences();

    IReadOnlyList<RegistrationChoice> GetRoles(string roleGroup);

    Task<IReadOnlyList<RegistrationSetting>> GetSettingsAsync(CancellationToken cancellationToken);
}
