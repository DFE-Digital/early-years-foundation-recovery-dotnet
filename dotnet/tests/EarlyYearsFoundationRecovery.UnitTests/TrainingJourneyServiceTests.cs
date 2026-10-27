using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Domain.Training;
using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Web.Controllers;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Security.Claims;
using EarlyYearsFoundationRecovery.Web.Content;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class TrainingJourneyServiceTests
{
    [Theory]
    [InlineData(false, "nursery", null, "new", "Nursery", "New to early years")]
    [InlineData(true, "nursery", null, "experienced", "Nursery", "Experienced")]
    [InlineData(false, "other", "<Custom setting>", "new", "<Custom setting>", "New to early years")]
    [InlineData(true, "legacy-setting", null, "legacy-experience", "legacy-setting", "legacy-experience")]
    public async Task RegistrationSummaryLoadsDisplayLabelsOnGetAndRejectedCompletion(
        bool rejectedCompletion, string setting, string? customSetting, string experience,
        string expectedSetting, string expectedExperience)
    {
        var (_, account) = AccountNameController(registered: false);
        account.UpdateSetting(setting, customSetting, true, "other");
        account.UpdateLocation("England", null);
        account.UpdateRole("Practitioner", null);
        account.UpdateExperience(experience);
        var controller = RegistrationControllerFor(account);

        var result = Assert.IsType<ViewResult>(rejectedCompletion
            ? await controller.CompleteRegistration(CancellationToken.None)
            : await controller.CheckAnswers(CancellationToken.None));

        Assert.Same(account, result.Model);
        Assert.Equal(expectedSetting, result.ViewData["SettingName"]);
        Assert.Equal(expectedExperience, result.ViewData["ExperienceName"]);
        Assert.False(account.IsRegistrationComplete);
        if (rejectedCompletion)
        {
            Assert.Equal("CheckAnswers", result.ViewName);
            Assert.False(controller.ModelState.IsValid);
        }
    }

    [Fact]
    public async Task RegistrationSummaryRequiresCurrentAccountForGetAndCompletion()
    {
        var (_, account) = AccountNameController(registered: false);
        var controller = RegistrationControllerFor(account);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.IsType<NotFoundResult>(await controller.CheckAnswers(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.CompleteRegistration(CancellationToken.None));
    }

    [Fact]
    public async Task RegistrationGeneralRolesIncludeCustomChoiceAndSaveTrimmedDescription()
    {
        var (_, account) = AccountNameController(registered: false);
        account.UpdateSetting("nursery", null, true, "other");
        var controller = RegistrationControllerFor(account);
        var form = Assert.IsType<ViewResult>(await controller.Role(CancellationToken.None));
        var roles = Assert.IsAssignableFrom<IReadOnlyList<RegistrationChoice>>(form.ViewData["Roles"]);
        Assert.Contains(roles, role => role.Name == "other");

        var result = await controller.Role(new RoleModel
        {
            RoleType = "other", RoleTypeOther = "  Custom educator  "
        }, CancellationToken.None);

        Assert.Equal("Experience", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("other", account.RoleType);
        Assert.Equal("Custom educator", account.RoleTypeOther);
    }

    [Theory]
    [InlineData("other", " ", "other")]
    [InlineData("unknown", null, "other")]
    [InlineData("other", "Forged role", "childminder")]
    [InlineData("Practitioner", null, "childminder")]
    public async Task InvalidRegistrationRolePreservesSavedRoleAndRepopulatesChoices(
        string role, string? other, string group)
    {
        var (_, account) = AccountNameController(registered: false);
        account.UpdateSetting("setting", null, false, group);
        account.UpdateRole(group == "other" ? "Practitioner" : "Childminder", null);
        var savedRole = account.RoleType;
        var controller = RegistrationControllerFor(account);

        var result = Assert.IsType<ViewResult>(await controller.Role(
            new RoleModel { RoleType = role, RoleTypeOther = other }, CancellationToken.None));

        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(Assert.IsAssignableFrom<IReadOnlyList<RegistrationChoice>>(result.ViewData["Roles"]));
        Assert.Equal(savedRole, account.RoleType);
        Assert.Null(account.RoleTypeOther);
    }

    [Fact]
    public async Task RegistrationAuthorityRevisitPrefillsReferenceId()
    {
        var (_, account) = AccountNameController(registered: false);
        account.UpdateSetting("nursery", null, true, "other");
        account.UpdateLocation("England", "Example council");
        var controller = RegistrationControllerFor(account);

        var result = Assert.IsType<ViewResult>(await controller.LocalAuthority(CancellationToken.None));

        Assert.Equal("example", Assert.IsType<LocalAuthorityModel>(result.Model).LocalAuthority);
    }

    [Theory]
    [InlineData("Scotland", true)]
    [InlineData("England", false)]
    public async Task RegistrationAuthorityPostSkipsInapplicableStepWithoutMutating(string country, bool required)
    {
        var (_, account) = AccountNameController(registered: false);
        account.UpdateSetting("training", null, required, "none");
        account.UpdateLocation(country, null);
        var controller = RegistrationControllerFor(account);

        var result = await controller.LocalAuthority(
            new LocalAuthorityModel { LocalAuthority = "example" }, CancellationToken.None);

        Assert.Equal("Role", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Null(account.LocalAuthority);
    }

    [Fact]
    public async Task RegistrationAuthorityPostRequiresAccountBeforeValidatingSelection()
    {
        var (_, account) = AccountNameController(registered: false);
        var controller = RegistrationControllerFor(account);
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.IsType<NotFoundResult>(await controller.LocalAuthority(
            new LocalAuthorityModel { LocalAuthority = "unknown" }, CancellationToken.None));
    }

    [Fact]
    public async Task InvalidRegistrationAuthorityPreservesSavedAnswerAndChoices()
    {
        var (_, account) = AccountNameController(registered: false);
        account.UpdateSetting("nursery", null, true, "other");
        account.UpdateLocation("England", "Example council");
        var controller = RegistrationControllerFor(account);
        var input = new LocalAuthorityModel { LocalAuthority = "unknown" };

        var result = Assert.IsType<ViewResult>(await controller.LocalAuthority(input, CancellationToken.None));

        Assert.Same(input, result.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(Assert.IsAssignableFrom<IReadOnlyList<RegistrationChoice>>(result.ViewData["LocalAuthorities"]));
        Assert.Equal("Example council", account.LocalAuthority);
    }

    [Fact]
    public async Task RegistrationStandardRoleClearsCustomTextAndNoRoleStepsCannotOverwriteIt()
    {
        var (_, account) = AccountNameController(registered: false);
        account.UpdateSetting("nursery", null, false, "other");
        account.UpdateRole("other", "Old custom role");
        var controller = RegistrationControllerFor(account);

        Assert.IsType<RedirectToActionResult>(await controller.Role(
            new RoleModel { RoleType = "Practitioner", RoleTypeOther = "Stale custom role" },
            CancellationToken.None));
        Assert.Equal("Practitioner", account.RoleType);
        Assert.Null(account.RoleTypeOther);
        await controller.Setting(new SettingModel { SettingType = "training" }, CancellationToken.None);
        Assert.Equal("Preferences", Assert.IsType<RedirectToActionResult>(await controller.Role(
            new RoleModel { RoleType = "other", RoleTypeOther = "Forged role" }, CancellationToken.None)).ActionName);
        Assert.Equal("Preferences", Assert.IsType<RedirectToActionResult>(await controller.Experience(
            new ExperienceModel { EarlyYearsExperience = "new" }, CancellationToken.None)).ActionName);
        Assert.Equal("N/A", account.RoleType);
        Assert.Null(account.RoleTypeOther);
        Assert.Null(account.EarlyYearsExperience);
    }

    [Fact]
    public async Task RegistrationChangedSettingClearsDependentAnswersAndCannotCompleteWithoutReplacements()
    {
        var (_, account) = AccountNameController(registered: false);
        account.UpdateSetting("nursery", null, true, "other");
        account.UpdateLocation("England", "Example council");
        account.UpdateRole("other", "Old educator");
        account.UpdateExperience("new");
        var controller = RegistrationControllerFor(account);

        await controller.Setting(new SettingModel { SettingType = "childminder" }, CancellationToken.None);

        Assert.Null(account.LocalAuthority);
        Assert.Null(account.RoleType);
        Assert.Null(account.RoleTypeOther);
        Assert.Null(account.EarlyYearsExperience);
        Assert.IsType<ViewResult>(await controller.CompleteRegistration(CancellationToken.None));
        Assert.False(account.IsRegistrationComplete);
    }

    [Theory]
    [InlineData("england", "nursery", "Practitioner", null)]
    [InlineData("scotland", "nursery", "other", "Custom educator")]
    [InlineData("england", "childminder", "Childminder", null)]
    [InlineData("england", "council", null, null)]
    [InlineData("scotland", "training", null, null)]
    [InlineData("england", "other", "other", "Custom educator")]
    public async Task RegistrationConditionalBranchesCanComplete(
        string country, string setting, string? role, string? roleOther)
    {
        var account = new UserAccount("subject-1", "person@example.test");
        var controller = RegistrationControllerFor(account);
        Assert.Equal("Name", Assert.IsType<RedirectToActionResult>(
            await controller.Terms(new TermsModel { Accepted = true }, CancellationToken.None)).ActionName);
        Assert.Equal("Location", Assert.IsType<RedirectToActionResult>(
            await controller.Name(new NameModel { FirstName = "Alex", LastName = "Example" }, CancellationToken.None)).ActionName);
        Assert.Equal("Setting", Assert.IsType<RedirectToActionResult>(
            await controller.Location(new LocationModel { Country = country }, CancellationToken.None)).ActionName);
        Assert.Equal("LocalAuthority", Assert.IsType<RedirectToActionResult>(
            await controller.Setting(new SettingModel
            {
                SettingType = setting, SettingTypeOther = setting == "other" ? "Custom setting" : null
            }, CancellationToken.None)).ActionName);

        if (account.SettingRequiresLocalAuthority && account.Country == "England")
        {
            Assert.IsType<ViewResult>(await controller.LocalAuthority(CancellationToken.None));
            Assert.Equal("Role", Assert.IsType<RedirectToActionResult>(
                await controller.LocalAuthority(new LocalAuthorityModel { LocalAuthority = "example" },
                    CancellationToken.None)).ActionName);
            Assert.Equal("Example council", account.LocalAuthority);
        }
        else
        {
            Assert.Equal("Role", Assert.IsType<RedirectToActionResult>(
                await controller.LocalAuthority(CancellationToken.None)).ActionName);
            Assert.Null(account.LocalAuthority);
        }
        if (account.RoleGroup == "none")
        {
            Assert.Equal("Preferences", Assert.IsType<RedirectToActionResult>(
                await controller.Role(CancellationToken.None)).ActionName);
            Assert.Equal("Preferences", Assert.IsType<RedirectToActionResult>(
                await controller.Experience(CancellationToken.None)).ActionName);
            Assert.Equal("N/A", account.RoleType);
            Assert.Null(account.EarlyYearsExperience);
        }
        else
        {
            Assert.Equal("Experience", Assert.IsType<RedirectToActionResult>(
                await controller.Role(new RoleModel { RoleType = role!, RoleTypeOther = roleOther },
                    CancellationToken.None)).ActionName);
            Assert.Equal("Preferences", Assert.IsType<RedirectToActionResult>(
                await controller.Experience(new ExperienceModel { EarlyYearsExperience = "new" },
                    CancellationToken.None)).ActionName);
            Assert.Equal(role, account.RoleType);
            Assert.Equal(roleOther, account.RoleTypeOther);
        }
        Assert.Equal("CheckAnswers", Assert.IsType<RedirectToActionResult>(
            await controller.Preferences(new PreferencesModel { TrainingEmails = false, ResearchParticipant = true },
                CancellationToken.None)).ActionName);
        Assert.IsType<ViewResult>(await controller.CheckAnswers(CancellationToken.None));
        Assert.False(account.IsRegistrationComplete);
        Assert.Equal("Training", Assert.IsType<RedirectToActionResult>(
            await controller.CompleteRegistration(CancellationToken.None)).ControllerName);
        Assert.True(account.IsRegistrationComplete);
    }

    private static RegistrationController RegistrationControllerFor(UserAccount account) =>
        new(new AccountService(new InMemoryAccountStore(account), new InMemoryNoteStore(), TimeProvider.System),
            new AccountTestOptions())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject-1")], "test"))
                }
            }
        };

    [Fact]
    public async Task AccountSettingPrefillsAndChoosingANewSettingDoesNotSave()
    {
        var (controller, account) = AccountNameController();
        var before = Assert.IsType<ViewResult>(await controller.Setting(CancellationToken.None));
        var initial = Assert.IsType<AccountSettingModel>(before.Model);
        Assert.Equal("other", initial.SettingType);
        Assert.Equal("Example setting", initial.SettingTypeOther);
        Assert.False(initial.ShowDetails);

        var result = Assert.IsType<ViewResult>(await controller.Setting(
            new AccountSettingModel { SettingType = "nursery" }, "details", CancellationToken.None));
        var details = Assert.IsType<AccountSettingModel>(result.Model);

        Assert.True(details.ShowDetails);
        Assert.Equal("Nursery", details.SelectedSetting?.Name);
        Assert.Contains(details.Roles, role => role.Name == "Practitioner");
        Assert.Contains(details.Roles, role => role.Name == "other");
        Assert.Equal("other", account.SettingType);
        Assert.Equal("Example setting", account.SettingTypeOther);
        Assert.False(controller.TempData.ContainsKey("Status"));
    }

    [Fact]
    public async Task AccountSettingSavesDependentAnswersTogetherAndPreservesUnrelatedDetails()
    {
        var (controller, account) = AccountNameController();
        account.UpdateLocation("England", null);
        var completed = account.RegistrationCompletedAt;

        var result = await controller.Setting(new AccountSettingModel
        {
            SettingType = "nursery", LocalAuthority = "example",
            RoleType = "other", RoleTypeOther = "  Custom educator  ", EarlyYearsExperience = "experienced"
        }, "save", CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("nursery", account.SettingType);
        Assert.Null(account.SettingTypeOther);
        Assert.True(account.SettingRequiresLocalAuthority);
        Assert.Equal("other", account.RoleGroup);
        Assert.Equal("Example council", account.LocalAuthority);
        Assert.Equal("other", account.RoleType);
        Assert.Equal("Custom educator", account.RoleTypeOther);
        Assert.Equal("experienced", account.EarlyYearsExperience);
        Assert.Equal("England", account.Country);
        Assert.Equal("Alex", account.FirstName);
        Assert.False(account.TrainingEmails);
        Assert.False(account.ResearchParticipant);
        Assert.Equal(completed, account.RegistrationCompletedAt);
        Assert.Equal("You have updated your details.", controller.TempData["Status"]);
    }

    [Theory]
    [InlineData("unknown", "example", "Practitioner", "experienced", null)]
    [InlineData("nursery", null, "Practitioner", "experienced", null)]
    [InlineData("council", null, "Practitioner", "experienced", null)]
    [InlineData("nursery", "unknown", "Practitioner", "experienced", null)]
    [InlineData("nursery", "example", "Childminder", "experienced", null)]
    [InlineData("childminder", "example", "Practitioner", "experienced", null)]
    [InlineData("nursery", "example", "Practitioner", "unknown", null)]
    [InlineData("nursery", "example", "other", "experienced", " ")]
    [InlineData("other", "example", "Practitioner", "experienced", null)]
    public async Task InvalidAccountSettingLeavesAllSavedAnswersUntouched(
        string setting, string? authority, string role, string experience, string? other)
    {
        var (controller, account) = AccountNameController();
        account.UpdateLocation("England", null);
        var input = new AccountSettingModel
        {
            SettingType = setting, LocalAuthority = authority, RoleType = role,
            EarlyYearsExperience = experience, RoleTypeOther = other,
            RequiresLocalAuthority = false,
            Roles = [new("forged", role)]
        };

        var result = Assert.IsType<ViewResult>(await controller.Setting(input, "save", CancellationToken.None));

        Assert.Same(input, result.Model);
        Assert.False(controller.ModelState.IsValid);
        Assert.NotEmpty(input.Settings);
        Assert.Equal("other", account.SettingType);
        Assert.Equal("Example setting", account.SettingTypeOther);
        Assert.Equal("N/A", account.RoleType);
        Assert.Null(account.EarlyYearsExperience);
        Assert.Null(account.LocalAuthority);
        Assert.True(account.IsRegistrationComplete);
        Assert.False(controller.TempData.ContainsKey("Status"));
    }

    [Theory]
    [InlineData("England", "training")]
    [InlineData("Scotland", "nursery")]
    public async Task AccountSettingClearsAnswersThatDoNotApply(string country, string setting)
    {
        var (controller, account) = AccountNameController();
        account.UpdateSetting("other", "Old setting", true, "other");
        account.UpdateLocation(country, "Example council");
        account.UpdateRole("other", "Old role");
        account.UpdateExperience("new");

        var result = await controller.Setting(new AccountSettingModel
        {
            SettingType = setting, SettingTypeOther = "Forged extra",
            LocalAuthority = "unknown", RoleType = "Practitioner", RoleTypeOther = "Forged extra",
            EarlyYearsExperience = "experienced"
        }, "save", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(account.LocalAuthority);
        Assert.Null(account.SettingTypeOther);
        Assert.Null(account.RoleTypeOther);
        Assert.Equal(setting == "training" ? "N/A" : "Practitioner", account.RoleType);
        Assert.Equal(setting == "training" ? null : "experienced", account.EarlyYearsExperience);
    }

    [Fact]
    public async Task AccountSettingSupportsCustomSettingAndUnchangedSettingPrefill()
    {
        var (controller, account) = AccountNameController();
        var result = await controller.Setting(new AccountSettingModel
        {
            SettingType = "other", SettingTypeOther = "  Custom setting  ",
            RoleType = "Practitioner", EarlyYearsExperience = "new"
        }, "save", CancellationToken.None);
        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Custom setting", account.SettingTypeOther);

        var details = Assert.IsType<AccountSettingModel>(Assert.IsType<ViewResult>(
            await controller.Setting(new AccountSettingModel
            {
                SettingType = "other", SettingTypeOther = "Custom setting"
            }, "details", CancellationToken.None)).Model);
        Assert.Equal("Practitioner", details.RoleType);
        Assert.Equal("new", details.EarlyYearsExperience);
        Assert.False(details.RequiresLocalAuthority);
    }

    [Fact]
    public async Task AccountSettingRoutesRequireRegisteredCurrentAccountAndKnownStage()
    {
        var (controller, _) = AccountNameController();
        Assert.IsType<BadRequestResult>(await controller.Setting(
            new AccountSettingModel { SettingType = "training" }, "unknown", CancellationToken.None));
        var (unregistered, _) = AccountNameController(registered: false);
        foreach (var result in new[]
        {
            await unregistered.Setting(CancellationToken.None),
            await unregistered.Setting(new AccountSettingModel(), "save", CancellationToken.None)
        })
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Registration", redirect.ControllerName);
            Assert.Equal("Terms", redirect.ActionName);
        }
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.IsType<NotFoundResult>(await controller.Setting(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Setting(
            new AccountSettingModel(), "save", CancellationToken.None));
    }

    [Fact]
    public async Task AccountSettingFieldValidationKeepsDependentAnswersVisible()
    {
        var (controller, account) = AccountNameController();
        controller.ModelState.AddModelError(nameof(AccountSettingModel.RoleTypeOther), "Maximum length is 200.");
        var input = new AccountSettingModel
        {
            SettingType = "nursery", RoleType = "other", RoleTypeOther = new string('x', 201),
            EarlyYearsExperience = "new"
        };

        var result = Assert.IsType<ViewResult>(await controller.Setting(input, "save", CancellationToken.None));

        Assert.True(Assert.IsType<AccountSettingModel>(result.Model).ShowDetails);
        Assert.NotEmpty(input.Roles);
        Assert.Equal("other", account.SettingType);
        Assert.Equal("Example setting", account.SettingTypeOther);
    }

    [Fact]
    public async Task AccountSettingDetailsArePersistedWithOneStoreSave()
    {
        var account = new UserAccount("subject", "person@example.test");
        account.UpdateLocation("England", null);
        var store = new InMemoryAccountStore(account);
        var service = new AccountService(store, new InMemoryNoteStore(), TimeProvider.System);

        await service.UpdateSettingDetailsAsync(account, new("nursery", "Nursery", true, "other"),
            null, "Example council", "Practitioner", null, "new", CancellationToken.None);

        Assert.Equal(1, store.SaveCount);
        Assert.Equal("Example council", account.LocalAuthority);
        Assert.Equal("Practitioner", account.RoleType);
        Assert.Equal("new", account.EarlyYearsExperience);
    }

    [Fact]
    public async Task AccountSettingCanRequireAuthorityWithoutRoleOrExperience()
    {
        var (controller, account) = AccountNameController();
        account.UpdateLocation("England", null);

        var result = await controller.Setting(new AccountSettingModel
        {
            SettingType = "council", LocalAuthority = "example"
        }, "save", CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("council", account.SettingType);
        Assert.Equal("Example council", account.LocalAuthority);
        Assert.Equal("N/A", account.RoleType);
        Assert.Null(account.EarlyYearsExperience);
    }

    [Fact]
    public async Task AccountRolePrefillsAndSavesRoleAndExperienceWithoutChangingSetting()
    {
        var (controller, account) = AccountNameController();
        account.UpdateSetting("nursery", null, true, "other");
        account.UpdateLocation("England", "Example council");
        account.UpdateRole("Practitioner", null);
        account.UpdateExperience("new");
        var before = Assert.IsType<ViewResult>(await controller.Role(CancellationToken.None));
        var model = Assert.IsType<AccountRoleModel>(before.Model);
        Assert.Equal("Practitioner", model.RoleType);
        Assert.Equal("new", model.EarlyYearsExperience);
        Assert.NotEmpty(model.Roles);

        var result = await controller.Role(new AccountRoleModel
        {
            RoleType = "other", RoleTypeOther = "Custom educator", EarlyYearsExperience = "experienced"
        }, CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("other", account.RoleType);
        Assert.Equal("Custom educator", account.RoleTypeOther);
        Assert.Equal("experienced", account.EarlyYearsExperience);
        Assert.Equal("nursery", account.SettingType);
        Assert.Equal("Example council", account.LocalAuthority);
        Assert.Equal("You have updated your details.", controller.TempData["Status"]);
        Assert.True(account.IsRegistrationComplete);
    }

    [Theory]
    [InlineData("unknown", null, "experienced")]
    [InlineData("Practitioner", null, "unknown")]
    [InlineData("other", " ", "experienced")]
    [InlineData("Childminder", null, "experienced")]
    public async Task InvalidAccountRoleDoesNotChangeEitherSavedValue(string role, string? other, string experience)
    {
        var (controller, account) = AccountNameController();
        account.UpdateSetting("nursery", null, false, "other");
        account.UpdateRole("Practitioner", null);
        account.UpdateExperience("new");
        var input = new AccountRoleModel { RoleType = role, RoleTypeOther = other, EarlyYearsExperience = experience };

        var result = Assert.IsType<ViewResult>(await controller.Role(input, CancellationToken.None));

        Assert.Same(input, result.Model);
        Assert.NotEmpty(input.Roles);
        Assert.NotEmpty(input.Experiences);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal("Practitioner", account.RoleType);
        Assert.Equal("new", account.EarlyYearsExperience);
        Assert.False(controller.TempData.ContainsKey("Status"));
    }

    [Fact]
    public async Task StandardAccountRoleClearsOldCustomDescription()
    {
        var (controller, account) = AccountNameController();
        account.UpdateSetting("nursery", null, false, "other");
        account.UpdateRole("other", "Custom role");
        account.UpdateExperience("new");

        await controller.Role(new AccountRoleModel
        {
            RoleType = "Practitioner", RoleTypeOther = "Forged extra", EarlyYearsExperience = "experienced"
        }, CancellationToken.None);

        Assert.Equal("Practitioner", account.RoleType);
        Assert.Null(account.RoleTypeOther);
        Assert.Equal("experienced", account.EarlyYearsExperience);
    }

    [Fact]
    public async Task AccountRoleUsesCurrentSettingGroupAndDoesNotAcceptForgedChoices()
    {
        var (controller, account) = AccountNameController();
        account.UpdateSetting("childminder", null, false, "childminder");
        account.UpdateRole("Childminder", null);
        account.UpdateExperience("new");
        var initial = Assert.IsType<ViewResult>(await controller.Role(CancellationToken.None));
        Assert.Equal("Childminder", Assert.Single(Assert.IsType<AccountRoleModel>(initial.Model).Roles).Name);

        var invalid = await controller.Role(new AccountRoleModel
        {
            RoleType = "other", RoleTypeOther = "Bypass", EarlyYearsExperience = "new",
            Roles = [new("other", "other")]
        }, CancellationToken.None);

        Assert.IsType<ViewResult>(invalid);
        Assert.Equal("Childminder", account.RoleType);
        Assert.Null(account.RoleTypeOther);
    }

    [Fact]
    public async Task AccountRoleRoutesRejectInapplicableAndUnregisteredEdits()
    {
        var (controller, account) = AccountNameController();
        var input = new AccountRoleModel { RoleType = "Practitioner", EarlyYearsExperience = "new" };
        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(
            await controller.Role(CancellationToken.None)).ActionName);
        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(
            await controller.Role(input, CancellationToken.None)).ActionName);
        Assert.Equal("N/A", account.RoleType);
        var (unregistered, _) = AccountNameController(registered: false);
        foreach (var result in new[]
        {
            await unregistered.Role(CancellationToken.None),
            await unregistered.Role(input, CancellationToken.None)
        })
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Registration", redirect.ControllerName);
            Assert.Equal("Terms", redirect.ActionName);
        }
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.IsType<NotFoundResult>(await controller.Role(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Role(input, CancellationToken.None));
    }

    [Fact]
    public async Task AccountLocationPrefillsReferenceIdsAndSavesCountryAndAuthorityTogether()
    {
        var (controller, account) = AccountNameController();
        account.UpdateSetting("nursery", null, true, "none");
        account.UpdateLocation("England", "Example council");
        var before = Assert.IsType<ViewResult>(await controller.Location(CancellationToken.None));
        var model = Assert.IsType<AccountLocationModel>(before.Model);
        Assert.Equal("england", model.Country);
        Assert.Equal("example", model.LocalAuthority);
        Assert.True(model.SettingRequiresLocalAuthority);

        var result = await controller.Location(
            new AccountLocationModel { Country = "england", LocalAuthority = "other" }, CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal("England", account.Country);
        Assert.Equal("Other council", account.LocalAuthority);
        Assert.Equal("nursery", account.SettingType);
        Assert.Equal("You have updated your details.", controller.TempData["Status"]);
    }

    [Theory]
    [InlineData("england", null)]
    [InlineData("england", "unknown")]
    [InlineData("unknown", "example")]
    public async Task InvalidAccountLocationLeavesBothSavedValuesUntouched(string country, string? authority)
    {
        var (controller, account) = AccountNameController();
        account.UpdateSetting("nursery", null, true, "none");
        account.UpdateLocation("Scotland", null);
        var input = new AccountLocationModel { Country = country, LocalAuthority = authority };

        var result = Assert.IsType<ViewResult>(await controller.Location(input, CancellationToken.None));

        Assert.Same(input, result.Model);
        Assert.NotEmpty(input.Countries);
        Assert.NotEmpty(input.LocalAuthorities);
        Assert.True(input.SettingRequiresLocalAuthority);
        Assert.False(controller.ModelState.IsValid);
        Assert.Equal("Scotland", account.Country);
        Assert.Null(account.LocalAuthority);
    }

    [Theory]
    [InlineData(true, "scotland")]
    [InlineData(false, "england")]
    public async Task AccountLocationClearsAuthorityWhenNotApplicable(bool requiresAuthority, string country)
    {
        var (controller, account) = AccountNameController();
        account.UpdateSetting("nursery", null, true, "none");
        account.UpdateLocation("England", "Example council");
        if (!requiresAuthority)
        {
            account.UpdateSetting("training", null, false, "none");
        }

        var result = await controller.Location(new AccountLocationModel
        {
            Country = country, LocalAuthority = "forged", SettingRequiresLocalAuthority = !requiresAuthority
        }, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(result);
        Assert.Null(account.LocalAuthority);
        Assert.Equal(country == "england" ? "England" : "Scotland", account.Country);
    }

    [Fact]
    public async Task AccountLocationRequiresRegisteredCurrentAccount()
    {
        var (controller, _) = AccountNameController(registered: false);
        var input = new AccountLocationModel { Country = "scotland" };
        foreach (var result in new[]
        {
            await controller.Location(CancellationToken.None),
            await controller.Location(input, CancellationToken.None)
        })
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Terms", redirect.ActionName);
            Assert.Equal("Registration", redirect.ControllerName);
        }
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.IsType<NotFoundResult>(await controller.Location(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Location(input, CancellationToken.None));
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task AccountPreferencesSaveIndependentlyAndReturnToAccount(bool research, bool selected)
    {
        var (controller, account) = AccountNameController();
        account.UpdatePreferences(!selected, !selected);
        var before = Assert.IsType<ViewResult>(research
            ? await controller.ResearchPreferences(CancellationToken.None)
            : await controller.EmailPreferences(CancellationToken.None));
        Assert.Equal(!selected, Assert.IsType<AccountPreferenceModel>(before.Model).Selected);
        var model = new AccountPreferenceModel { Selected = selected };

        var result = research
            ? await controller.ResearchPreferences(model, CancellationToken.None)
            : await controller.EmailPreferences(model, CancellationToken.None);

        Assert.Equal("Index", Assert.IsType<RedirectToActionResult>(result).ActionName);
        Assert.Equal(research ? !selected : selected, account.TrainingEmails);
        Assert.Equal(research ? selected : !selected, account.ResearchParticipant);
        Assert.Equal("You have updated your details.", controller.TempData["Status"]);
        Assert.True(account.IsRegistrationComplete);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AccountPreferencesRequireExplicitChoiceAndPreserveSavedValues(bool research)
    {
        var (controller, account) = AccountNameController();
        account.UpdatePreferences(true, false);
        var model = new AccountPreferenceModel();

        var result = Assert.IsType<ViewResult>(research
            ? await controller.ResearchPreferences(model, CancellationToken.None)
            : await controller.EmailPreferences(model, CancellationToken.None));

        Assert.Same(model, result.Model);
        Assert.Equal(research, result.ViewData["IsResearchPreference"]);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(account.TrainingEmails);
        Assert.False(account.ResearchParticipant);
        Assert.False(controller.TempData.ContainsKey("Status"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AccountPreferenceRoutesRequireRegisteredCurrentAccount(bool research)
    {
        var (controller, _) = AccountNameController(registered: false);
        var get = research ? await controller.ResearchPreferences(CancellationToken.None)
            : await controller.EmailPreferences(CancellationToken.None);
        var post = research
            ? await controller.ResearchPreferences(new AccountPreferenceModel { Selected = true }, CancellationToken.None)
            : await controller.EmailPreferences(new AccountPreferenceModel { Selected = true }, CancellationToken.None);
        foreach (var result in new[] { get, post })
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Terms", redirect.ActionName);
            Assert.Equal("Registration", redirect.ControllerName);
        }
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());
        Assert.IsType<NotFoundResult>(research ? await controller.ResearchPreferences(CancellationToken.None)
            : await controller.EmailPreferences(CancellationToken.None));
        Assert.IsType<NotFoundResult>(research
            ? await controller.ResearchPreferences(new AccountPreferenceModel(), CancellationToken.None)
            : await controller.EmailPreferences(new AccountPreferenceModel(), CancellationToken.None));
    }

    [Fact]
    public async Task AccountNameEditPrefillsSavedNameAndReturnsToAccountAfterSaving()
    {
        var (controller, account) = AccountNameController();
        var before = Assert.IsType<ViewResult>(await controller.Name(CancellationToken.None));
        var model = Assert.IsType<NameModel>(before.Model);
        Assert.Equal("Alex", model.FirstName);
        Assert.Equal("Example", model.LastName);

        var saved = Assert.IsType<RedirectToActionResult>(await controller.Name(
            new NameModel { FirstName = "  Updated  ", LastName = "  Learner  " }, CancellationToken.None));

        Assert.Equal("Index", saved.ActionName);
        Assert.Equal("Updated", account.FirstName);
        Assert.Equal("Learner", account.LastName);
        Assert.True(account.IsRegistrationComplete);
        Assert.Equal("You have updated your details.", controller.TempData["Status"]);
        var revisited = Assert.IsType<ViewResult>(await controller.Name(CancellationToken.None));
        Assert.Equal("Updated", Assert.IsType<NameModel>(revisited.Model).FirstName);
    }

    [Fact]
    public async Task InvalidAccountNameEditPreservesInputWithoutChangingSavedName()
    {
        var (controller, account) = AccountNameController();
        controller.ModelState.AddModelError("FirstName", "The FirstName field is required.");
        var input = new NameModel { FirstName = "", LastName = "Changed" };

        var result = Assert.IsType<ViewResult>(await controller.Name(input, CancellationToken.None));

        Assert.Same(input, result.Model);
        Assert.Equal(true, result.ViewData["IsAccountEdit"]);
        Assert.Equal("Alex", account.FirstName);
        Assert.Equal("Example", account.LastName);
        Assert.False(controller.TempData.ContainsKey("Status"));
    }

    [Fact]
    public async Task AccountNameEditingRedirectsUnregisteredLearnersToRegistration()
    {
        var (controller, _) = AccountNameController(registered: false);
        foreach (var result in new[]
        {
            await controller.Name(CancellationToken.None),
            await controller.Name(new NameModel { FirstName = "New", LastName = "Name" }, CancellationToken.None)
        })
        {
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Registration", redirect.ControllerName);
            Assert.Equal("Terms", redirect.ActionName);
        }
    }

    [Fact]
    public async Task AccountNameEditingRequiresCurrentAccount()
    {
        var (controller, _) = AccountNameController();
        controller.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.IsType<NotFoundResult>(await controller.Name(CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Name(new NameModel(), CancellationToken.None));
    }

    private static (AccountController Controller, UserAccount Account) AccountNameController(bool registered = true)
    {
        var account = new UserAccount("subject-1", "person@example.test");
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateLocation("Scotland", null);
        account.UpdateSetting("other", "Example setting", false, "none");
        account.UpdatePreferences(false, false);
        if (registered)
        {
            Assert.True(account.CompleteRegistration(DateTimeOffset.UtcNow));
        }
        var controller = new AccountController(
            new AccountService(new InMemoryAccountStore(account), new InMemoryNoteStore(), TimeProvider.System),
            new TestWebHostEnvironment(), new AccountTestOptions())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject-1")], "test"))
                }
            }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new InMemoryTempDataProvider());
        return (controller, account);
    }

    private sealed class TestWebHostEnvironment : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Tests";
        public string EnvironmentName { get; set; } = "Development";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = "";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    }

    private sealed class AccountTestOptions : IRegistrationOptionsProvider
    {
        public IReadOnlyList<RegistrationChoice> GetCountries() =>
            [new("england", "England"), new("scotland", "Scotland")];
        public IReadOnlyList<RegistrationChoice> GetLocalAuthorities() =>
            [new("example", "Example council"), new("other", "Other council")];
        public IReadOnlyList<RegistrationChoice> GetExperiences() =>
            [new("new", "New to early years"), new("experienced", "Experienced")];
        public IReadOnlyList<RegistrationChoice> GetRoles(string roleGroup) => roleGroup == "other"
            ? [new("practitioner", "Practitioner")]
            : [new("childminder", "Childminder")];
        public Task<IReadOnlyList<RegistrationSetting>> GetSettingsAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RegistrationSetting>>(
            [
                new("nursery", "Nursery", true, "other"),
                new("childminder", "Childminder", false, "childminder"),
                new("council", "Local authority", true, "none"),
                new("training", "Training provider", false, "none")
            ]);
    }

    [Fact]
    public async Task DraftModulesAppearAsUpcomingWithoutExposingTrainingRoutes()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            IsDraft = true,
            Upcoming = "Coming later.",
            Pages = [.. module.Pages, new("certificate", "Certificate", "certificate", "")]
        };
        var controller = TestController(module);

        var result = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var listing = Assert.Single(Assert.IsType<TrainingModuleViewModel[]>(result.Model));

        Assert.Equal("upcoming", listing.State);
        Assert.Equal("Coming later.", listing.Module.Upcoming);
        Assert.Null(listing.LatestAssessment);
        Assert.IsType<NotFoundResult>(await controller.Module(module.Name, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Page(module.Name, "q1", CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Certificate(module.Name, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.AssessmentResult(module.Name, CancellationToken.None));
        Assert.IsType<NotFoundResult>(await controller.Answer(
            module.Name, "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DraftClassificationOverridesHistoricalLearningStateWithoutChangingSavedProgress(bool completed)
    {
        var module = ConfidenceModule("summative") with { IsDraft = true };
        var account = new UserAccount("subject-1", "person@example.test");
        var progress = new InMemoryProgressStore();
        var saved = new TrainingModuleProgress(account.Id, module.Name);
        var completedAt = DateTimeOffset.Parse("2026-01-02T10:30:00Z");
        saved.RecordStart(completedAt);
        if (completed)
        {
            saved.RecordCompletion(completedAt);
        }
        await progress.AddAsync(saved, CancellationToken.None);
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);

        var listing = Assert.Single(await service.GetModulesAsync(account, CancellationToken.None));

        Assert.Equal("upcoming", listing.State);
        Assert.Equal(completedAt, saved.StartedAt);
        Assert.Equal(completed ? completedAt : (DateTimeOffset?)null, saved.CompletedAt);
        Assert.Equal(saved.CompletedAt, listing.CompletedAt);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ModuleListReceivesLatestGradedAssessmentForInProgressModule(bool passed)
    {
        var module = ConfidenceModule("summative");
        module = module with { Pages = [new("learning", "Learning", "sub_module_intro", ""), .. module.Pages] };
        var controller = TestController(module);
        await controller.Page(module.Name, "learning", CancellationToken.None);
        await controller.Answer(module.Name, "q1",
            new TrainingAnswerModel { SelectedAnswerIds = [passed ? "1" : "2"] }, CancellationToken.None);

        var result = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var listing = Assert.Single(Assert.IsType<TrainingModuleViewModel[]>(result.Model));

        Assert.Equal("in-progress", listing.State);
        Assert.NotNull(listing.LatestAssessment);
        Assert.Equal(passed, listing.LatestAssessment.Passed);
    }

    [Fact]
    public async Task ModuleListingRetainsRecordedCompletionDateWithoutWritingProgress()
    {
        var module = ConfidenceModule("summative");
        var progress = new InMemoryProgressStore();
        var account = new UserAccount("subject-1", "person@example.test");
        var saved = new TrainingModuleProgress(account.Id, module.Name);
        var completedAt = DateTimeOffset.Parse("2026-01-02T10:30:00Z");
        saved.RecordCompletion(completedAt);
        await progress.AddAsync(saved, CancellationToken.None);
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);

        var listing = Assert.Single(await service.GetModulesAsync(account, CancellationToken.None));

        Assert.Equal("completed", listing.State);
        Assert.Equal(completedAt, listing.CompletedAt);
        Assert.Equal(completedAt, saved.CompletedAt);
        Assert.Equal(0, saved.VisitedPageCount);
    }

    [Fact]
    public async Task OverviewReceivesActualVisitedPageNamesNotOnlyTotalCount()
    {
        var controller = ConfidenceController("formative");
        await controller.Page("module-one", "q1", CancellationToken.None);
        await controller.Page("module-one", "q1", CancellationToken.None);

        var result = Assert.IsType<ViewResult>(await controller.Module("module-one", CancellationToken.None));
        var model = Assert.IsType<TrainingModuleDetailViewModel>(result.Model);

        Assert.Equal(["q1"], model.VisitedPageNames);
        Assert.Equal(1, model.VisitedPageCount);
    }

    [Fact]
    public async Task OverviewTopicAvailabilityUsesSavedVisitsWithoutRestrictingDirectPageAccess()
    {
        var module = ConfidenceModule("formative");
        module = module with
        {
            Pages = [
                new("section", "Learning section", "sub_module_intro", ""),
                new("topic", "Topic", "topic_intro", ""),
                module.Pages[0],
                module.Pages[1] with { PageType = "topic_intro" }
            ]
        };
        var controller = TestController(module);
        var initial = Assert.IsType<ViewResult>(await controller.Module(module.Name, CancellationToken.None));
        var initialTopic = Assert.IsType<TrainingModuleDetailViewModel>(initial.Model).Sections[0].Topics[0];
        Assert.Null(initialTopic.PageName);

        Assert.IsType<ViewResult>(await controller.Page(module.Name, "topic", CancellationToken.None));
        await controller.Page(module.Name, "q1", CancellationToken.None);
        var revisited = Assert.IsType<ViewResult>(await controller.Module(module.Name, CancellationToken.None));
        var sections = Assert.IsType<TrainingModuleDetailViewModel>(revisited.Model).Sections;
        Assert.Equal("topic", sections[0].Topics[0].PageName);
        Assert.Equal("Completed", sections[0].Topics[0].Status);
        Assert.Null(sections[0].Topics[1].PageName);
    }

    [Fact]
    public async Task OverviewDoesNotStartModuleOrLosePreLearningResumePage()
    {
        var controller = ConfidenceController("pre_confidence");
        await controller.Module("module-one", CancellationToken.None);
        var initial = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        Assert.Equal("available", Assert.Single(Assert.IsType<TrainingModuleViewModel[]>(initial.Model)).State);

        await controller.Page("module-one", "q1", CancellationToken.None);
        var overview = Assert.IsType<ViewResult>(await controller.Module("module-one", CancellationToken.None));
        Assert.Equal("q1", Assert.IsType<TrainingModuleDetailViewModel>(overview.Model).ResumePage?.Name);
        var listing = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        Assert.Equal("available", Assert.Single(Assert.IsType<TrainingModuleViewModel[]>(listing.Model)).State);
    }

    [Theory]
    [InlineData("text_page", false)]
    [InlineData("pre_confidence_intro", false)]
    [InlineData("pre_confidence", false)]
    [InlineData("topic_intro", false)]
    [InlineData("formative", false)]
    [InlineData("confidence", false)]
    [InlineData("sub_module_intro", true)]
    public async Task OnlyLearningIntroductionStartsModuleWhileEveryPageRetainsResume(string pageType, bool starts)
    {
        var module = ConfidenceModule(pageType);
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        await service.RecordPageViewAsync(account, module.Name, "q1", CancellationToken.None);

        var saved = Assert.Single(await progress.GetForUserAsync(account.Id, CancellationToken.None));
        Assert.Equal(starts, saved.StartedAt is not null);
        Assert.Equal("q1", saved.LastPageName);
        Assert.Equal(1, saved.VisitedPageCount);
        var listing = Assert.Single(await service.GetModulesAsync(account, CancellationToken.None));
        Assert.Equal(starts ? "in-progress" : "available", listing.State);
        Assert.Null(listing.CompletedAt);
    }

    [Fact]
    public async Task LearningStartAndLegacyStartRemainStableAcrossRevisits()
    {
        var module = ConfidenceModule("sub_module_intro");
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");
        var legacy = new TrainingModuleProgress(account.Id, module.Name);
        var originalStart = DateTimeOffset.Parse("2026-01-01T09:00:00Z");
        legacy.RecordStart(originalStart);
        await progress.AddAsync(legacy, CancellationToken.None);

        await service.RecordPageViewAsync(account, module.Name, "q1", CancellationToken.None);
        await service.RecordPageViewAsync(account, module.Name, "next", CancellationToken.None);
        await service.RecordPageViewAsync(account, module.Name, "q1", CancellationToken.None);

        Assert.Equal(originalStart, legacy.StartedAt);
        Assert.Equal(2, legacy.VisitedPageCount);
        Assert.Equal("q1", legacy.LastPageName);
        Assert.Equal("in-progress", Assert.Single(await service.GetModulesAsync(account, CancellationToken.None)).State);
    }

    [Fact]
    public async Task SubmittingPreConfidenceDoesNotStartModule()
    {
        var controller = ConfidenceController("pre_confidence");
        await controller.Answer("module-one", "q1",
            new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);

        var listing = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        Assert.Equal("available", Assert.Single(Assert.IsType<TrainingModuleViewModel[]>(listing.Model)).State);
    }

    [Fact]
    public async Task LearningIntroductionStartsModuleAndEarlierRevisitsPreserveStateAndResume()
    {
        var module = ConfidenceModule("pre_confidence");
        module = module with { Pages = [module.Pages[0], module.Pages[1] with { PageType = "sub_module_intro" }] };
        var controller = TestController(module);

        await controller.Page(module.Name, "q1", CancellationToken.None);
        await controller.Page(module.Name, "next", CancellationToken.None);
        await controller.Page(module.Name, "q1", CancellationToken.None);
        await controller.Module(module.Name, CancellationToken.None);

        var result = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var listing = Assert.Single(Assert.IsType<TrainingModuleViewModel[]>(result.Model));
        Assert.Equal("in-progress", listing.State);
        Assert.Equal(2, listing.VisitedPageCount);
        var overview = Assert.IsType<ViewResult>(await controller.Module(module.Name, CancellationToken.None));
        Assert.Equal("q1", Assert.IsType<TrainingModuleDetailViewModel>(overview.Model).ResumePage?.Name);
    }

    [Fact]
    public async Task SkippingPreConfidenceDoesNotSubmitAnswersOrPreventAnsweringLater()
    {
        var module = ConfidenceModule("pre_confidence");
        module = module with
        {
            Pages = [
                new TrainingPage("before-intro", "Before you start", "text_page", ""),
                module.Pages[0],
                module.Pages[1] with { PageType = "sub_module_intro" },
                new TrainingPage("certificate", "Certificate", "certificate", "")
            ]
        };
        var controller = TestController(module);
        var intro = Assert.IsType<ViewResult>(await controller.Page(module.Name, "before-intro", CancellationToken.None));
        var skipPage = Assert.IsType<TrainingPageViewModel>(intro.Model).SkipPreConfidencePage;
        Assert.Equal("next", skipPage?.Name);

        Assert.IsType<ViewResult>(await controller.Page(module.Name, skipPage!.Name, CancellationToken.None));
        var question = Assert.IsType<ViewResult>(await controller.Page(module.Name, "q1", CancellationToken.None));
        var model = Assert.IsType<TrainingPageViewModel>(question.Model);
        Assert.Empty(model.SelectedAnswerIds);
        Assert.False(model.IsReadOnly);

        Assert.IsType<RedirectToActionResult>(await controller.Answer(
            module.Name, "q1", new TrainingAnswerModel { SelectedAnswerIds = ["2"] }, CancellationToken.None));
        var saved = Assert.IsType<ViewResult>(await controller.Page(module.Name, "q1", CancellationToken.None));
        Assert.Equal(["2"], Assert.IsType<TrainingPageViewModel>(saved.Model).SelectedAnswerIds);
        Assert.IsType<RedirectToActionResult>(await controller.Certificate(module.Name, CancellationToken.None));
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("2", false)]
    public async Task LearningCheckSubmissionRedirectsToRefreshSafeSavedQuestion(string answer, bool correct)
    {
        var controller = ConfidenceController("formative");

        var submitted = Assert.IsType<RedirectToActionResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = [answer] }, CancellationToken.None));

        Assert.Equal(nameof(TrainingController.Page), submitted.ActionName);
        Assert.Equal("module-one", submitted.RouteValues!["moduleName"]);
        Assert.Equal("q1", submitted.RouteValues["pageName"]);
        for (var refresh = 0; refresh < 2; refresh++)
        {
            var result = Assert.IsType<ViewResult>(await controller.Page("module-one", "q1", CancellationToken.None));
            var model = Assert.IsType<TrainingPageViewModel>(result.Model);
            Assert.True(model.IsReadOnly);
            Assert.Equal([answer], model.SelectedAnswerIds);
            Assert.Equal(correct, model.IsFormativeAnswerCorrect);
            Assert.Equal("next", model.NextPage?.Name);
        }
    }

    [Fact]
    public async Task InvalidLearningCheckAnswerDoesNotRedirectOrSaveAnAnswer()
    {
        var controller = ConfidenceController("formative");
        var result = Assert.IsType<ViewResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel(), CancellationToken.None));

        Assert.Equal("Page", result.ViewName);
        Assert.False(controller.ModelState.IsValid);
        var refreshed = Assert.IsType<ViewResult>(await controller.Page("module-one", "q1", CancellationToken.None));
        var model = Assert.IsType<TrainingPageViewModel>(refreshed.Model);
        Assert.Empty(model.SelectedAnswerIds);
        Assert.False(model.IsReadOnly);
    }

    [Fact]
    public async Task CompletedModuleOverviewReturnsToCertificateEvenAfterRevisitingEarlierContent()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [
                new TrainingPage("intro", "Start test", "assessment_intro", ""),
                module.Pages[0],
                module.Pages[1] with { PageType = "thankyou" },
                new TrainingPage("certificate", "Certificate", "certificate", "")
            ]
        };
        var controller = TestController(module);
        await controller.Answer(module.Name, "q1",
            new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);
        await controller.Page(module.Name, "next", CancellationToken.None);
        await controller.Page(module.Name, "q1", CancellationToken.None);

        var result = Assert.IsType<ViewResult>(await controller.Module(module.Name, CancellationToken.None));
        var model = Assert.IsType<TrainingModuleDetailViewModel>(result.Model);

        Assert.Equal("certificate", model.ResumePage?.Name);
        Assert.Equal("View certificate", model.ResumeLabel);
    }

    [Fact]
    public async Task FailedModuleOverviewOffersAssessmentIntroInsteadOfLastVisitedContent()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [
                new TrainingPage("intro", "Start test", "assessment_intro", ""),
                module.Pages[0],
                module.Pages[1]
            ]
        };
        var controller = TestController(module);
        await controller.Answer(module.Name, "q1",
            new TrainingAnswerModel { SelectedAnswerIds = ["2"] }, CancellationToken.None);
        await controller.Page(module.Name, "next", CancellationToken.None);

        var result = Assert.IsType<ViewResult>(await controller.Module(module.Name, CancellationToken.None));
        var model = Assert.IsType<TrainingModuleDetailViewModel>(result.Model);

        Assert.Equal("intro", model.ResumePage?.Name);
        Assert.Equal("Retake test", model.ResumeLabel);
    }

    [Fact]
    public void FailedOverviewFallsBackToFirstQuestionWhenThereIsNoAssessmentIntro()
    {
        var module = ConfidenceModule("summative");
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("q1", ["2"], false);
        attempt.Grade(1, DateTimeOffset.UtcNow);
        var model = new TrainingModuleDetailViewModel(module, 2, "next", LatestAssessment: attempt);

        Assert.Equal("q1", model.ResumePage?.Name);
        Assert.Equal("Retake test", model.ResumeLabel);
        Assert.Equal("q1", new AssessmentSummaryViewModel(module, attempt).RetakePage?.Name);
    }

    [Fact]
    public void ActiveRetryResumesItsCurrentQuestionInsteadOfOfferingAnotherRetake()
    {
        var module = ConfidenceModule("summative");
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("q1", ["1"], true);
        var model = new TrainingModuleDetailViewModel(module, 1, "q1", LatestAssessment: attempt);

        Assert.Equal("q1", model.ResumePage?.Name);
        Assert.Equal("Continue module", model.ResumeLabel);
    }

    [Fact]
    public void PassingWithoutCompletingStillResumesTheLearningJourney()
    {
        var module = ConfidenceModule("summative");
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("q1", ["1"], true);
        attempt.Grade(1, DateTimeOffset.UtcNow);
        var model = new TrainingModuleDetailViewModel(module, 2, "next", LatestAssessment: attempt);

        Assert.Equal("next", model.ResumePage?.Name);
        Assert.Equal("Continue module", model.ResumeLabel);
    }

    [Fact]
    public async Task CertificateDownloadUsesTheAuthenticatedLearnerAndRecordedCompletion()
    {
        var module = ConfidenceModule("summative") with { Criteria = "- Learn about development" };
        module = module with
        {
            Pages = [
                module.Pages[0],
                module.Pages[1] with { PageType = "thankyou" },
                new TrainingPage("certificate", "Certificate", "certificate", "")
            ]
        };
        var controller = TestController(module);
        await controller.Answer(
            module.Name, "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);
        await controller.Page(module.Name, "next", CancellationToken.None);
        var preview = Assert.IsType<ViewResult>(await controller.Certificate(module.Name, CancellationToken.None));
        var model = Assert.IsType<CertificateViewModel>(preview.Model);
        Assert.Equal("Alex Example", model.Certificate.LearnerName);
        Assert.Equal(module.Criteria, model.Certificate.Criteria);
        Assert.Equal(module.Title, model.Certificate.ModuleTitle);
        var listingResult = Assert.IsType<ViewResult>(await controller.Index(CancellationToken.None));
        var listing = Assert.Single(Assert.IsType<TrainingModuleViewModel[]>(listingResult.Model));
        Assert.Equal("completed", listing.State);
        Assert.Equal(model.Certificate.CompletedAt, listing.CompletedAt);

        controller.Request.Path = "/training/module-one/certificate.pdf";
        var download = Assert.IsType<FileContentResult>(await controller.Certificate(module.Name, CancellationToken.None));
        Assert.Equal("application/pdf", download.ContentType);
        Assert.Equal("certificate-of-achievement.pdf", download.FileDownloadName);
        Assert.True(download.FileContents.AsSpan().StartsWith("%PDF-"u8));

        controller.Request.Path = "/training/module-one/certificate";
        var repeated = Assert.IsType<ViewResult>(await controller.Certificate(module.Name, CancellationToken.None));
        Assert.Equal(model.Certificate.CompletedAt,
            Assert.IsType<CertificateViewModel>(repeated.Model).Certificate.CompletedAt);
    }

    [Fact]
    public async Task DirectPdfDownloadCannotBypassCompletion()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [module.Pages[0], new TrainingPage("certificate", "Certificate", "certificate", "")]
        };
        var controller = TestController(module);
        controller.Request.Path = "/training/module-one/certificate.pdf";

        var result = Assert.IsType<RedirectToActionResult>(
            await controller.Certificate(module.Name, CancellationToken.None));
        Assert.Equal(nameof(TrainingController.Module), result.ActionName);
    }

    [Theory]
    [InlineData("1", true)]
    [InlineData("2", false)]
    public async Task CompletedAssessmentRedirectsToRefreshSafeResultPage(string selectedAnswer, bool passed)
    {
        var controller = ConfidenceController("summative");
        var submitted = Assert.IsType<RedirectToActionResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = [selectedAnswer] }, CancellationToken.None));

        Assert.Equal(nameof(TrainingController.AssessmentResult), submitted.ActionName);
        var first = Assert.IsType<ViewResult>(await controller.AssessmentResult("module-one", CancellationToken.None));
        var refreshed = Assert.IsType<ViewResult>(await controller.AssessmentResult("module-one", CancellationToken.None));
        var summary = Assert.IsType<AssessmentSummaryViewModel>(first.Model);
        var refreshedSummary = Assert.IsType<AssessmentSummaryViewModel>(refreshed.Model);
        Assert.Equal(passed, summary.Assessment.Passed);
        Assert.Same(summary.Assessment, refreshedSummary.Assessment);
        Assert.Equal("next", summary.NextPage?.Name);
        Assert.Equal("q1", summary.FirstQuestion?.Name);
    }

    [Fact]
    public async Task IncompleteAssessmentCannotDisplayAResult()
    {
        var controller = ConfidenceController("summative");

        var result = Assert.IsType<RedirectToActionResult>(await controller.AssessmentResult(
            "module-one", CancellationToken.None));

        Assert.Equal(nameof(TrainingController.Module), result.ActionName);
        Assert.IsType<string>(controller.TempData["TrainingNotice"]);
    }

    [Fact]
    public async Task ContentfulResultPageUsesTheSameAssessmentSummary()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [
                module.Pages[0],
                new TrainingPage("results", "Results", "assessment_results", "Results"),
                module.Pages[1]
            ]
        };
        var controller = TestController(module);
        await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);

        var page = Assert.IsType<RedirectToActionResult>(await controller.Page(
            "module-one", "results", CancellationToken.None));
        Assert.Equal(nameof(TrainingController.AssessmentResult), page.ActionName);
        var result = Assert.IsType<ViewResult>(await controller.AssessmentResult("module-one", CancellationToken.None));
        var summary = Assert.IsType<AssessmentSummaryViewModel>(result.Model);
        Assert.Equal("next", summary.NextPage?.Name);
    }

    [Theory]
    [InlineData("thankyou")]
    [InlineData("certificate")]
    public async Task CompletionAndCertificatesCannotBeReachedBeforePassing(string pageType)
    {
        var module = ConfidenceModule("summative");
        module = module with { Pages = [module.Pages[0], module.Pages[1] with { PageType = pageType }] };
        var controller = TestController(module);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Page(
            "module-one", "next", CancellationToken.None));

        if (pageType == "certificate")
        {
            Assert.Equal(nameof(TrainingController.Certificate), result.ActionName);
            result = Assert.IsType<RedirectToActionResult>(await controller.Certificate(
                "module-one", CancellationToken.None));
        }

        Assert.Equal(nameof(TrainingController.Module), result.ActionName);
        Assert.IsType<string>(controller.TempData["TrainingNotice"]);
        var overview = Assert.IsType<ViewResult>(await controller.Module("module-one", CancellationToken.None));
        Assert.Equal(0, Assert.IsType<TrainingModuleDetailViewModel>(overview.Model).VisitedPageCount);
    }

    [Fact]
    public async Task CertificateIsAvailableOnlyAfterPassingAndCompletingTheModule()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [
                module.Pages[0],
                module.Pages[1] with { PageType = "thankyou" },
                new TrainingPage("certificate", "Certificate", "certificate", "Certificate")
            ]
        };
        var controller = TestController(module);
        await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);

        Assert.IsType<RedirectToActionResult>(await controller.Page("module-one", "certificate", CancellationToken.None));
        Assert.IsType<ViewResult>(await controller.Page("module-one", "next", CancellationToken.None));
        Assert.IsType<ViewResult>(await controller.Certificate("module-one", CancellationToken.None));
    }

    [Theory]
    [InlineData("1", "2", true)]
    [InlineData("2", "1", false)]
    public async Task FormativeAnswersCannotBeChangedAfterSubmission(string first, string second, bool correct)
    {
        var module = ConfidenceModule("formative");
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), new InMemoryProgressStore(), new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        await service.SubmitAnswerAsync(account, module.Name, "q1", [first], CancellationToken.None);
        var secondResult = await service.SubmitAnswerAsync(account, module.Name, "q1", [second], CancellationToken.None);

        Assert.Equal(correct, secondResult.Correct);
        Assert.Equal([first], await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
    }

    [Fact]
    public async Task PassedAssessmentCannotBeRetakenOrChanged()
    {
        var module = ConfidenceModule("summative");
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        await service.SubmitAnswerAsync(account, module.Name, "q1", ["1"], CancellationToken.None);
        var resubmission = await service.SubmitAnswerAsync(account, module.Name, "q1", ["2"], CancellationToken.None);

        Assert.True(resubmission.Passed);
        Assert.Equal(100m, resubmission.Score);
        var attempt = Assert.Single(await progress.GetAssessmentsAsync(account.Id, module.Name, CancellationToken.None));
        Assert.Equal(["1"], Assert.Single(attempt.Answers).SelectedAnswerIds);
    }

    [Fact]
    public async Task SummativeQuestionsAdvanceWithoutShowingImmediateCorrectness()
    {
        var module = ConfidenceModule("summative");
        module = module with
        {
            Pages = [module.Pages[0], module.Pages[0] with { Name = "q2" }, module.Pages[1]]
        };
        var controller = TestController(module);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None));

        Assert.Equal(nameof(TrainingController.Page), result.ActionName);
        Assert.Equal("q2", result.RouteValues!["pageName"]);
        var view = Assert.IsType<ViewResult>(await controller.Page("module-one", "q1", CancellationToken.None));
        var model = Assert.IsType<TrainingPageViewModel>(view.Model);
        Assert.Equal(["1"], model.SelectedAnswerIds);
        Assert.False(model.IsReadOnly);
    }

    [Theory]
    [InlineData("summative", true)]
    [InlineData("formative", true)]
    [InlineData("pre_confidence", false)]
    [InlineData("confidence", false)]
    public async Task OnlyFormativeAndPassedSummativeAnswersAreReadOnly(string pageType, bool readOnly)
    {
        var controller = ConfidenceController(pageType);
        await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None);

        var view = Assert.IsType<ViewResult>(await controller.Page("module-one", "q1", CancellationToken.None));
        var model = Assert.IsType<TrainingPageViewModel>(view.Model);

        Assert.Equal(readOnly, model.IsReadOnly);
        Assert.Equal(["1"], model.SelectedAnswerIds);
    }

    [Fact]
    public async Task DirectQuestionRequestsRequireCompletedRegistration()
    {
        var controller = TestController(ConfidenceModule("confidence"), registered: false);

        var page = Assert.IsType<RedirectToActionResult>(await controller.Page(
            "module-one", "q1", CancellationToken.None));
        var answer = Assert.IsType<RedirectToActionResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None));

        Assert.Equal("Registration", page.ControllerName);
        Assert.Equal("Terms", page.ActionName);
        Assert.Equal("Registration", answer.ControllerName);
        Assert.Equal("Terms", answer.ActionName);
    }

    [Fact]
    public async Task AnsweringDraftModulesReturnsNotFound()
    {
        var controller = TestController(ConfidenceModule("confidence") with { IsDraft = true });

        Assert.IsType<NotFoundResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["1"] }, CancellationToken.None));
    }

    [Fact]
    public async Task ModuleOverviewResumesFromLastVisitedPage()
    {
        var controller = ConfidenceController("pre_confidence");

        await controller.Page("module-one", "next", CancellationToken.None);
        var result = Assert.IsType<ViewResult>(await controller.Module("module-one", CancellationToken.None));
        var model = Assert.IsType<TrainingModuleDetailViewModel>(result.Model);

        Assert.Equal("next", model.ResumePage?.Name);
        Assert.Equal(1, model.VisitedPageCount);

        await controller.Page("module-one", "q1", CancellationToken.None);
        result = Assert.IsType<ViewResult>(await controller.Module("module-one", CancellationToken.None));
        model = Assert.IsType<TrainingModuleDetailViewModel>(result.Model);
        Assert.Equal("q1", model.ResumePage?.Name);
        Assert.Equal(2, model.VisitedPageCount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("removed-page")]
    public void ResumeFallsBackToFirstPageWhenThereIsNoCurrentMilestone(string? lastPageName)
    {
        var model = new TrainingModuleDetailViewModel(ConfidenceModule("confidence"), 0, lastPageName);

        Assert.Equal("q1", model.ResumePage?.Name);
    }

    [Fact]
    public void EmptyModuleHasNoResumeTarget()
    {
        var module = new TrainingModule("empty", "Empty", 1, true, []);

        Assert.Null(new TrainingModuleDetailViewModel(module, 0).ResumePage);
    }

    [Theory]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    public async Task ConfidenceAnswersCanBeRestoredAndEditedWithoutAnAssessment(string pageType)
    {
        var module = ConfidenceModule(pageType);
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        Assert.Empty(await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
        await service.SubmitAnswerAsync(account, module.Name, "q1", ["1"], CancellationToken.None);
        Assert.Equal(["1"], await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
        await service.SubmitAnswerAsync(account, module.Name, "q1", ["2"], CancellationToken.None);
        Assert.Equal(["2"], await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
        Assert.Empty(await progress.GetAssessmentsAsync(account.Id, module.Name, CancellationToken.None));
        Assert.Empty(await service.GetSelectedAnswersAsync(account, "another-module", "q1", CancellationToken.None));
        Assert.Empty(await service.GetSelectedAnswersAsync(account, module.Name, "another-question", CancellationToken.None));
    }

    [Theory]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    [InlineData("formative")]
    [InlineData("summative")]
    public async Task SingleSelectQuestionsRejectMultipleAnswers(string pageType)
    {
        var module = ConfidenceModule(pageType);
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module), progress, new InMemoryNoteStore(), TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitAnswerAsync(account, module.Name, "q1", ["1", "2"], CancellationToken.None));

        Assert.Empty(await service.GetSelectedAnswersAsync(account, module.Name, "q1", CancellationToken.None));
        Assert.Empty(await progress.GetAssessmentsAsync(account.Id, module.Name, CancellationToken.None));
    }

    [Theory]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    public async Task ConfidenceSubmissionRedirectsToNextPageAndRestoresSelection(string pageType)
    {
        var controller = ConfidenceController(pageType);

        var result = Assert.IsType<RedirectToActionResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel { SelectedAnswerIds = ["2"] }, CancellationToken.None));

        Assert.Equal(nameof(TrainingController.Page), result.ActionName);
        Assert.Equal("next", result.RouteValues!["pageName"]);
        var pageResult = Assert.IsType<ViewResult>(await controller.Page("module-one", "q1", CancellationToken.None));
        var model = Assert.IsType<TrainingPageViewModel>(pageResult.Model);
        Assert.Equal("Confidence check", model.Title);
        Assert.Equal(["2"], model.SelectedAnswerIds);
    }

    [Theory]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    public async Task MissingConfidenceAnswerShowsValidationInsteadOfAdvancing(string pageType)
    {
        var controller = ConfidenceController(pageType);

        var result = Assert.IsType<ViewResult>(await controller.Answer(
            "module-one", "q1", new TrainingAnswerModel(), CancellationToken.None));

        Assert.Equal("Page", result.ViewName);
        Assert.False(controller.ModelState.IsValid);
        Assert.True(controller.ModelState.ContainsKey(nameof(TrainingAnswerModel.SelectedAnswerIds)));
    }

    private static TrainingModule ConfidenceModule(string pageType) =>
        new("module-one", "Module one", 1, false,
        [
            new TrainingPage("q1", "q1", pageType, "How confident do you feel?",
                [new("1", "Very confident"), new("2", "Not very confident")], ["1"], pageType),
            new TrainingPage("next", "Next page", "page", "Keep learning.")
        ]);

    private static TrainingController ConfidenceController(string pageType)
        => TestController(ConfidenceModule(pageType));

    private static TrainingController TestController(TrainingModule module, bool registered = true)
    {
        var account = new UserAccount("subject-1", "person@example.test");
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateLocation("Scotland", null);
        account.UpdateSetting("other", "Example setting", false, "none");
        account.UpdatePreferences(false, false);
        if (registered)
        {
            Assert.True(account.CompleteRegistration(DateTimeOffset.UtcNow));
        }
        var notes = new InMemoryNoteStore();
        var controller = new TrainingController(
            new AccountService(new InMemoryAccountStore(account), notes, TimeProvider.System),
            new TrainingJourneyService(
                new InMemoryContentProvider(module),
                new InMemoryProgressStore(), notes, TimeProvider.System),
            new StubCertificatePdfRenderer())
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "subject-1")], "test"))
                }
            }
        };
        controller.TempData = new TempDataDictionary(controller.HttpContext, new InMemoryTempDataProvider());
        return controller;
    }

    private sealed class StubCertificatePdfRenderer : ICertificatePdfRenderer
    {
        public Task<byte[]> GenerateAsync(CertificateData certificate, CancellationToken cancellationToken) =>
            Task.FromResult("%PDF-test"u8.ToArray());
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        public IDictionary<string, object> LoadTempData(HttpContext context) => new Dictionary<string, object>();

        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
        }
    }

    private sealed class InMemoryAccountStore(UserAccount account) : IUserAccountStore
    {
        public int SaveCount { get; private set; }

        public Task<UserAccount?> FindByGovOneSubjectAsync(string subject, CancellationToken cancellationToken) =>
            Task.FromResult<UserAccount?>(subject == account.GovOneSubject ? account : null);

        public Task AddAsync(UserAccount userAccount, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            SaveCount++;
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task SubmitAnswerGradesOnlyWhenAllSummativeQuestionsAreAnswered()
    {
        var module = new TrainingModule(
            "module-one",
            "Module one",
            1,
            false,
            [
                new TrainingPage("q1", "Question one", "summative", "Question one",
                    [new TrainingQuestionOption("correct", "Correct")], ["correct"], "summative"),
                new TrainingPage("q2", "Question two", "summative", "Question two",
                    [new TrainingQuestionOption("wrong", "Wrong")], ["wrong"], "summative")
            ]);
        var progress = new InMemoryProgressStore();
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module),
            progress,
            new InMemoryNoteStore(),
            TimeProvider.System);
        var account = new UserAccount("subject-1", "person@example.test");

        var first = await service.SubmitAnswerAsync(account, module.Name, "q1", ["correct"], CancellationToken.None);
        var second = await service.SubmitAnswerAsync(account, module.Name, "q2", ["wrong"], CancellationToken.None);

        Assert.False(first.IsComplete);
        Assert.True(second.IsComplete);
        Assert.Equal(100m, second.Score);
        Assert.True(second.Passed);
    }

    [Fact]
    public async Task SubmitAnswerRejectsOptionsNotPublishedForTheQuestion()
    {
        var module = new TrainingModule(
            "module-one",
            "Module one",
            1,
            false,
            [
                new TrainingPage("q1", "Question one", "formative", "Question one",
                    [new TrainingQuestionOption("valid", "Valid")], ["valid"], "formative")
            ]);
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module),
            new InMemoryProgressStore(),
            new InMemoryNoteStore(),
            TimeProvider.System);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SubmitAnswerAsync(
                new UserAccount("subject-1", "person@example.test"),
                module.Name,
                "q1",
                ["forged"],
                CancellationToken.None));
    }

    [Fact]
    public async Task FailedSummativeAttemptCanBeRetakenAndModuleCompletesOnlyAfterPassing()
    {
        var module = new TrainingModule(
            "module-one",
            "Module one",
            1,
            false,
            [
                new TrainingPage("q1", "Question one", "summative", "Question one",
                    [
                        new TrainingQuestionOption("correct", "Correct"),
                        new TrainingQuestionOption("incorrect", "Incorrect")
                    ],
                    ["correct"],
                    "summative")
            ]);
        var progress = new InMemoryProgressStore();
        var account = new UserAccount("subject-1", "person@example.test");
        var service = new TrainingJourneyService(
            new InMemoryContentProvider(module),
            progress,
            new InMemoryNoteStore(),
            TimeProvider.System);

        var failedAttempt = await service.SubmitAnswerAsync(
            account, module.Name, "q1", ["incorrect"], CancellationToken.None);
        var completedAfterFailure = await service.CompleteModuleAsync(
            account, module.Name, CancellationToken.None);
        var passedAttempt = await service.SubmitAnswerAsync(
            account, module.Name, "q1", ["correct"], CancellationToken.None);
        var completedAfterPassing = await service.CompleteModuleAsync(
            account, module.Name, CancellationToken.None);

        Assert.True(failedAttempt.IsComplete);
        Assert.False(failedAttempt.Passed);
        Assert.False(completedAfterFailure);
        Assert.True(passedAttempt.Passed);
        Assert.True(completedAfterPassing);
        Assert.Equal(2, (await progress.GetAssessmentsAsync(
            account.Id, module.Name, CancellationToken.None)).Count);
    }

    private sealed class InMemoryContentProvider(TrainingModule module) : ITrainingContentProvider
    {
        public Task<IReadOnlyList<TrainingModule>> GetModulesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrainingModule>>([module]);

        public Task<TrainingModule?> GetModuleAsync(string moduleName, CancellationToken cancellationToken) =>
            Task.FromResult<TrainingModule?>(module.Name == moduleName ? module : null);
    }

    private sealed class InMemoryProgressStore : ITrainingProgressStore
    {
        private readonly List<TrainingModuleProgress> _progress = [];
        private readonly List<AssessmentAttempt> _attempts = [];
        private readonly List<TrainingResponse> _responses = [];

        public Task<IReadOnlyList<TrainingModuleProgress>> GetForUserAsync(
            long userId,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<TrainingModuleProgress>>(
                _progress.Where(item => item.UserAccountId == userId).ToArray());

        public Task<TrainingModuleProgress?> FindAsync(
            long userId,
            string moduleName,
            CancellationToken cancellationToken) =>
            Task.FromResult(_progress.SingleOrDefault(item =>
                item.UserAccountId == userId && item.ModuleName == moduleName));

        public Task AddAsync(TrainingModuleProgress progress, CancellationToken cancellationToken)
        {
            _progress.Add(progress);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<AssessmentAttempt>> GetAssessmentsAsync(
            long userId,
            string moduleName,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<AssessmentAttempt>>(
                _attempts
                    .Where(item => item.UserAccountId == userId && item.ModuleName == moduleName)
                    .OrderBy(item => item.StartedAt)
                    .ToArray());

        public Task AddAssessmentAsync(AssessmentAttempt attempt, CancellationToken cancellationToken)
        {
            _attempts.Add(attempt);
            return Task.CompletedTask;
        }

        public Task AddResponseAsync(TrainingResponse response, CancellationToken cancellationToken)
        {
            _responses.Add(response);
            return Task.CompletedTask;
        }

        public Task<TrainingResponse?> GetLatestResponseAsync(
            long userId,
            string moduleName,
            string questionName,
            CancellationToken cancellationToken) =>
            Task.FromResult(_responses.LastOrDefault(response =>
                response.UserAccountId == userId && response.ModuleName == moduleName &&
                response.QuestionName == questionName));

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryNoteStore : ILearningNoteStore
    {
        public Task<LearningNote?> FindAsync(long userId, string moduleName, CancellationToken cancellationToken) =>
            Task.FromResult<LearningNote?>(null);

        public Task AddAsync(LearningNote note, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task DeleteForUserAsync(long userId, CancellationToken cancellationToken) => Task.CompletedTask;

        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
