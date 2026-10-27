using System.ComponentModel.DataAnnotations;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Training;
using EarlyYearsFoundationRecovery.Web.Content;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed record CertificateViewModel(string ModuleName, CertificateData Certificate);

public sealed record TrainingModuleViewModel(TrainingModule Module, string State, int VisitedPageCount);

public sealed record TrainingModuleDetailViewModel(
    TrainingModule Module,
    int VisitedPageCount,
    string? LastPageName = null,
    DateTimeOffset? CompletedAt = null,
    AssessmentAttempt? LatestAssessment = null)
{
    public TrainingPage? ResumePage
    {
        get
        {
            if (CompletedAt is not null && Module.Pages.FirstOrDefault(page => page.PageType == "certificate") is { } certificate)
            {
                return certificate;
            }
            if (LatestAssessment?.Passed == false)
            {
                return Module.Pages.FirstOrDefault(page => page.PageType == "assessment_intro")
                    ?? Module.Pages.FirstOrDefault(page => page.PageType == "summative");
            }
            return Module.Pages.FirstOrDefault(page => page.Name == LastPageName) ?? Module.Pages.FirstOrDefault();
        }
    }

    public string ResumeLabel => ResumePage?.PageType == "certificate" && CompletedAt is not null
        ? "View certificate"
        : LatestAssessment?.Passed == false
            ? "Retake test"
            : VisitedPageCount == 0 ? "Start module" : "Continue module";
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

public sealed record FormativeFeedbackViewModel(TrainingPage Question, bool Correct)
{
    public string Guidance => Correct ? Question.SuccessMessage : Question.FailureMessage;

    public IReadOnlyList<string> CorrectAnswers => (Question.Options ?? [])
        .Where(option => Question.CorrectAnswerIds?.Contains(option.Id) == true)
        .Select(option => option.Label)
        .ToArray();
}

public sealed record AssessmentSummaryViewModel(TrainingModule Module, AssessmentAttempt Assessment)
{
    public IReadOnlyList<IncorrectAnswerViewModel> IncorrectAnswers =>
        Assessment.CompletedAt is null
            ? []
            : Module.Pages
                .Where(page => page.PageType == "summative")
                .Select(page => (Question: page, Answer: Assessment.Answers.SingleOrDefault(answer =>
                    answer.QuestionName == page.Name && !answer.IsCorrect)))
                .Where(item => item.Answer is not null)
                .Select(item => new IncorrectAnswerViewModel(item.Question,
                    item.Answer!.SelectedAnswerIds.Select(id =>
                        item.Question.Options?.SingleOrDefault(option => option.Id == id)?.Label
                        ?? "This answer is no longer available in the module content.").ToArray()))
                .ToArray();

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

    public TrainingPage? RetakePage =>
        Module.Pages.FirstOrDefault(page => page.PageType == "assessment_intro") ?? FirstQuestion;
}

public sealed record IncorrectAnswerViewModel(TrainingPage Question, IReadOnlyList<string> SelectedAnswers);
