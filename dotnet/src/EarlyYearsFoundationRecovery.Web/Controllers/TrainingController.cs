using System.Security.Claims;
using EarlyYearsFoundationRecovery.Application.Accounts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Accounts;
using EarlyYearsFoundationRecovery.Web.Models;
using EarlyYearsFoundationRecovery.Web.Content;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EarlyYearsFoundationRecovery.Web.Controllers;

[Authorize]
[Route("training")]
public sealed class TrainingController(
    AccountService accounts,
    TrainingJourneyService training,
    ICertificatePdfRenderer certificatePdf) : Controller
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
        var models = new List<TrainingModuleViewModel>();
        foreach (var item in modules)
        {
            var assessment = item.State == "in-progress"
                ? await training.GetLatestAssessmentAsync(account, item.Module.Name, cancellationToken)
                : null;
            models.Add(new TrainingModuleViewModel(
                item.Module,
                item.State,
                item.VisitedPageCount)
            {
                CompletedAt = item.CompletedAt,
                LatestAssessment = assessment
            });
        }
        return View(models.ToArray());
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

        var listing = (await training.GetModulesAsync(account, cancellationToken))
            .SingleOrDefault(item => item.Module.Name == moduleName);
        var completedAt = await training.GetCompletionDateAsync(account, moduleName, cancellationToken);
        var latestAssessment = await training.GetLatestAssessmentAsync(account, moduleName, cancellationToken);
        return View(new TrainingModuleDetailViewModel(
            module, listing?.VisitedPageCount ?? 0, listing?.LastPageName, completedAt, latestAssessment)
        {
            VisitedPageNames = listing?.VisitedPageNames ?? []
        });
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

        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }

        var module = await training.GetModuleAsync(moduleName, cancellationToken);
        var page = module?.Pages.SingleOrDefault(item => item.Name == pageName);
        if (module is null || module.IsDraft || page is null)
        {
            return NotFound();
        }

        if (page.PageType == "assessment_results")
        {
            return RedirectToAction(nameof(AssessmentResult), new { moduleName });
        }

        if (page.PageType == "thankyou" &&
            !await training.CompleteModuleAsync(account, moduleName, cancellationToken))
        {
            TempData["TrainingNotice"] = "Pass the end of module test before completing this module.";
            return RedirectToAction(nameof(Module), new { moduleName });
        }

        if (page.PageType == "certificate")
        {
            return RedirectToAction(nameof(Certificate), new { moduleName });
        }

        await training.RecordPageViewAsync(account, moduleName, pageName, cancellationToken);
        if (page.PageType is "confidence" or "pre_confidence")
        {
            await training.RecordConfidenceCheckQuestionViewAsync(
                account, moduleName, pageName, cancellationToken);
        }

        var index = module.Pages.ToList().IndexOf(page);
        var nextPage = index >= 0 && index + 1 < module.Pages.Count
            ? module.Pages[index + 1]
            : null;
        var latestAssessment = await training.GetLatestAssessmentAsync(account, moduleName, cancellationToken);
        var selectedAnswers = page.PageType is "confidence" or "pre_confidence" or "formative"
            ? await training.GetSelectedAnswersAsync(account, moduleName, pageName, cancellationToken)
            : latestAssessment?.Answers.SingleOrDefault(answer => answer.QuestionName == pageName)?.SelectedAnswerIds.ToArray() ?? [];
        return View("Page", new TrainingPageViewModel(module, page, nextPage, latestAssessment)
        {
            SelectedAnswerIds = selectedAnswers,
            Note = new LearningNoteModel
            {
                Content = page.SupportsNotes
                    ? (await training.GetPageNoteAsync(account, moduleName, pageName, cancellationToken))?.Content ?? ""
                    : ""
            }
        });
    }

    [HttpPost("{moduleName}/content/{pageName}/notes")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNote(
        string moduleName,
        string pageName,
        [Bind(Prefix = "Note")] LearningNoteModel note,
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
        var page = module?.Pages.SingleOrDefault(item => item.Name == pageName);
        if (module is null || module.IsDraft || page?.SupportsNotes != true)
        {
            return NotFound();
        }
        var index = module.Pages.ToList().IndexOf(page);
        var nextPage = module.Pages.Skip(index + 1).FirstOrDefault();
        if (!ModelState.IsValid)
        {
            return View("Page", new TrainingPageViewModel(module, page, nextPage, null) { Note = note });
        }

        await training.SavePageNoteAsync(account, moduleName, pageName, note.Content, cancellationToken);
        return nextPage is null
            ? RedirectToAction(nameof(Module), new { moduleName })
            : RedirectToAction(nameof(Page), new { moduleName, pageName = nextPage.Name });
    }

    [HttpGet("{moduleName}/certificate")]
    [HttpGet("{moduleName}/certificate.pdf")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    public async Task<IActionResult> Certificate(string moduleName, CancellationToken cancellationToken)
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
        if (module is null || module.IsDraft || !module.Pages.Any(page => page.PageType == "certificate"))
        {
            return NotFound();
        }

        var completedAt = await training.GetCompletionDateAsync(account, moduleName, cancellationToken);
        if (completedAt is null)
        {
            TempData["TrainingNotice"] = "Complete this module before viewing your certificate.";
            return RedirectToAction(nameof(Module), new { moduleName });
        }

        var certificate = new CertificateData(
            $"{account.FirstName} {account.LastName}", module.Title, module.Criteria, completedAt.Value);
        if (Request.Path.Value?.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase) == true)
        {
            var pdf = await certificatePdf.GenerateAsync(certificate, cancellationToken);
            return File(pdf, "application/pdf", "certificate-of-achievement.pdf");
        }

        var certificatePage = module.Pages.First(page => page.PageType == "certificate");
        await training.RecordPageViewAsync(account, moduleName, certificatePage.Name, cancellationToken);
        return View(new CertificateViewModel(moduleName, certificate));
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

        if (!account.IsRegistrationComplete)
        {
            return RedirectToAction("Terms", "Registration");
        }

        var module = await training.GetModuleAsync(moduleName, cancellationToken);
        var question = module?.Pages.SingleOrDefault(item => item.Name == questionName);
        if (module is null || module.IsDraft || question is null ||
            question.PageType is not ("formative" or "summative" or "confidence" or "pre_confidence"))
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
        catch (ArgumentException exception) when (exception.ParamName == "selectedAnswerIds")
        {
            ModelState.AddModelError(nameof(model.SelectedAnswerIds), "Select a valid answer before continuing.");
            var index = module.Pages.ToList().IndexOf(question);
            var nextPage = index >= 0 && index + 1 < module.Pages.Count ? module.Pages[index + 1] : null;
            var latestAssessment = await training.GetLatestAssessmentAsync(account, moduleName, cancellationToken);
            return View("Page", new TrainingPageViewModel(module, question, nextPage, latestAssessment)
            {
                SelectedAnswerIds = model.SelectedAnswerIds
            });
        }

        if (question.PageType is "confidence" or "pre_confidence" ||
            (question.PageType == "summative" && !result.IsComplete))
        {
            var index = module.Pages.ToList().IndexOf(question);
            return index + 1 < module.Pages.Count
                ? RedirectToAction(nameof(Page), new { moduleName, pageName = module.Pages[index + 1].Name })
                : RedirectToAction(nameof(Module), new { moduleName });
        }

        if (question.PageType == "summative")
        {
            return RedirectToAction(nameof(AssessmentResult), new { moduleName });
        }

        return RedirectToAction(nameof(Page), new { moduleName, pageName = questionName });
    }

    [HttpGet("{moduleName}/assessment-result")]
    public async Task<IActionResult> AssessmentResult(string moduleName, CancellationToken cancellationToken)
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

        var assessment = await training.GetLatestAssessmentAsync(account, moduleName, cancellationToken);
        if (assessment?.CompletedAt is null)
        {
            TempData["TrainingNotice"] = "Answer all questions in the end of module test to see your result.";
            return RedirectToAction(nameof(Module), new { moduleName });
        }

        var resultsPage = module.Pages.FirstOrDefault(page => page.PageType == "assessment_results");
        if (resultsPage is not null)
        {
            await training.RecordPageViewAsync(account, moduleName, resultsPage.Name, cancellationToken);
        }

        return View(new AssessmentSummaryViewModel(module, assessment));
    }

    private async Task<UserAccount?> CurrentAccountAsync(CancellationToken cancellationToken)
    {
        var subject = User.FindFirstValue("sub");
        return string.IsNullOrWhiteSpace(subject)
            ? null
            : await accounts.FindAsync(subject, cancellationToken);
    }
}
