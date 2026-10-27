using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using EarlyYearsFoundationRecovery.Infrastructure.Persistence;
using EarlyYearsFoundationRecovery.Infrastructure.Registration;
using EarlyYearsFoundationRecovery.Web.Authentication;
using EarlyYearsFoundationRecovery.Web.Content;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var connectionString = builder.Configuration.GetConnectionString("Application");
if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "ConnectionStrings:Application must be configured before starting the application.");
}

builder.Services.AddControllersWithViews();
builder.Services.AddScoped<TrainingContentRenderer>();
builder.Services.AddMemoryCache();
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<IUserAccountStore, UserAccountStore>();
builder.Services.AddScoped<ITrainingProgressStore, TrainingProgressStore>();
builder.Services.AddScoped<ILearningNoteStore, LearningNoteStore>();
builder.Services.AddScoped<AccountService>();
builder.Services.AddScoped<TrainingJourneyService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHealthChecks().AddCheck<ApplicationDatabaseHealthCheck>("database");

var hasContentfulSpace = !string.IsNullOrWhiteSpace(builder.Configuration["Contentful:Space"]);
var isContentfulPreview =
    string.Equals(builder.Configuration["Contentful:Preview"], "true", StringComparison.OrdinalIgnoreCase);
var contentfulTokenKey = isContentfulPreview ? "Contentful:PreviewToken" : "Contentful:DeliveryToken";
var hasContentfulToken = !string.IsNullOrWhiteSpace(builder.Configuration[contentfulTokenKey]);

if (hasContentfulSpace && hasContentfulToken)
{
    builder.Services.AddHttpClient<ContentfulDeliveryClient>(client =>
        client.Timeout = TimeSpan.FromSeconds(10));
    builder.Services.AddScoped<ITrainingContentProvider, ContentfulTrainingContentProvider>();
    builder.Services.AddScoped<IRegistrationOptionsProvider, ContentfulRegistrationOptionsProvider>();
    builder.Services.AddSingleton<RegistrationReferenceData>();
}
else if (hasContentfulSpace || hasContentfulToken)
{
    throw new InvalidOperationException(
        $"Contentful:Space and {contentfulTokenKey} must both be configured to enable Contentful.");
}
else if (builder.Environment.IsDevelopment())
{
    builder.Services.AddSingleton<ITrainingContentProvider, SampleTrainingContentProvider>();
    builder.Services.AddSingleton<RegistrationReferenceData>();
    builder.Services.AddSingleton<IRegistrationOptionsProvider, SampleRegistrationOptionsProvider>();
}
else
{
    throw new InvalidOperationException(
        "Contentful:Space and the appropriate Contentful token must be configured outside Development.");
}

var govOne = builder.Configuration.GetSection("Authentication:GovOne");
var hasGovOneConfiguration =
    !string.IsNullOrWhiteSpace(govOne["Authority"]) &&
    !string.IsNullOrWhiteSpace(govOne["ClientId"]) &&
    !string.IsNullOrWhiteSpace(govOne["PrivateKeyPath"]);

if (!hasGovOneConfiguration && !builder.Environment.IsDevelopment())
{
    throw new InvalidOperationException(
        "Gov.UK One Login Authority, ClientId, and PrivateKeyPath must be configured outside Development.");
}

builder.Services
    .AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = hasGovOneConfiguration
            ? OpenIdConnectDefaults.AuthenticationScheme
            : CookieAuthenticationDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.Cookie.Name = "__Host-EYFR.Session";
        options.Cookie.Path = "/";
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(1);
        options.LoginPath = "/account/sign-in";
        options.AccessDeniedPath = "/account/access-denied";
    });

if (hasGovOneConfiguration)
{
    builder.Services
        .AddAuthentication()
        .AddOpenIdConnect(OpenIdConnectDefaults.AuthenticationScheme, options =>
        {
            options.Authority = govOne["Authority"];
            options.ClientId = govOne["ClientId"];
            options.CallbackPath = govOne["CallbackPath"] ?? "/signin-gov-one";
            options.ResponseType = "code";
            options.UsePkce = true;
            options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
            options.SaveTokens = false;
            options.GetClaimsFromUserInfoEndpoint = true;
            options.MapInboundClaims = false;
            options.Scope.Clear();
            options.Scope.Add("openid");
            options.Scope.Add("email");
            options.TokenValidationParameters = new TokenValidationParameters
            {
                NameClaimType = "name",
                ValidateIssuer = true
            };

            options.Events = new OpenIdConnectEvents
            {
                OnAuthorizationCodeReceived = async context =>
                {
                    var configuration = await context.Options.ConfigurationManager!
                        .GetConfigurationAsync(context.HttpContext.RequestAborted);
                    var tokenRequest = context.TokenEndpointRequest
                        ?? throw new InvalidOperationException("The OIDC token request was not created.");
                    var assertion = GovOneClientAssertion.Create(
                        options.ClientId!,
                        configuration.TokenEndpoint,
                        govOne["PrivateKeyPath"]!);
                    tokenRequest.ClientAssertion = assertion;
                    tokenRequest.ClientAssertionType =
                        "urn:ietf:params:oauth:client-assertion-type:jwt-bearer";
                },
                OnTokenValidated = async context =>
                {
                    var subject = context.Principal?.FindFirst("sub")?.Value;
                    var email = context.Principal?.FindFirst("email")?.Value;
                    if (string.IsNullOrWhiteSpace(subject) || string.IsNullOrWhiteSpace(email))
                    {
                        throw new SecurityTokenValidationException(
                            "The Gov.UK One Login identity token must contain subject and email claims.");
                    }

                    var accountService = context.HttpContext.RequestServices.GetRequiredService<AccountService>();
                    await accountService.EnsureAccountAsync(
                        subject,
                        email,
                        context.HttpContext.RequestAborted);
                }
            };
        });
}
builder.Services.AddAuthorization();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health", new HealthCheckOptions
{
    AllowCachingResponses = false
});
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public partial class Program
{
}
