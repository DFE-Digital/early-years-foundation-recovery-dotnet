using System.Net;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.RegularExpressions;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Infrastructure.Registration;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class RegistrationReviewHttpTests
{
    [Fact]
    public async Task ReviewSurvivesInvalidPostAndEndsOnSummaryAcrossRequests()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        var token = await GetTokenAsync(client, "/registration/name?returnTo=check-your-answers");
        var invalid = await PostAsync(client, "/registration/name", token,
            ("FirstName", ""), ("LastName", "Example"));
        Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);

        var saved = await PostAsync(client, "/registration/name", token,
            ("FirstName", "Casey"), ("LastName", "Jones"));
        AssertRedirect(saved, "/registration/check-your-answers");
        var summary = await client.GetStringAsync("/registration/check-your-answers");
        Assert.Contains("Casey Jones", summary);

        token = await GetTokenAsync(client, "/registration/name");
        var normal = await PostAsync(client, "/registration/name", token,
            ("FirstName", "Alex"), ("LastName", "Example"));
        AssertRedirect(normal, "/registration/where-you-live");
    }

    [Fact]
    public async Task ReviewSettingChangeCollectsNewRoleAndExperienceBeforeSummary()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        var token = await GetTokenAsync(client, "/registration/setting-type?returnTo=check-your-answers");
        var setting = await PostAsync(client, "/registration/setting-type", token,
            ("SettingType", "childminder_independent"));
        AssertRedirect(setting, "/registration/role-type");

        token = await GetTokenAsync(client, "/registration/role-type");
        var role = await PostAsync(client, "/registration/role-type", token, ("RoleType", "Childminder"));
        AssertRedirect(role, "/registration/early-years-experience");

        token = await GetTokenAsync(client, "/registration/early-years-experience");
        var experience = await PostAsync(client, "/registration/early-years-experience", token,
            ("EarlyYearsExperience", "0-2"));
        AssertRedirect(experience, "/registration/check-your-answers");
        var summary = await client.GetStringAsync("/registration/check-your-answers");
        Assert.Contains("Childminder", summary);
        Assert.Contains("Less than 2 years", summary);
    }

    [Fact]
    public async Task MissingAntiForgeryTokenCannotSaveReviewAnswer()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        await GetTokenAsync(client, "/registration/name?returnTo=check-your-answers");
        var response = await client.PostAsync("/registration/name",
            new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["FirstName"] = "Should not save", ["LastName"] = "Example"
            }));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var summary = await client.GetStringAsync("/registration/check-your-answers");
        Assert.Contains("Alex Example", summary);
        Assert.DoesNotContain("Should not save", summary);
    }

    [Fact]
    public async Task ReviewCountryChangeCollectsNewAuthorityThenReturnsToSummary()
    {
        using var factory = CreateFactory();
        using var client = CreateClient(factory);
        var token = await GetTokenAsync(client, "/registration/setting-type?returnTo=check-your-answers");
        AssertRedirect(await PostAsync(client, "/registration/setting-type", token,
            ("SettingType", "local_authority")), "/registration/check-your-answers");
        await client.GetStringAsync("/registration/check-your-answers");

        token = await GetTokenAsync(client, "/registration/where-you-live?returnTo=check-your-answers");
        AssertRedirect(await PostAsync(client, "/registration/where-you-live", token,
            ("Country", "england")), "/registration/local-authority");
        token = await GetTokenAsync(client, "/registration/local-authority");
        AssertRedirect(await PostAsync(client, "/registration/local-authority", token,
            ("LocalAuthority", "Leeds")), "/registration/check-your-answers");
        var summary = await client.GetStringAsync("/registration/check-your-answers");
        Assert.Contains("Leeds", summary);
        Assert.DoesNotContain("Change role", summary);
    }

    private static WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureTestServices(services =>
            {
                var account = new UserAccount("review-http-user", "review@example.test");
                account.AcceptTerms(DateTimeOffset.UtcNow);
                account.UpdateName("Alex", "Example");
                account.UpdateLocation("Scotland", null);
                account.UpdateSetting("training_provider", null, false, "none");
                account.UpdatePreferences(false, true);
                services.RemoveAll<IUserAccountStore>();
                services.AddSingleton<IUserAccountStore>(new ReviewAccountStore(account));
                services.RemoveAll<IRegistrationOptionsProvider>();
                services.AddSingleton<IRegistrationOptionsProvider, SampleRegistrationOptionsProvider>();
                services.AddAuthentication(options =>
                {
                    options.DefaultAuthenticateScheme = "ReviewTest";
                    options.DefaultChallengeScheme = "ReviewTest";
                }).AddScheme<AuthenticationSchemeOptions, ReviewAuthenticationHandler>("ReviewTest", _ => { });
            });
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

    private static async Task<string> GetTokenAsync(HttpClient client, string path)
    {
        var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static Task<HttpResponseMessage> PostAsync(
        HttpClient client, string path, string token, params (string Key, string Value)[] fields)
    {
        var data = fields.ToDictionary(field => field.Key, field => field.Value);
        data["__RequestVerificationToken"] = token;
        return client.PostAsync(path, new FormUrlEncodedContent(data));
    }

    private static void AssertRedirect(HttpResponseMessage response, string path)
    {
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(path, response.Headers.Location?.OriginalString);
    }

    private sealed class ReviewAccountStore(UserAccount account) : IUserAccountStore
    {
        public Task<UserAccount?> FindByGovOneSubjectAsync(string subject, CancellationToken cancellationToken) =>
            Task.FromResult(subject == account.GovOneSubject ? account : null);

        public Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ReviewAuthenticationHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            var principal = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim("sub", "review-http-user")], Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }
}
