using System.Net;
using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed partial class PostgresPersistenceTests
{
    [PostgresFact]
    public async Task HttpSignOutRequiresProtectedPostAndClearsLocalSessionWithoutDeletingLearning()
    {
        await SeedEditableAccountAsync();
        await using (var db = CreateContext())
        {
            await Journey(db).SavePageNoteAsync(await db.UserAccounts.SingleAsync(),
                Module.Name, "first", "Keep after sign-out", CancellationToken.None);
        }
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        using (var get = await client.GetAsync("/account/sign-out"))
        {
            Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
            AssertNoSessionRenewal(get);
        }
        using (var missingToken = await client.PostAsync("/account/sign-out",
            new FormUrlEncodedContent(new Dictionary<string, string>())))
        {
            Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
            AssertNoSessionRenewal(missingToken);
        }
        Assert.Contains("Alex Example", await client.GetStringAsync("/account"));
        using (var signOut = await SubmitFormAsync(client, "/account", "/account/sign-out"))
        {
            Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode);
            Assert.Equal("/account/sign-in", signOut.Headers.Location?.OriginalString);
            Assert.Equal("__Host-EYFR.Session=", GetSessionCookie(signOut));
        }
        using (var protectedPage = await client.GetAsync("/account"))
        {
            Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
            Assert.StartsWith("/account/sign-in", protectedPage.Headers.Location?.PathAndQuery);
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        await AssertOriginalEditableAccountAsync();
        await using (var db = CreateContext())
        {
            Assert.Equal("Keep after sign-out", (await db.LearningNotes.SingleAsync()).Content);
        }
    }

    [PostgresFact]
    public async Task HttpProductionSignOutClearsCookieAndUsesProviderEndSession()
    {
        await SeedEditableAccountAsync();
        long accountId;
        await using (var db = CreateContext())
        {
            accountId = (await db.UserAccounts.SingleAsync()).Id;
        }
        using var factory = CreateHttpFactory().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Production");
            builder.UseSetting("ConnectionStrings:Application", _testConnection);
            builder.UseSetting("Contentful:Space", "test-space");
            builder.UseSetting("Contentful:DeliveryToken", "test-token");
            builder.UseSetting("Contentful:Preview", "false");
            builder.UseSetting("Authentication:GovOne:Authority", "https://identity.example.test");
            builder.UseSetting("Authentication:GovOne:ClientId", "integration-test-client");
            builder.UseSetting("Authentication:GovOne:PrivateKeyPath", "unused-test-key.pem");
            builder.ConfigureTestServices(services =>
                services.PostConfigure<OpenIdConnectOptions>(OpenIdConnectDefaults.AuthenticationScheme,
                    options => options.ConfigurationManager =
                        new StaticConfigurationManager<OpenIdConnectConfiguration>(
                            new OpenIdConnectConfiguration
                            {
                                Issuer = "https://identity.example.test",
                                AuthorizationEndpoint = "https://identity.example.test/authorize",
                                EndSessionEndpoint = "https://identity.example.test/logout"
                            })));
        });
        using var client = CreateRegistrationClient(factory);
        var options = factory.Services.GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CookieAuthenticationDefaults.AuthenticationScheme);
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", "local-development-user"),
            new Claim("eyfr_account_id", accountId.ToString(System.Globalization.CultureInfo.InvariantCulture))
        ], CookieAuthenticationDefaults.AuthenticationScheme);
        // Bootstrap an existing session without enabling Development sign-in or mocking authentication.
        var cookie = options.TicketDataFormat.Protect(new AuthenticationTicket(
            new ClaimsPrincipal(identity),
            new AuthenticationProperties
            {
                IssuedUtc = DateTimeOffset.UtcNow,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(1)
            }, CookieAuthenticationDefaults.AuthenticationScheme));
        client.DefaultRequestHeaders.Add("Cookie", "__Host-EYFR.Session=" + cookie);
        using (var missingToken = await client.PostAsync("/account/sign-out",
            new FormUrlEncodedContent(new Dictionary<string, string>())))
        {
            Assert.Equal(HttpStatusCode.BadRequest, missingToken.StatusCode);
            AssertNoSessionRenewal(missingToken);
        }
        using var signOut = await SubmitFormAsync(client, "/account", "/account/sign-out");
        Assert.Equal(HttpStatusCode.Redirect, signOut.StatusCode);
        Assert.Equal("__Host-EYFR.Session=", GetSessionCookie(signOut));
        var location = Assert.IsType<Uri>(signOut.Headers.Location);
        Assert.Equal("https://identity.example.test/logout", location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("https://localhost/signout-callback-oidc", query["post_logout_redirect_uri"].ToString());
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
        client.DefaultRequestHeaders.Remove("Cookie");
        using var callback = await client.GetAsync(QueryHelpers.AddQueryString(
            "/signout-callback-oidc", "state", query["state"].ToString()));
        Assert.Equal(HttpStatusCode.Redirect, callback.StatusCode);
        Assert.Equal("https://localhost/account/sign-in", callback.Headers.Location?.OriginalString);
        using var protectedPage = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        Assert.Equal("identity.example.test", protectedPage.Headers.Location?.Host);
        await AssertOriginalEditableAccountAsync();
    }

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
            startedAt = Assert.IsType<DateTimeOffset>((await db.TrainingModuleProgress.AsNoTracking().SingleAsync()).StartedAt);
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
