using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Domain.Training;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class AssessmentSummaryRenderingTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public AssessmentSummaryRenderingTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(true, "England", "other")]
    [InlineData(true, "Scotland", "other")]
    [InlineData(false, "England", "none")]
    [InlineData(true, "England", "none")]
    public async Task RegistrationSummaryShowsOnlyApplicableAnswersAndChangeLinks(
        bool requiresAuthority, string country, string roleGroup)
    {
        var account = new EarlyYearsFoundationRecovery.Domain.Accounts.UserAccount("subject", "person@example.test");
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateSetting("other", "<Custom setting>", requiresAuthority, roleGroup);
        account.UpdateLocation(country, "Example council");
        if (roleGroup != "none")
        {
            account.UpdateRole("other", "<Custom educator>");
            account.UpdateExperience("new");
        }
        account.UpdatePreferences(false, true);

        var html = await RenderAsync(account, "/Views/Registration/CheckAnswers.cshtml",
            summarySetting: "<Custom setting>", summaryExperience: "New to early years");

        Assert.Contains("action=\"/registration/check-your-answers\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("&lt;Custom setting&gt;", html);
        Assert.DoesNotContain("<Custom setting>", html);
        Assert.Contains("href=\"/registration/setting-type\"", html);
        Assert.Contains("Change setting</a>", html);
        Assert.Contains("Change training email preference</a>", html);
        Assert.Contains("Change research preference</a>", html);
        Assert.Contains("<dt>Training emails</dt><dd>No ", html);
        Assert.Contains("<dt>Research participation</dt><dd>Yes ", html);
        Assert.DoesNotContain("<dd>False", html);
        Assert.DoesNotContain("<dd>True", html);

        var authorityApplicable = requiresAuthority && country == "England";
        Assert.Equal(authorityApplicable, html.Contains("<dt>Local authority</dt>", StringComparison.Ordinal));
        Assert.Equal(authorityApplicable, html.Contains("href=\"/registration/local-authority\"", StringComparison.Ordinal));
        Assert.Equal(roleGroup != "none", html.Contains("href=\"/registration/role-type\"", StringComparison.Ordinal));
        Assert.Equal(roleGroup != "none", html.Contains("href=\"/registration/early-years-experience\"", StringComparison.Ordinal));
        Assert.Equal(roleGroup != "none", html.Contains("New to early years", StringComparison.Ordinal));
        Assert.Equal(roleGroup != "none", html.Contains("&lt;Custom educator&gt;", StringComparison.Ordinal));
        Assert.DoesNotContain("<Custom educator>", html);
    }

    [Fact]
    public async Task RegistrationSummaryMakesMissingAnswersExplicitInsteadOfShowingBlankValues()
    {
        var account = new EarlyYearsFoundationRecovery.Domain.Accounts.UserAccount("subject", "person@example.test");

        var html = await RenderAsync(account, "/Views/Registration/CheckAnswers.cshtml");

        Assert.Contains("Not agreed", html);
        foreach (var label in new[] { "Name", "Country", "Setting", "Role", "Experience", "Training emails", "Research participation" })
        {
            Assert.Contains($"<dt>{label}</dt><dd>Not answered ", html);
        }
    }

    [Fact]
    public async Task RegistrationSummaryRejectedCompletionRetainsLabelsAndShowsErrorWithMissingAuthority()
    {
        var account = new EarlyYearsFoundationRecovery.Domain.Accounts.UserAccount("subject", "person@example.test");
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateSetting("nursery", null, true, "other");
        account.UpdateLocation("England", null);
        account.UpdateRole("Practitioner", null);
        account.UpdateExperience("new");
        account.UpdatePreferences(true, false);
        const string error = "Complete the required registration steps before continuing.";

        var html = await RenderAsync(account, "/Views/Registration/CheckAnswers.cshtml",
            summarySetting: "Nursery", summaryExperience: "New to early years", summaryError: error);

        Assert.Contains(error, html);
        Assert.Contains("<dt>Local authority</dt><dd>Not answered ", html);
        Assert.Contains("<dt>Setting</dt><dd>Nursery ", html);
        Assert.Contains("<dt>Experience</dt><dd>New to early years ", html);
        Assert.Contains("<dt>Training emails</dt><dd>Yes ", html);
        Assert.Contains("<dt>Research participation</dt><dd>No ", html);
        Assert.Contains("href=\"/registration/local-authority\"", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RegistrationRoleFormOffersCustomDescriptionWithoutJavascriptOnlyForGeneralRoles(bool general)
    {
        IReadOnlyList<RegistrationChoice> roles = general
            ? [new("practitioner", "Practitioner"), new("other", "other")]
            : [new("childminder", "Childminder")];
        var html = await RenderAsync(new RoleModel { RoleType = general ? "Practitioner" : "Childminder" },
            "/Views/Registration/Role.cshtml", registrationRoles: roles);

        Assert.Contains("action=\"/registration/role-type\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains(">Continue</button>", html);
        Assert.Equal(general, html.Contains("name=\"RoleTypeOther\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false, "details", "Continue")]
    [InlineData(true, "save", "Save")]
    public async Task AccountSettingFormSeparatesChoiceFromAtomicSave(bool details, string stage, string button)
    {
        var html = await RenderAsync(new AccountSettingModel
        {
            SettingType = "nursery", ShowDetails = details, RequiresLocalAuthority = true,
            SelectedSetting = new("nursery", "Nursery", true, "other"),
            Settings = [new("nursery", "Nursery", true, "other")],
            Roles = [new("practitioner", "Practitioner"), new("other", "other")],
            Experiences = [new("new", "New to early years")],
            LocalAuthorities = [new("example", "Example council")]
        }, "/Views/Account/Setting.cshtml");

        Assert.Contains("action=\"/account/setting\"", html);
        Assert.Contains($"value=\"{stage}\"", html);
        Assert.Contains($">{button}</button>", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains("href=\"/account\">Cancel</a>", html);
        Assert.Equal(details, html.Contains("name=\"RoleType\"", StringComparison.Ordinal));
        Assert.Equal(details, html.Contains("name=\"LocalAuthority\"", StringComparison.Ordinal));
        Assert.Equal(details, html.Contains("name=\"EarlyYearsExperience\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AccountSettingWithoutDependentAnswersStillRequiresExplicitSave()
    {
        var html = await RenderAsync(new AccountSettingModel
        {
            SettingType = "training", ShowDetails = true,
            SelectedSetting = new("training", "Training provider", false, "none")
        }, "/Views/Account/Setting.cshtml");

        Assert.Contains("Training provider", html);
        Assert.Contains(">Save</button>", html);
        Assert.DoesNotContain("name=\"LocalAuthority\"", html);
        Assert.DoesNotContain("name=\"RoleType\"", html);
        Assert.DoesNotContain("name=\"EarlyYearsExperience\"", html);
    }

    [Fact]
    public async Task AccountSettingSummaryEncodesCustomDescriptionAndOffersEdit()
    {
        var account = new EarlyYearsFoundationRecovery.Domain.Accounts.UserAccount("subject", "person@example.test");
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateLocation("Scotland", null);
        account.UpdateSetting("other", "<Custom setting>", false, "none");
        account.UpdatePreferences(false, false);
        Assert.True(account.CompleteRegistration(DateTimeOffset.UtcNow));

        var html = await RenderAsync(account, "/Views/Account/Index.cshtml");

        Assert.Contains("href=\"/account/setting\"", html);
        Assert.Contains("&lt;Custom setting&gt;", html);
        Assert.DoesNotContain("<Custom setting>", html);
    }

    [Fact]
    public async Task AccountRoleFormOffersAllFieldsWithoutRequiringJavascript()
    {
        var html = await RenderAsync(new AccountRoleModel
        {
            RoleType = "Practitioner", EarlyYearsExperience = "new",
            Roles = [new("practitioner", "Practitioner"), new("other", "other")],
            Experiences = [new("new", "New to early years")]
        }, "/Views/Account/Role.cshtml");

        Assert.Contains("action=\"/account/role\"", html);
        Assert.Contains("name=\"RoleTypeOther\"", html);
        Assert.Contains("name=\"EarlyYearsExperience\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains(">Save</button>", html);
        Assert.Contains("href=\"/account\">Cancel</a>", html);
        Assert.Matches("<option(?=[^>]*value=\"Practitioner\")(?=[^>]*selected=\"selected\")[^>]*>", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AccountRoleSummaryShowsApplicableDetailsAndEditLink(bool applicable)
    {
        var account = new EarlyYearsFoundationRecovery.Domain.Accounts.UserAccount("subject", "person@example.test");
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateLocation("Scotland", null);
        account.UpdateSetting("training", null, false, applicable ? "other" : "none");
        if (applicable)
        {
            account.UpdateRole("other", "<Custom educator>");
            account.UpdateExperience("new");
        }
        account.UpdatePreferences(false, false);
        Assert.True(account.CompleteRegistration(DateTimeOffset.UtcNow));

        var html = await RenderAsync(account, "/Views/Account/Index.cshtml");

        Assert.Equal(applicable, html.Contains("href=\"/account/role\"", StringComparison.Ordinal));
        Assert.Equal(applicable, html.Contains("&lt;Custom educator&gt;", StringComparison.Ordinal));
        Assert.DoesNotContain("<Custom educator>", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AccountLocationFormProvidesReferenceChoicesSaveAndCancel(bool needsAuthority)
    {
        var html = await RenderAsync(new AccountLocationModel
        {
            Country = "england", LocalAuthority = "example", SettingRequiresLocalAuthority = needsAuthority,
            Countries = [new("england", "England")],
            LocalAuthorities = [new("example", "Example council")]
        }, "/Views/Account/Location.cshtml");

        Assert.Contains("action=\"/account/location\"", html);
        Assert.Matches("<option(?=[^>]*value=\"england\")(?=[^>]*selected=\"selected\")[^>]*>", html);
        Assert.Equal(needsAuthority, html.Contains("Example council", StringComparison.Ordinal));
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains(">Save</button>", html);
        Assert.Contains("href=\"/account\">Cancel</a>", html);
    }

    [Theory]
    [InlineData(true, "England", "Example council")]
    [InlineData(true, "Scotland", "Not applicable")]
    [InlineData(false, "England", "Not applicable")]
    public async Task AccountLocationSummaryShowsCountryAndApplicableAuthority(
        bool requiresAuthority, string country, string expectedAuthority)
    {
        var account = new EarlyYearsFoundationRecovery.Domain.Accounts.UserAccount("subject", "person@example.test");
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateSetting("training", null, requiresAuthority, "none");
        account.UpdateLocation(country, "Example council");
        account.UpdatePreferences(false, false);
        Assert.True(account.CompleteRegistration(DateTimeOffset.UtcNow));

        var html = await RenderAsync(account, "/Views/Account/Index.cshtml");

        Assert.Contains($"<dd>{country}</dd>", html);
        Assert.Contains($"<dd>{expectedAuthority}</dd>", html);
        Assert.Contains("href=\"/account/location\"", html);
    }

    [Theory]
    [InlineData(true, "/account/research-preferences", "research")]
    [InlineData(false, "/account/email-preferences", "email")]
    public async Task AccountPreferenceFormOffersExplicitChoiceSaveAndCancel(bool research, string action, string heading)
    {
        var html = await RenderAsync(new AccountPreferenceModel { Selected = false },
            "/Views/Account/Preference.cshtml", researchPreference: research);

        Assert.Contains($"action=\"{action}\"", html);
        Assert.Contains(heading, html);
        Assert.Contains("value=\"true\"", html);
        Assert.Contains("value=\"false\"", html);
        Assert.Contains("checked=\"checked\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Contains(">Save</button>", html);
        Assert.Contains("href=\"/account\">Cancel</a>", html);
        Assert.Contains("<legend>", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task AccountPageDisplaysIndependentPreferencesAndEditLinks(bool selected)
    {
        var account = new EarlyYearsFoundationRecovery.Domain.Accounts.UserAccount("subject", "person@example.test");
        account.UpdatePreferences(selected, !selected);
        account.AcceptTerms(DateTimeOffset.UtcNow);
        account.UpdateName("Alex", "Example");
        account.UpdateLocation("Scotland", null);
        account.UpdateSetting("other", "Example", false, "none");
        Assert.True(account.CompleteRegistration(DateTimeOffset.UtcNow));

        var html = await RenderAsync(account, "/Views/Account/Index.cshtml");

        Assert.Contains("href=\"/account/email-preferences\"", html);
        Assert.Contains("href=\"/account/research-preferences\"", html);
        Assert.Contains(selected ? "You have chosen to receive emails" : "You have chosen not to receive emails", html);
        Assert.Contains(selected ? "You have chosen not to participate in research." : "You have chosen to participate in research.", html);
    }

    [Theory]
    [InlineData(true, "/account/name", "Save")]
    [InlineData(false, "/registration/name", "Continue")]
    public async Task NameFormUsesSeparateAccountAndRegistrationJourneys(bool accountEdit, string action, string button)
    {
        var html = await RenderAsync(new NameModel { FirstName = "Alex", LastName = "Example" },
            "/Views/Registration/Name.cshtml", accountEdit);

        Assert.Contains($"action=\"{action}\"", html);
        Assert.Contains($">{button}</button>", html);
        Assert.Contains("value=\"Alex\"", html);
        Assert.Contains("autocomplete=\"given-name\"", html);
        Assert.Contains("__RequestVerificationToken", html);
        Assert.Equal(accountEdit, html.Contains("Cancel", StringComparison.Ordinal));
        if (accountEdit)
        {
            Assert.Contains("href=\"/account\"", html);
            Assert.Contains("certificate", html);
            Assert.Contains("GOV.UK One Login", html);
        }
    }

    [Fact]
    public async Task AccountNameLinkUsesDedicatedEditRouteAndExplainsCertificateName()
    {
        var account = new EarlyYearsFoundationRecovery.Domain.Accounts.UserAccount("subject", "person@example.test");
        account.UpdateName("Alex", "Example");

        var html = await RenderAsync(account, "/Views/Account/Index.cshtml");

        Assert.Contains("href=\"/account/name\"", html);
        Assert.Contains("Change name", html);
        Assert.DoesNotContain("href=\"/registration/name\"", html);
        Assert.Contains("certificate", html);
        Assert.Contains("GOV.UK One Login", html);
    }

    [Fact]
    public async Task ModuleListGroupsLearningStatesAndOrdersModulesWithinEachGroup()
    {
        TrainingModuleViewModel Item(string name, string state, int position) =>
            new(OverviewModule() with { Name = name, Title = name, Position = position }, state, 1)
            {
                CompletedAt = state == "completed" ? DateTimeOffset.Parse("2026-10-20T12:00:00Z") : null
            };
        TrainingModuleViewModel[] modules =
        [
            Item("available-later", "available", 4),
            Item("completed-module", "completed", 2),
            Item("started-module", "in-progress", 3),
            Item("available-first", "available", 1)
        ];

        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        var started = ModuleListSection(html, "started");
        var available = ModuleListSection(html, "available");
        var completed = ModuleListSection(html, "completed");
        Assert.Contains("Modules in progress", started);
        Assert.Contains("started-module", started);
        Assert.DoesNotContain("available-first", started);
        Assert.Contains("Available modules", available);
        Assert.DoesNotContain("started-module", available);
        Assert.DoesNotContain("completed-module", available);
        Assert.True(available.IndexOf("available-first", StringComparison.Ordinal) <
            available.IndexOf("available-later", StringComparison.Ordinal));
        Assert.Contains("Completed modules", completed);
        Assert.Contains("completed-module", completed);
        Assert.True(html.IndexOf("id=\"started\"", StringComparison.Ordinal) <
            html.IndexOf("id=\"available\"", StringComparison.Ordinal));
        Assert.True(html.IndexOf("id=\"available\"", StringComparison.Ordinal) <
            html.IndexOf("id=\"completed\"", StringComparison.Ordinal));
        Assert.Contains("<h3>", html);
    }

    [Fact]
    public async Task NewLearnerSeesStartGuidanceAndNoEmptyCompletedGroup()
    {
        TrainingModuleViewModel[] modules = [new(OverviewModule(), "available", 0)];
        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.Contains("To begin the training course, start an available module.", ModuleListSection(html, "started"));
        Assert.Contains("Available modules", html);
        Assert.DoesNotContain("id=\"completed\"", html);
        Assert.DoesNotContain("id=\"upcoming\"", html);
        Assert.DoesNotContain("View certificate", html);
    }

    [Fact]
    public async Task ReturningLearnerWithNoActiveModulesSeesChooseModuleGuidance()
    {
        TrainingModuleViewModel[] modules =
        [
            new(OverviewModule() with { Name = "finished" }, "completed", 17)
            {
                CompletedAt = DateTimeOffset.Parse("2026-10-20T12:00:00Z")
            },
            new(OverviewModule() with { Name = "next" }, "available", 0)
        ];
        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.Contains("Please choose an available module.", ModuleListSection(html, "started"));
        Assert.DoesNotContain("To begin the training course", html);
        Assert.Contains("next", ModuleListSection(html, "available"));
        Assert.Contains("finished", ModuleListSection(html, "completed"));
    }

    [Fact]
    public async Task GroupedModuleTitlesAreEncodedAndLinkToTheirOverview()
    {
        TrainingModuleViewModel[] modules =
        [
            new(OverviewModule() with { Title = "<script>unsafe</script>", Pages = [] }, "available", 0)
        ];
        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.Contains("&lt;script&gt;unsafe&lt;/script&gt;", html);
        Assert.DoesNotContain("<script>", html);
        Assert.Contains("href=\"/training/module-one\"", html);
        Assert.DoesNotContain("pages visited", html);
        Assert.Contains("aria-labelledby=\"available-heading\"", html);
        Assert.Contains("id=\"available-heading\"", html);
    }

    [Fact]
    public async Task AllCompletedModulesShowCompletionMessageWithoutEmptyLearningGroups()
    {
        TrainingModuleViewModel[] modules =
        [
            new(OverviewModule(), "completed", 5) { CompletedAt = DateTimeOffset.Parse("2026-10-20T12:00:00Z") }
        ];
        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.Contains("You have completed all available modules.", html);
        Assert.Contains("Completed modules", ModuleListSection(html, "completed"));
        Assert.DoesNotContain("Modules in progress", html);
        Assert.DoesNotContain("id=\"available\"", html);
    }

    [Fact]
    public async Task InProgressOnlyDoesNotRenderEmptyAvailableOrCompletedGroups()
    {
        TrainingModuleViewModel[] modules = [new(OverviewModule(), "in-progress", 3)];
        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.Contains("3 of 17 pages visited", ModuleListSection(html, "started"));
        Assert.DoesNotContain("id=\"available\"", html);
        Assert.DoesNotContain("id=\"completed\"", html);
        Assert.DoesNotContain("You have not started", html);
    }

    [Fact]
    public async Task EmptyModuleListRetainsExplicitNoContentMessage()
    {
        var html = await RenderAsync(Array.Empty<TrainingModuleViewModel>(), "/Views/Training/Index.cshtml");

        Assert.Contains("There are no training modules available at the moment.", html);
        Assert.DoesNotContain("You have completed all", html);
    }

    [Fact]
    public async Task UpcomingModulesShowOrderedNonLinkedCardsWithEncodedContent()
    {
        TrainingModuleViewModel Item(string name, string title, int position, string upcoming) =>
            new(OverviewModule() with
            {
                Name = name, Title = title, Position = position, IsDraft = true, Upcoming = upcoming
            }, "upcoming", 0);
        TrainingModuleViewModel[] modules =
        [
            Item("later", "Later module", 10, ""),
            Item("first", "<script>unsafe</script>", 9, "<img src=x onerror=alert(1)>")
        ];

        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");
        var upcoming = ModuleListSection(html, "upcoming");

        Assert.Contains("Future modules in this course", upcoming);
        Assert.Contains("Coming soon - Module 9:", upcoming);
        Assert.True(upcoming.IndexOf("Module 9:", StringComparison.Ordinal) <
            upcoming.IndexOf("Module 10:", StringComparison.Ordinal));
        Assert.Contains("&lt;script&gt;unsafe&lt;/script&gt;", upcoming);
        Assert.Contains("&lt;img", upcoming);
        Assert.DoesNotContain("<a ", upcoming);
        Assert.DoesNotContain("<img ", upcoming);
        Assert.DoesNotContain("pages visited", upcoming);
        Assert.DoesNotContain("View certificate", upcoming);
        Assert.DoesNotContain("You have completed all", html);
    }

    [Fact]
    public async Task FutureModulesKeepCourseOpenAfterAllAvailableModulesAreCompleted()
    {
        TrainingModuleViewModel[] modules =
        [
            new(OverviewModule(), "completed", 17) { CompletedAt = DateTimeOffset.UtcNow },
            new(OverviewModule() with { Name = "future", Title = "Future module", IsDraft = true, Position = 2 },
                "upcoming", 0)
        ];

        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.Contains("You do not have any modules available.", ModuleListSection(html, "available"));
        Assert.Contains("Future module", ModuleListSection(html, "upcoming"));
        Assert.Contains("Completed modules", ModuleListSection(html, "completed"));
        Assert.DoesNotContain("You have completed all available modules.", html);
        Assert.True(html.IndexOf("id=\"upcoming\"", StringComparison.Ordinal) <
            html.IndexOf("id=\"completed\"", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UpcomingAndAvailableModulesRemainInSeparateGroups()
    {
        TrainingModuleViewModel[] modules =
        [
            new(OverviewModule() with { Name = "future", Title = "Future module", IsDraft = true }, "upcoming", 0),
            new(OverviewModule(), "available", 0)
        ];

        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.Contains("href=\"/training/module-one\"", ModuleListSection(html, "available"));
        Assert.DoesNotContain("Future module", ModuleListSection(html, "available"));
        Assert.Contains("Future module", ModuleListSection(html, "upcoming"));
        Assert.DoesNotContain("You do not have any modules available.", html);
        Assert.DoesNotContain("href=\"/training/future", html);
    }

    private static string ModuleListSection(string html, string id)
    {
        var match = System.Text.RegularExpressions.Regex.Match(html,
            $"<section id=\"{id}\"[^>]*>(.*?)</section>",
            System.Text.RegularExpressions.RegexOptions.Singleline);
        Assert.True(match.Success, $"Expected module group {id}.");
        return match.Groups[1].Value;
    }

    [Theory]
    [InlineData("2026-01-02T10:30:00Z", "2 January 2026", "2026-01-02")]
    [InlineData("2026-06-01T23:30:00Z", "2 June 2026", "2026-06-02")]
    public async Task CompletedModuleTableShowsRecordedDateAndCertificateAction(
        string timestamp, string expectedDate, string isoDate)
    {
        TrainingModuleViewModel[] modules =
        [
            new(OverviewModule() with { Title = "First module" }, "completed", 4)
            {
                CompletedAt = DateTimeOffset.Parse(timestamp)
            }
        ];

        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");
        var completed = ModuleListSection(html, "completed");

        Assert.Contains("<table", completed);
        Assert.Contains("Module name", completed);
        Assert.Contains("Date completed", completed);
        Assert.Contains("Actions", completed);
        Assert.Contains("scope=\"col\"", completed);
        Assert.Contains(expectedDate, completed);
        Assert.Contains($"datetime=\"{isoDate}\"", completed);
        Assert.Contains("href=\"/training/module-one\">First module</a>", completed);
        Assert.Contains("href=\"/training/module-one/certificate\"", completed);
        Assert.Contains("View certificate", completed);
        Assert.DoesNotContain("pages visited", completed);
    }

    [Fact]
    public async Task CompletedTablePreservesModuleOrderAndEncodesTitles()
    {
        TrainingModuleViewModel Item(string name, string title, int position) =>
            new(OverviewModule() with { Name = name, Title = title, Position = position }, "completed", 0)
            {
                CompletedAt = DateTimeOffset.Parse("2026-01-02T10:30:00Z")
            };
        var html = await RenderAsync(new[]
        {
            Item("later", "Later module", 3),
            Item("first", "<script>unsafe</script>", 1)
        }, "/Views/Training/Index.cshtml");
        var completed = ModuleListSection(html, "completed");

        Assert.Contains("<table", completed);
        Assert.True(completed.IndexOf("/training/first\"", StringComparison.Ordinal) <
            completed.IndexOf("/training/later\"", StringComparison.Ordinal));
        Assert.Contains("&lt;script&gt;unsafe&lt;/script&gt;", completed);
        Assert.DoesNotContain("<script>", completed);
        Assert.Contains("View certificate", completed);
    }

    [Fact]
    public async Task CompletedTableRejectsMissingCompletionDateRatherThanInventingOne()
    {
        TrainingModuleViewModel[] modules = [new(OverviewModule(), "completed", 0)];

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => RenderAsync(modules, "/Views/Training/Index.cshtml"));

        Assert.Equal("A completed module must have a recorded completion date.", exception.Message);
    }

    [Theory]
    [InlineData(false, "Retake end of module test", "/training/module-one/content/test")]
    [InlineData(true, "View previous test result", "/training/module-one/assessment-result")]
    public async Task InProgressModuleOffersActionForGradedAssessment(bool passed, string label, string destination)
    {
        var attempt = new AssessmentAttempt(1, "module-one", DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("question", ["1"], passed);
        attempt.Grade(1, DateTimeOffset.UtcNow);
        TrainingModuleViewModel[] modules =
        [
            new(OverviewModule(), "in-progress", 4) { LatestAssessment = attempt }
        ];

        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.Contains(label, ModuleListSection(html, "started"));
        Assert.Contains($"href=\"{destination}\"", html);
        Assert.Contains("href=\"/training/module-one\"", html);
        Assert.DoesNotContain(passed ? "Retake end of module test" : "View previous test result", html);
    }

    [Fact]
    public async Task UngradedAssessmentDoesNotOfferResultOrRetake()
    {
        TrainingModuleViewModel[] modules =
        [
            new(OverviewModule(), "in-progress", 4)
            {
                LatestAssessment = new AssessmentAttempt(1, "module-one", DateTimeOffset.UtcNow)
            }
        ];

        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.DoesNotContain("Retake end of module test", html);
        Assert.DoesNotContain("View previous test result", html);
    }

    [Theory]
    [InlineData("available")]
    [InlineData("completed")]
    public async Task AssessmentActionsAreRestrictedToInProgressModules(string state)
    {
        var attempt = new AssessmentAttempt(1, "module-one", DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("question", ["1"], false);
        attempt.Grade(1, DateTimeOffset.UtcNow);
        TrainingModuleViewModel[] modules =
        [
            new(OverviewModule(), state, 4)
            {
                LatestAssessment = attempt,
                CompletedAt = state == "completed" ? DateTimeOffset.UtcNow : null
            }
        ];

        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.DoesNotContain("Retake end of module test", html);
        Assert.DoesNotContain("View previous test result", html);
        Assert.Equal(state == "completed", html.Contains("View certificate", StringComparison.Ordinal));
    }

    [Fact]
    public async Task FailedModuleWithoutTestIntroductionLinksToFirstTestQuestion()
    {
        var attempt = new AssessmentAttempt(1, "module-one", DateTimeOffset.UtcNow);
        attempt.Grade(1, DateTimeOffset.UtcNow);
        var module = OverviewModule();
        TrainingModuleViewModel[] modules =
        [
            new(module with { Pages = module.Pages.Where(page => page.PageType != "assessment_intro").ToArray() },
                "in-progress", 4) { LatestAssessment = attempt }
        ];

        var html = await RenderAsync(modules, "/Views/Training/Index.cshtml");

        Assert.Contains("Retake end of module test", html);
        Assert.Contains("href=\"/training/module-one/content/test-question\"", html);
    }

    [Fact]
    public async Task OverviewRendersDescriptionOutcomesAndCriteriaThroughSanitizedContentRenderer()
    {
        var module = OverviewModule() with
        {
            Description = "Explore **child development**.\n\n<script>alert(1)</script>",
            Outcomes = "- Observe individual needs\n- [Support learning](/training/module-one/content/topic-one)",
            Criteria = "- Healthy relationships\n\n[Unsafe](javascript:alert%281%29)"
        };
        var html = await RenderAsync(new TrainingModuleDetailViewModel(module, 0), "/Views/Training/Module.cshtml");

        Assert.Contains("Explore <strong>child development</strong>.", html);
        Assert.Contains("On completion of this module you will be able to:", html);
        Assert.Contains("<li>Observe individual needs</li>", html);
        Assert.Contains("This module covers:", html);
        Assert.Contains("<li>Healthy relationships</li>", html);
        Assert.Contains("href=\"/training/module-one/content/topic-one\">Support learning</a>", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("javascript:", html);
        Assert.True(html.IndexOf("Explore <strong>", StringComparison.Ordinal) <
            html.IndexOf("Module content", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task MissingOverviewMetadataDoesNotRenderEmptyHeadings(string content)
    {
        var module = OverviewModule() with { Description = content, Outcomes = content, Criteria = content };
        var html = await RenderAsync(new TrainingModuleDetailViewModel(module, 0), "/Views/Training/Module.cshtml");

        Assert.DoesNotContain("On completion of this module", html);
        Assert.DoesNotContain("This module covers:", html);
        Assert.Contains("Learning section", html);
        Assert.Contains("Start module", html);
    }

    [Fact]
    public async Task OverviewGroupsTopicsAndHidesIndividualQuestionsAndFeedback()
    {
        var module = OverviewModule();
        var html = await RenderAsync(new TrainingModuleDetailViewModel(module, 0), "/Views/Training/Module.cshtml");

        Assert.Contains("Learning section", html);
        Assert.Contains("First topic", html);
        Assert.Contains("Second topic", html);
        Assert.Contains("Not started", html);
        Assert.DoesNotContain("Private question title", html);
        Assert.DoesNotContain("Feedback question title", html);
        Assert.DoesNotContain("Before confidence question", html);
        Assert.DoesNotContain("href=\"/training/module-one/content/topic-one\"", html);
        Assert.DoesNotContain("href=\"/training/module-one/content/topic-two\"", html);
        Assert.DoesNotContain("href=\"/training/module-one/content/certificate\"", html);
    }

    [Fact]
    public async Task OverviewPartiallyViewedTopicHasStatusButIsNotLinked()
    {
        var module = OverviewModule();
        var model = new TrainingModuleDetailViewModel(module, 2, "topic-one")
        {
            VisitedPageNames = ["section", "topic-one"]
        };
        var html = await RenderAsync(model, "/Views/Training/Module.cshtml");

        Assert.Contains("In progress", html);
        Assert.DoesNotContain("href=\"/training/module-one/content/topic-one\">First topic", html);
        Assert.DoesNotContain("href=\"/training/module-one/content/topic-two\"", html);
    }

    [Fact]
    public async Task OverviewCompletedTopicLinksToItsHeaderNotLastVisitedPage()
    {
        var module = OverviewModule();
        var model = new TrainingModuleDetailViewModel(module, 3, "question")
        {
            VisitedPageNames = ["section", "topic-one", "question"]
        };
        var html = await RenderAsync(model, "/Views/Training/Module.cshtml");

        Assert.Contains("href=\"/training/module-one/content/topic-one\">First topic</a>", html);
        Assert.DoesNotContain("href=\"/training/module-one/content/topic-two\"", html);
        Assert.Contains("Completed", html);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverviewReflectionLinkRequiresPassedAssessmentEvenIfPreviouslyVisited(bool passed)
    {
        var module = OverviewModule();
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("test-question", [passed ? "1" : "2"], passed);
        attempt.Grade(1, DateTimeOffset.UtcNow);
        var model = new TrainingModuleDetailViewModel(module, 2, "question", LatestAssessment: attempt)
        {
            VisitedPageNames = ["reflection", "confidence-question"]
        };
        var html = await RenderAsync(model, "/Views/Training/Module.cshtml");

        Assert.Equal(passed, html.Contains("href=\"/training/module-one/content/reflection\">Reflect on learning</a>",
            StringComparison.Ordinal));
        Assert.Contains(passed ? "Completed" : "Failed", html);
    }

    [Fact]
    public async Task OverviewCompletionMakesTopicsRevisitableWithoutIndividualVisitRecords()
    {
        var module = OverviewModule();
        var html = await RenderAsync(new TrainingModuleDetailViewModel(module, 0, CompletedAt: DateTimeOffset.UtcNow),
            "/Views/Training/Module.cshtml");

        Assert.Contains("href=\"/training/module-one/content/topic-one\">First topic</a>", html);
        Assert.Contains("href=\"/training/module-one/content/topic-two\">Second topic</a>", html);
        Assert.Contains("Complete module", html);
        Assert.DoesNotContain("Not started", html);
        Assert.DoesNotContain("In progress", html);
    }

    private static TrainingModule OverviewModule() =>
        new("module-one", "Module one", 1, false,
        [
            new("before", "Confidence check", "text_page", ""),
            new("before-question", "Before confidence question", "pre_confidence", ""),
            new("section", "Learning section", "sub_module_intro", ""),
            new("topic-one", "First topic", "topic_intro", ""),
            new("question", "Private question title", "formative", ""),
            new("topic-two", "Second topic", "topic_intro", ""),
            new("reading", "Reading page", "text_page", ""),
            new("summary", "Review and reflect", "summary_intro", ""),
            new("recap", "Module recap", "recap_page", ""),
            new("test", "End of module test", "assessment_intro", ""),
            new("test-question", "Test question", "summative", ""),
            new("results", "Results", "assessment_results", ""),
            new("reflection", "Reflect on learning", "confidence_intro", ""),
            new("confidence-question", "After confidence question", "confidence", ""),
            new("feedback", "Feedback question title", "feedback", ""),
            new("thanks", "Thank you", "thankyou", ""),
            new("certificate", "Download certificate", "certificate", "")
        ]);

    [Fact]
    public void OverviewSectionsPreserveOrderCountsAndTopicBoundaries()
    {
        var module = OverviewModule();
        module = module with
        {
            Pages = [
                new("interrupt", "Not an overview section", "interruption_page", ""),
                .. module.Pages.Take(7),
                new("section-two", "Another learning section", "sub_module_intro", ""),
                new("topic-three", "Third topic", "topic_intro", ""),
                .. module.Pages.Skip(7)
            ]
        };
        var sections = new TrainingModuleDetailViewModel(module, 2)
        {
            VisitedPageNames = ["topic-one", "question"]
        }.Sections;

        Assert.Equal(["Learning section", "Another learning section", "Review and reflect", "Complete module"],
            sections.Select(section => section.Title));
        Assert.Equal([5, 2, 7, 1], sections.Select(section => section.PageCount));
        Assert.Equal(["First topic", "Second topic"], sections[0].Topics.Select(topic => topic.Title));
        Assert.Equal("Completed", sections[0].Topics[0].Status);
        Assert.Equal("topic-one", sections[0].Topics[0].PageName);
        Assert.Equal("Not started", sections[0].Topics[1].Status);
        Assert.Null(sections[0].Topics[1].PageName);
        Assert.Equal("In progress", sections[0].Status);
    }

    [Fact]
    public void OverviewIgnoresRemovedVisitNamesWhenCalculatingTopicCompletion()
    {
        var module = OverviewModule();
        var sections = new TrainingModuleDetailViewModel(module, 10)
        {
            VisitedPageNames = ["removed-page", "another-removed-page"]
        }.Sections;

        Assert.All(sections, section => Assert.Equal("Not started", section.Status));
        Assert.All(sections.SelectMany(section => section.Topics), topic => Assert.Null(topic.PageName));
    }

    [Fact]
    public void OverviewWithoutContentHasNoSections()
    {
        Assert.Empty(new TrainingModuleDetailViewModel(
            OverviewModule() with { Pages = [] }, 0).Sections);
    }

    [Theory]
    [InlineData("confidence", true)]
    [InlineData("pre_confidence", false)]
    [InlineData("formative", false)]
    [InlineData("summative", false)]
    public async Task ConfidenceGuidanceMatchesRailsAndIsAccessible(string pageType, bool showDescription)
    {
        var question = FormativeQuestion() with
        {
            PageType = pageType,
            Description = "**Reflect on your learning.**\n\n[Unsafe](javascript:alert%281%29)",
            Hint = "Choose honestly. <script>alert(1)</script>"
        };
        var module = new TrainingModule("module-one", "Module one", 1, false, [question]);
        var html = await RenderAsync(new TrainingPageViewModel(module, question, null, null),
            "/Views/Training/Page.cshtml");

        if (showDescription)
        {
            Assert.Contains("<strong>Reflect on your learning.</strong>", html);
            Assert.Contains("id=\"question-description\"", html);
            Assert.Contains("aria-describedby=\"question-description question-hint answer-error\"", html);
        }
        else
        {
            Assert.DoesNotContain("Reflect on your learning.", html);
            Assert.DoesNotContain("question-description", html);
        }
        if (pageType is "confidence" or "pre_confidence")
        {
            Assert.Contains("id=\"question-hint\"", html);
            Assert.Contains("Choose honestly. &lt;script&gt;alert(1)&lt;/script&gt;", html);
            Assert.DoesNotContain("Select one answer.", html);
            if (!showDescription)
            {
                Assert.Contains("aria-describedby=\"question-hint answer-error\"", html);
            }
        }
        else
        {
            Assert.DoesNotContain("question-hint", html);
            Assert.DoesNotContain("Choose honestly.", html);
        }
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("javascript:", html);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task EmptyConfidenceGuidanceDoesNotCreateDanglingAccessibilityReferences(string guidance)
    {
        var question = FormativeQuestion() with { PageType = "confidence", Description = guidance, Hint = guidance };
        var module = new TrainingModule("module-one", "Module one", 1, false, [question]);
        var html = await RenderAsync(new TrainingPageViewModel(module, question, null, null),
            "/Views/Training/Page.cshtml");

        Assert.DoesNotContain("question-hint", html);
        Assert.DoesNotContain("question-description", html);
        Assert.Contains("aria-describedby=\"answer-error\"", html);
    }

    [Theory]
    [InlineData("**Think back.**", "", "question-description answer-error")]
    [InlineData("", "Choose one response.", "question-hint answer-error")]
    public async Task ConfidenceOptionsReferenceOnlyTheGuidanceThatExists(
        string description, string hint, string expectedReferences)
    {
        var question = FormativeQuestion() with
        {
            PageType = "confidence", Description = description, Hint = hint
        };
        var module = new TrainingModule("module-one", "Module one", 1, false, [question]);
        var html = await RenderAsync(new TrainingPageViewModel(module, question, null, null),
            "/Views/Training/Page.cshtml");

        Assert.Equal(question.Options!.Count, System.Text.RegularExpressions.Regex.Matches(
            html, $"aria-describedby=\"{expectedReferences}\"").Count);
        Assert.Equal(description.Length > 0, html.Contains("id=\"question-description\"", StringComparison.Ordinal));
        Assert.Equal(hint.Length > 0, html.Contains("id=\"question-hint\"", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("text_page")]
    [InlineData("pre_confidence_intro")]
    public async Task ConfidenceIntroductionOffersOptionalSkipToLearningContent(string introType)
    {
        var module = PreConfidenceModule(introType);
        var html = await RenderAsync(new TrainingPageViewModel(module, module.Pages[0], module.Pages[1], null),
            "/Views/Training/Page.cshtml");

        Assert.Contains("href=\"/training/module-one/content/before-one\"", html);
        Assert.Contains("href=\"/training/module-one/content/start-learning\">Skip questions</a>", html);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task SkipIsNotOfferedFromQuestionsLearningContentOrPostConfidence(int pageIndex)
    {
        var module = PreConfidenceModule("text_page");
        var html = await RenderAsync(new TrainingPageViewModel(module, module.Pages[pageIndex],
            pageIndex + 1 < module.Pages.Count ? module.Pages[pageIndex + 1] : null, null),
            "/Views/Training/Page.cshtml");

        Assert.DoesNotContain("Skip questions", html);
    }

    [Fact]
    public async Task IntroductionDoesNotOfferSkipWithoutALearningContentDestination()
    {
        var module = PreConfidenceModule("text_page");
        module = module with { Pages = module.Pages.Where(page => page.PageType != "sub_module_intro").ToArray() };
        var html = await RenderAsync(new TrainingPageViewModel(module, module.Pages[0], module.Pages[1], null),
            "/Views/Training/Page.cshtml");

        Assert.DoesNotContain("Skip questions", html);
        Assert.Contains("href=\"/training/module-one/content/before-one\"", html);
    }

    [Fact]
    public async Task LearningStartPreviousReturnsToIntroductionWithoutForcingConfidenceQuestions()
    {
        var module = PreConfidenceModule("text_page");
        var html = await RenderAsync(new TrainingPageViewModel(module, module.Pages[3], module.Pages[4], null),
            "/Views/Training/Page.cshtml");

        Assert.Contains("href=\"/training/module-one/content/before-intro\">Previous</a>", html);
        Assert.DoesNotContain("href=\"/training/module-one/content/before-two\"", html);
    }

    [Fact]
    public async Task InterruptionPageDoesNotOfferConfidenceSkip()
    {
        var module = PreConfidenceModule("interruption_page");
        var html = await RenderAsync(new TrainingPageViewModel(module, module.Pages[0], module.Pages[1], null),
            "/Views/Training/Page.cshtml");

        Assert.DoesNotContain("Skip questions", html);
    }

    [Fact]
    public async Task PreviousWithinConfidenceCheckStillReturnsToPreviousQuestion()
    {
        var module = PreConfidenceModule("text_page");
        var html = await RenderAsync(new TrainingPageViewModel(module, module.Pages[2], module.Pages[3], null),
            "/Views/Training/Page.cshtml");

        Assert.Contains("href=\"/training/module-one/content/before-one\">Previous</a>", html);
    }

    [Fact]
    public async Task LearningStartWithoutPreConfidenceReturnsToImmediatePreviousPage()
    {
        var module = PreConfidenceModule("text_page");
        module = module with { Pages = module.Pages.Where(page => page.PageType != "pre_confidence").ToArray() };
        var html = await RenderAsync(new TrainingPageViewModel(module, module.Pages[1], module.Pages[2], null),
            "/Views/Training/Page.cshtml");

        Assert.Contains("href=\"/training/module-one/content/before-intro\">Previous</a>", html);
    }

    private static TrainingModule PreConfidenceModule(string introType) =>
        new("module-one", "Module one", 1, false,
        [
            new("before-intro", "Before you start", introType, "An optional confidence check."),
            new("before-one", "First confidence question", "pre_confidence", "How confident are you?",
                [new("1", "Very confident")]),
            new("before-two", "Second confidence question", "pre_confidence", "How confident are you?",
                [new("1", "Very confident")]),
            new("start-learning", "Start learning", "sub_module_intro", "Learning material."),
            new("after-one", "After learning", "confidence", "How confident are you now?",
                [new("1", "Very confident")])
        ]);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CompletedResultsShowOnlyIncorrectQuestionsAndTheirSelectedAnswers(bool passed)
    {
        var (module, attempt) = CreateAssessment(passed);
        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.Contains("Review your incorrect answers", html);
        Assert.Contains("Which approach supports development?", html);
        Assert.Contains("You selected:", html);
        Assert.Contains("Ignore individual needs", html);
        Assert.Contains("Revisit the topic", html);
        Assert.Contains("href=\"/training/module-one/content/topic\"", html);
        Assert.DoesNotContain("Correct question body", html);
        Assert.DoesNotContain("Unselected distractor", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<form", html);
    }

    [Fact]
    public async Task PerfectScoreDoesNotShowIncorrectAnswerReview()
    {
        var (module, _) = CreateAssessment(true);
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        foreach (var question in module.Pages.Where(page => page.PageType == "summative"))
        {
            attempt.AddOrReplaceAnswer(question.Name, ["1"], true);
        }
        attempt.Grade(4, DateTimeOffset.UtcNow);

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.DoesNotContain("Review your incorrect answers", html);
        Assert.DoesNotContain("Which approach supports development?", html);
        Assert.DoesNotContain("You selected:", html);
    }

    [Fact]
    public async Task UnfinishedAttemptDoesNotExposeIncorrectAnswers()
    {
        var (module, _) = CreateAssessment(false);
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("incorrect", ["2"], false);

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.DoesNotContain("Review your incorrect answers", html);
        Assert.DoesNotContain("Which approach supports development?", html);
        Assert.DoesNotContain("You selected:", html);
    }

    [Fact]
    public async Task ReviewListsAllSelectedAnswersAndEncodesTheirLabels()
    {
        var (module, _) = CreateAssessment(false);
        var question = module.Pages.Single(page => page.Name == "incorrect") with
        {
            Options = [new("1", "Support needs"), new("2", "<img src=x onerror=alert(1)>"), new("3", "Another selected answer")],
            AllowsMultipleAnswers = true
        };
        module = module with { Pages = module.Pages.Select(page => page.Name == question.Name ? question : page).ToArray() };
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer(question.Name, ["2", "3"], false);
        attempt.Grade(1, DateTimeOffset.UtcNow);

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.Contains("&lt;img", html);
        Assert.Contains("Another selected answer", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("Support needs", html);
    }

    [Fact]
    public async Task RemovedAnswerOptionsAreExplicitlyReportedInsteadOfBeingSilentlyOmitted()
    {
        var (module, _) = CreateAssessment(false);
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("incorrect", ["removed-option"], false);
        attempt.Grade(1, DateTimeOffset.UtcNow);

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.Contains("This answer is no longer available in the module content.", html);
    }

    [Fact]
    public async Task FailedResultsRetakeLinkReturnsToPublishedAssessmentIntroduction()
    {
        var (module, attempt) = CreateAssessment(false);
        module = module with
        {
            Pages = [new TrainingPage("test-intro", "End of module test", "assessment_intro", ""), .. module.Pages]
        };

        var html = await RenderAsync(new AssessmentSummaryViewModel(module, attempt));

        Assert.Contains("href=\"/training/module-one/content/test-intro\"", html);
        Assert.DoesNotContain("href=\"/training/module-one/content/incorrect\"", html);
    }

    [Fact]
    public async Task CompletedOverviewRendersViewCertificateButton()
    {
        var (module, attempt) = CreateAssessment(true);
        module = module with
        {
            Pages = [.. module.Pages, new TrainingPage("certificate", "Certificate", "certificate", "")]
        };
        var model = new TrainingModuleDetailViewModel(
            module, 4, "topic", DateTimeOffset.UtcNow, attempt);

        var html = await RenderAsync(model, "/Views/Training/Module.cshtml");

        Assert.Contains("href=\"/training/module-one/content/certificate\">View certificate</a>", html);
        Assert.DoesNotContain(">Continue module</a>", html);
    }

    [Fact]
    public async Task FailedOverviewRendersRetakeButton()
    {
        var (module, attempt) = CreateAssessment(false);
        module = module with
        {
            Pages = [new TrainingPage("test-intro", "End of module test", "assessment_intro", ""), .. module.Pages]
        };
        var model = new TrainingModuleDetailViewModel(module, 3, "topic", LatestAssessment: attempt);

        var html = await RenderAsync(model, "/Views/Training/Module.cshtml");

        Assert.Contains("href=\"/training/module-one/content/test-intro\">Retake test</a>", html);
        Assert.DoesNotContain(">Continue module</a>", html);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SavedFormativeQuestionShowsRelevantGuidance(bool correct)
    {
        var question = FormativeQuestion();
        var module = new TrainingModule("module-one", "Module one", 1, false,
            [question, new TrainingPage("next", "Next topic", "text_page", "")]);
        var html = await RenderAsync(new TrainingPageViewModel(module, question, module.Pages[1], null)
            { SelectedAnswerIds = [correct ? "1" : "2"] }, "/Views/Training/Page.cshtml");

        Assert.Contains(correct ? "<strong>Success guidance</strong>" : "<strong>Failure guidance</strong>", html);
        Assert.DoesNotContain(correct ? "Failure guidance" : "Success guidance", html);
        Assert.Contains("href=\"/training/module-one/content/next\"", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("javascript:", html);
        if (!correct)
        {
            Assert.Contains("Correct answer", html);
            Assert.Contains("&lt;Support individual needs&gt;", html);
            Assert.Contains("Explanation", html);
        }
        else
        {
            Assert.DoesNotContain("<h2>Correct answer</h2>", html);
            Assert.DoesNotContain("<h2>Explanation</h2>", html);
        }
        Assert.Contains("disabled=\"disabled\"", html);
        Assert.DoesNotContain(">Check answer</button>", html);
    }

    [Fact]
    public async Task IncorrectMultiSelectLearningCheckListsEveryCorrectOption()
    {
        var question = FormativeQuestion() with
        {
            AllowsMultipleAnswers = true,
            CorrectAnswerIds = ["1", "3"]
        };
        var html = await RenderAsync(new FormativeFeedbackViewModel(question, false),
            "/Views/Training/_FormativeFeedback.cshtml");

        Assert.Contains("&lt;Support individual needs&gt;", html);
        Assert.Contains("Observe development", html);
        Assert.DoesNotContain("Ignore needs", html);
    }

    [Theory]
    [InlineData("formative")]
    [InlineData("summative")]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    public async Task UnansweredQuestionDoesNotExposeLearningCheckFeedback(string pageType)
    {
        var question = FormativeQuestion() with { PageType = pageType };
        var module = new TrainingModule("module-one", "Module one", 1, false, [question]);

        var html = await RenderAsync(new TrainingPageViewModel(module, question, null, null), "/Views/Training/Page.cshtml");

        Assert.DoesNotContain("Success guidance", html);
        Assert.DoesNotContain("Failure guidance", html);
        Assert.DoesNotContain("Explanation", html);
    }

    private static TrainingPage FormativeQuestion() =>
        new("learning-check", "Question", "formative", "How can you help?",
            [new("1", "<Support individual needs>"), new("2", "Ignore needs"), new("3", "Observe development")], ["1"],
            FailureMessage: "**Failure guidance**\n\n<script>alert(1)</script>",
            SuccessMessage: "**Success guidance**\n\n[Unsafe](javascript:alert%281%29)");

    [Fact]
    public async Task LearningCheckWithoutOptionalGuidanceStillOffersContinuation()
    {
        var question = FormativeQuestion() with { FailureMessage = "", SuccessMessage = "" };
        var module = new TrainingModule("module-one", "Module one", 1, false,
            [question, new TrainingPage("next", "Next", "text_page", "")]);

        var html = await RenderAsync(new TrainingPageViewModel(module, question, module.Pages[1], null)
            { SelectedAnswerIds = ["2"] }, "/Views/Training/Page.cshtml");

        Assert.Contains("Correct answer", html);
        Assert.DoesNotContain("<h2>Explanation</h2>", html);
        Assert.Contains("href=\"/training/module-one/content/next\"", html);
    }

    private static (TrainingModule Module, AssessmentAttempt Attempt) CreateAssessment(bool passed)
    {
        var module = new TrainingModule("module-one", "Module one", 1, false,
        [
            new("topic", "Topic", "text_page", "Learning material"),
            new("incorrect", "Question one", "summative", "Which approach supports development?",
                [new("1", "Support individual needs"), new("2", "Ignore individual needs"), new("3", "Unselected distractor")],
                ["1"], FailureMessage: "[Revisit the topic](/training/module-one/content/topic)\n\n<script>alert(1)</script>"),
            new("correct-one", "Question two", "summative", "Correct question body", [new("1", "Yes")], ["1"]),
            new("correct-two", "Question three", "summative", "Another assessment question", [new("1", "Yes")], ["1"]),
            new("correct-three", "Question four", "summative", "Another assessment question", [new("1", "Yes")], ["1"]),
            new("results", "Results", "assessment_results", ""),
            new("reflection", "Reflect", "confidence", "")
        ]);
        var attempt = new AssessmentAttempt(1, module.Name, DateTimeOffset.UtcNow);
        attempt.AddOrReplaceAnswer("incorrect", ["2"], false);
        foreach (var question in module.Pages.Where(page => page.Name.StartsWith("correct-", StringComparison.Ordinal)))
        {
            attempt.AddOrReplaceAnswer(question.Name, ["1"], passed || question.Name == "correct-one");
        }
        attempt.Grade(4, DateTimeOffset.UtcNow);
        return (module, attempt);
    }

    private async Task<string> RenderAsync<TModel>(
        TModel model, string viewPath = "/Views/Training/AssessmentResult.cshtml", bool accountEdit = false,
        bool researchPreference = false, IReadOnlyList<RegistrationChoice>? registrationRoles = null,
        string? summarySetting = null, string? summaryExperience = null, string? summaryError = null)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Scheme = "https";
        http.Request.Host = new HostString("localhost");
        http.SetEndpoint(new Endpoint(null, EndpointMetadataCollection.Empty, "Razor rendering test"));
        var routeData = new RouteData();
        routeData.Values["controller"] = "Training";
        routeData.Values["action"] = "AssessmentResult";
        var actionContext = new ActionContext(http, routeData, new ActionDescriptor());
        var engine = services.GetRequiredService<IRazorViewEngine>();
        var result = engine.GetView(null, viewPath, isMainPage: false);
        Assert.True(result.Success, string.Join(", ", result.SearchedLocations ?? []));
        using var writer = new StringWriter();
        var viewData = new ViewDataDictionary<TModel>(
            services.GetRequiredService<IModelMetadataProvider>(), new ModelStateDictionary()) { Model = model };
        viewData["IsAccountEdit"] = accountEdit;
        viewData["IsResearchPreference"] = researchPreference;
        viewData["Roles"] = registrationRoles;
        viewData["SettingName"] = summarySetting;
        viewData["ExperienceName"] = summaryExperience;
        if (summaryError is not null)
        {
            viewData.ModelState.AddModelError(string.Empty, summaryError);
        }
        var viewContext = new ViewContext(actionContext, result.View, viewData,
            new TempDataDictionary(http, services.GetRequiredService<ITempDataProvider>()),
            writer, new HtmlHelperOptions());
        await result.View.RenderAsync(viewContext);
        return writer.ToString();
    }
}
