using System.Net;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class NonDevelopmentAuthenticationHttpTests
{
    [Fact]
    public async Task ConfiguredDevelopmentOffersExplicitOneLoginAlongsideLocalSignIn()
    {
        using var factory = CreateFactory("Development");
        using var client = CreateClient(factory);
        var html = await client.GetStringAsync("/account/sign-in");
        Assert.Contains("href=\"/account/one-login-sign-in\"", html);
        Assert.Contains("Sign in with GOV.UK One Login", html);
        Assert.Contains("development-sign-in", html);
        using var challenge = await client.GetAsync("/account/one-login-sign-in");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var location = Assert.IsType<Uri>(challenge.Headers.Location);
        Assert.Equal("https://identity.example.test/authorize", location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("https://localhost/signin-gov-one", query["redirect_uri"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        AssertNoSessionCookie(challenge);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task SignInUsesOidcCodeFlowInsteadOfDevelopmentPage(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);

        using var response = await client.GetAsync("/account/sign-in");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var location = Assert.IsType<Uri>(response.Headers.Location);
        Assert.Equal("https://identity.example.test/authorize", location.GetLeftPart(UriPartial.Path));
        var query = QueryHelpers.ParseQuery(location.Query);
        Assert.Equal("integration-test-client", query["client_id"].ToString());
        Assert.Equal("code", query["response_type"].ToString());
        Assert.Equal("https://localhost/signin-gov-one", query["redirect_uri"].ToString());
        Assert.Equal("openid email", query["scope"].ToString());
        Assert.Equal("S256", query["code_challenge_method"].ToString());
        Assert.False(string.IsNullOrWhiteSpace(query["code_challenge"]));
        Assert.False(string.IsNullOrWhiteSpace(query["state"]));
        Assert.False(string.IsNullOrWhiteSpace(query["nonce"]));
        Assert.DoesNotContain("development-sign-in", await response.Content.ReadAsStringAsync());
        AssertNoSessionCookie(response);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task DevelopmentSignInRejectsValidAntiforgeryPostOutsideDevelopment(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);
        using var scope = factory.Services.CreateScope();
        var context = new DefaultHttpContext { RequestServices = scope.ServiceProvider };
        context.Request.Scheme = "https";
        context.Request.Host = new HostString("localhost");
        var tokens = scope.ServiceProvider.GetRequiredService<IAntiforgery>().GetAndStoreTokens(context);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/account/development-sign-in")
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                [tokens.FormFieldName] = Assert.IsType<string>(tokens.RequestToken)
            })
        };
        request.Headers.Add("Cookie",
            Assert.IsType<string>(Assert.Single(context.Response.Headers.SetCookie)).Split(';')[0]);

        using var response = await client.SendAsync(request);

        // A 400 would only prove antiforgery rejected the request, not the environment guard.
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        AssertNoSessionCookie(response);
        using var account = await client.GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, account.StatusCode);
        Assert.Equal("identity.example.test", account.Headers.Location?.Host);
    }

    [Theory]
    [InlineData("Production")]
    [InlineData("Staging")]
    public async Task DevelopmentSignInCannotBypassAntiforgeryOrUseGet(string environment)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);

        using var post = await client.PostAsync("/account/development-sign-in",
            new FormUrlEncodedContent(new Dictionary<string, string>()));
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        AssertNoSessionCookie(post);
        using var get = await client.GetAsync("/account/development-sign-in");
        Assert.Equal(HttpStatusCode.MethodNotAllowed, get.StatusCode);
        AssertNoSessionCookie(get);
    }

    [Theory]
    [InlineData("Production", "access_denied")]
    [InlineData("Production", "server_error")]
    [InlineData("Production", "invalid_state")]
    [InlineData("Production", "missing_correlation")]
    [InlineData("Staging", "access_denied")]
    [InlineData("Staging", "server_error")]
    [InlineData("Staging", "invalid_state")]
    [InlineData("Staging", "missing_correlation")]
    public async Task FailedCallbackOffersLocalRecoveryWithoutSigningIn(string environment, string failure)
    {
        using var factory = CreateFactory(environment);
        using var client = CreateClient(factory);
        using var challenge = await client.GetAsync("/account/sign-in");
        Assert.Equal(HttpStatusCode.Redirect, challenge.StatusCode);
        var state = QueryHelpers.ParseQuery(Assert.IsType<Uri>(challenge.Headers.Location).Query)["state"].ToString();
        using var freshClient = CreateClient(factory);
        var callbackClient = failure == "missing_correlation" ? freshClient : client;
        const string privateDescription = "provider-private-description";
        var callback = QueryHelpers.AddQueryString("/signin-gov-one", new Dictionary<string, string?>
        {
            ["state"] = failure == "invalid_state" ? "tampered-state" : state,
            ["error"] = failure == "access_denied" ? "access_denied" : "server_error",
            ["error_description"] = privateDescription
        });

        using var response = await callbackClient.GetAsync(callback);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/account/sign-in-problem", response.Headers.Location?.OriginalString);
        AssertNoSessionCookie(response);
        using var recovery = await callbackClient.GetAsync("/account/sign-in-problem?returnUrl=https://untrusted.example");
        Assert.Equal(HttpStatusCode.OK, recovery.StatusCode);
        Assert.True(recovery.Headers.CacheControl?.NoStore);
        var html = await recovery.Content.ReadAsStringAsync();
        Assert.Contains("<h1>You have not signed in</h1>", html);
        Assert.Contains("href=\"/account/sign-in\"", html);
        Assert.Contains("Return to the training homepage", html);
        Assert.DoesNotContain(privateDescription, html);
        Assert.DoesNotContain(state, html);
        Assert.DoesNotContain("untrusted.example", html);
        AssertNoSessionCookie(recovery);
        using var protectedPage = await callbackClient.GetAsync("/account");
        Assert.Equal(HttpStatusCode.Redirect, protectedPage.StatusCode);
        Assert.Equal("identity.example.test", protectedPage.Headers.Location?.Host);
        using var retry = await callbackClient.GetAsync("/account/sign-in");
        Assert.Equal(HttpStatusCode.Redirect, retry.StatusCode);
        var retryState = QueryHelpers.ParseQuery(Assert.IsType<Uri>(retry.Headers.Location).Query)["state"].ToString();
        Assert.False(string.IsNullOrWhiteSpace(retryState));
        Assert.NotEqual(state, retryState);
    }

    private static WebApplicationFactory<Program> CreateFactory(string environment) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            foreach (var setting in new Dictionary<string, string>
            {
                ["ConnectionStrings:Application"] =
                    "Host=localhost;Database=eyfr_unused_authentication_test;Username=test",
                ["Contentful:Space"] = "test-space",
                ["Contentful:Preview"] = "false",
                ["Contentful:DeliveryToken"] = "test-token",
                ["Authentication:GovOne:Authority"] = "https://identity.example.test",
                ["Authentication:GovOne:ClientId"] = "integration-test-client",
                ["Authentication:GovOne:PrivateKeyPath"] = "unused-test-key.pem",
                ["Authentication:GovOne:CallbackPath"] = "/signin-gov-one"
            })
            {
                builder.UseSetting(setting.Key, setting.Value);
            }
            builder.ConfigureTestServices(services =>
                services.PostConfigure<OpenIdConnectOptions>(
                    OpenIdConnectDefaults.AuthenticationScheme, options =>
                    {
                        options.ConfigurationManager =
                            new StaticConfigurationManager<OpenIdConnectConfiguration>(
                                new OpenIdConnectConfiguration
                                {
                                    Issuer = "https://identity.example.test",
                                    AuthorizationEndpoint = "https://identity.example.test/authorize",
                                    TokenEndpoint = "https://identity.example.test/token"
                                });
                    }));
        });

    private static HttpClient CreateClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            AllowAutoRedirect = false
        });

    private static void AssertNoSessionCookie(HttpResponseMessage response)
    {
        if (response.Headers.TryGetValues("Set-Cookie", out var cookies))
        {
            Assert.DoesNotContain(cookies,
                cookie => cookie.StartsWith("__Host-EYFR.Session=", StringComparison.Ordinal));
        }
    }
}
