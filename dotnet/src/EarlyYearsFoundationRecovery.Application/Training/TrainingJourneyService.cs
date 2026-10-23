using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Domain.Training;

namespace EarlyYearsFoundationRecovery.Application.Training;

public sealed class TrainingJourneyService(
    ITrainingContentProvider content,
    ITrainingProgressStore progress,
    ILearningNoteStore notes,
    TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<ModuleListing>> GetModulesAsync(
        UserAccount account,
        CancellationToken cancellationToken)
    {
        var modules = await content.GetModulesAsync(cancellationToken);
        var progressByModule = (await progress.GetForUserAsync(account.Id, cancellationToken))
            .ToDictionary(item => item.ModuleName, StringComparer.Ordinal);

        return modules
            .OrderBy(module => module.Position)
            .Where(module => !string.IsNullOrWhiteSpace(module.Name))
            .Select(module =>
            {
                progressByModule.TryGetValue(module.Name, out var userProgress);
                var isCompleted = userProgress?.CompletedAt is not null;
                var isStarted = userProgress?.StartedAt is not null;
                var state = isCompleted ? "completed" : isStarted ? "in-progress" : "available";
                return new ModuleListing(module, state, userProgress?.VisitedPageCount ?? 0);
            })
            .ToArray();
    }

    public async Task<TrainingModule?> GetModuleAsync(string moduleName, CancellationToken cancellationToken) =>
        await content.GetModuleAsync(moduleName, cancellationToken);

    public async Task RecordPageViewAsync(
        UserAccount account,
        string moduleName,
        string pageName,
        CancellationToken cancellationToken)
    {
        var module = await content.GetModuleAsync(moduleName, cancellationToken);
        if (module is null || module.Pages.All(page => page.Name != pageName))
        {
            throw new InvalidOperationException("The requested training content does not exist.");
        }

        var userProgress = await progress.FindAsync(account.Id, moduleName, cancellationToken);
        if (userProgress is null)
        {
            userProgress = new TrainingModuleProgress(account.Id, moduleName);
            await progress.AddAsync(userProgress, cancellationToken);
        }

        userProgress.RecordPageView(pageName, timeProvider.GetUtcNow());
        await progress.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordStartAsync(
        UserAccount account,
        string moduleName,
        CancellationToken cancellationToken)
    {
        var module = await content.GetModuleAsync(moduleName, cancellationToken);
        if (module is null)
        {
            throw new InvalidOperationException("The requested training module does not exist.");
        }

        var userProgress = await progress.FindAsync(account.Id, moduleName, cancellationToken);
        if (userProgress is null)
        {
            userProgress = new TrainingModuleProgress(account.Id, moduleName);
            await progress.AddAsync(userProgress, cancellationToken);
        }

        userProgress.RecordStart(timeProvider.GetUtcNow());
        await progress.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssessmentResult> SubmitAnswerAsync(
        UserAccount account,
        string moduleName,
        string questionName,
        IEnumerable<string> selectedAnswerIds,
        CancellationToken cancellationToken)
    {
        var module = await content.GetModuleAsync(moduleName, cancellationToken);
        var question = module?.Pages.SingleOrDefault(page =>
            page.Name == questionName &&
            page.PageType is "formative" or "summative" or "confidence" or "pre_confidence");
        if (question is null)
        {
            throw new InvalidOperationException("The requested training question does not exist.");
        }

        var submitted = selectedAnswerIds
            .Where(answer => !string.IsNullOrWhiteSpace(answer))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var validOptionIds = (question.Options ?? [])
            .Select(option => option.Id)
            .ToHashSet(StringComparer.Ordinal);
        if (submitted.Length == 0 || submitted.Any(answer => !validOptionIds.Contains(answer)))
        {
            throw new ArgumentException("Select a valid answer before continuing.", nameof(selectedAnswerIds));
        }

        var correct = question.PageType is "confidence" or "pre_confidence" ||
                      AssessmentGrader.IsCorrect(question.CorrectAnswerIds ?? [], submitted);
        if (question.PageType != "summative")
        {
            await progress.AddResponseAsync(
                new TrainingResponse(
                    account.Id,
                    moduleName,
                    questionName,
                    question.PageType,
                    submitted,
                    correct,
                    timeProvider.GetUtcNow()),
                cancellationToken);
            await RecordPageViewAsync(account, moduleName, questionName, cancellationToken);
            return new AssessmentResult(correct, null, null, false);
        }

        var attempts = await progress.GetAssessmentsAsync(account.Id, moduleName, cancellationToken);
        var attempt = attempts.LastOrDefault(item => item.CompletedAt is null);
        if (attempt is null)
        {
            attempt = new AssessmentAttempt(account.Id, moduleName, timeProvider.GetUtcNow());
            await progress.AddAssessmentAsync(attempt, cancellationToken);
        }

        attempt.AddOrReplaceAnswer(questionName, submitted, correct);
        await RecordPageViewAsync(account, moduleName, questionName, cancellationToken);

        var summativeQuestionNames = module!.Pages
            .Where(page => page.PageType == "summative")
            .Select(page => page.Name)
            .ToHashSet(StringComparer.Ordinal);
        var allQuestionsAnswered = summativeQuestionNames.Count > 0 &&
                                   summativeQuestionNames.All(name =>
                                       attempt.Answers.Any(answer => answer.QuestionName == name));
        if (!allQuestionsAnswered)
        {
            await progress.SaveChangesAsync(cancellationToken);
            return new AssessmentResult(correct, null, null, false);
        }

        var score = attempt.Grade(summativeQuestionNames.Count, timeProvider.GetUtcNow());
        await progress.SaveChangesAsync(cancellationToken);
        return new AssessmentResult(correct, score, attempt.Passed, true);
    }

    public async Task<bool> CompleteModuleAsync(
        UserAccount account,
        string moduleName,
        CancellationToken cancellationToken)
    {
        var module = await content.GetModuleAsync(moduleName, cancellationToken);
        if (module is null)
        {
            throw new InvalidOperationException("The requested training module does not exist.");
        }

        var summativeQuestionCount = module.Pages.Count(page => page.PageType == "summative");
        if (summativeQuestionCount > 0)
        {
            var attempts = await progress.GetAssessmentsAsync(account.Id, moduleName, cancellationToken);
            if (attempts.LastOrDefault()?.Passed != true)
            {
                return false;
            }
        }

        var userProgress = await progress.FindAsync(account.Id, moduleName, cancellationToken);
        if (userProgress is null)
        {
            return false;
        }

        userProgress.RecordCompletion(timeProvider.GetUtcNow());
        await progress.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<LearningNote?> GetNoteAsync(
        UserAccount account,
        string moduleName,
        CancellationToken cancellationToken) =>
        await notes.FindAsync(account.Id, moduleName, cancellationToken);

    public async Task SaveNoteAsync(
        UserAccount account,
        string moduleName,
        string content,
        CancellationToken cancellationToken)
    {
        if (content.Length > 20_000)
        {
            throw new ArgumentOutOfRangeException(nameof(content), "Learning notes cannot exceed 20,000 characters.");
        }

        var note = await notes.FindAsync(account.Id, moduleName, cancellationToken);
        if (note is null)
        {
            note = new LearningNote(account.Id, moduleName);
            await notes.AddAsync(note, cancellationToken);
        }

        note.Update(content, timeProvider.GetUtcNow());
        await notes.SaveChangesAsync(cancellationToken);
    }

    public async Task<AssessmentAttempt?> GetLatestAssessmentAsync(
        UserAccount account,
        string moduleName,
        CancellationToken cancellationToken)
    {
        var attempts = await progress.GetAssessmentsAsync(account.Id, moduleName, cancellationToken);
        return attempts.LastOrDefault();
    }
}

public sealed record ModuleListing(TrainingModule Module, string State, int VisitedPageCount);

public sealed record AssessmentResult(bool Correct, decimal? Score, bool? Passed, bool IsComplete);
