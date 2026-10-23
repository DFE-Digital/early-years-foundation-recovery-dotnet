using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Domain.Training;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.Infrastructure.Persistence;

public sealed class UserAccountStore(ApplicationDbContext db) : IUserAccountStore
{
    public Task<UserAccount?> FindByGovOneSubjectAsync(string subject, CancellationToken cancellationToken) =>
        db.UserAccounts.SingleOrDefaultAsync(account => account.GovOneSubject == subject, cancellationToken);

    public Task AddAsync(UserAccount account, CancellationToken cancellationToken) =>
        db.UserAccounts.AddAsync(account, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}

public sealed class TrainingProgressStore(ApplicationDbContext db) : ITrainingProgressStore
{
    public async Task<IReadOnlyList<TrainingModuleProgress>> GetForUserAsync(
        long userId,
        CancellationToken cancellationToken) =>
        await db.TrainingModuleProgress
            .Include(progress => progress.VisitedPages)
            .Where(progress => progress.UserAccountId == userId)
            .ToListAsync(cancellationToken);

    public Task<TrainingModuleProgress?> FindAsync(
        long userId,
        string moduleName,
        CancellationToken cancellationToken) =>
        db.TrainingModuleProgress
            .Include(progress => progress.VisitedPages)
            .SingleOrDefaultAsync(
                progress => progress.UserAccountId == userId && progress.ModuleName == moduleName,
                cancellationToken);

    public Task AddAsync(TrainingModuleProgress progress, CancellationToken cancellationToken) =>
        db.TrainingModuleProgress.AddAsync(progress, cancellationToken).AsTask();

    public async Task<IReadOnlyList<AssessmentAttempt>> GetAssessmentsAsync(
        long userId,
        string moduleName,
        CancellationToken cancellationToken) =>
        await db.AssessmentAttempts
            .Include(attempt => attempt.Answers)
            .Where(attempt => attempt.UserAccountId == userId && attempt.ModuleName == moduleName)
            .OrderBy(attempt => attempt.StartedAt)
            .ToListAsync(cancellationToken);

    public Task AddAssessmentAsync(AssessmentAttempt attempt, CancellationToken cancellationToken) =>
        db.AssessmentAttempts.AddAsync(attempt, cancellationToken).AsTask();

    public Task AddResponseAsync(TrainingResponse response, CancellationToken cancellationToken) =>
        db.TrainingResponses.AddAsync(response, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}

public sealed class LearningNoteStore(ApplicationDbContext db) : ILearningNoteStore
{
    public Task<LearningNote?> FindAsync(
        long userId,
        string moduleName,
        CancellationToken cancellationToken) =>
        db.LearningNotes.SingleOrDefaultAsync(
            note => note.UserAccountId == userId && note.ModuleName == moduleName,
            cancellationToken);

    public Task AddAsync(LearningNote note, CancellationToken cancellationToken) =>
        db.LearningNotes.AddAsync(note, cancellationToken).AsTask();

    public async Task DeleteForUserAsync(long userId, CancellationToken cancellationToken)
    {
        var notes = await db.LearningNotes
            .Where(note => note.UserAccountId == userId)
            .ToListAsync(cancellationToken);
        db.LearningNotes.RemoveRange(notes);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}
