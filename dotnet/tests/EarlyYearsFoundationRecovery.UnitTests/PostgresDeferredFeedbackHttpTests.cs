using System.Net;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed partial class PostgresPersistenceTests
{
    [PostgresFact]
    public async Task HttpDeferredFeedbackCannotSubmitAnswersOrBypassCompletionEligibility()
    {
        await SeedEditableAccountAsync();
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        const string module = "/training/integration-module";
        var feedback = await client.GetStringAsync(module + "/content/feedback-one");
        Assert.Contains("No feedback has been submitted.", feedback);
        Assert.DoesNotContain("CMS feedback question must not appear.", feedback);
        Assert.DoesNotContain("name=\"SelectedAnswerIds\"", feedback);
        Assert.DoesNotContain(module + "/questions/feedback-one", feedback);
        var continuation = Regex.Match(feedback,
            "<a[^>]*href=\"([^\"]+)\"[^>]*>Continue without submitting feedback</a>");
        Assert.True(continuation.Success);
        var destination = WebUtility.HtmlDecode(continuation.Groups[1].Value);
        Assert.Equal(module + "/content/thank-you", destination);
        using (var rejected = await SubmitFormAsync(client, "/account",
            module + "/questions/feedback-one", ("SelectedAnswerIds", "anything")))
        {
            Assert.Equal(HttpStatusCode.NotFound, rejected.StatusCode);
        }
        AssertHttpRedirect(await client.GetAsync(destination), module);
        var overview = await client.GetStringAsync(module);
        Assert.Contains("Pass the end of module test before completing this module.", overview);
        AssertHttpRedirect(await client.GetAsync(module + "/certificate.pdf"), module);
        await using (var db = CreateContext())
        {
            var progress = await db.TrainingModuleProgress.Include(item => item.VisitedPages).SingleAsync();
            Assert.Null(progress.StartedAt);
            Assert.Null(progress.CompletedAt);
            Assert.Single(progress.VisitedPages, page => page.Name == "feedback-one");
            Assert.DoesNotContain(progress.VisitedPages, page => page.Name == "feedback-two");
            Assert.DoesNotContain(progress.VisitedPages, page => page.Name == "thank-you");
            Assert.Empty(await db.TrainingResponses.ToListAsync());
            Assert.Empty(await db.AssessmentAttempts.ToListAsync());
            Assert.Empty(await db.LearningNotes.ToListAsync());
        }
    }
}
