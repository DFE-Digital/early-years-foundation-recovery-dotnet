using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Domain.Training;

namespace EarlyYearsFoundationRecovery.Application.Contracts;

public interface IUserAccountStore
{
    Task<UserAccount?> FindByGovOneSubjectAsync(string subject, CancellationToken cancellationToken);

    Task AddAsync(UserAccount account, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ITrainingProgressStore
{
    Task<IReadOnlyList<TrainingModuleProgress>> GetForUserAsync(long userId, CancellationToken cancellationToken);

    Task<TrainingModuleProgress?> FindAsync(long userId, string moduleName, CancellationToken cancellationToken);

    Task AddAsync(TrainingModuleProgress progress, CancellationToken cancellationToken);

    Task<IReadOnlyList<AssessmentAttempt>> GetAssessmentsAsync(long userId, string moduleName, CancellationToken cancellationToken);

    Task AddAssessmentAsync(AssessmentAttempt attempt, CancellationToken cancellationToken);

    Task AddResponseAsync(TrainingResponse response, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ILearningNoteStore
{
    Task<LearningNote?> FindAsync(long userId, string moduleName, CancellationToken cancellationToken);

    Task AddAsync(LearningNote note, CancellationToken cancellationToken);

    Task DeleteForUserAsync(long userId, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ITrainingContentProvider
{
    Task<IReadOnlyList<TrainingModule>> GetModulesAsync(CancellationToken cancellationToken);

    Task<TrainingModule?> GetModuleAsync(string moduleName, CancellationToken cancellationToken);
}
