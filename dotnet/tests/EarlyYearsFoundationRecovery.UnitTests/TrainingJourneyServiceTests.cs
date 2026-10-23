using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Domain.Training;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class TrainingJourneyServiceTests
{
    [Fact]
    public async Task SubmitAnswerGradesOnlyWhenAllSummativeQuestionsAreAnswered()
    {
        var module = new TrainingModule(
            "module-one",
            "Module one",
            1,
            false,
            [
                new TrainingPage("q1", "Question one", "summative", "Question one",
                    [new TrainingQuestionOption("correct", "Correct")], ["correct"], "summative"),
                new TrainingPage("q2", "Question two", "summative", "Question two",
                    [new TrainingQuestionOption("wrong", "Wrong")], ["wrong"], "summative")
            ]);
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module),
            progress,
            new InMemoryNoteStore(),
            TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        var first = await service.SubmitAnswerAsync(account, module.Name, "q1", ["correct"], CancellationToken.None);
        var second = await service.SubmitAnswerAsync(account, module.Name, "q2", ["wrong"], CancellationToken.None);

        Assert.False(first.IsComplete);
        Assert.True(second.IsComplete);
        Assert.Equal(100m, second.Score);
        Assert.True(second.Passed);
    }

    [Fact]
    public async Task SubmitAnswerRejectsOptionsNotPublishedForTheQuestion()
    {
        var module = new TrainingModule(
            "module-one",
            "Module one",
            1,
            false,
            [
                new TrainingPage("q1", "Question one", "formative", "Question one",
                    [new TrainingQuestionOption("valid", "Valid")], ["valid"], "formative")
            ]);
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module),
            new InMemoryProgressStore(),
            new InMemoryNoteStore(),
            TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitAnswerAsync(
                new UserAccount("subject-1", "person@example.test"),
                module.Name,
                "q1",
                ["forged"],
                CancellationToken.None));
    }

    [Fact]
    public async Task FailedSummativeAttemptCanBeRetakenAndModuleCompletesOnlyAfterPassing()
    {
        var module = new TrainingModule(
            "module-one",
            "Module one",
            1,
            false,
            [
                new TrainingPage("q1", "Question one", "summative", "Question one",
                    [
                        new TrainingQuestionOption("correct", "Correct"),
                        new TrainingQuestionOption("incorrect", "Incorrect")
                    ],
                    ["correct"],
                    "summative")
            ]);
        var progress = new InMemoryProgressStore();
        var account = new UserAccount("subject-1", "person@example.test");
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module),
            progress,
            new InMemoryNoteStore(),
            TimeProvider.System);

        var failedAttempt = await service.SubmitAnswerAsync(
            account, module.Name, "q1", ["incorrect"], CancellationToken.None);
        var completedAfterFailure = await service.CompleteModuleAsync(
            account, module.Name, CancellationToken.None);
        var passedAttempt = await service.SubmitAnswerAsync(
            account, module.Name, "q1", ["correct"], CancellationToken.None);
        var completedAfterPassing = await service.CompleteModuleAsync(
            account, module.Name, CancellationToken.None);

        Assert.True(failedAttempt.IsComplete);
        Assert.False(failedAttempt.Passed);
        Assert.False(completedAfterFailure);
        Assert.True(passedAttempt.Passed);
        Assert.True(completedAfterPassing);
        Assert.Equal(2, (await progress.GetAssessmentsAsync(
            account.Id, module.Name, CancellationToken.None)).Count);
    }

    private sealed class InMemoryContentProvider(TrainingModule module) : ITrainingContentProvider
    {
        public Task<IReadOnlyList<TrainingModule>> GetModulesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrainingModule>>([module]);

        public Task<TrainingModule?> GetModuleAsync(string moduleName, CancellationToken cancellationToken) =>
            Task.FromResult<TrainingModule?>(module.Name == moduleName ? module : null);
    }

    private sealed class InMemoryProgressStore : ITrainingProgressStore
    {
        private readonly List<TrainingModuleProgress> _progress = [];
        private readonly List<AssessmentAttempt> _attempts = [];

        public Task<IReadOnlyList<TrainingModuleProgress>> GetForUserAsync(
            long userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrainingModuleProgress>>(
                _progress.Where(item => item.UserAccountId == userId).ToArray());

        public Task<TrainingModuleProgress?> FindAsync(
            long userId,
            string moduleName,
            CancellationToken cancellationToken) =>
            Task.FromResult(_progress.SingleOrDefault(item =>
                item.UserAccountId == userId && item.ModuleName == moduleName));

        public Task AddAsync(TrainingModuleProgress progress, CancellationToken cancellationToken)
        {
            _progress.Add(progress);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AssessmentAttempt>> GetAssessmentsAsync(
            long userId,
            string moduleName,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AssessmentAttempt>>(
                _attempts
                    .Where(item => item.UserAccountId == userId && item.ModuleName == moduleName)
                    .OrderBy(item => item.StartedAt)
                    .ToArray());

        public Task AddAssessmentAsync(AssessmentAttempt attempt, CancellationToken cancellationToken)
        {
            _attempts.Add(attempt);
            return Task.CompletedTask;
        }

        public Task AddResponseAsync(TrainingResponse response, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryNoteStore : ILearningNoteStore
    {
        public Task<LearningNote?> FindAsync(long userId, string moduleName, CancellationToken cancellationToken) =>
            Task.FromResult<LearningNote?>(null);

        public Task AddAsync(LearningNote note, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteForUserAsync(long userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
