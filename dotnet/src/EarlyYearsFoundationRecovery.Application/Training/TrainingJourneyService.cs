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
                var state = module.IsDraft ? "upcoming" : isCompleted ? "completed" : isStarted ? "in-progress" : "available";
                return new ModuleListing(module, state, userProgress?.VisitedPageCount ?? 0, userProgress?.LastPageName)
                {
                    CompletedAt = userProgress?.CompletedAt,
                    VisitedPageNames = userProgress?.VisitedPages.Select(page => page.Name).ToArray() ?? []
                };
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<LearningLogModule>> GetLearningLogAsync(
        UserAccount account,
        CancellationToken cancellationToken)
    {
        var activeModules = (await GetModulesAsync(account, cancellationToken))
            .Where(item => !item.Module.IsDraft && item.State is "in-progress" or "completed")
            .ToArray();
        var userNotes = await notes.GetForUserAsync(account.Id, cancellationToken);
        return activeModules
            .Select(item => new LearningLogModule(
                item.Module,
                userNotes.Where(note => note.ModuleName == item.Module.Name).ToArray()))
            .ToArray();
    }

    public async Task<bool> HasStartedPublishedModuleAsync(
        UserAccount account, CancellationToken cancellationToken) =>
        account.IsRegistrationComplete &&
        (await GetModulesAsync(account, cancellationToken))
            .Any(item => !item.Module.IsDraft && item.State is "in-progress" or "completed");

    public async Task<TrainingModule?> GetModuleAsync(string moduleName, CancellationToken cancellationToken) =>
        await content.GetModuleAsync(moduleName, cancellationToken);

    public async Task<DateTimeOffset?> GetCompletionDateAsync(
        UserAccount account,
        string moduleName,
        CancellationToken cancellationToken) =>
        (await progress.FindAsync(account.Id, moduleName, cancellationToken))?.CompletedAt;

    public async Task RecordPageViewAsync(
        UserAccount account,
        string moduleName,
        string pageName,
        CancellationToken cancellationToken)
    {
        var module = await content.GetModuleAsync(moduleName, cancellationToken);
        var page = module?.Pages.SingleOrDefault(page => page.Name == pageName);
        if (module is null || page is null)
        {
            throw new InvalidOperationException("The requested training content does not exist.");
        }

        var userProgress = await progress.FindAsync(account.Id, moduleName, cancellationToken);
        var isFirstModuleStart = userProgress?.StartedAt is null;
        if (userProgress is null)
        {
            userProgress = new TrainingModuleProgress(account.Id, moduleName);
            await progress.AddAsync(userProgress, cancellationToken);
        }

        var now = timeProvider.GetUtcNow();
        if (page.PageType == "sub_module_intro")
        {
            if (isFirstModuleStart && module.Pages.Any(item => item.PageType == "pre_confidence"))
            {
                var confidenceCheck = await progress.FindConfidenceCheckAsync(
                    account.Id, moduleName, "pre", cancellationToken);
                if (confidenceCheck is null)
                {
                    confidenceCheck = new ConfidenceCheckProgress(account.Id, moduleName, "pre");
                    await progress.AddConfidenceCheckAsync(confidenceCheck, cancellationToken);
                }
                confidenceCheck.RecordSkip(now);
            }
            userProgress.RecordStart(now);
        }
        userProgress.RecordPageView(pageName, now);
        await progress.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordConfidenceCheckQuestionViewAsync(
        UserAccount account,
        string moduleName,
        string questionName,
        CancellationToken cancellationToken)
    {
        var module = await content.GetModuleAsync(moduleName, cancellationToken);
        var question = module?.Pages.SingleOrDefault(page => page.Name == questionName);
        if (module is null || module.IsDraft || question is null ||
            question.PageType is not ("pre_confidence" or "confidence"))
        {
            throw new InvalidOperationException("The requested confidence-check question does not exist.");
        }

        var checkType = question.PageType == "pre_confidence" ? "pre" : "post";
        var checkQuestions = module.Pages.Where(page => page.PageType == question.PageType).ToArray();
        var index = Array.FindIndex(checkQuestions, page => page.Name == questionName);
        var check = await progress.FindConfidenceCheckAsync(account.Id, moduleName, checkType, cancellationToken);
        var now = timeProvider.GetUtcNow();
        var shouldStart = index == 0 && check?.StartedAt is null;
        var shouldComplete = !shouldStart &&
                             index == checkQuestions.Length - 1 &&
                             check?.CompletedAt is null;
        if (!shouldStart && !shouldComplete)
        {
            return;
        }

        if (check is null)
        {
            check = new ConfidenceCheckProgress(account.Id, moduleName, checkType);
            await progress.AddConfidenceCheckAsync(check, cancellationToken);
        }
        if (shouldComplete)
        {
            check.RecordCompletion(now);
        }
        else
        {
            check.RecordStart(now);
        }

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
        if (submitted.Length == 0 || submitted.Any(answer => !validOptionIds.Contains(answer)) ||
            (!question.AllowsMultipleAnswers && submitted.Length > 1))
        {
            throw new ArgumentException("Select a valid answer before continuing.", nameof(selectedAnswerIds));
        }

        var correct = question.PageType is "confidence" or "pre_confidence" ||
                      AssessmentGrader.IsCorrect(question.CorrectAnswerIds ?? [], submitted);
        if (question.PageType != "summative")
        {
            if (question.PageType == "formative")
            {
                var previousResponse = await progress.GetLatestResponseAsync(
                    account.Id, moduleName, questionName, cancellationToken);
                if (previousResponse is not null)
                {
                    return new AssessmentResult(previousResponse.Correct, null, null, false);
                }
            }

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
        var passedAttempt = attempts.LastOrDefault(item => item.Passed == true);
        if (passedAttempt is not null)
        {
            return new AssessmentResult(
                passedAttempt.Answers.SingleOrDefault(answer => answer.QuestionName == questionName)?.IsCorrect == true,
                passedAttempt.Score,
                true,
                true);
        }

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

    public async Task<IReadOnlyList<string>> GetSelectedAnswersAsync(
        UserAccount account,
        string moduleName,
        string questionName,
        CancellationToken cancellationToken)
    {
        var response = await progress.GetLatestResponseAsync(
            account.Id, moduleName, questionName, cancellationToken);
        return response?.SelectedAnswerIds ?? [];
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

    public Task<LearningNote?> GetPageNoteAsync(
        UserAccount account, string moduleName, string pageName, CancellationToken cancellationToken) =>
        notes.FindPageAsync(account.Id, moduleName, pageName, cancellationToken);

    public async Task SavePageNoteAsync(
        UserAccount account, string moduleName, string pageName, string content, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(content);
        if (content.Length > 20_000)
        {
            throw new ArgumentOutOfRangeException(nameof(content), "Learning notes cannot exceed 20,000 characters.");
        }

        var module = await GetModuleAsync(moduleName, cancellationToken);
        var page = module?.Pages.SingleOrDefault(item => item.Name == pageName);
        if (!account.IsRegistrationComplete || module is null || module.IsDraft || page?.SupportsNotes != true)
        {
            throw new InvalidOperationException("Learning notes are not available for this page.");
        }

        var note = await notes.FindPageAsync(account.Id, moduleName, pageName, cancellationToken);
        if (note is null)
        {
            note = new LearningNote(account.Id, moduleName, pageName, page.Title);
            await notes.AddAsync(note, cancellationToken);
        }
        note.Update(content, timeProvider.GetUtcNow());
        await notes.SaveChangesAsync(cancellationToken);
    }

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

public sealed record ModuleListing(TrainingModule Module, string State, int VisitedPageCount, string? LastPageName = null)
{
    public DateTimeOffset? CompletedAt { get; init; }
    public IReadOnlyCollection<string> VisitedPageNames { get; init; } = [];
}

public sealed record LearningLogModule(TrainingModule Module, IReadOnlyList<LearningNote> Notes);

public sealed record AssessmentResult(bool Correct, decimal? Score, bool? Passed, bool IsComplete);
