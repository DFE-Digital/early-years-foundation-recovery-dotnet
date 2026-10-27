using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Domain.Training;
using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Web.Controllers;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Security.Claims;
using EarlyYearsFoundationRecovery.Web.Content;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class TrainingJourneyServiceTests
{
    [Fact]
    public async Task CertificateDownloadUsesTheAuthenticatedLearnerAndRecordedCompletion()
    {
        var module = ConfidenceModule("summative") with { Criteria = "- Learn about development" };
        module = module with
        {
            Pages = [
                module.Pages[0],
                module.Pages[1] with { PageType = "thankyou" },
                new TrainingPage("certificate", "Certificate", "certificate", "")
            ]
        };
        var controller = TestController(module);
        await controller.Answer(
            module.Name, "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);
        await controller.Page(module.Name, "next", CancellationToken.None);
        var preview = Assert.IsType<ViewResult>(await controller.Certificate(module.Name, CancellationToken.None));
        var model = Assert.IsType<CertificateViewModel>(preview.Model);
        Assert.Equal("Alex Example", model.Certificate.LearnerName);
        Assert.Equal(module.Criteria, model.Certificate.Criteria);
        Assert.Equal(module.Title, model.Certificate.ModuleTitle);

        controller.Request.Path = "/training/module-one/certificate.pdf";
        var download = Assert.IsType<FileContentResult>(await controller.Certificate(module.Name, CancellationToken.None));
        Assert.Equal("application/pdf", download.ContentType);
        Assert.Equal("certificate-of-achievement.pdf", download.FileDownloadName);
        Assert.True(download.FileContents.AsSpan().StartsWith("%PDF-"u8));

        controller.Request.Path = "/training/module-one/certificate";
        var repeated = Assert.IsType<ViewResult>(await controller.Certificate(module.Name, CancellationToken.None));
        Assert.Equal(model.Certificate.CompletedAt,
            Assert.IsType<CertificateViewModel>(repeated.Model).Certificate.CompletedAt);
    }

    [Fact]
    public async Task DirectPdfDownloadCannotBypassCompletion()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [module.Pages[0], new TrainingPage("certificate", "Certificate", "certificate", "")]
        };
        var controller = TestController(module);
        controller.Request.Path = "/training/module-one/certificate.pdf";

        var result = Assert.IsType<RedirectToActionResult>(
            await controller.Certificate(module.Name, CancellationToken.None));
        Assert.Equal(nameof(TrainingController.Module), result.ActionName);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("2", false)]
    public async Task CompletedAssessmentRedirectsToRefreshSafeResultPage(string selectedAnswer, bool passed)
    {
        var controller = ConfidenceController("summative");
        var submitted = Assert.IsType<RedirectToActionResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = [selectedAnswer] }, CancellationToken.None));

        Assert.Equal(nameof(TrainingController.AssessmentResult), submitted.ActionName);
        var first = Assert.IsType<ViewResult>(await controller.AssessmentResult("module-one", CancellationToken.None));
        var refreshed = Assert.IsType<ViewResult>(await controller.AssessmentResult("module-one", CancellationToken.None));
        var summary = Assert.IsType<AssessmentSummaryViewModel>(first.Model);
        var refreshedSummary = Assert.IsType<AssessmentSummaryViewModel>(refreshed.Model);
        Assert.Equal(passed, summary.Assessment.Passed);
        Assert.Same(summary.Assessment, refreshedSummary.Assessment);
        Assert.Equal("next", summary.NextPage?.Name);
        Assert.Equal("q1", summary.FirstQuestion?.Name);
    }

    [Fact]
    public async Task IncompleteAssessmentCannotDisplayAResult()
    {
        var controller = ConfidenceController("summative");

        var result = Assert.IsType<RedirectToActionResult>(await controller.AssessmentResult(
            "module-one", CancellationToken.None));

        Assert.Equal(nameof(TrainingController.Module), result.ActionName);
        Assert.IsType<string>(controller.TempData["TrainingNotice"]);
    }

    [Fact]
    public async Task ContentfulResultPageUsesTheSameAssessmentSummary()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [
                module.Pages[0],
                new TrainingPage("results", "Results", "assessment_results", "Results"),
                module.Pages[1]
            ]
        };
        var controller = TestController(module);
        await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);

        var page = Assert.IsType<RedirectToActionResult>(await controller.Page(
            "module-one", "results", CancellationToken.None));
        Assert.Equal(nameof(TrainingController.AssessmentResult), page.ActionName);
        var result = Assert.IsType<ViewResult>(await controller.AssessmentResult("module-one", CancellationToken.None));
        var summary = Assert.IsType<AssessmentSummaryViewModel>(result.Model);
        Assert.Equal("next", summary.NextPage?.Name);
    }

    [Theory]
    [InlineData("thankyou")]
    [InlineData("certificate")]
    public async Task CompletionAndCertificatesCannotBeReachedBeforePassing(string pageType)
    {
        var module = ConfidenceModule("summative");
        module = module with { Pages = [module.Pages[0], module.Pages[1] with { PageType = pageType }] };
        var controller = TestController(module);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Page(
            "module-one", "next", CancellationToken.None));

        if (pageType == "certificate")
        {
            Assert.Equal(nameof(TrainingController.Certificate), result.ActionName);
            result = Assert.IsType<RedirectToActionResult>(await controller.Certificate(
                "module-one", CancellationToken.None));
        }

        Assert.Equal(nameof(TrainingController.Module), result.ActionName);
        Assert.IsType<string>(controller.TempData["TrainingNotice"]);
        var overview = Assert.IsType<ViewResult>(await controller.Module("module-one", CancellationToken.None));
        Assert.Equal(0, Assert.IsType<TrainingModuleDetailViewModel>(overview.Model).VisitedPageCount);
    }

    [Fact]
    public async Task CertificateIsAvailableOnlyAfterPassingAndCompletingTheModule()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [
                module.Pages[0],
                module.Pages[1] with { PageType = "thankyou" },
                new TrainingPage("certificate", "Certificate", "certificate", "Certificate")
            ]
        };
        var controller = TestController(module);
        await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(await controller.Page("module-one", "certificate", CancellationToken.None));
        Assert.IsType<ViewResult>(await controller.Page("module-one", "next", CancellationToken.None));
        Assert.IsType<ViewResult>(await controller.Certificate("module-one", CancellationToken.None));
    }

    [Theory]
    [InlineData("1", "2", true)]
    [InlineData("2", "1", false)]
    public async Task FormativeAnswersCannotBeChangedAfterSubmission(string first, string second, bool correct)
    {
        var module = ConfidenceModule("formative");
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), new InMemoryProgressStore(), new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        await service.SubmitAnswerAsync(account, module.Name, "q1", [first], CancellationToken.None);
        var secondResult = await service.SubmitAnswerAsync(account, module.Name, "q1", [second], CancellationToken.None);

        Assert.Equal(correct, secondResult.Correct);
        Assert.Equal([first], await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
    }

    [Fact]
    public async Task PassedAssessmentCannotBeRetakenOrChanged()
    {
        var module = ConfidenceModule("summative");
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        await service.SubmitAnswerAsync(account, module.Name, "q1", ["1"], CancellationToken.None);
        var resubmission = await service.SubmitAnswerAsync(account, module.Name, "q1", ["2"], CancellationToken.None);

        Assert.True(resubmission.Passed);
        Assert.Equal(100m, resubmission.Score);
        var attempt = Assert.Single(await progress.GetAssessmentsAsync(account.Id, module.Name, CancellationToken.None));
        Assert.Equal(["1"], Assert.Single(attempt.Answers).SelectedAnswerIds);
    }

    [Fact]
    public async Task SummativeQuestionsAdvanceWithoutShowingImmediateCorrectness()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [module.Pages[0], module.Pages[0] with { Name = "q2" }, module.Pages[1]]
        };
        var controller = TestController(module);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None));

        Assert.Equal(nameof(TrainingController.Page), result.ActionName);
        Assert.Equal("q2", result.RouteValues!["pageName"]);
        var view = Assert.IsType<ViewResult>(await controller.Page("module-one", "q1", CancellationToken.None));
        var model = Assert.IsType<TrainingPageViewModel>(view.Model);
        Assert.Equal(["1"], model.SelectedAnswerIds);
        Assert.False(model.IsReadOnly);
    }

    [Theory]
    [InlineData("summative", true)]
    [InlineData("formative", true)]
    [InlineData("pre_confidence", false)]
    [InlineData("confidence", false)]
    public async Task OnlyFormativeAndPassedSummativeAnswersAreReadOnly(string pageType, bool readOnly)
    {
        var controller = ConfidenceController(pageType);
        await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(await controller.Page("module-one", "q1", CancellationToken.None));
        var model = Assert.IsType<TrainingPageViewModel>(view.Model);

        Assert.Equal(readOnly, model.IsReadOnly);
        Assert.Equal(["1"], model.SelectedAnswerIds);
    }

    [Fact]
    public async Task DirectQuestionRequestsRequireCompletedRegistration()
    {
        var controller = TestController(ConfidenceModule("confidence"), registered: false);

        var page = Assert.IsType<RedirectToActionResult>(await controller.Page(
            "module-one", "q1", CancellationToken.None));
        var answer = Assert.IsType<RedirectToActionResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None));

        Assert.Equal("Registration", page.ControllerName);
        Assert.Equal("Terms", page.ActionName);
        Assert.Equal("Registration", answer.ControllerName);
        Assert.Equal("Terms", answer.ActionName);
    }

    [Fact]
    public async Task AnsweringDraftModulesReturnsNotFound()
    {
        var controller = TestController(ConfidenceModule("confidence") with { IsDraft = true });

        Assert.IsType<NotFoundResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None));
    }

    [Fact]
    public async Task ModuleOverviewResumesFromLastVisitedPage()
    {
        var controller = ConfidenceController("pre_confidence");

        await controller.Page("module-one", "next", CancellationToken.None);
        var result = Assert.IsType<ViewResult>(await controller.Module("module-one", CancellationToken.None));
        var model = Assert.IsType<TrainingModuleDetailViewModel>(result.Model);

        Assert.Equal("next", model.ResumePage?.Name);
        Assert.Equal(1, model.VisitedPageCount);

        await controller.Page("module-one", "q1", CancellationToken.None);
        result = Assert.IsType<ViewResult>(await controller.Module("module-one", CancellationToken.None));
        model = Assert.IsType<TrainingModuleDetailViewModel>(result.Model);
        Assert.Equal("q1", model.ResumePage?.Name);
        Assert.Equal(2, model.VisitedPageCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("removed-page")]
    public void ResumeFallsBackToFirstPageWhenThereIsNoCurrentMilestone(string? lastPageName)
    {
        var model = new TrainingModuleDetailViewModel(ConfidenceModule("confidence"), 0, lastPageName);

        Assert.Equal("q1", model.ResumePage?.Name);
    }

    [Fact]
    public void EmptyModuleHasNoResumeTarget()
    {
        var module = new TrainingModule("empty", "Empty", 1, true, []);

        Assert.Null(new TrainingModuleDetailViewModel(module, 0).ResumePage);
    }

    [Theory]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    public async Task ConfidenceAnswersCanBeRestoredAndEditedWithoutAnAssessment(string pageType)
    {
        var module = ConfidenceModule(pageType);
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        Assert.Empty(await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
        await service.SubmitAnswerAsync(account, module.Name, "q1", ["1"], CancellationToken.None);
        Assert.Equal(["1"], await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
        await service.SubmitAnswerAsync(account, module.Name, "q1", ["2"], CancellationToken.None);
        Assert.Equal(["2"], await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
        Assert.Empty(await progress.GetAssessmentsAsync(account.Id, module.Name, CancellationToken.None));
        Assert.Empty(await service.GetSelectedAnswersAsync(account, "another-module", "q1", CancellationToken.None));
        Assert.Empty(await service.GetSelectedAnswersAsync(account, module.Name, "another-question", CancellationToken.None));
    }

    [Theory]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    [InlineData("formative")]
    [InlineData("summative")]
    public async Task SingleSelectQuestionsRejectMultipleAnswers(string pageType)
    {
        var module = ConfidenceModule(pageType);
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitAnswerAsync(account, module.Name, "q1", ["1", "2"], CancellationToken.None));

        Assert.Empty(await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
        Assert.Empty(await progress.GetAssessmentsAsync(account.Id, module.Name, CancellationToken.None));
    }

    [Theory]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    public async Task ConfidenceSubmissionRedirectsToNextPageAndRestoresSelection(string pageType)
    {
        var controller = ConfidenceController(pageType);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["2"] }, CancellationToken.None));

        Assert.Equal(nameof(TrainingController.Page), result.ActionName);
        Assert.Equal("next", result.RouteValues!["pageName"]);
        var pageResult = Assert.IsType<ViewResult>(await controller.Page("module-one", "q1", CancellationToken.None));
        var model = Assert.IsType<TrainingPageViewModel>(pageResult.Model);
        Assert.Equal("Confidence check", model.Title);
        Assert.Equal(["2"], model.SelectedAnswerIds);
    }

    [Theory]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    public async Task MissingConfidenceAnswerShowsValidationInsteadOfAdvancing(string pageType)
    {
        var controller = ConfidenceController(pageType);

        var result = Assert.IsType<ViewResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel(), CancellationToken.None));

        Assert.Equal("Page", result.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(TrainingAnswerModel.SelectedAnswerIds)));
    }

    private static TrainingModule ConfidenceModule(string pageType) =>
        new("module-one", "Module one", 1, false,
        [
            new TrainingPage("q1", "q1", pageType, "How confident do you feel?",
                [new("1", "Very confident"), new("2", "Not very confident")], ["1"], pageType),
            new TrainingPage("next", "Next page", "page", "Keep learning.")
        ]);

    private static TrainingController ConfidenceController(string pageType)
        => TestController(ConfidenceModule(pageType));

    private static TrainingController TestController(TrainingModule module, bool registered = true)
    {
        var account = new UserAccount("subject-1", "person@example.test");
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateLocation("Scotland", null);
        account.UpdateSetting("other", "Example setting", false, "none");
        account.UpdatePreferences(false, false);
        if (registered)
        {
            Assert.True(account.CompleteRegistration(DateTimeOffset.UtcNow));
        }
        var notes = new InMemoryNoteStore();
        var controller = new TrainingController(
            new AccountService(new InMemoryAccountStore(account), notes, TimeProvider.System),
            new TrainingJourneyService(
                new InMemoryContentProvider(module),
                new InMemoryProgressStore(), notes, TimeProvider.System),
            new StubCertificatePdfRenderer())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject-1")], "test"))
                }
            }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new InMemoryTempDataProvider());
        return controller;
    }

    private sealed class StubCertificatePdfRenderer : ICertificatePdfRenderer
    {
        public Task<byte[]> GenerateAsync(CertificateData certificate, CancellationToken cancellationToken) =>
            Task.FromResult("%PDF-test"u8.ToArray());
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class InMemoryAccountStore(UserAccount account) : IUserAccountStore
    {
        public Task<UserAccount?> FindByGovOneSubjectAsync(string subject, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(subject == account.GovOneSubject ? account : null);

        public Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

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
        private readonly List<TrainingResponse> _responses = [];

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

        public Task AddResponseAsync(TrainingResponse response, CancellationToken cancellationToken)
        {
            _responses.Add(response);
            return Task.CompletedTask;
        }

        public Task<TrainingResponse?> GetLatestResponseAsync(
            long userId,
            string moduleName,
            string questionName,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responses.LastOrDefault(response =>
                response.UserAccountId == userId && response.ModuleName == moduleName &&
                response.QuestionName == questionName));

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
