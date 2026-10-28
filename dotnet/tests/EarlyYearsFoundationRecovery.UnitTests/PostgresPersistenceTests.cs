using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Domain.Training;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class PostgresFactAttribute : FactAttribute
{
    public PostgresFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EYFR_TEST_POSTGRES")))
        {
            Skip = "Set EYFR_TEST_POSTGRES to run isolated PostgreSQL integration tests.";
        }
    }
}

public sealed partial class PostgresPersistenceTests : IAsyncLifetime
{
    private readonly string _databaseName = $"eyfr_integration_{Guid.NewGuid():N}";
    private string? _adminConnection;
    private string? _testConnection;
    private bool _created;

    public async Task InitializeAsync()
    {
        var configured = Environment.GetEnvironmentVariable("EYFR_TEST_POSTGRES");
        if (string.IsNullOrWhiteSpace(configured))
        {
            return;
        }

        var builder = new NpgsqlConnectionStringBuilder(configured) { Pooling = false };
        _adminConnection = builder.ConnectionString;
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
        _created = true;
        builder.Database = _databaseName;
        _testConnection = builder.ConnectionString;
    }

    public async Task DisposeAsync()
    {
        if (!_created)
        {
            return;
        }
        await using var connection = new NpgsqlConnection(_adminConnection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE \"{_databaseName}\"", connection);
        await command.ExecuteNonQueryAsync();
    }

    [PostgresFact]
    public async Task PageNotesSurviveNewContextsAndRemainScopedToTheirLearnerAndPage()
    {
        var (ownerId, otherId) = await SeedAsync();
        await using (var db = CreateContext())
        {
            var service = Journey(db);
            var owner = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var other = await db.UserAccounts.SingleAsync(account => account.Id == otherId);
            await service.SaveNoteAsync(owner, Module.Name, "Legacy module note", CancellationToken.None);
            await service.SavePageNoteAsync(owner, Module.Name, "first", "First draft", CancellationToken.None);
            await service.SavePageNoteAsync(owner, Module.Name, "second", "Second page", CancellationToken.None);
            await service.SavePageNoteAsync(other, Module.Name, "first", "Other learner", CancellationToken.None);
        }

        long originalNoteId;
        await using (var db = CreateContext())
        {
            var owner = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            var first = await service.GetPageNoteAsync(owner, Module.Name, "first", CancellationToken.None);
            Assert.NotNull(first);
            originalNoteId = first.Id;
            Assert.Equal("First draft", first.Content);
            Assert.Equal("First reflection", first.Title);
            await service.SavePageNoteAsync(owner, Module.Name, "first", "Revised", CancellationToken.None);
            await service.SavePageNoteAsync(owner, Module.Name, "second", "", CancellationToken.None);
        }

        await using (var db = CreateContext())
        {
            var store = new LearningNoteStore(db);
            var ownerNotes = await store.GetForUserAsync(ownerId, CancellationToken.None);
            Assert.Equal(3, ownerNotes.Count);
            Assert.Equal("Revised", ownerNotes.Single(note => note.Id == originalNoteId).Content);
            Assert.Equal("", ownerNotes.Single(note => note.PageName == "second").Content);
            Assert.Equal("Legacy module note", (await store.FindAsync(ownerId, Module.Name, CancellationToken.None))!.Content);
            Assert.Equal("Other learner", Assert.Single(await store.GetForUserAsync(otherId, CancellationToken.None)).Content);
            Assert.Null(await store.FindPageAsync(ownerId, "another-module", "first", CancellationToken.None));
        }
    }

    [PostgresFact]
    public async Task ConfidenceLifecycleAndEditableAnswersSurviveNewContexts()
    {
        var (ownerId, otherId) = await SeedAsync();
        DateTimeOffset? started;
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            await service.RecordPageViewAsync(account, Module.Name, "learning", CancellationToken.None);
            await service.RecordConfidenceCheckQuestionViewAsync(account, Module.Name, "pre-one", CancellationToken.None);
            await service.SubmitAnswerAsync(account, Module.Name, "pre-one", ["1"], CancellationToken.None);
            started = (await new TrainingProgressStore(db).FindConfidenceCheckAsync(
                ownerId, Module.Name, "pre", CancellationToken.None))!.StartedAt;
        }

        DateTimeOffset? completed;
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            Assert.Equal(["1"], await service.GetSelectedAnswersAsync(account, Module.Name, "pre-one", CancellationToken.None));
            await service.SubmitAnswerAsync(account, Module.Name, "pre-one", ["2"], CancellationToken.None);
            await service.RecordConfidenceCheckQuestionViewAsync(account, Module.Name, "pre-two", CancellationToken.None);
            await service.RecordConfidenceCheckQuestionViewAsync(account, Module.Name, "post-one", CancellationToken.None);
            completed = (await new TrainingProgressStore(db).FindConfidenceCheckAsync(
                ownerId, Module.Name, "pre", CancellationToken.None))!.CompletedAt;
        }

        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            await service.RecordConfidenceCheckQuestionViewAsync(account, Module.Name, "pre-one", CancellationToken.None);
            await service.RecordConfidenceCheckQuestionViewAsync(account, Module.Name, "pre-two", CancellationToken.None);
            var store = new TrainingProgressStore(db);
            var pre = await store.FindConfidenceCheckAsync(ownerId, Module.Name, "pre", CancellationToken.None);
            Assert.NotNull(pre);
            Assert.NotNull(started);
            Assert.NotNull(completed);
            Assert.Equal(started, pre.StartedAt);
            Assert.Equal(completed, pre.CompletedAt);
            Assert.NotNull(pre.SkippedAt);
            Assert.NotNull((await store.FindConfidenceCheckAsync(ownerId, Module.Name, "post", CancellationToken.None))!.StartedAt);
            Assert.Equal(["2"], await service.GetSelectedAnswersAsync(account, Module.Name, "pre-one", CancellationToken.None));
            Assert.Null(await store.FindConfidenceCheckAsync(otherId, Module.Name, "pre", CancellationToken.None));
            Assert.Empty(await store.GetAssessmentsAsync(ownerId, Module.Name, CancellationToken.None));
            Assert.Equal(2, await db.ConfidenceCheckProgress.CountAsync());
        }
    }

    [PostgresFact]
    public async Task ClosingAnAccountDeletesOnlyItsPageAndLegacyNotes()
    {
        var (ownerId, otherId) = await SeedAsync();
        await using (var db = CreateContext())
        {
            var owner = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var other = await db.UserAccounts.SingleAsync(account => account.Id == otherId);
            var service = Journey(db);
            await service.SaveNoteAsync(owner, Module.Name, "Legacy", CancellationToken.None);
            await service.SavePageNoteAsync(owner, Module.Name, "first", "Private", CancellationToken.None);
            await service.SavePageNoteAsync(other, Module.Name, "first", "Retained", CancellationToken.None);
        }
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = new AccountService(new UserAccountStore(db), new LearningNoteStore(db), TimeProvider.System);
            await service.SetCloseReasonAsync(account, "not_using", null, CancellationToken.None);
            await service.CloseAsync(account, CancellationToken.None);
        }
        await using (var db = CreateContext())
        {
            Assert.True((await db.UserAccounts.SingleAsync(account => account.Id == ownerId)).IsClosed);
            Assert.Empty(await new LearningNoteStore(db).GetForUserAsync(ownerId, CancellationToken.None));
            Assert.Equal("Retained", Assert.Single(await new LearningNoteStore(db).GetForUserAsync(otherId, CancellationToken.None)).Content);
        }
    }

    [PostgresFact]
    public async Task UpgradingInitialSchemaPreservesExistingModuleNotes()
    {
        await using (var db = CreateContext())
        {
            await db.GetService<IMigrator>().MigrateAsync("20261023090944_InitialCreate");
            var account = RegisteredAccount("migration-owner");
            db.UserAccounts.Add(account);
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "LearningNotes" ("UserAccountId", "ModuleName", "Content", "UpdatedAt")
                VALUES ({account.Id}, {Module.Name}, {"Existing module note"}, {DateTimeOffset.UtcNow})
                """);
        }
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }
        await using (var db = CreateContext())
        {
            var note = await db.LearningNotes.SingleAsync();
            Assert.Equal("", note.PageName);
            Assert.Equal("", note.Title);
            Assert.Equal("Existing module note", note.Content);
            await Journey(db).SavePageNoteAsync(
                await db.UserAccounts.SingleAsync(), Module.Name, "first", "New page note", CancellationToken.None);
            Assert.Equal(2, await db.LearningNotes.CountAsync());
        }
    }

    [PostgresFact]
    public async Task InterruptedSummativeAttemptResumesAndReplacesAnswersWithoutDuplicates()
    {
        var (ownerId, otherId) = await SeedAsync();
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            await service.RecordPageViewAsync(account, Module.Name, "learning", CancellationToken.None);
            Assert.Equal(new AssessmentResult(false, null, null, false),
                await service.SubmitAnswerAsync(account, Module.Name, "test-one", ["c"], CancellationToken.None));
            Assert.False(await service.CompleteModuleAsync(account, Module.Name, CancellationToken.None));
        }

        long attemptId;
        DateTimeOffset startedAt;
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            var attempt = await service.GetLatestAssessmentAsync(account, Module.Name, CancellationToken.None);
            Assert.NotNull(attempt);
            attemptId = attempt.Id;
            startedAt = attempt.StartedAt;
            Assert.Null(attempt.CompletedAt);
            Assert.Null(attempt.Score);
            Assert.Null(attempt.Passed);
            Assert.Equal(["c"], Assert.Single(attempt.Answers).SelectedAnswerIds);
            Assert.Equal(new AssessmentResult(true, null, null, false),
                await service.SubmitAnswerAsync(account, Module.Name, "test-one", ["a", "b"], CancellationToken.None));
        }

        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            var attempt = Assert.Single(await new TrainingProgressStore(db)
                .GetAssessmentsAsync(ownerId, Module.Name, CancellationToken.None));
            Assert.Equal(attemptId, attempt.Id);
            Assert.Equal(startedAt, attempt.StartedAt);
            var answer = Assert.Single(attempt.Answers);
            Assert.True(answer.IsCorrect);
            Assert.Equal(["a", "b"], answer.SelectedAnswerIds);
            var progress = await new TrainingProgressStore(db).FindAsync(ownerId, Module.Name, CancellationToken.None);
            Assert.NotNull(progress);
            Assert.NotNull(progress.StartedAt);
            Assert.Null(progress.CompletedAt);
            Assert.Equal("test-one", progress.LastPageName);
            Assert.Single(progress.VisitedPages, page => page.Name == "test-one");
            Assert.Empty(await new TrainingProgressStore(db).GetAssessmentsAsync(otherId, Module.Name, CancellationToken.None));
            Assert.Empty(await new TrainingProgressStore(db).GetAssessmentsAsync(ownerId, "another-module", CancellationToken.None));
            Assert.Equal(new AssessmentResult(true, 100m, true, true),
                await service.SubmitAnswerAsync(account, Module.Name, "test-two", ["yes"], CancellationToken.None));
            Assert.Null(await service.GetCompletionDateAsync(account, Module.Name, CancellationToken.None));
        }

        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var attempt = await Journey(db).GetLatestAssessmentAsync(account, Module.Name, CancellationToken.None);
            Assert.NotNull(attempt);
            Assert.Equal(attemptId, attempt.Id);
            Assert.Equal(100m, attempt.Score);
            Assert.True(attempt.Passed);
            Assert.NotNull(attempt.CompletedAt);
            Assert.Equal(2, attempt.Answers.Count);
        }
    }

    [PostgresFact]
    public async Task FailedAssessmentRetriesPreserveHistoryAndPassingCompletionIsIdempotent()
    {
        var (ownerId, otherId) = await SeedAsync();
        await using (var db = CreateContext())
        {
            var owner = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var other = await db.UserAccounts.SingleAsync(account => account.Id == otherId);
            var service = Journey(db);
            await service.RecordPageViewAsync(owner, Module.Name, "learning", CancellationToken.None);
            await service.SubmitAnswerAsync(owner, Module.Name, "test-one", ["a", "b"], CancellationToken.None);
            Assert.Equal(new AssessmentResult(false, 50m, false, true),
                await service.SubmitAnswerAsync(owner, Module.Name, "test-two", ["no"], CancellationToken.None));
            Assert.False(await service.CompleteModuleAsync(owner, Module.Name, CancellationToken.None));
            await service.SubmitAnswerAsync(other, Module.Name, "test-one", ["c"], CancellationToken.None);
        }

        long failedId;
        DateTimeOffset failedAt;
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            var failed = await service.GetLatestAssessmentAsync(account, Module.Name, CancellationToken.None);
            Assert.NotNull(failed);
            failedId = failed.Id;
            failedAt = Assert.IsType<DateTimeOffset>(failed.CompletedAt);
            Assert.Equal(50m, failed.Score);
            Assert.False(failed.Passed);
            Assert.Null(await service.GetCompletionDateAsync(account, Module.Name, CancellationToken.None));
            Assert.Equal(new AssessmentResult(true, null, null, false),
                await service.SubmitAnswerAsync(account, Module.Name, "test-one", ["a", "b"], CancellationToken.None));
            Assert.False(await service.CompleteModuleAsync(account, Module.Name, CancellationToken.None));
        }

        long passedId;
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            var retry = await service.GetLatestAssessmentAsync(account, Module.Name, CancellationToken.None);
            Assert.NotNull(retry);
            passedId = retry.Id;
            Assert.NotEqual(failedId, passedId);
            Assert.Null(retry.CompletedAt);
            Assert.Single(retry.Answers);
            Assert.Equal(new AssessmentResult(true, 100m, true, true),
                await service.SubmitAnswerAsync(account, Module.Name, "test-two", ["yes"], CancellationToken.None));
            Assert.Null(await service.GetCompletionDateAsync(account, Module.Name, CancellationToken.None));
            Assert.True(await service.CompleteModuleAsync(account, Module.Name, CancellationToken.None));
        }

        DateTimeOffset completion;
        DateTimeOffset passedAt;
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var service = Journey(db);
            completion = Assert.IsType<DateTimeOffset>(
                await service.GetCompletionDateAsync(account, Module.Name, CancellationToken.None));
            var passed = await service.GetLatestAssessmentAsync(account, Module.Name, CancellationToken.None);
            Assert.NotNull(passed);
            passedAt = Assert.IsType<DateTimeOffset>(passed.CompletedAt);
            Assert.Equal(new AssessmentResult(true, 100m, true, true),
                await service.SubmitAnswerAsync(account, Module.Name, "test-two", ["no"], CancellationToken.None));
            Assert.True(await service.CompleteModuleAsync(account, Module.Name, CancellationToken.None));
        }

        await using (var db = CreateContext())
        {
            var store = new TrainingProgressStore(db);
            var attempts = await store.GetAssessmentsAsync(ownerId, Module.Name, CancellationToken.None);
            Assert.Equal(2, attempts.Count);
            var failed = attempts.Single(attempt => attempt.Id == failedId);
            Assert.Equal(failedAt, failed.CompletedAt);
            Assert.Equal(50m, failed.Score);
            Assert.False(failed.Passed);
            Assert.Equal(["no"], failed.Answers.Single(answer => answer.QuestionName == "test-two").SelectedAnswerIds);
            var passed = attempts.Single(attempt => attempt.Id == passedId);
            Assert.Equal(passedAt, passed.CompletedAt);
            Assert.Equal(100m, passed.Score);
            Assert.True(passed.Passed);
            Assert.Equal(["yes"], passed.Answers.Single(answer => answer.QuestionName == "test-two").SelectedAnswerIds);
            Assert.Equal(completion, (await store.FindAsync(ownerId, Module.Name, CancellationToken.None))!.CompletedAt);
            var otherAttempt = Assert.Single(await store.GetAssessmentsAsync(otherId, Module.Name, CancellationToken.None));
            Assert.Null(otherAttempt.CompletedAt);
            Assert.Equal(["c"], Assert.Single(otherAttempt.Answers).SelectedAnswerIds);
            Assert.Null((await store.FindAsync(otherId, Module.Name, CancellationToken.None))!.CompletedAt);
        }
    }

    [PostgresFact]
    public async Task FormativeAnswersRemainLockedAcrossContextsAndScopedToTheirLearner()
    {
        var (ownerId, otherId) = await SeedAsync();
        await using (var db = CreateContext())
        {
            var owner = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            Assert.Equal(new AssessmentResult(false, null, null, false),
                await Journey(db).SubmitAnswerAsync(owner, Module.Name, "practice", ["no"], CancellationToken.None));
        }
        await using (var db = CreateContext())
        {
            var owner = await db.UserAccounts.SingleAsync(account => account.Id == ownerId);
            var other = await db.UserAccounts.SingleAsync(account => account.Id == otherId);
            var service = Journey(db);
            Assert.Equal(["no"], await service.GetSelectedAnswersAsync(owner, Module.Name, "practice", CancellationToken.None));
            Assert.Equal(new AssessmentResult(false, null, null, false),
                await service.SubmitAnswerAsync(owner, Module.Name, "practice", ["yes"], CancellationToken.None));
            Assert.Equal(new AssessmentResult(true, null, null, false),
                await service.SubmitAnswerAsync(other, Module.Name, "practice", ["yes"], CancellationToken.None));
        }
        await using (var db = CreateContext())
        {
            var store = new TrainingProgressStore(db);
            var ownerResponse = await store.GetLatestResponseAsync(ownerId, Module.Name, "practice", CancellationToken.None);
            Assert.NotNull(ownerResponse);
            Assert.False(ownerResponse.Correct);
            Assert.Equal(["no"], ownerResponse.SelectedAnswerIds);
            Assert.True((await store.GetLatestResponseAsync(otherId, Module.Name, "practice", CancellationToken.None))!.Correct);
            Assert.Equal(2, await db.TrainingResponses.CountAsync());
            Assert.Empty(await store.GetAssessmentsAsync(ownerId, Module.Name, CancellationToken.None));
        }
    }

    private ApplicationDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(_testConnection ?? throw new InvalidOperationException("Integration database is not configured."))
            .Options);

    private async Task<(long OwnerId, long OtherId)> SeedAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var owner = RegisteredAccount("owner");
        var other = RegisteredAccount("other");
        db.UserAccounts.AddRange(owner, other);
        await db.SaveChangesAsync();
        return (owner.Id, other.Id);
    }

    private static UserAccount RegisteredAccount(string subject)
    {
        var account = new UserAccount(subject, $"{subject}@example.test");
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateLocation("Scotland", null);
        account.UpdateSetting("other", "Example setting", false, "none");
        account.UpdatePreferences(false, false);
        if (!account.CompleteRegistration(DateTimeOffset.UtcNow))
        {
            throw new InvalidOperationException("Integration account registration failed.");
        }
        return account;
    }

    private static TrainingJourneyService Journey(ApplicationDbContext db) =>
        new(new TestContentProvider(), new TrainingProgressStore(db), new LearningNoteStore(db), TimeProvider.System);

    private static readonly TrainingModule Module = new("integration-module", "Integration module", 1, false,
    [
        new("pre-one", "Pre one", "pre_confidence", "", [new("1", "Confident"), new("2", "Not confident")]),
        new("pre-two", "Pre two", "pre_confidence", "", [new("1", "Confident"), new("2", "Not confident")]),
        new("learning", "Learning", "sub_module_intro", ""),
        new("first", "First reflection", "text_page", "", Notes: true),
        new("second", "Second reflection", "text_page", "", Notes: true),
        new("post-one", "Post one", "confidence", "", [new("1", "Confident"), new("2", "Not confident")]),
        new("practice", "Practice question", "formative", "",
            [new("yes", "Correct"), new("no", "Incorrect")], ["yes"],
            FailureMessage: "Review how children learn before continuing.",
            SuccessMessage: "You recognised how children learn."),
        new("test-one", "First test question", "summative", "",
            [new("a", "First correct"), new("b", "Second correct"), new("c", "Incorrect")],
            ["a", "b"], AllowsMultipleAnswers: true),
        new("test-two", "Second test question", "summative", "",
            [new("yes", "Correct"), new("no", "Incorrect")], ["yes"]),
        new("results", "Test result", "assessment_results", ""),
        new("thank-you", "Module complete", "thankyou", ""),
        new("certificate", "Certificate", "certificate", "")
    ]);

    private sealed class TestContentProvider : ITrainingContentProvider
    {
        public Task<IReadOnlyList<TrainingModule>> GetModulesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrainingModule>>([Module]);

        public Task<TrainingModule?> GetModuleAsync(string moduleName, CancellationToken cancellationToken) =>
            Task.FromResult(moduleName == Module.Name ? Module : null);
    }
}
