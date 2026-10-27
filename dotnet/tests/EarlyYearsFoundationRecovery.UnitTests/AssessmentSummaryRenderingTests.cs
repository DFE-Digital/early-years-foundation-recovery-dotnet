using EarlyYearsFoundationRecovery.Application.Contracts;
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

    private async Task<string> RenderAsync(AssessmentSummaryViewModel model)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var http = new DefaultHttpContext { RequestServices = services };
        http.SetEndpoint(new Endpoint(null, EndpointMetadataCollection.Empty, "Razor rendering test"));
        var actionContext = new ActionContext(http, new RouteData(), new ActionDescriptor());
        var engine = services.GetRequiredService<IRazorViewEngine>();
        var result = engine.GetView(null, "/Views/Training/AssessmentResult.cshtml", isMainPage: false);
        Assert.True(result.Success, string.Join(", ", result.SearchedLocations ?? []));
        using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<AssessmentSummaryViewModel>(
            services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
        var viewContext = new ViewContext(actionContext, result.View, viewData,
            new TempDataDictionary(http, services.GetRequiredService<ITempDataProvider>()),
            writer, new HtmlHelperOptions());
        await result.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
