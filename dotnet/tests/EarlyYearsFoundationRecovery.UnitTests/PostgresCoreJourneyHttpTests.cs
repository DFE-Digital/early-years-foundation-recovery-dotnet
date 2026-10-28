using System.Net;
using System.Text.RegularExpressions;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Registration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed partial class PostgresPersistenceTests
{
    [PostgresFact]
    public async Task HttpRegistrationFailedRetryResumeAndCertificateUsePersistedData()
    {
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
        }
        using var factory = CreateHttpFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        const string module = "/training/integration-module";

        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(module)).StatusCode);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        AssertHttpRedirect(await client.GetAsync(module), "/registration/terms-and-conditions");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/terms-and-conditions",
            "/registration/terms-and-conditions", ("Accepted", "true")), "/registration/name");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/name", "/registration/name",
            ("FirstName", "Casey"), ("LastName", "Journey")), "/registration/where-you-live");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/where-you-live", "/registration/where-you-live",
            ("Country", "england")), "/registration/setting-type");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/setting-type", "/registration/setting-type",
            ("SettingType", "childminder_independent")), "/registration/local-authority");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/local-authority", "/registration/local-authority",
            ("LocalAuthority", "Leeds")), "/registration/role-type");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/role-type", "/registration/role-type",
            ("RoleType", "Childminder")), "/registration/early-years-experience");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/early-years-experience", "/registration/early-years-experience",
            ("EarlyYearsExperience", "0-2")), "/registration/preferences");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/preferences", "/registration/preferences",
            ("TrainingEmails", "false"), ("ResearchParticipant", "true")), "/registration/check-your-answers");
        var summary = await client.GetStringAsync("/registration/check-your-answers");
        Assert.Contains("Casey Journey", summary);
        Assert.Contains("Leeds", summary);
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/check-your-answers",
            "/registration/check-your-answers"), "/training");

        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.True(account.IsRegistrationComplete);
            Assert.Equal("Leeds", account.LocalAuthority);
            Assert.Equal("Childminder", account.RoleType);
            Assert.False(account.TrainingEmails);
            Assert.True(account.ResearchParticipant);
        }

        Assert.Equal(module + "/content/pre-intro",
            await GetModuleActionAsync(client, module, "Start module"));
        Assert.Contains("Available modules", await client.GetStringAsync("/training"));
        AssertHttpRedirect(await client.GetAsync(module + "/certificate.pdf"), module);
        AssertHttpRedirect(await client.GetAsync(module + "/content/thank-you"), module);
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/pre-one", module + "/questions/pre-one",
            ("SelectedAnswerIds", "2")), module + "/content/pre-two");
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/pre-two", module + "/questions/pre-two",
            ("SelectedAnswerIds", "1")), module + "/content/learning");
        var confidence = await client.GetStringAsync(module + "/content/pre-one");
        AssertSelectedAnswer(confidence, "2");
        Assert.DoesNotMatch("<input(?=[^>]*name=\"SelectedAnswerIds\")(?=[^>]*disabled)[^>]*>", confidence);
        Assert.DoesNotContain("Your answer has been saved and cannot be changed.", confidence);
        Assert.DoesNotContain("<h2>Correct answer</h2>", confidence);
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/pre-one", module + "/questions/pre-one",
            ("SelectedAnswerIds", "1")), module + "/content/pre-two");
        AssertSelectedAnswer(await client.GetStringAsync(module + "/content/pre-one"), "1");
        await using (var db = CreateContext())
        {
            var progress = await db.TrainingModuleProgress.SingleAsync();
            Assert.Null(progress.StartedAt);
            Assert.Null(progress.CompletedAt);
            var check = await db.ConfidenceCheckProgress.SingleAsync();
            Assert.Equal("pre", check.CheckType);
            Assert.NotNull(check.StartedAt);
            Assert.NotNull(check.CompletedAt);
            Assert.Null(check.SkippedAt);
            Assert.Empty(await db.AssessmentAttempts.ToListAsync());
        }
        await client.GetStringAsync(module + "/content/learning");
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/first", module + "/content/first/notes",
            ("Note.Content", "My HTTP reflection")), module + "/content/second");
        Assert.Contains("My HTTP reflection", await client.GetStringAsync("/my-account/learning-log"));
        var resumeLearning = await GetModuleActionAsync(client, module, "Continue module");
        Assert.Equal(module + "/content/first", resumeLearning);
        Assert.Contains("My HTTP reflection", await client.GetStringAsync(resumeLearning));
        Assert.Contains("Modules in progress", await client.GetStringAsync("/training"));

        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/post-one", module + "/questions/post-one",
            ("SelectedAnswerIds", "2")), module + "/content/practice");
        var postConfidence = await client.GetStringAsync(module + "/content/post-one");
        AssertSelectedAnswer(postConfidence, "2");
        Assert.DoesNotMatch("<input(?=[^>]*name=\"SelectedAnswerIds\")(?=[^>]*disabled)[^>]*>", postConfidence);
        Assert.DoesNotContain("Your answer has been saved and cannot be changed.", postConfidence);
        Assert.DoesNotContain("<h2>Correct answer</h2>", postConfidence);
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/post-one", module + "/questions/post-one",
            ("SelectedAnswerIds", "1")), module + "/content/practice");
        AssertSelectedAnswer(await client.GetStringAsync(module + "/content/post-one"), "1");
        AssertHttpRedirect(await client.GetAsync(module + "/certificate.pdf"), module);

        using (var invalid = await SubmitFormAsync(client, module + "/content/practice", module + "/questions/practice"))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("Select a valid answer before continuing.", await invalid.Content.ReadAsStringAsync());
        }
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/practice", module + "/questions/practice",
            ("SelectedAnswerIds", "no")), module + "/content/practice");
        var feedback = await client.GetStringAsync(module + "/content/practice");
        AssertSelectedAnswer(feedback, "no");
        Assert.Contains("Your answer has been saved and cannot be changed.", feedback);
        Assert.Contains("<h2>Correct answer</h2>", feedback);
        Assert.Contains("<h2>Explanation</h2>", feedback);
        Assert.Contains("Review how children learn before continuing.", feedback);
        Assert.Contains(module + "/content/test-one", feedback);
        Assert.Matches("<input(?=[^>]*value=\"no\")(?=[^>]*disabled)[^>]*>", feedback);
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/practice", module + "/questions/practice",
            ("SelectedAnswerIds", "yes")), module + "/content/practice");
        var refreshedFeedback = await client.GetStringAsync(module + "/content/practice");
        AssertSelectedAnswer(refreshedFeedback, "no");
        Assert.Contains("Review how children learn before continuing.", refreshedFeedback);
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            var response = await new TrainingProgressStore(db).GetLatestResponseAsync(
                account.Id, Module.Name, "practice", CancellationToken.None);
            Assert.NotNull(response);
            Assert.Equal(["no"], response.SelectedAnswerIds);
            Assert.False(response.Correct);
            Assert.Single(await db.TrainingResponses.Where(item => item.QuestionName == "practice").ToListAsync());
            foreach (var question in new[] { "pre-one", "post-one" })
            {
                var confidenceResponse = await new TrainingProgressStore(db).GetLatestResponseAsync(
                    account.Id, Module.Name, question, CancellationToken.None);
                Assert.NotNull(confidenceResponse);
                Assert.Equal(["1"], confidenceResponse.SelectedAnswerIds);
            }
            var checks = await db.ConfidenceCheckProgress.ToListAsync();
            Assert.Equal(2, checks.Count);
            Assert.All(checks, check =>
            {
                Assert.NotNull(check.StartedAt);
                Assert.NotNull(check.CompletedAt);
                Assert.Null(check.SkippedAt);
            });
            Assert.NotNull((await db.TrainingModuleProgress.SingleAsync()).StartedAt);
            Assert.Null((await db.TrainingModuleProgress.SingleAsync()).CompletedAt);
            Assert.Empty(await db.AssessmentAttempts.ToListAsync());
        }

        // The real anti-forgery filter must reject this before any answer is saved.
        using var rejected = await client.PostAsync(module + "/questions/test-one",
            new FormUrlEncodedContent([new("SelectedAnswerIds", "c")]));
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/test-one", module + "/questions/test-one",
            ("SelectedAnswerIds", "c")), module + "/content/test-two");
        AssertHttpRedirect(await SubmitFormAsync(client, "/account", "/account/sign-out"), "/account/sign-in");
        Assert.Equal(HttpStatusCode.Redirect, (await client.GetAsync(module)).StatusCode);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        var resumeTest = await GetModuleActionAsync(client, module, "Continue module");
        Assert.Equal(module + "/content/test-one", resumeTest);
        var resumed = await client.GetStringAsync(resumeTest);
        AssertSelectedAnswer(resumed, "c");
        AssertHttpRedirect(await client.GetAsync(module + "/assessment-result"), module);
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/test-two", module + "/questions/test-two",
            ("SelectedAnswerIds", "no")), module + "/assessment-result");
        var failed = await client.GetStringAsync(module + "/assessment-result");
        Assert.Contains("You did not pass", failed);
        Assert.Contains("Retake test", failed);
        var retake = await GetModuleActionAsync(client, module, "Retake test");
        Assert.Equal(module + "/content/test-one", retake);
        Assert.Contains("Retake end of module test", await client.GetStringAsync("/training"));
        Assert.Contains("End of module test", await client.GetStringAsync(retake));
        AssertHttpRedirect(await client.GetAsync(module + "/content/thank-you"), module);
        AssertHttpRedirect(await client.GetAsync(module + "/certificate.pdf"), module);

        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/test-one", module + "/questions/test-one",
            ("SelectedAnswerIds", "a"), ("SelectedAnswerIds", "b")), module + "/content/test-two");
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/test-two", module + "/questions/test-two",
            ("SelectedAnswerIds", "yes")), module + "/assessment-result");
        var passed = await client.GetStringAsync(module + "/assessment-result");
        Assert.Contains("You passed the assessment.", passed);
        Assert.Contains(module + "/content/feedback-one", passed);
        Assert.Contains("You passed the assessment.", await client.GetStringAsync(module + "/assessment-result"));
        var resumePassed = await GetModuleActionAsync(client, module, "Continue module");
        Assert.Equal(module + "/content/results", resumePassed);
        AssertHttpRedirect(await client.GetAsync(resumePassed), module + "/assessment-result");
        Assert.Contains("View previous test result", await client.GetStringAsync("/training"));
        AssertHttpRedirect(await client.GetAsync(module + "/certificate.pdf"), module);
        var deferredFeedback = await client.GetStringAsync(module + "/content/feedback-one");
        Assert.Contains("Feedback collection is not available in this version of the service.", deferredFeedback);
        Assert.Contains("No feedback has been submitted.", deferredFeedback);
        Assert.DoesNotContain("CMS feedback question must not appear.", deferredFeedback);
        var continuation = Regex.Match(deferredFeedback,
            "<a[^>]*href=\"([^\"]+)\"[^>]*>Continue without submitting feedback</a>");
        Assert.True(continuation.Success);
        var completionPath = WebUtility.HtmlDecode(continuation.Groups[1].Value);
        Assert.Equal(module + "/content/thank-you", completionPath);
        await using (var db = CreateContext())
        {
            Assert.Null((await db.TrainingModuleProgress.SingleAsync()).CompletedAt);
            Assert.Empty(await db.TrainingResponses.Where(response => response.ResponseType == "feedback").ToListAsync());
        }
        AssertHttpRedirect(await client.GetAsync(module + "/certificate.pdf"), module);
        Assert.Contains("Your module is complete.", await client.GetStringAsync(completionPath));
        var certificateAction = await GetModuleActionAsync(client, module, "View certificate");
        Assert.Equal(module + "/content/certificate", certificateAction);
        AssertHttpRedirect(await client.GetAsync(certificateAction), module + "/certificate");
        var completedModules = await client.GetStringAsync("/training");
        Assert.Contains("Completed modules", completedModules);
        Assert.Contains("You have completed all available modules.", completedModules);
        Assert.Contains(module + "/certificate", completedModules);
        Assert.Contains("Casey Journey", await client.GetStringAsync(module + "/certificate"));
        using var download = await client.GetAsync(module + "/certificate.pdf");
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment", download.Content.Headers.ContentDisposition?.DispositionType);
        Assert.Contains("no-store", download.Headers.CacheControl?.ToString());
        using var pdf = PdfDocument.Open(await download.Content.ReadAsByteArrayAsync());
        var pdfPage = Assert.Single(pdf.GetPages());
        var text = Regex.Replace(ContentOrderTextExtractor.GetText(pdfPage), @"\s+", " ");
        Assert.Contains("Casey Journey", text);
        Assert.Contains("Integration module", text);
        Assert.InRange(pdfPage.Width, 590, 600);
        Assert.InRange(pdfPage.Height, 835, 850);

        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            var store = new TrainingProgressStore(db);
            var attempts = await store.GetAssessmentsAsync(account.Id, Module.Name, CancellationToken.None);
            Assert.Equal(2, attempts.Count);
            Assert.Equal(0m, attempts[0].Score);
            Assert.False(attempts[0].Passed);
            Assert.Equal(100m, attempts[1].Score);
            Assert.True(attempts[1].Passed);
            Assert.NotNull((await store.FindAsync(account.Id, Module.Name, CancellationToken.None))!.CompletedAt);
            Assert.Equal("My HTTP reflection", (await db.LearningNotes.SingleAsync()).Content);
        }

        AssertHttpRedirect(await SubmitFormAsync(client, "/account/name", "/account/name",
            ("FirstName", "Casey"), ("LastName", "Updated")), "/account");
        Assert.Contains("Casey Updated", await client.GetStringAsync("/account"));
        Assert.Contains("Casey Updated", await client.GetStringAsync(module + "/certificate"));
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/close", "/account/close",
            ("Reason", "not_using")), "/account/close/confirm");
        // Visiting the cancellation destination must not close the account.
        await client.GetStringAsync("/account");
        await using (var db = CreateContext())
        {
            Assert.False((await db.UserAccounts.SingleAsync()).IsClosed);
            Assert.Single(await db.LearningNotes.ToListAsync());
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/close/confirm", "/account/close/confirm"),
            "/account/closed");
        var afterClosure = await client.GetAsync(module + "/certificate.pdf");
        Assert.Equal(HttpStatusCode.Redirect, afterClosure.StatusCode);
        Assert.StartsWith("/account/sign-in", afterClosure.Headers.Location?.PathAndQuery);
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.True(account.IsClosed);
            Assert.Equal("Redacted", account.FirstName);
            Assert.Equal("User", account.LastName);
            Assert.Equal($"redacted_user{account.Id}@example.com", account.Email);
            Assert.StartsWith("closed-", account.GovOneSubject);
            Assert.Empty(await db.LearningNotes.ToListAsync());
        }
    }

    [PostgresFact]
    public async Task HttpSkippingConfidenceStartsLearningWithoutAnswersAndAllowsReturningLater()
    {
        await using (var db = CreateContext())
        {
            await db.Database.MigrateAsync();
            db.UserAccounts.Add(RegisteredAccount("local-development-user"));
            await db.SaveChangesAsync();
        }
        using var factory = CreateHttpFactory();
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });
        const string module = "/training/integration-module";
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        var introduction = await client.GetStringAsync(module + "/content/pre-intro");
        var skip = Regex.Match(introduction, "<a[^>]*href=\"([^\"]+)\"[^>]*>Skip questions</a>");
        Assert.True(skip.Success);
        Assert.Equal(module + "/content/learning", WebUtility.HtmlDecode(skip.Groups[1].Value));
        await using (var db = CreateContext())
        {
            Assert.Null((await db.TrainingModuleProgress.SingleAsync()).StartedAt);
            Assert.Empty(await db.ConfidenceCheckProgress.ToListAsync());
        }
        var learning = await client.GetStringAsync(WebUtility.HtmlDecode(skip.Groups[1].Value));
        Assert.Contains("<h1>Learning</h1>", learning);
        Assert.DoesNotContain("Skip questions", learning);
        DateTimeOffset skippedAt;
        DateTimeOffset learningStartedAt;
        await using (var db = CreateContext())
        {
            var check = await db.ConfidenceCheckProgress.SingleAsync();
            Assert.Equal("pre", check.CheckType);
            skippedAt = Assert.IsType<DateTimeOffset>(check.SkippedAt);
            Assert.Null(check.StartedAt);
            Assert.Null(check.CompletedAt);
            learningStartedAt = Assert.IsType<DateTimeOffset>((await db.TrainingModuleProgress.SingleAsync()).StartedAt);
            Assert.Empty(await db.TrainingResponses.ToListAsync());
            Assert.Empty(await db.AssessmentAttempts.ToListAsync());
        }
        AssertHttpRedirect(await client.GetAsync(module + "/certificate.pdf"), module);
        await client.GetStringAsync(module + "/content/pre-intro");
        await client.GetStringAsync(module + "/content/learning");
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/pre-one", module + "/questions/pre-one",
            ("SelectedAnswerIds", "1")), module + "/content/pre-two");
        AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/pre-two", module + "/questions/pre-two",
            ("SelectedAnswerIds", "2")), module + "/content/learning");
        AssertSelectedAnswer(await client.GetStringAsync(module + "/content/pre-one"), "1");
        await using (var db = CreateContext())
        {
            var check = await db.ConfidenceCheckProgress.SingleAsync();
            Assert.Equal(skippedAt, check.SkippedAt);
            Assert.NotNull(check.StartedAt);
            Assert.NotNull(check.CompletedAt);
            var progress = await db.TrainingModuleProgress.SingleAsync();
            Assert.Equal(learningStartedAt, progress.StartedAt);
            Assert.Null(progress.CompletedAt);
            Assert.Equal(2, await db.TrainingResponses.CountAsync());
            Assert.Empty(await db.AssessmentAttempts.ToListAsync());
        }
    }

    private WebApplicationFactory<Program> CreateHttpFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Application"] = _testConnection,
                    ["Contentful:Space"] = "",
                    ["Contentful:DeliveryToken"] = "",
                    ["Contentful:PreviewToken"] = "",
                    ["Authentication:GovOne:Authority"] = ""
                }));
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(_testConnection));
                services.RemoveAll<ITrainingContentProvider>();
                services.AddSingleton<ITrainingContentProvider, TestContentProvider>();
                services.RemoveAll<IRegistrationOptionsProvider>();
                services.AddSingleton<IRegistrationOptionsProvider, SampleRegistrationOptionsProvider>();
            });
        });

    private static async Task<string> GetModuleActionAsync(HttpClient client, string module, string label)
    {
        var html = await client.GetStringAsync(module);
        var action = Regex.Match(html,
            $"<a(?=[^>]*class=\"button\")[^>]*href=\"([^\"]+)\"[^>]*>{Regex.Escape(label)}</a>");
        Assert.True(action.Success, $"Expected module action '{label}'.");
        return WebUtility.HtmlDecode(action.Groups[1].Value);
    }

    private static void AssertSelectedAnswer(string html, string answerId) =>
        Assert.Matches($"<input(?=[^>]*value=\"{Regex.Escape(answerId)}\")(?=[^>]*checked)[^>]*>", html);

    private static async Task<HttpResponseMessage> SubmitFormAsync(
        HttpClient client, string formPath, string postPath, params (string Key, string Value)[] fields)
    {
        using var form = await client.GetAsync(formPath);
        Assert.Equal(HttpStatusCode.OK, form.StatusCode);
        var html = await form.Content.ReadAsStringAsync();
        var token = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success, $"No anti-forgery token found at {formPath}.");
        var data = fields.Select(field => new KeyValuePair<string, string>(field.Key, field.Value)).ToList();
        data.Add(new("__RequestVerificationToken", WebUtility.HtmlDecode(token.Groups[1].Value)));
        return await client.PostAsync(postPath, new FormUrlEncodedContent(data));
    }

    private static void AssertHttpRedirect(HttpResponseMessage response, string path)
    {
        using (response)
        {
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
            Assert.Equal(path, response.Headers.Location?.OriginalString);
        }
    }
}
