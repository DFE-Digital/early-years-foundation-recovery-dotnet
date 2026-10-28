using System.Net;
using Microsoft.EntityFrameworkCore;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed partial class PostgresPersistenceTests
{
    [PostgresFact]
    public async Task HttpInvalidLocationHasLinkedErrorsAndRetainsHintWithoutSaving()
    {
        await SeedEditableAccountAsync();
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        using (var invalidCountry = await SubmitFormAsync(client, "/account/location", "/account/location",
            ("Country", "unknown"), ("LocalAuthority", "Leeds")))
        {
            Assert.Equal(HttpStatusCode.OK, invalidCountry.StatusCode);
            var html = await invalidCountry.Content.ReadAsStringAsync();
            Assert.Contains("<title>Error: Change where you live", html);
            Assert.Contains("tabindex=\"-1\" autofocus", html);
            Assert.Contains("href=\"#Country\"", html);
            Assert.Contains("Select a valid country.", html);
            Assert.Contains("aria-invalid=\"true\" aria-describedby=\"country-error\"", html);
        }
        using (var invalidAuthority = await SubmitFormAsync(client, "/account/location", "/account/location",
            ("Country", "england"), ("LocalAuthority", "unknown")))
        {
            Assert.Equal(HttpStatusCode.OK, invalidAuthority.StatusCode);
            var html = await invalidAuthority.Content.ReadAsStringAsync();
            Assert.Contains("href=\"#LocalAuthority\"", html);
            Assert.Contains("Select a valid local authority.", html);
            Assert.Contains("aria-invalid=\"true\" aria-describedby=\"authority-hint authority-error\"", html);
            Assert.Contains("id=\"authority-error\"", html);
            Assert.Contains("id=\"authority-hint\"", html);
        }
        await AssertOriginalEditableAccountAsync();
        var validForm = await client.GetStringAsync("/account/location");
        Assert.DoesNotContain("aria-invalid=\"true\"", validForm);
        Assert.DoesNotContain("<title>Error:", validForm);
        Assert.Contains("aria-describedby=\"authority-hint\"", validForm);
    }

    [PostgresFact]
    public async Task HttpMissingPreferenceHasLinkedErrorsWithoutChangingEitherPreference()
    {
        await SeedEditableAccountAsync();
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        foreach (var path in new[] { "/account/email-preferences", "/account/research-preferences" })
        {
            using var invalid = await SubmitFormAsync(client, path, path);
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            var html = await invalid.Content.ReadAsStringAsync();
            Assert.Contains("<title>Error:", html);
            Assert.Contains("There is a problem", html);
            Assert.Contains("tabindex=\"-1\" autofocus", html);
            Assert.Contains("href=\"#preference-yes\"", html);
            Assert.Contains("Select yes or no.", html);
            Assert.Contains("id=\"preference-error\"", html);
            Assert.Contains("aria-invalid=\"true\" aria-describedby=\"preference-error\"", html);
            await using var db = CreateContext();
            var account = await db.UserAccounts.SingleAsync();
            Assert.False(account.TrainingEmails);
            Assert.True(account.ResearchParticipant);
        }
    }

    [PostgresFact]
    public async Task HttpInvalidNameHasLinkedErrorsWithoutChangingSavedName()
    {
        await SeedEditableAccountAsync();
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        foreach (var path in new[] { "/account/name", "/registration/name" })
        {
            using var invalid = await SubmitFormAsync(client, path, path,
                ("FirstName", ""), ("LastName", ""));
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            var html = await invalid.Content.ReadAsStringAsync();
            Assert.Contains("<title>Error:", html);
            Assert.Contains("There is a problem", html);
            Assert.Contains("tabindex=\"-1\" autofocus", html);
            Assert.Contains("href=\"#FirstName\"", html);
            Assert.Contains("href=\"#LastName\"", html);
            Assert.Contains("The First name field is required.", html);
            Assert.Contains("The Last name field is required.", html);
            Assert.Contains("aria-invalid=\"true\" aria-describedby=\"first-name-error\"", html);
            Assert.Contains("aria-invalid=\"true\" aria-describedby=\"last-name-error\"", html);
        }
        await using var db = CreateContext();
        var account = await db.UserAccounts.SingleAsync();
        Assert.Equal("Alex", account.FirstName);
        Assert.Equal("Example", account.LastName);
    }

    [PostgresFact]
    public async Task HttpSettingPreviewCancelAndInvalidSavePreserveDetailsBeforeAtomicChange()
    {
        await SeedEditableAccountAsync();
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        using (var preview = await SubmitFormAsync(client, "/account/setting", "/account/setting",
            ("stage", "details"), ("SettingType", "childminder_independent")))
        {
            Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
            Assert.Contains("name=\"stage\" value=\"save\"", await preview.Content.ReadAsStringAsync());
        }
        await client.GetStringAsync("/account");
        await AssertOriginalEditableAccountAsync();
        using (var invalid = await SubmitFormAsync(client, "/account/setting", "/account/setting",
            ("stage", "save"), ("SettingType", "childminder_independent"),
            ("LocalAuthority", "Leeds"), ("RoleType", "other"), ("EarlyYearsExperience", "0-2")))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("Select a valid role.", await invalid.Content.ReadAsStringAsync());
        }
        await AssertOriginalEditableAccountAsync();
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/setting", "/account/setting",
            ("stage", "save"), ("SettingType", "childminder_independent"),
            ("LocalAuthority", "Leeds"), ("RoleType", "Childminder"), ("EarlyYearsExperience", "0-2")), "/account");
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.Equal("childminder_independent", account.SettingType);
            Assert.Null(account.SettingTypeOther);
            Assert.Equal("childminder", account.RoleGroup);
            Assert.Equal("Childminder", account.RoleType);
            Assert.Null(account.RoleTypeOther);
            Assert.Equal("0-2", account.EarlyYearsExperience);
            Assert.Equal("Leeds", account.LocalAuthority);
            Assert.True(account.IsRegistrationComplete);
            Assert.False(account.TrainingEmails);
            Assert.True(account.ResearchParticipant);
        }
        Assert.Contains("You have updated your details.", await client.GetStringAsync("/account"));
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/setting", "/account/setting",
            ("stage", "save"), ("SettingType", "training_provider"),
            ("LocalAuthority", "Leeds"), ("RoleType", "other"), ("RoleTypeOther", "Ignore this"),
            ("EarlyYearsExperience", "0-2")), "/account");
        AssertHttpRedirect(await client.GetAsync("/account/role"), "/account");
        AssertHttpRedirect(await SubmitFormAsync(client, "/account", "/account/role",
            ("RoleType", "other"), ("RoleTypeOther", "Ignore this"), ("EarlyYearsExperience", "0-2")), "/account");
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.Equal("training_provider", account.SettingType);
            Assert.Equal("none", account.RoleGroup);
            Assert.Equal("N/A", account.RoleType);
            Assert.Null(account.RoleTypeOther);
            Assert.Null(account.LocalAuthority);
            Assert.Null(account.EarlyYearsExperience);
            Assert.True(account.IsRegistrationComplete);
        }
    }

    [PostgresFact]
    public async Task HttpLocationRoleAndPreferenceEditsValidateAndPreserveUnrelatedAnswers()
    {
        await SeedEditableAccountAsync();
        using var factory = CreateHttpFactory();
        using var client = CreateRegistrationClient(factory);
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/sign-in", "/account/development-sign-in"), "/account");
        using (var invalid = await SubmitFormAsync(client, "/account/role", "/account/role",
            ("RoleType", "other"), ("RoleTypeOther", ""), ("EarlyYearsExperience", "0-2")))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("Describe your role.", await invalid.Content.ReadAsStringAsync());
        }
        await AssertOriginalEditableAccountAsync();
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/role", "/account/role",
            ("RoleType", "other"), ("RoleTypeOther", "  New <lead>  "), ("EarlyYearsExperience", "0-2")), "/account");
        Assert.Contains("New &lt;lead&gt;", await client.GetStringAsync("/account/role"));
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/location", "/account/location",
            ("Country", "scotland"), ("LocalAuthority", "Leeds")), "/account");
        using (var invalid = await SubmitFormAsync(client, "/account/location", "/account/location",
            ("Country", "england"), ("LocalAuthority", "")))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
            Assert.Contains("Select a valid local authority.", await invalid.Content.ReadAsStringAsync());
        }
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.Equal("Scotland", account.Country);
            Assert.Null(account.LocalAuthority);
            Assert.Equal("New <lead>", account.RoleTypeOther);
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/location", "/account/location",
            ("Country", "england"), ("LocalAuthority", "Leeds")), "/account");
        using (var invalid = await SubmitFormAsync(client, "/account/email-preferences", "/account/email-preferences"))
        {
            Assert.Equal(HttpStatusCode.OK, invalid.StatusCode);
        }
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.False(account.TrainingEmails);
            Assert.True(account.ResearchParticipant);
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/email-preferences", "/account/email-preferences",
            ("Selected", "true"), ("ResearchParticipant", "false")), "/account");
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.True(account.TrainingEmails);
            Assert.True(account.ResearchParticipant);
        }
        AssertHttpRedirect(await SubmitFormAsync(client, "/account/research-preferences", "/account/research-preferences",
            ("Selected", "false"), ("TrainingEmails", "false")), "/account");
        await using (var db = CreateContext())
        {
            var account = await db.UserAccounts.SingleAsync();
            Assert.True(account.TrainingEmails);
            Assert.False(account.ResearchParticipant);
            Assert.Equal("England", account.Country);
            Assert.Equal("Leeds", account.LocalAuthority);
            Assert.Equal("other", account.SettingType);
            Assert.Equal("Original setting", account.SettingTypeOther);
            Assert.Equal("New <lead>", account.RoleTypeOther);
            Assert.Equal("0-2", account.EarlyYearsExperience);
            Assert.Equal("Alex", account.FirstName);
            Assert.True(account.IsRegistrationComplete);
            Assert.Empty(await db.TrainingModuleProgress.ToListAsync());
        }
    }

    private async Task SeedEditableAccountAsync()
    {
        await using var db = CreateContext();
        await db.Database.MigrateAsync();
        var account = RegisteredAccount("local-development-user");
        account.UpdateLocation("England", null);
        account.UpdateSetting("other", "Original setting", true, "other");
        account.UpdateLocalAuthority("Leeds");
        account.UpdateRole("other", "Original role");
        account.UpdateExperience("0-2");
        account.UpdatePreferences(false, true);
        db.UserAccounts.Add(account);
        await db.SaveChangesAsync();
    }

    private async Task AssertOriginalEditableAccountAsync()
    {
        await using var db = CreateContext();
        var account = await db.UserAccounts.SingleAsync();
        Assert.Equal("England", account.Country);
        Assert.Equal("Leeds", account.LocalAuthority);
        Assert.Equal("other", account.SettingType);
        Assert.Equal("Original setting", account.SettingTypeOther);
        Assert.Equal("other", account.RoleType);
        Assert.Equal("Original role", account.RoleTypeOther);
        Assert.Equal("0-2", account.EarlyYearsExperience);
        Assert.False(account.TrainingEmails);
        Assert.True(account.ResearchParticipant);
    }
}
