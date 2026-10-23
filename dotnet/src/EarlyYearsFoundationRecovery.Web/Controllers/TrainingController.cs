using System.Security.Claims;
using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[Route("training")]
public sealed class TrainingController(
    AccountService accounts,
    TrainingJourneyService training) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }

        var modules = await training.GetModulesAsync(account, cancellationToken);
        return View(modules
            .Where(item => !item.Module.IsDraft)
            .Select(item => new TrainingModuleViewModel(
                item.Module,
                item.State,
                item.VisitedPageCount))
            .ToArray());
    }

    [HttpGet("{moduleName}")]
    public async Task<IActionResult> Module(string moduleName, CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }

        var module = await training.GetModuleAsync(moduleName, cancellationToken);
        if (module is null || module.IsDraft)
        {
            return NotFound();
        }

        await training.RecordStartAsync(account, moduleName, cancellationToken);
        var listing = (await training.GetModulesAsync(account, cancellationToken))
            .SingleOrDefault(item => item.Module.Name == moduleName);
        return View(new TrainingModuleDetailViewModel(module, listing?.VisitedPageCount ?? 0));
    }

    [HttpGet("{moduleName}/content/{pageName}")]
    public async Task<IActionResult> Page(
        string moduleName,
        string pageName,
        CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var module = await training.GetModuleAsync(moduleName, cancellationToken);
        var page = module?.Pages.SingleOrDefault(item => item.Name == pageName);
        if (module is null || module.IsDraft || page is null)
        {
            return NotFound();
        }

        await training.RecordPageViewAsync(account, moduleName, pageName, cancellationToken);
        if (page.PageType == "thankyou")
        {
            await training.CompleteModuleAsync(account, moduleName, cancellationToken);
        }

        var index = module.Pages.ToList().IndexOf(page);
        var nextPage = index >= 0 && index + 1 < module.Pages.Count
            ? module.Pages[index + 1]
            : null;
        var latestAssessment = await training.GetLatestAssessmentAsync(account, moduleName, cancellationToken);
        return View("Page", new TrainingPageViewModel(module, page, nextPage, latestAssessment));
    }

    [HttpPost("{moduleName}/questions/{questionName}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Answer(
        string moduleName,
        string questionName,
        TrainingAnswerModel model,
        CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var module = await training.GetModuleAsync(moduleName, cancellationToken);
        var question = module?.Pages.SingleOrDefault(item => item.Name == questionName);
        if (module is null || question is null)
        {
            return NotFound();
        }

        AssessmentResult result;
        try
        {
            result = await training.SubmitAnswerAsync(
                account,
                moduleName,
                questionName,
                model.SelectedAnswerIds,
                cancellationToken);
        }
        catch (ArgumentException exception)
        {
            ModelState.AddModelError(nameof(model.SelectedAnswerIds), exception.Message);
            var index = module.Pages.ToList().IndexOf(question);
            var nextPage = index >= 0 && index + 1 < module.Pages.Count ? module.Pages[index + 1] : null;
            var latestAssessment = await training.GetLatestAssessmentAsync(account, moduleName, cancellationToken);
            return View("Page", new TrainingPageViewModel(module, question, nextPage, latestAssessment));
        }
        catch (InvalidOperationException)
        {
            return NotFound();
        }

        return View("AnswerResult", new AssessmentResultViewModel(module, question, result));
    }

    private async Task<UserAccount?> CurrentAccountAsync(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(subject)
            ? null
            : await accounts.FindAsync(subject, cancellationToken);
    }
}
