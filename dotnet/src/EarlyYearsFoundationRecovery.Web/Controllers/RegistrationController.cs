using System.Security.Claims;
using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[Route("registration")]
public sealed class RegistrationController(
    AccountService accounts,
    IRegistrationOptionsProvider options) : Controller
{
    [HttpGet("terms-and-conditions")]
    public IActionResult Terms() => View(new TermsModel());

    [HttpPost("terms-and-conditions")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Terms(TermsModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        await accounts.UpdateTermsAsync(account, cancellationToken);
        return RedirectToAction(nameof(Name));
    }

    [HttpGet("name")]
    public async Task<IActionResult> Name(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        return account is null
            ? NotFound()
            : View(new NameModel { FirstName = account.FirstName ?? string.Empty, LastName = account.LastName ?? string.Empty });
    }

    [HttpPost("name")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Name(NameModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        await accounts.UpdateNameAsync(account, model.FirstName, model.LastName, cancellationToken);
        return RedirectToAction(nameof(Location));
    }

    [HttpGet("where-you-live")]
    public async Task<IActionResult> Location(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        ViewBag.Countries = options.GetCountries();
        var countryId = options.GetCountries()
            .SingleOrDefault(country => country.Name == account.Country)?.Id ?? string.Empty;
        return View(new LocationModel { Country = countryId });
    }

    [HttpPost("where-you-live")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Location(LocationModel model, CancellationToken cancellationToken)
    {
        var selectedCountry = options.GetCountries().SingleOrDefault(country => country.Id == model.Country);
        if (selectedCountry is null)
        {
            ModelState.AddModelError(nameof(model.Country), "Select a valid country.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Countries = options.GetCountries();
            return View(model);
        }

        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        await accounts.UpdateLocationAsync(account, selectedCountry!.Name, null, cancellationToken);
        return RedirectToAction(nameof(Setting));
    }

    [HttpGet("setting-type")]
    public async Task<IActionResult> Setting(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var settings = await options.GetSettingsAsync(cancellationToken);
        ViewBag.Settings = settings;
        return View(new SettingModel
        {
            SettingType = account.SettingType ?? string.Empty,
            SettingTypeOther = account.SettingTypeOther
        });
    }

    [HttpPost("setting-type")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Setting(SettingModel model, CancellationToken cancellationToken)
    {
        var settings = await options.GetSettingsAsync(cancellationToken);
        var isOther = string.Equals(model.SettingType, "other", StringComparison.OrdinalIgnoreCase);
        var selectedSetting = isOther
            ? new RegistrationSetting("other", "Other", true, "other")
            : settings.SingleOrDefault(setting => setting.Id == model.SettingType);
        if (selectedSetting is null)
        {
            ModelState.AddModelError(nameof(model.SettingType), "Select a valid setting type.");
        }

        if (isOther && string.IsNullOrWhiteSpace(model.SettingTypeOther))
        {
            ModelState.AddModelError(nameof(model.SettingTypeOther), "Describe your setting.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Settings = settings;
            return View(model);
        }

        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        await accounts.UpdateSettingAsync(
            account,
            selectedSetting!.Id,
            model.SettingTypeOther,
            selectedSetting.RequiresLocalAuthority,
            selectedSetting.RoleGroup,
            cancellationToken);
        return RedirectToAction(nameof(LocalAuthority));
    }

    [HttpGet("local-authority")]
    public async Task<IActionResult> LocalAuthority(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (!RequiresLocalAuthority(account))
        {
            return RedirectToAction(nameof(Role));
        }

        ViewBag.LocalAuthorities = options.GetLocalAuthorities();
        return View(new LocalAuthorityModel
        {
            LocalAuthority = account.LocalAuthority ?? string.Empty
        });
    }

    [HttpPost("local-authority")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> LocalAuthority(
        LocalAuthorityModel model,
        CancellationToken cancellationToken)
    {
        var authority = options.GetLocalAuthorities()
            .SingleOrDefault(item => item.Id == model.LocalAuthority);
        if (authority is null)
        {
            ModelState.AddModelError(nameof(model.LocalAuthority), "Select a valid local authority.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.LocalAuthorities = options.GetLocalAuthorities();
            return View(model);
        }

        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        await accounts.UpdateLocalAuthorityAsync(account, authority!.Name, cancellationToken);
        return RedirectToAction(nameof(Role));
    }

    [HttpGet("role-type")]
    public async Task<IActionResult> Role(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (account.RoleGroup == "none")
        {
            return RedirectToAction(nameof(Preferences));
        }

        ViewBag.Roles = options.GetRoles(account.RoleGroup ?? "other");
        return View(new RoleModel
        {
            RoleType = account.RoleType ?? string.Empty,
            RoleTypeOther = account.RoleTypeOther
        });
    }

    [HttpPost("role-type")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Role(RoleModel model, CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (account.RoleGroup == "none")
        {
            return RedirectToAction(nameof(Preferences));
        }

        var roles = options.GetRoles(account.RoleGroup ?? "other");
        if (!roles.Any(role => role.Name == model.RoleType))
        {
            ModelState.AddModelError(nameof(model.RoleType), "Select a valid role.");
        }

        var isOther = string.Equals(model.RoleType, "other", StringComparison.OrdinalIgnoreCase);
        if (isOther && string.IsNullOrWhiteSpace(model.RoleTypeOther))
        {
            ModelState.AddModelError(nameof(model.RoleTypeOther), "Describe your role.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Roles = roles;
            return View(model);
        }

        await accounts.UpdateRoleAsync(
            account,
            model.RoleType,
            model.RoleTypeOther,
            cancellationToken);
        return RedirectToAction(nameof(Experience));
    }

    [HttpGet("early-years-experience")]
    public async Task<IActionResult> Experience(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (account.RoleGroup == "none")
        {
            return RedirectToAction(nameof(Preferences));
        }

        ViewBag.Experiences = options.GetExperiences();
        return View(new ExperienceModel
        {
            EarlyYearsExperience = account.EarlyYearsExperience ?? string.Empty
        });
    }

    [HttpPost("early-years-experience")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Experience(ExperienceModel model, CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (account.RoleGroup == "none")
        {
            return RedirectToAction(nameof(Preferences));
        }

        if (!options.GetExperiences().Any(experience => experience.Id == model.EarlyYearsExperience))
        {
            ModelState.AddModelError(nameof(model.EarlyYearsExperience), "Select a valid experience option.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.Experiences = options.GetExperiences();
            return View(model);
        }

        await accounts.UpdateExperienceAsync(account, model.EarlyYearsExperience, cancellationToken);
        return RedirectToAction(nameof(Preferences));
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> Preferences(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        return account is null
            ? NotFound()
            : View(new PreferencesModel
            {
                TrainingEmails = account.TrainingEmails,
                ResearchParticipant = account.ResearchParticipant
            });
    }

    [HttpPost("preferences")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preferences(PreferencesModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        await accounts.UpdatePreferencesAsync(
            account,
            model.TrainingEmails!.Value,
            model.ResearchParticipant!.Value,
            cancellationToken);
        return RedirectToAction(nameof(CheckAnswers));
    }

    [HttpGet("check-your-answers")]
    public async Task<IActionResult> CheckAnswers(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var setting = (await options.GetSettingsAsync(cancellationToken))
            .SingleOrDefault(item => item.Id == account.SettingType);
        ViewBag.SettingName = account.SettingType == "other"
            ? account.SettingTypeOther
            : setting?.Name ?? account.SettingType;
        return View(account);
    }

    [HttpPost("check-your-answers")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CompleteRegistration(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (!await accounts.CompleteRegistrationAsync(account, cancellationToken))
        {
            ModelState.AddModelError(string.Empty, "Complete the required registration steps before continuing.");
            return View("CheckAnswers", account);
        }

        return RedirectToAction("Index", "Training");
    }

    private static bool RequiresLocalAuthority(UserAccount account) =>
        account.SettingRequiresLocalAuthority &&
        string.Equals(account.Country, "England", StringComparison.OrdinalIgnoreCase);

    private async Task<UserAccount?> CurrentAccountAsync(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(subject)
            ? null
            : await accounts.FindAsync(subject, cancellationToken);
    }
}
