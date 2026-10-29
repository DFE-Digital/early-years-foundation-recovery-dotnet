using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed partial class PostgresPersistenceTests
{
    [PostgresFact]
    public async Task HttpNewHostRestoresSessionNotesAndInterruptedAssessmentFromPersistentStores()
    {
        await SeedEditableAccountAsync();
        var keyDirectory = Directory.CreateDirectory(
            Path.Combine(Path.GetTempPath(), $"eyfr-restart-keys-{Guid.NewGuid():N}"));
        const string module = "/training/integration-module";
        try
        {
            string cookie;
            long accountId;
            long attemptId;
            DateTimeOffset startedAt;
            using (var firstHost = CreateRestartFactory(keyDirectory))
            using (var client = CreateRegistrationClient(firstHost))
            {
                using (var signIn = await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"))
                {
                    Assert.Equal(System.Net.HttpStatusCode.Redirect, signIn.StatusCode);
                    cookie = GetSessionCookie(signIn);
                }
                await client.GetStringAsync(module + "/content/learning");
                AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/first", module + "/content/first/notes",
                    ("Note.Content", "Persisted across web hosts")), module + "/content/second");
                AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/test-one", module + "/questions/test-one",
                    ("SelectedAnswerIds", "a"), ("SelectedAnswerIds", "b")), module + "/content/test-two");
                await using var db = CreateContext();
                accountId = (await db.UserAccounts.SingleAsync()).Id;
                attemptId = (await db.AssessmentAttempts.SingleAsync()).Id;
                startedAt = Assert.IsType<DateTimeOffset>((await db.TrainingModuleProgress.SingleAsync()).StartedAt);
            }

            Assert.NotEmpty(keyDirectory.GetFiles("key-*.xml"));
            using (var secondHost = CreateRestartFactory(keyDirectory))
            using (var client = CreateRegistrationClient(secondHost))
            {
                client.DefaultRequestHeaders.Add("Cookie", cookie);
                Assert.Contains("Alex Example", await client.GetStringAsync("/account"));
                Assert.Contains("Persisted across web hosts", await client.GetStringAsync("/my-account/learning-log"));
                var question = await client.GetStringAsync(module + "/content/test-one");
                AssertSelectedAnswer(question, "a");
                AssertSelectedAnswer(question, "b");
                AssertHttpRedirect(await SubmitFormAsync(client, module + "/content/test-two", module + "/questions/test-two",
                    ("SelectedAnswerIds", "yes")), module + "/assessment-result");
                await using var db = CreateContext();
                Assert.Equal(accountId, (await db.UserAccounts.SingleAsync()).Id);
                Assert.Equal(startedAt, (await db.TrainingModuleProgress.SingleAsync()).StartedAt);
                Assert.Null((await db.TrainingModuleProgress.SingleAsync()).CompletedAt);
                var attempt = await db.AssessmentAttempts.Include(item => item.Answers).SingleAsync();
                Assert.Equal(attemptId, attempt.Id);
                Assert.NotNull(attempt.CompletedAt);
                Assert.True(attempt.Passed);
                Assert.Equal(2, attempt.Answers.Count);
                Assert.Equal("Persisted across web hosts", (await db.LearningNotes.SingleAsync()).Content);
            }
        }
        finally
        {
            keyDirectory.Delete(recursive: true);
        }
    }

    private WebApplicationFactory<Program> CreateRestartFactory(DirectoryInfo keyDirectory) =>
        CreateHttpFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddDataProtection()
                .SetApplicationName("EyfrHostRestartTests")
                .PersistKeysToFileSystem(keyDirectory)));
}
