using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed partial class PostgresPersistenceTests
{
    [PostgresFact]
    public async Task HttpCustomRegistrationValidatesDescriptionsAndRestoresDraftAfterSignIn()
    {
        await MigrateRegistrationDatabaseAsync();
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        await StartRegistrationAsync(client, "england");
        using (var invalid = await SubmitFormAsync(client, "/registration/setting-type", "/registration/setting-type",
            ("SettingType", "other"), ("SettingTypeOther", " ")))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("Describe your setting.", await invalid.Content.ReadAsStringAsync());
        }
        await using (var db = CreateContext())
        {
            Assert.Null((await db.UserAccounts.SingleAsync()).SettingType);
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/setting-type", "/registration/setting-type",
            ("SettingType", "other"), ("SettingTypeOther", "Community <club>")), "/registration/local-authority");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/local-authority", "/registration/local-authority",
            ("LocalAuthority", "Leeds")), "/registration/role-type");
        using (var invalid = await SubmitFormAsync(client, "/registration/role-type", "/registration/role-type",
            ("RoleType", "other"), ("RoleTypeOther", " ")))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("Describe your role.", await invalid.Content.ReadAsStringAsync());
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/role-type", "/registration/role-type",
            ("RoleType", "other"), ("RoleTypeOther", "  Volunteer <lead>  ")), "/registration/early-years-experience");
        AssertHttpRedirect(await SubmitFormAsync(client, "/account", "/account/sign-out"), "/account/sign-in");
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        var restoredRole = await client.GetStringAsync("/registration/role-type");
        Assert.Contains("Volunteer &lt;lead&gt;", restoredRole);
        Assert.Contains("Community &lt;club&gt;", await client.GetStringAsync("/registration/setting-type"));
        using (var incomplete = await SubmitFormAsync(client, "/registration/check-your-answers",
            "/registration/check-your-answers"))
        {
            Assert.Equal(HttpStatusCode.OK, incomplete.StatusCode);
            Assert.Contains("Complete the required registration steps", await incomplete.Content.ReadAsStringAsync());
        }
        AssertHttpRedirect(await client.GetAsync("/training"), "/registration/terms-and-conditions");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/early-years-experience",
            "/registration/early-years-experience", ("EarlyYearsExperience", "0-2")), "/registration/preferences");
        using (var invalid = await SubmitFormAsync(client, "/registration/preferences", "/registration/preferences",
            ("TrainingEmails", "false")))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        }
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.Null(account.TrainingEmails);
            Assert.Null(account.ResearchParticipant);
            Assert.False(account.IsRegistrationComplete);
            Assert.Equal("Volunteer <lead>", account.RoleTypeOther);
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/preferences", "/registration/preferences",
            ("TrainingEmails", "false"), ("ResearchParticipant", "false")), "/registration/check-your-answers");
        var summary = await client.GetStringAsync("/registration/check-your-answers");
        Assert.Contains("Community &lt;club&gt;", summary);
        Assert.Contains("Volunteer &lt;lead&gt;", summary);
        Assert.Contains("Less than 2 years", summary);
        Assert.DoesNotContain("Volunteer <lead>", summary);
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/check-your-answers",
            "/registration/check-your-answers"), "/training");
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.True(account.IsRegistrationComplete);
            Assert.Equal("other", account.SettingType);
            Assert.Equal("Community <club>", account.SettingTypeOther);
            Assert.Equal("Leeds", account.LocalAuthority);
            Assert.Equal("other", account.RoleType);
            Assert.Equal("Volunteer <lead>", account.RoleTypeOther);
            Assert.False(account.TrainingEmails);
            Assert.False(account.ResearchParticipant);
        }
        Assert.Contains("Integration module", await client.GetStringAsync("/training"));
    }

    [PostgresFact]
    public async Task HttpReviewToNonEnglandNoRoleSettingClearsStaleAnswersAndSkipsInapplicableSteps()
    {
        await MigrateRegistrationDatabaseAsync();
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        await StartRegistrationAsync(client, "england");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/setting-type", "/registration/setting-type",
            ("SettingType", "other"), ("SettingTypeOther", "Old setting")), "/registration/local-authority");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/local-authority", "/registration/local-authority",
            ("LocalAuthority", "Leeds")), "/registration/role-type");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/role-type", "/registration/role-type",
            ("RoleType", "other"), ("RoleTypeOther", "Old role")), "/registration/early-years-experience");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/early-years-experience",
            "/registration/early-years-experience", ("EarlyYearsExperience", "0-2")), "/registration/preferences");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/preferences", "/registration/preferences",
            ("TrainingEmails", "true"), ("ResearchParticipant", "false")), "/registration/check-your-answers");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/where-you-live?returnTo=check-your-answers",
            "/registration/where-you-live", ("Country", "scotland")), "/registration/check-your-answers");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/setting-type?returnTo=check-your-answers",
            "/registration/setting-type", ("SettingType", "training_provider"),
            ("SettingTypeOther", "Must not persist")), "/registration/check-your-answers");
        var summary = await client.GetStringAsync("/registration/check-your-answers");
        Assert.Contains("Scotland", summary);
        Assert.Contains("Training provider", summary);
        Assert.DoesNotContain("Change local authority", summary);
        Assert.DoesNotContain("Change role", summary);
        Assert.DoesNotContain("Change experience", summary);
        Assert.DoesNotContain("Old setting", summary);
        Assert.DoesNotContain("Old role", summary);
        Assert.DoesNotContain("Leeds", summary);
        AssertHttpRedirect(await client.GetAsync("/registration/local-authority"), "/registration/role-type");
        AssertHttpRedirect(await client.GetAsync("/registration/role-type"), "/registration/preferences");
        AssertHttpRedirect(await client.GetAsync("/registration/early-years-experience"), "/registration/preferences");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/preferences", "/registration/local-authority",
            ("LocalAuthority", "Leeds")), "/registration/role-type");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/preferences", "/registration/role-type",
            ("RoleType", "other"), ("RoleTypeOther", "Must not persist")), "/registration/preferences");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/preferences", "/registration/early-years-experience",
            ("EarlyYearsExperience", "0-2")), "/registration/preferences");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/check-your-answers",
            "/registration/check-your-answers"), "/training");
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.True(account.IsRegistrationComplete);
            Assert.Equal("Scotland", account.Country);
            Assert.Equal("training_provider", account.SettingType);
            Assert.Null(account.SettingTypeOther);
            Assert.Null(account.LocalAuthority);
            Assert.Equal("none", account.RoleGroup);
            Assert.Equal("N/A", account.RoleType);
            Assert.Null(account.RoleTypeOther);
            Assert.Null(account.EarlyYearsExperience);
            Assert.True(account.TrainingEmails);
            Assert.False(account.ResearchParticipant);
        }
        Assert.Contains("Integration module", await client.GetStringAsync("/training"));
    }

    [PostgresFact]
    public async Task HttpNonEnglandRegistrationReviewCollectsNewAuthorityWithoutRoleOrExperience()
    {
        await MigrateRegistrationDatabaseAsync();
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        await StartRegistrationAsync(client, "scotland");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/setting-type", "/registration/setting-type",
            ("SettingType", "local_authority")), "/registration/local-authority");
        AssertHttpRedirect(await client.GetAsync("/registration/local-authority"), "/registration/role-type");
        AssertHttpRedirect(await client.GetAsync("/registration/role-type"), "/registration/preferences");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/preferences", "/registration/preferences",
            ("TrainingEmails", "false"), ("ResearchParticipant", "true")), "/registration/check-your-answers");
        var summary = await client.GetStringAsync("/registration/check-your-answers");
        Assert.DoesNotContain("Change local authority", summary);
        Assert.DoesNotContain("Change role", summary);
        Assert.DoesNotContain("Change experience", summary);
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/where-you-live?returnTo=check-your-answers",
            "/registration/where-you-live", ("Country", "england")), "/registration/local-authority");
        using (var incomplete = await SubmitFormAsync(client, "/registration/check-your-answers",
            "/registration/check-your-answers"))
        {
            Assert.Equal(HttpStatusCode.OK, incomplete.StatusCode);
            Assert.Contains("Complete the required registration steps", await incomplete.Content.ReadAsStringAsync());
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/local-authority",
            "/registration/local-authority", ("LocalAuthority", "Leeds")), "/registration/role-type");
        AssertHttpRedirect(await client.GetAsync("/registration/role-type"), "/registration/preferences");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/preferences", "/registration/preferences",
            ("TrainingEmails", "false"), ("ResearchParticipant", "true")), "/registration/check-your-answers");
        summary = await client.GetStringAsync("/registration/check-your-answers");
        Assert.Contains("Leeds", summary);
        Assert.Contains("Change local authority", summary);
        Assert.DoesNotContain("Change role", summary);
        Assert.DoesNotContain("Change experience", summary);
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/check-your-answers",
            "/registration/check-your-answers"), "/training");
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.True(account.IsRegistrationComplete);
            Assert.Equal("England", account.Country);
            Assert.Equal("local_authority", account.SettingType);
            Assert.Equal("Leeds", account.LocalAuthority);
            Assert.Equal("N/A", account.RoleType);
            Assert.Null(account.EarlyYearsExperience);
            Assert.False(account.TrainingEmails);
            Assert.True(account.ResearchParticipant);
        }
    }

    private async Task MigrateRegistrationDatabaseAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
    }

    private static HttpClient CreateRegistrationClient(WebApplicationFactory<Program> factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false
        });

    private static async Task StartRegistrationAsync(HttpClient client, string country)
    {
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/terms-and-conditions",
            "/registration/terms-and-conditions", ("Accepted", "true")), "/registration/name");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/name", "/registration/name",
            ("FirstName", "Alex"), ("LastName", "Registration")), "/registration/where-you-live");
        AssertHttpRedirect(await SubmitFormAsync(client, "/registration/where-you-live", "/registration/where-you-live",
            ("Country", country)), "/registration/setting-type");
    }
}
