using System.ComponentModel.DataAnnotations;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Training;
using EarlyYearsFoundationRecovery.Web.Content;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed record CertificateViewModel(string ModuleName, CertificateData Certificate);

public sealed record TrainingModuleViewModel(TrainingModule Module, string State, int VisitedPageCount);

public sealed record TrainingModuleDetailViewModel(TrainingModule Module, int VisitedPageCount, string? LastPageName = null)
{
    public TrainingPage? ResumePage =>
        Module.Pages.FirstOrDefault(page => page.Name == LastPageName) ?? Module.Pages.FirstOrDefault();
}

public sealed class TrainingAnswerModel
{
    public List<string> SelectedAnswerIds { get; set; } = [];
}

public sealed record TrainingPageViewModel(
    TrainingModule Module,
    TrainingPage Page,
    TrainingPage? NextPage,
    AssessmentAttempt? LatestAssessment)
{
    public bool IsConfidenceCheck => Page.PageType is "confidence" or "pre_confidence";

    public string Title => Page.PageType switch
    {
        "confidence" or "pre_confidence" => "Confidence check",
        "formative" => "Learning check",
        "summative" => "End of module test",
        _ => Page.Title
    };

    public IReadOnlyList<string> SelectedAnswerIds { get; init; } = [];

    public bool IsReadOnly =>
        (Page.PageType == "summative" && LatestAssessment?.Passed == true) ||
        (Page.PageType == "formative" && SelectedAnswerIds.Count > 0);

    public bool IsFormativeAnswerCorrect =>
        AssessmentGrader.IsCorrect(Page.CorrectAnswerIds ?? [], SelectedAnswerIds);

    public TrainingPage? PreviousPage
    {
        get
        {
            var index = Module.Pages.ToList().IndexOf(Page);
            return index > 0 ? Module.Pages[index - 1] : null;
        }
    }
}

public sealed class LearningNoteModel
{
    [StringLength(20_000)]
    public string Content { get; set; } = string.Empty;
}

public sealed record LearningNoteViewModel(string ModuleName, LearningNoteModel Note);

public sealed record AssessmentResultViewModel(
    TrainingModule Module,
    TrainingPage Question,
    AssessmentResult Result);

public sealed record AssessmentSummaryViewModel(TrainingModule Module, AssessmentAttempt Assessment)
{
    public TrainingPage? NextPage
    {
        get
        {
            var pages = Module.Pages.ToList();
            var index = pages.FindIndex(page => page.PageType == "assessment_results");
            if (index < 0)
            {
                index = pages.FindLastIndex(page => page.PageType == "summative");
            }

            return index >= 0 && index + 1 < pages.Count ? pages[index + 1] : null;
        }
    }

    public TrainingPage? FirstQuestion => Module.Pages.FirstOrDefault(page => page.PageType == "summative");
}
