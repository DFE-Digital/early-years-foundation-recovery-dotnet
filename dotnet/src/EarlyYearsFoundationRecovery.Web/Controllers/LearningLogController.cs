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

        var groups = await training.GetLearningLogAsync(account, cancellationToken);
        return View(new LearningLogViewModel(groups.Select(group => new LearningLogModuleViewModel(
            group.Module.Name,
            group.Module.Title,
            group.Notes.Select(note => new LearningLogEntryViewModel(
                string.IsNullOrWhiteSpace(note.Title) ? "Earlier module note" : note.Title,
                group.Module.Pages.Any(page => page.Name == note.PageName) ? note.PageName : null,
                note.Content,
                note.UpdatedAt,
                string.IsNullOrEmpty(note.PageName))).ToArray())).ToArray()));
    }

    [HttpGet("{moduleName}")]
    public async Task<IActionResult> Show(string moduleName, CancellationToken cancellationToken)
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

        var note = await training.GetNoteAsync(account, moduleName, cancellationToken);
        return View(new LearningNoteViewModel(
            moduleName,
            new LearningNoteModel { Content = note?.Content ?? string.Empty }));
    }

    [HttpPost("{moduleName}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(
        string moduleName,
        [Bind(Prefix = "Note")] LearningNoteModel model,
        CancellationToken cancellationToken)
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
