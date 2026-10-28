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

    public Task<TrainingResponse?> GetLatestResponseAsync(
        long userId,
        string moduleName,
        string questionName,
        CancellationToken cancellationToken) =>
        db.TrainingResponses
            .Where(response => response.UserAccountId == userId &&
                               response.ModuleName == moduleName &&
                               response.QuestionName == questionName)
            .OrderByDescending(response => response.CreatedAt)
            .ThenByDescending(response => response.Id)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ConfidenceCheckProgress?> FindConfidenceCheckAsync(
        long userId, string moduleName, string checkType, CancellationToken cancellationToken) =>
        db.ConfidenceCheckProgress.SingleOrDefaultAsync(
            check => check.UserAccountId == userId &&
                     check.ModuleName == moduleName &&
                     check.CheckType == checkType,
            cancellationToken);

    public Task AddConfidenceCheckAsync(
        ConfidenceCheckProgress confidenceCheck, CancellationToken cancellationToken) =>
        db.ConfidenceCheckProgress.AddAsync(confidenceCheck, cancellationToken).AsTask();

    public Task SaveChangesAsync(CancellationToken cancellationToken) =>
        db.SaveChangesAsync(cancellationToken);
}

public sealed class LearningNoteStore(ApplicationDbContext db) : ILearningNoteStore
{
    public async Task<IReadOnlyList<LearningNote>> GetForUserAsync(
        long userId, CancellationToken cancellationToken) =>
        await db.LearningNotes
            .Where(note => note.UserAccountId == userId)
            .OrderByDescending(note => note.UpdatedAt)
            .ToListAsync(cancellationToken);

    public Task<LearningNote?> FindAsync(
        long userId,
        string moduleName,
        CancellationToken cancellationToken) =>
        db.LearningNotes.SingleOrDefaultAsync(
            note => note.UserAccountId == userId && note.ModuleName == moduleName && note.PageName == "",
            cancellationToken);

    public Task<LearningNote?> FindPageAsync(
        long userId, string moduleName, string pageName, CancellationToken cancellationToken) =>
        db.LearningNotes.SingleOrDefaultAsync(
            note => note.UserAccountId == userId && note.ModuleName == moduleName && note.PageName == pageName,
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
