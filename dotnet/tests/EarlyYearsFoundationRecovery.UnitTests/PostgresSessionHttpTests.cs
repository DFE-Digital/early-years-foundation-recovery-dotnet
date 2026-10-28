using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed partial class PostgresPersistenceTests
{
    [PostgresFact]
    public async Task HttpExpiredSessionBlocksReadsAndWritesAndSignInRestoresSavedLearning()
    {
        await SeedEditableAccountAsync();
        long accountId;
        DateTimeOffset startedAt;
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            accountId = account.Id;
            var service = Journey(db);
            await service.RecordPageViewAsync(account, Module.Name, "learning", CancellationToken.None);
            await service.SavePageNoteAsync(account, Module.Name, "first", "Saved before expiry", CancellationToken.None);
            await service.SubmitAnswerAsync(account, Module.Name, "test-one", ["a", "b"], CancellationToken.None);
            startedAt = Assert.IsType<DateTimeOffset>((await db.TrainingModuleProgress.SingleAsync()).StartedAt);
        }
        var clock = new SessionTestClock();
        using var factory = CreateSessionFactory(clock);
        using var client = CreateRegistrationClient(factory);
        using (var signIn = await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"))
        {
            Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
            var cookie = GetSessionCookie(signIn);
            var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
                .Get(CookieAuthenticationDefaults.AuthenticationScheme);
            var ticket = options.TicketDataFormat.Unprotect(cookie[(cookie.IndexOf('=') + 1)..]);
            Assert.NotNull(ticket);
            Assert.Equal(TimeSpan.FromHours(1), ticket.Properties.ExpiresUtc - ticket.Properties.IssuedUtc);
        }
        var nameForm = await client.GetStringAsync("/account/name");
        var token = Regex.Match(nameForm, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(token.Success);

        clock.Advance(TimeSpan.FromMinutes(29));
        using (var active = await client.GetAsync("/account"))
        {
            Assert.Equal(HttpStatusCode.OK, active.StatusCode);
            AssertNoSessionRenewal(active);
        }
        clock.Advance(TimeSpan.FromMinutes(31) + TimeSpan.FromSeconds(1));
        foreach (var path in new[]
        {
            "/account", "/my-account/learning-log",
            "/training/integration-module/content/test-one",
            "/training/integration-module/certificate.pdf"
        })
        {
            using var expired = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Redirect, expired.StatusCode);
            Assert.StartsWith("/account/sign-in", expired.Headers.Location?.PathAndQuery);
            AssertNoSessionRenewal(expired);
        }
        using (var rejectedEdit = await client.PostAsync("/account/name", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                ["__RequestVerificationToken"] = WebUtility.HtmlDecode(token.Groups[1].Value),
                ["FirstName"] = "Should not save",
                ["LastName"] = "Expired session"
            })))
        {
            Assert.Equal(HttpStatusCode.Redirect, rejectedEdit.StatusCode);
            Assert.StartsWith("/account/sign-in", rejectedEdit.Headers.Location?.PathAndQuery);
            AssertNoSessionRenewal(rejectedEdit);
        }
        await AssertOriginalEditableAccountAsync();
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        Assert.Contains("Saved before expiry", await client.GetStringAsync("/my-account/learning-log"));
        AssertSelectedAnswer(await client.GetStringAsync("/training/integration-module/content/test-one"), "a");
        AssertSelectedAnswer(await client.GetStringAsync("/training/integration-module/content/test-one"), "b");
        await using (var db = CreateContext())
        {
            Assert.Equal(accountId, (await db.UserAccounts.SingleAsync()).Id);
            var progress = await db.TrainingModuleProgress.SingleAsync();
            Assert.Equal(startedAt, progress.StartedAt);
            Assert.Null(progress.CompletedAt);
            Assert.Equal("Saved before expiry", (await db.LearningNotes.SingleAsync()).Content);
            var attempt = Assert.Single(await db.AssessmentAttempts.Include(attempt => attempt.Answers).ToListAsync());
            Assert.Null(attempt.CompletedAt);
            Assert.Single(attempt.Answers);
        }
    }

    [PostgresFact]
    public async Task HttpSlidingSessionRenewsAfterHalfLifeWithoutRevivingOriginalCookie()
    {
        await SeedEditableAccountAsync();
        var clock = new SessionTestClock();
        using var factory = CreateSessionFactory(clock);
        using var client = CreateRegistrationClient(factory);
        using var signIn = await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in");
        Assert.Equal(HttpStatusCode.Redirect, signIn.StatusCode);
        var originalCookie = GetSessionCookie(signIn);

        clock.Advance(TimeSpan.FromMinutes(31));
        using var renewal = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, renewal.StatusCode);
        var renewedCookie = GetSessionCookie(renewal);
        Assert.NotEqual(originalCookie, renewedCookie);

        clock.Advance(TimeSpan.FromMinutes(30));
        using var originalClient = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false,
            HandleCookies = false
        });
        originalClient.DefaultRequestHeaders.Add("Cookie", originalCookie);
        using var expired = await originalClient.GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, expired.StatusCode);
        Assert.StartsWith("/account/sign-in", expired.Headers.Location?.PathAndQuery);
        AssertNoSessionRenewal(expired);
        using var stillActive = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.OK, stillActive.StatusCode);
        Assert.Contains("Alex Example", await stillActive.Content.ReadAsStringAsync());
        await AssertOriginalEditableAccountAsync();
    }

    private WebApplicationFactory<Program> CreateSessionFactory(TimeProvider clock) =>
        CreateHttpFactory().WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.PostConfigure<CookieAuthenticationOptions>(
                CookieAuthenticationDefaults.AuthenticationScheme, options => options.TimeProvider = clock)));

    private static string GetSessionCookie(HttpResponseMessage response) =>
        Assert.Single(response.Headers.GetValues("Set-Cookie"),
            cookie => cookie.StartsWith("__Host-EYFR.Session=", StringComparison.Ordinal)).Split(';')[0];

    private static void AssertNoSessionRenewal(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            Assert.DoesNotContain(cookies,
                cookie => cookie.StartsWith("__Host-EYFR.Session=", StringComparison.Ordinal));
        }
    }

    private sealed class SessionTestClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now += duration;
    }
}
