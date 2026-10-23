using System.ComponentModel.DataAnnotations;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Application.Training;
using EarlyYearsFoundationRecovery.Domain.Training;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed record TrainingModuleViewModel(TrainingModule Module, string State, int VisitedPageCount);

public sealed record TrainingModuleDetailViewModel(TrainingModule Module, int VisitedPageCount);

public sealed class TrainingAnswerModel
{
    public List<string> SelectedAnswerIds { get; set; } = [];
}

public sealed record TrainingPageViewModel(
    TrainingModule Module,
    TrainingPage Page,
    TrainingPage? NextPage,
    AssessmentAttempt? LatestAssessment);

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
