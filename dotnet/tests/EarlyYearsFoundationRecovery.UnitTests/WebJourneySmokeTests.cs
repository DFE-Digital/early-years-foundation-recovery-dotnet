using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class WebJourneySmokeTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly HttpClient _client;

    public WebJourneySmokeTests(WebApplicationFactory<Program> factory)
    {
        _client = factory.WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Application"] =
                        "Host=localhost;Database=eyfr_web_test;Username=test;Password=test"
                }));
        }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    [Fact]
    public async Task HomePageRendersServiceName()
    {
        var response = await _client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Early years child development training", html);
        Assert.Contains("Sign in to start training", html);
    }

    [Fact]
    public async Task SignInPageOffersDevelopmentOnlySignIn()
    {
        var response = await _client.GetAsync("/account/sign-in");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("local test user", html);
        Assert.Contains("development-sign-in", html);
        Assert.DoesNotContain("href=\"/account/one-login-sign-in\"", html);
    }

    [Fact]
    public async Task UnconfiguredOneLoginRouteIsUnavailable()
    {
        using var response = await _client.GetAsync("/account/one-login-sign-in");
        Assert.Equal(System.Net.HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/training")]
    [InlineData("/training/module-one/certificate")]
    [InlineData("/training/module-one/certificate.pdf")]
    public async Task TrainingPagesRequireAuthentication(string path)
    {
        var response = await _client.GetAsync(path);

        Assert.Equal(System.Net.HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/account/sign-in", response.Headers.Location?.PathAndQuery);
    }
}
