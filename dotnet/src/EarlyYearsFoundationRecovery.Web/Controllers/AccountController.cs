using System.Security.Claims;
using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Route("account")]
public sealed class AccountController(
    AccountService accounts,
    IWebHostEnvironment environment,
    IRegistrationOptionsProvider options) : Controller
{
    [AllowAnonymous]
    [HttpGet("sign-in")]
    public IActionResult SignIn()
    {
        if (!environment.IsDevelopment())
        {
            return Challenge(
                new AuthenticationProperties { RedirectUri = Url.Action(nameof(Index)) },
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        return View();
    }

    [AllowAnonymous]
    [HttpPost("development-sign-in")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DevelopmentSignIn(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        const string subject = "local-development-user";
        const string email = "local-user@example.test";
        await accounts.EnsureAccountAsync(subject, email, cancellationToken);
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", subject),
            new Claim("email", email)
        ],
        CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        return account is null ? NotFound() : View(account);
    }

    [Authorize]
    [HttpGet("name")]
    public async Task<IActionResult> Name(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        ViewData["IsAccountEdit"] = true;
        return View("../Registration/Name", new NameModel
        {
            FirstName = account.FirstName ?? string.Empty,
            LastName = account.LastName ?? string.Empty
        });
    }

    [Authorize]
    [HttpPost("name")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Name(NameModel model, CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        if (!ModelState.IsValid)
        {
            ViewData["IsAccountEdit"] = true;
            return View("../Registration/Name", model);
        }
        await accounts.UpdateNameAsync(account, model.FirstName, model.LastName, cancellationToken);
        TempData["Status"] = "You have updated your details.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpGet("email-preferences")]
    public Task<IActionResult> EmailPreferences(CancellationToken cancellationToken) =>
        EditPreferenceAsync(false, cancellationToken);

    [Authorize]
    [HttpGet("role")]
    public async Task<IActionResult> Role(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        if (account.RoleGroup == "none")
        {
            return RedirectToAction(nameof(Index));
        }
        var model = new AccountRoleModel
        {
            RoleType = account.RoleType ?? string.Empty,
            RoleTypeOther = account.RoleTypeOther,
            EarlyYearsExperience = account.EarlyYearsExperience ?? string.Empty
        };
        PopulateRoleChoices(model, account);
        return View(model);
    }

    [Authorize]
    [HttpGet("setting")]
    public async Task<IActionResult> Setting(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        return View(new AccountSettingModel
        {
            SettingType = account.SettingType ?? string.Empty,
            SettingTypeOther = account.SettingTypeOther,
            Settings = await options.GetSettingsAsync(cancellationToken)
        });
    }

    [Authorize]
    [HttpPost("setting")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setting(
        AccountSettingModel model, string? stage, CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        if (stage is not ("details" or "save"))
        {
            return BadRequest();
        }
        model.Settings = await options.GetSettingsAsync(cancellationToken);
        var setting = model.SettingType == "other"
            ? new RegistrationSetting("other", "Other", true, "other")
            : model.Settings.SingleOrDefault(item => item.Id == model.SettingType);
        if (setting is null)
        {
            ModelState.AddModelError(nameof(model.SettingType), "Select a valid setting type.");
        }
        if (model.SettingType == "other" && string.IsNullOrWhiteSpace(model.SettingTypeOther))
        {
            ModelState.AddModelError(nameof(model.SettingTypeOther), "Describe your setting.");
        }
        if (setting is null ||
            ModelState[nameof(model.SettingType)]?.Errors.Count > 0 ||
            ModelState[nameof(model.SettingTypeOther)]?.Errors.Count > 0)
        {
            return View(model);
        }
        model.SelectedSetting = setting;
        model.ShowDetails = true;
        model.RequiresLocalAuthority = setting.RequiresLocalAuthority &&
            string.Equals(account.Country, "England", StringComparison.OrdinalIgnoreCase);
        model.LocalAuthorities = options.GetLocalAuthorities();
        model.Roles = setting.RoleGroup == "none" ? [] : options.GetRoleChoices(setting.RoleGroup);
        model.Experiences = options.GetExperiences();

        if (stage == "details")
        {
            var unchangedSetting = account.SettingType == setting.Id;
            model.LocalAuthority = unchangedSetting
                ? model.LocalAuthorities.SingleOrDefault(item => item.Name == account.LocalAuthority)?.Id
                : null;
            model.RoleType = unchangedSetting ? account.RoleType : null;
            model.RoleTypeOther = unchangedSetting ? account.RoleTypeOther : null;
            model.EarlyYearsExperience = unchangedSetting ? account.EarlyYearsExperience : null;
            foreach (var field in new[]
            {
                nameof(model.LocalAuthority), nameof(model.RoleType),
                nameof(model.RoleTypeOther), nameof(model.EarlyYearsExperience)
            })
            {
                ModelState.Remove(field);
            }
            return View(model);
        }

        var authority = model.RequiresLocalAuthority
            ? model.LocalAuthorities.SingleOrDefault(item => item.Id == model.LocalAuthority)
            : null;
        if (model.RequiresLocalAuthority && authority is null)
        {
            ModelState.AddModelError(nameof(model.LocalAuthority), "Select a valid local authority.");
        }
        if (setting.RoleGroup != "none")
        {
            ValidateRoleChoices(model.RoleType, model.RoleTypeOther, model.EarlyYearsExperience,
                model.Roles, model.Experiences);
        }
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        await accounts.UpdateSettingDetailsAsync(
            account, setting, model.SettingTypeOther?.Trim(), authority?.Name,
            model.RoleType, model.RoleTypeOther?.Trim(), model.EarlyYearsExperience, cancellationToken);
        TempData["Status"] = "You have updated your details.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpPost("role")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Role(AccountRoleModel model, CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        if (account.RoleGroup == "none")
        {
            return RedirectToAction(nameof(Index));
        }
        PopulateRoleChoices(model, account);
        ValidateRoleChoices(model.RoleType, model.RoleTypeOther, model.EarlyYearsExperience,
            model.Roles, model.Experiences);
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        await accounts.UpdateRoleAndExperienceAsync(
            account, model.RoleType, model.RoleTypeOther?.Trim(), model.EarlyYearsExperience, cancellationToken);
        TempData["Status"] = "You have updated your details.";
        return RedirectToAction(nameof(Index));
    }

    private void PopulateRoleChoices(AccountRoleModel model, UserAccount account)
    {
        model.Roles = options.GetRoleChoices(account.RoleGroup ?? "other");
        model.Experiences = options.GetExperiences();
    }

    private void ValidateRoleChoices(
        string? roleType,
        string? roleOther,
        string? experience,
        IReadOnlyList<RegistrationChoice> roles,
        IReadOnlyList<RegistrationChoice> experiences)
    {
        if (ModelState[nameof(AccountRoleModel.RoleType)]?.Errors.Count is not > 0 &&
            !roles.Any(role => role.Name == roleType))
        {
            ModelState.AddModelError(nameof(AccountRoleModel.RoleType), "Select a valid role.");
        }
        if (roleType == "other" && string.IsNullOrWhiteSpace(roleOther))
        {
            ModelState.AddModelError(nameof(AccountRoleModel.RoleTypeOther), "Describe your role.");
        }
        if (ModelState[nameof(AccountRoleModel.EarlyYearsExperience)]?.Errors.Count is not > 0 &&
            !experiences.Any(item => item.Id == experience))
        {
            ModelState.AddModelError(nameof(AccountRoleModel.EarlyYearsExperience), "Select a valid experience option.");
        }
    }

    [Authorize]
    [HttpGet("location")]
    public async Task<IActionResult> Location(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        var model = new AccountLocationModel
        {
            Country = options.GetCountries().SingleOrDefault(item => item.Name == account.Country)?.Id ?? string.Empty,
            LocalAuthority = options.GetLocalAuthorities()
                .SingleOrDefault(item => item.Name == account.LocalAuthority)?.Id
        };
        PopulateLocationChoices(model, account);
        return View(model);
    }

    [Authorize]
    [HttpPost("location")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Location(AccountLocationModel model, CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        PopulateLocationChoices(model, account);
        var country = model.Countries.SingleOrDefault(item => item.Id == model.Country);
        if (country is null)
        {
            ModelState.AddModelError(nameof(model.Country), "Select a valid country.");
        }
        var needsAuthority = account.SettingRequiresLocalAuthority && country?.Name == "England";
        var authority = needsAuthority
            ? model.LocalAuthorities.SingleOrDefault(item => item.Id == model.LocalAuthority)
            : null;
        if (needsAuthority && authority is null)
        {
            ModelState.AddModelError(nameof(model.LocalAuthority), "Select a valid local authority.");
        }
        if (!ModelState.IsValid)
        {
            return View(model);
        }
        await accounts.UpdateLocationAsync(account,
            country?.Name ?? throw new InvalidOperationException("A valid country is required."),
            authority?.Name, cancellationToken);
        TempData["Status"] = "You have updated your details.";
        return RedirectToAction(nameof(Index));
    }

    private void PopulateLocationChoices(AccountLocationModel model, UserAccount account)
    {
        model.Countries = options.GetCountries();
        model.LocalAuthorities = options.GetLocalAuthorities();
        model.SettingRequiresLocalAuthority = account.SettingRequiresLocalAuthority;
    }

    [Authorize]
    [HttpPost("email-preferences")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> EmailPreferences(AccountPreferenceModel model, CancellationToken cancellationToken) =>
        SavePreferenceAsync(model, false, cancellationToken);

    [Authorize]
    [HttpGet("research-preferences")]
    public Task<IActionResult> ResearchPreferences(CancellationToken cancellationToken) =>
        EditPreferenceAsync(true, cancellationToken);

    [Authorize]
    [HttpPost("research-preferences")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ResearchPreferences(AccountPreferenceModel model, CancellationToken cancellationToken) =>
        SavePreferenceAsync(model, true, cancellationToken);

    private async Task<IActionResult> EditPreferenceAsync(bool research, CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        ViewData["IsResearchPreference"] = research;
        return View("Preference", new AccountPreferenceModel
        {
            Selected = research ? account.ResearchParticipant : account.TrainingEmails
        });
    }

    private async Task<IActionResult> SavePreferenceAsync(
        AccountPreferenceModel model, bool research, CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }
        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }
        if (model.Selected is null && !ModelState.ContainsKey(nameof(model.Selected)))
        {
            ModelState.AddModelError(nameof(model.Selected), "Select yes or no.");
        }
        if (!ModelState.IsValid)
        {
            ViewData["IsResearchPreference"] = research;
            return View("Preference", model);
        }
        var selected = model.Selected ?? throw new InvalidOperationException("A preference selection is required.");
        var trainingEmails = research
            ? account.TrainingEmails ?? throw new InvalidOperationException("A registered account must have an email preference.")
            : selected;
        var researchParticipant = research
            ? selected
            : account.ResearchParticipant ?? throw new InvalidOperationException("A registered account must have a research preference.");
        await accounts.UpdatePreferencesAsync(account, trainingEmails, researchParticipant, cancellationToken);
        TempData["Status"] = "You have updated your details.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize]
    [HttpGet("close")]
    public IActionResult Close() => View(new CloseAccountModel());

    [Authorize]
    [HttpPost("close")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Close(CloseAccountModel model, CancellationToken cancellationToken)
    {
        if (model.Reason is not ("not_using" or "technical_issue" or "other"))
        {
            ModelState.AddModelError(nameof(model.Reason), "Select a reason for closing your account.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        await accounts.SetCloseReasonAsync(account, model.Reason, model.CustomReason, cancellationToken);
        return RedirectToAction(nameof(ConfirmClose));
    }

    [Authorize]
    [HttpGet("close/confirm")]
    public async Task<IActionResult> ConfirmClose(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        return string.IsNullOrWhiteSpace(account.ClosedReason)
            ? RedirectToAction(nameof(Close))
            : View(account);
    }

    [Authorize]
    [HttpPost("close/confirm")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmClosePost(CancellationToken cancellationToken)
    {
        var account = await GetCurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(account.ClosedReason))
        {
            return RedirectToAction(nameof(Close));
        }

        await accounts.CloseAsync(account, cancellationToken);
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Closed));
    }

    [AllowAnonymous]
    [HttpGet("closed")]
    public IActionResult Closed() => View();

    [Authorize]
    [HttpPost("sign-out")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOutUser()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        if (!environment.IsDevelopment())
        {
            return SignOut(
                new AuthenticationProperties { RedirectUri = Url.Action(nameof(SignIn), "Account", null, Request.Scheme) },
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        return RedirectToAction(nameof(SignIn));
    }

    private Task<UserAccount?> GetCurrentAccountAsync(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(subject)
            ? Task.FromResult<UserAccount?>(null)
            : accounts.FindAsync(subject, cancellationToken);
    }
}
