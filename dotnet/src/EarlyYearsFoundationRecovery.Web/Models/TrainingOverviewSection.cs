using EarlyYearsFoundationRecovery.Application.Contracts;

namespace EarlyYearsFoundationRecovery.Web.Models;

public sealed record TrainingOverviewTopic(string Title, string Status, string? PageName);

public sealed record TrainingOverviewSection(
    string Title, int PageCount, string Status, IReadOnlyList<TrainingOverviewTopic> Topics)
{
    public static IReadOnlyList<TrainingOverviewSection> Create(TrainingModuleDetailViewModel model)
    {
        var groups = new List<List<TrainingPage>>();
        var feedbackStarted = false;
        foreach (var page in model.Module.Pages.Where(page => page.PageType != "interruption_page"))
        {
            var firstFeedback = page.PageType == "feedback" && !feedbackStarted;
            feedbackStarted |= page.PageType == "feedback";
            if (groups.Count == 0 || firstFeedback ||
                page.PageType is "sub_module_intro" or "summary_intro" or "certificate")
            {
                groups.Add([]);
            }
            groups[^1].Add(page);
        }

        var visited = model.VisitedPageNames.ToHashSet(StringComparer.Ordinal);
        string Status(IEnumerable<TrainingPage> pages)
        {
            var names = pages.Select(page => page.Name).ToArray();
            return model.CompletedAt is not null || names.All(visited.Contains)
                ? "Completed"
                : names.Any(visited.Contains) ? "In progress" : "Not started";
        }

        var sections = new List<TrainingOverviewSection>();
        foreach (var group in groups)
        {
            var first = group[0];
            if (first.PageType is "pre_confidence" or "pre_confidence_intro" or "feedback" ||
                (first.PageType == "text_page" && first.Title == "Confidence check"))
            {
                continue;
            }

            var topics = new List<TrainingOverviewTopic>();
            for (var index = 0; index < group.Count; index++)
            {
                var page = group[index];
                if (!IsTopic(page))
                {
                    continue;
                }

                var pages = group.Skip(index).Take(1)
                    .Concat(group.Skip(index + 1).TakeWhile(item => !IsTopic(item)));
                var status = Status(pages);
                string? pageName = status == "Completed" ? page.Name : null;
                if (page.PageType == "assessment_intro")
                {
                    status = model.LatestAssessment?.Passed switch
                    {
                        true => "Completed",
                        false => "Failed",
                        _ => status
                    };
                }
                if (page.PageType == "confidence_intro")
                {
                    if (model.LatestAssessment?.Passed == false)
                    {
                        pageName = null;
                    }
                    else if (model.LatestAssessment?.Passed == true)
                    {
                        pageName = page.Name;
                    }
                }
                topics.Add(new TrainingOverviewTopic(page.Title, status, pageName));
            }
            sections.Add(new TrainingOverviewSection(
                first.PageType == "certificate" ? "Complete module" : first.Title,
                group.Count, Status(group), topics));
        }
        return sections;
    }

    private static bool IsTopic(TrainingPage page) =>
        page.PageType is "topic_intro" or "recap_page" or "assessment_intro" or "confidence_intro" or "certificate";
}
