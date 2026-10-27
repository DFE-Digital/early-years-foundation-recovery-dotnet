using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Training;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class AssessmentSummaryRenderingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AssessmentSummaryRenderingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedResultsShowOnlyIncorrectQuestionsAndTheirSelectedAnswers(bool passed)
    {
        var (module, attempt) = CreateAssessment(passed);
        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.Contains("Review your incorrect answers", html);
        Assert.Contains("Which approach supports development?", html);
        Assert.Contains("You selected:", html);
        Assert.Contains("Ignore individual needs", html);
        Assert.Contains("Revisit the topic", html);
        Assert.Contains("href=\"/training/module-one/content/topic\"", html);
        Assert.DoesNotContain("Correct question body", html);
        Assert.DoesNotContain("Unselected distractor", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<form", html);
    }

    [Fact]
    public async Task PerfectScoreDoesNotShowIncorrectAnswerReview()
    {
        var (module, _) = CreateAssessment(true);
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        foreach (var question in module.Pages.Where(page => page.PageType == "summative"))
        {
            attempt.AddOrReplaceAnswer(question.Name, ["1"], true);
        }
        attempt.Grade(4, DateTimeOffset.UtcNow);

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.DoesNotContain("Review your incorrect answers", html);
        Assert.DoesNotContain("Which approach supports development?", html);
        Assert.DoesNotContain("You selected:", html);
    }

    [Fact]
    public async Task UnfinishedAttemptDoesNotExposeIncorrectAnswers()
    {
        var (module, _) = CreateAssessment(false);
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("incorrect", ["2"], false);

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.DoesNotContain("Review your incorrect answers", html);
        Assert.DoesNotContain("Which approach supports development?", html);
        Assert.DoesNotContain("You selected:", html);
    }

    [Fact]
    public async Task ReviewListsAllSelectedAnswersAndEncodesTheirLabels()
    {
        var (module, _) = CreateAssessment(false);
        var question = module.Pages.Single(page => page.Name == "incorrect") with
        {
            Options = [new("1", "Support needs"), new("2", "<img src=x onerror=alert(1)>"), new("3", "Another selected answer")],
            AllowsMultipleAnswers = true
        };
        module = module with { Pages = module.Pages.Select(page => page.Name == question.Name ? question : page).ToArray() };
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer(question.Name, ["2", "3"], false);
        attempt.Grade(1, DateTimeOffset.UtcNow);

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.Contains("&lt;img", html);
        Assert.Contains("Another selected answer", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("Support needs", html);
    }

    [Fact]
    public async Task RemovedAnswerOptionsAreExplicitlyReportedInsteadOfBeingSilentlyOmitted()
    {
        var (module, _) = CreateAssessment(false);
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("incorrect", ["removed-option"], false);
        attempt.Grade(1, DateTimeOffset.UtcNow);

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.Contains("This answer is no longer available in the module content.", html);
    }

    [Fact]
    public async Task FailedResultsRetakeLinkReturnsToPublishedAssessmentIntroduction()
    {
        var (module, attempt) = CreateAssessment(false);
        module = module with
        {
            Pages = [new TrainingPage("test-intro", "End of module test", "assessment_intro", ""), .. module.Pages]
        };

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.Contains("href=\"/training/module-one/content/test-intro\"", html);
        Assert.DoesNotContain("href=\"/training/module-one/content/incorrect\"", html);
    }

    [Fact]
    public async Task CompletedOverviewRendersViewCertificateButton()
    {
        var (module, attempt) = CreateAssessment(true);
        module = module with
        {
            Pages = [.. module.Pages, new TrainingPage("certificate", "Certificate", "certificate", "")]
        };
        var model = new TrainingModuleDetailViewModel(
            module, 4, "topic", DateTimeOffset.UtcNow, attempt);

        var html = await RenderAsync(model, "/Views/Training/Module.cshtml");

        Assert.Contains("href=\"/training/module-one/content/certificate\">View certificate</a>", html);
        Assert.DoesNotContain(">Continue module</a>", html);
    }

    [Fact]
    public async Task FailedOverviewRendersRetakeButton()
    {
        var (module, attempt) = CreateAssessment(false);
        module = module with
        {
            Pages = [new TrainingPage("test-intro", "End of module test", "assessment_intro", ""), .. module.Pages]
        };
        var model = new TrainingModuleDetailViewModel(module, 3, "topic", LatestAssessment: attempt);

        var html = await RenderAsync(model, "/Views/Training/Module.cshtml");

        Assert.Contains("href=\"/training/module-one/content/test-intro\">Retake test</a>", html);
        Assert.DoesNotContain(">Continue module</a>", html);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task FormativeFeedbackShowsRelevantGuidanceOnSubmissionAndRevisit(bool correct, bool revisit)
    {
        var question = FormativeQuestion();
        var module = new TrainingModule("module-one", "Module one", 1, false,
            [question, new TrainingPage("next", "Next topic", "text_page", "")]);
        var html = revisit
            ? await RenderAsync(new TrainingPageViewModel(module, question, module.Pages[1], null)
                { SelectedAnswerIds = [correct ? "1" : "2"] }, "/Views/Training/Page.cshtml")
            : await RenderAsync(new AssessmentResultViewModel(module, question,
                new AssessmentResult(correct, null, null, false)), "/Views/Training/AnswerResult.cshtml");

        Assert.Contains(correct ? "<strong>Success guidance</strong>" : "<strong>Failure guidance</strong>", html);
        Assert.DoesNotContain(correct ? "Failure guidance" : "Success guidance", html);
        Assert.Contains("href=\"/training/module-one/content/next\"", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("javascript:", html);
        if (!correct)
        {
            Assert.Contains("Correct answer", html);
            Assert.Contains("&lt;Support individual needs&gt;", html);
            Assert.Contains("Explanation", html);
        }
        else
        {
            Assert.DoesNotContain("<h2>Correct answer</h2>", html);
            Assert.DoesNotContain("<h2>Explanation</h2>", html);
        }
        if (revisit)
        {
            Assert.Contains("disabled=\"disabled\"", html);
            Assert.DoesNotContain(">Check answer</button>", html);
        }
    }

    [Fact]
    public async Task IncorrectMultiSelectLearningCheckListsEveryCorrectOption()
    {
        var question = FormativeQuestion() with
        {
            AllowsMultipleAnswers = true,
            CorrectAnswerIds = ["1", "3"]
        };
        var module = new TrainingModule("module-one", "Module one", 1, false, [question]);

        var html = await RenderAsync(new AssessmentResultViewModel(module, question,
            new AssessmentResult(false, null, null, false)), "/Views/Training/AnswerResult.cshtml");

        Assert.Contains("&lt;Support individual needs&gt;", html);
        Assert.Contains("Observe development", html);
        Assert.DoesNotContain("Ignore needs", html);
    }

    [Theory]
    [InlineData("formative")]
    [InlineData("summative")]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    public async Task UnansweredQuestionDoesNotExposeLearningCheckFeedback(string pageType)
    {
        var question = FormativeQuestion() with { PageType = pageType };
        var module = new TrainingModule("module-one", "Module one", 1, false, [question]);

        var html = await RenderAsync(new TrainingPageViewModel(module, question, null, null), "/Views/Training/Page.cshtml");

        Assert.DoesNotContain("Success guidance", html);
        Assert.DoesNotContain("Failure guidance", html);
        Assert.DoesNotContain("Explanation", html);
    }

    private static TrainingPage FormativeQuestion() =>
        new("learning-check", "Question", "formative", "How can you help?",
            [new("1", "<Support individual needs>"), new("2", "Ignore needs"), new("3", "Observe development")], ["1"],
            FailureMessage: "**Failure guidance**\n\n<script>alert(1)</script>",
            SuccessMessage: "**Success guidance**\n\n[Unsafe](javascript:alert%281%29)");

    [Fact]
    public async Task LearningCheckWithoutOptionalGuidanceStillOffersContinuation()
    {
        var question = FormativeQuestion() with { FailureMessage = "", SuccessMessage = "" };
        var module = new TrainingModule("module-one", "Module one", 1, false,
            [question, new TrainingPage("next", "Next", "text_page", "")]);

        var html = await RenderAsync(new AssessmentResultViewModel(module, question,
            new AssessmentResult(false, null, null, false)), "/Views/Training/AnswerResult.cshtml");

        Assert.Contains("Correct answer", html);
        Assert.DoesNotContain("<h2>Explanation</h2>", html);
        Assert.Contains("href=\"/training/module-one/content/next\"", html);
    }

    private static (TrainingModule Module, AssessmentAttempt Attempt) CreateAssessment(bool passed)
    {
        var module = new TrainingModule("module-one", "Module one", 1, false,
        [
            new("topic", "Topic", "text_page", "Learning material"),
            new("incorrect", "Question one", "summative", "Which approach supports development?",
                [new("1", "Support individual needs"), new("2", "Ignore individual needs"), new("3", "Unselected distractor")],
                ["1"], FailureMessage: "[Revisit the topic](/training/module-one/content/topic)\n\n<script>alert(1)</script>"),
            new("correct-one", "Question two", "summative", "Correct question body", [new("1", "Yes")], ["1"]),
            new("correct-two", "Question three", "summative", "Another assessment question", [new("1", "Yes")], ["1"]),
            new("correct-three", "Question four", "summative", "Another assessment question", [new("1", "Yes")], ["1"]),
            new("results", "Results", "assessment_results", ""),
            new("reflection", "Reflect", "confidence", "")
        ]);
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("incorrect", ["2"], false);
        foreach (var question in module.Pages.Where(page => page.Name.StartsWith("correct-", StringComparison.Ordinal)))
        {
            attempt.AddOrReplaceAnswer(question.Name, ["1"], passed || question.Name == "correct-one");
        }
        attempt.Grade(4, DateTimeOffset.UtcNow);
        return (module, attempt);
    }

    private async Task<string> RenderAsync<TModel>(
        TModel model, string viewPath = "/Views/Training/AssessmentResult.cshtml")
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("localhost");
        http.SetEndpoint(new Endpoint(null, EndpointMetadataCollection.Empty, "Razor rendering test"));
        var routeData = new RouteData();
        routeData.Values["controller"] = "Training";
        routeData.Values["action"] = "AssessmentResult";
        var actionContext = new ActionContext(http, routeData, new ActionDescriptor());
        var engine = services.GetRequiredService<IRazorViewEngine>();
        var result = engine.GetView(null, viewPath, isMainPage: false);
        Assert.True(result.Success, string.Join(", ", result.SearchedLocations ?? []));
        using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<TModel>(
            services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
        var viewContext = new ViewContext(actionContext, result.View, viewData,
            new TempDataDictionary(http, services.GetRequiredService<ITempDataProvider>()),
            writer, new HtmlHelperOptions());
        await result.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
