using System.Security.Claims;
using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[Route("my-account/learning-log")]
public sealed class LearningLogController(
    AccountService accounts,
    TrainingJourneyService training) : Controller
{
    [HttpGet("{moduleName}")]
    public async Task<IActionResult> Show(string moduleName, CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var module = await training.GetModuleAsync(moduleName, cancellationToken);
        if (module is null || module.IsDraft)
        {
            return NotFound();
        }

        var note = await training.GetNoteAsync(account, moduleName, cancellationToken);
        return View(new LearningNoteViewModel(
            moduleName,
            new LearningNoteModel { Content = note?.Content ?? string.Empty }));
    }

    [HttpPost("{moduleName}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        string moduleName,
        LearningNoteModel model,
        CancellationToken cancellationToken)
    {
        var account = await CurrentAccountAsync(cancellationToken);
        if (account is null)
        {
            return NotFound();
        }

        var module = await training.GetModuleAsync(moduleName, cancellationToken);
        if (module is null || module.IsDraft)
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View("Show", new LearningNoteViewModel(moduleName, model));
        }

        await training.SaveNoteAsync(account, moduleName, model.Content, cancellationToken);
        TempData["Status"] = "Your learning log has been saved.";
        return RedirectToAction(nameof(Show), new { moduleName });
    }

    private async Task<UserAccount?> CurrentAccountAsync(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(subject)
            ? null
            : await accounts.FindAsync(subject, cancellationToken);
    }
}
