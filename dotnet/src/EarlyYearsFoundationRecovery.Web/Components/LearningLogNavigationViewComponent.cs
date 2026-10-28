using System.Security.Claims;
using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Application.Training;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Components;

public sealed class LearningLogNavigationViewComponent(
    AccountService accounts,
    TrainingJourneyService training) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(CancellationToken cancellationToken = default)
    {
        var subject = HttpContext.User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(subject))
        {
            return Content(string.Empty);
        }

        var account = await accounts.FindAsync(subject, cancellationToken);
        if (account is null ||
            !await training.HasStartedPublishedModuleAsync(account, cancellationToken))
        {
            return Content(string.Empty);
        }

        return View();
    }
}
