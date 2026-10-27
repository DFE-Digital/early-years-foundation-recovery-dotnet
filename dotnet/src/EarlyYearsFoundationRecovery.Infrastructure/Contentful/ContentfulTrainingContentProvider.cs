using System.Text.Json;
using EarlyYearsFoundationRecovery.Application.Contracts;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public sealed class ContentfulTrainingContentProvider(ContentfulDeliveryClient contentful) : ITrainingContentProvider
{
    public async Task<IReadOnlyList<TrainingModule>> GetModulesAsync(CancellationToken cancellationToken)
    {
        var entries = await contentful.GetEntriesAsync("trainingModule", cancellationToken);
        return entries
            .Where(entry => ContentfulDeliveryClient.ContentType(entry) == "trainingModule")
            .Select(entry => MapModule(entry, entries))
            .Where(module => !string.IsNullOrWhiteSpace(module.Name))
            .OrderBy(module => module.Position)
            .ToArray();
    }

    public async Task<TrainingModule?> GetModuleAsync(string moduleName, CancellationToken cancellationToken)
    {
        var modules = await GetModulesAsync(cancellationToken);
        return modules.SingleOrDefault(module => module.Name == moduleName);
    }

    private static TrainingModule MapModule(JsonElement entry, IReadOnlyList<JsonElement> entries)
    {
        var fields = ContentfulDeliveryClient.Fields(entry);
        var name = ContentfulDeliveryClient.StringField(fields, "name");
        var title = ContentfulDeliveryClient.StringField(fields, "title");
        var position = IntegerField(fields, "position");
        var pages = ResolveReferences(ContentfulDeliveryClient.Field(fields, "pages"), entries)
            .Where(item => ContentfulDeliveryClient.ContentType(item) is "page" or "question" or "video")
            .Select(page => MapPage(page, entries))
            .Where(page => !string.IsNullOrWhiteSpace(page.Name))
            .ToArray();
        if (!pages.Any(page => page.PageType == "pre_confidence"))
        {
            var legacyConfidence = new[] { "Strongly agree", "Agree", "Neither agree nor disagree", "Disagree", "Strongly disagree" };
            pages = pages.Select(page => page.PageType == "confidence"
                ? page with
                {
                    Options = legacyConfidence
                        .Select((label, index) => new TrainingQuestionOption(
                            (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                            label))
                        .ToArray()
                }
                : page).ToArray();
        }
        var isDraft = string.IsNullOrWhiteSpace(name) ||
                      string.IsNullOrWhiteSpace(title) ||
                      pages.Length == 0;

        return new TrainingModule(name, title, position, isDraft, pages,
            ContentfulDeliveryClient.StringField(fields, "criteria", entries),
            ContentfulDeliveryClient.StringField(fields, "description", entries),
            ContentfulDeliveryClient.StringField(fields, "outcomes", entries),
            ContentfulDeliveryClient.StringField(fields, "upcoming", entries));
    }

    private static TrainingPage MapPage(JsonElement entry, IReadOnlyList<JsonElement> entries)
    {
        var fields = ContentfulDeliveryClient.Fields(entry);
        var contentType = ContentfulDeliveryClient.ContentType(entry);
        var answers = ArrayField(fields, "answers");
        var options = answers
            .Select((answer, index) => new TrainingQuestionOption(
                (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
                answer.ValueKind == JsonValueKind.Array && answer.GetArrayLength() > 0
                    ? answer[0].GetString() ?? string.Empty
                    : string.Empty))
            .Where(option => !string.IsNullOrWhiteSpace(option.Label))
            .ToArray();
        var correctAnswers = answers
            .Select((answer, index) => (answer, id: (index + 1).ToString(System.Globalization.CultureInfo.InvariantCulture)))
            .Where(item => item.answer.ValueKind == JsonValueKind.Array &&
                           item.answer.GetArrayLength() > 1 &&
                           item.answer[1].ValueKind == JsonValueKind.True)
            .Select(item => item.id)
            .ToArray();
        var pageType = ContentfulDeliveryClient.StringField(fields, "page_type");
        if (contentType == "video")
        {
            pageType = "video_page";
        }

        if (contentType == "question" && (pageType is "confidence" or "pre_confidence"))
        {
            options =
            [
                new("1", "Very confident"),
                new("2", "Somewhat confident"),
                new("3", "Neutral"),
                new("4", "Not very confident"),
                new("5", "Not confident at all")
            ];
            correctAnswers = options.Select(option => option.Id).ToArray();
        }

        var name = ContentfulDeliveryClient.StringField(fields, "name");

        return new TrainingPage(
            name,
            ContentfulDeliveryClient.StringField(fields, "heading") is { Length: > 0 } heading ? heading : name,
            pageType.Length > 0 ? pageType : contentType,
            ContentfulDeliveryClient.StringField(fields, "body", entries),
            options,
            correctAnswers,
            contentType == "question" ? pageType : null,
            (pageType is "formative" or "summative") && correctAnswers.Length > 1,
            contentType == "video" ? MapVideo(fields, entries) : null,
            ContentfulDeliveryClient.StringField(fields, "failure_message", entries),
            ContentfulDeliveryClient.StringField(fields, "success_message", entries),
            ContentfulDeliveryClient.StringField(fields, "description", entries),
            ContentfulDeliveryClient.StringField(fields, "hint"));
    }

    private static TrainingVideo MapVideo(JsonElement fields, IReadOnlyList<JsonElement> entries)
    {
        var id = ContentfulDeliveryClient.StringField(fields, "video_id");
        string? embedUrl = null;
        if (id.Length > 0)
        {
            if (id.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not ('-' or '_')))
            {
                throw new InvalidDataException("Contentful video IDs must contain only letters, numbers, hyphens or underscores.");
            }

            var provider = ContentfulDeliveryClient.StringField(fields, "video_provider").ToLowerInvariant();
            embedUrl = provider switch
            {
                "youtube" => $"https://www.youtube.com/embed/{id}",
                "vimeo" => $"https://player.vimeo.com/video/{id}",
                _ => throw new InvalidDataException($"Unsupported Contentful video provider '{provider}'.")
            };
        }

        return new TrainingVideo(
            embedUrl,
            ContentfulDeliveryClient.StringField(fields, "title"),
            ContentfulDeliveryClient.StringField(fields, "transcript", entries));
    }

    private static IEnumerable<JsonElement> ResolveReferences(
        JsonElement value,
        IReadOnlyList<JsonElement> entries)
    {
        if (value.ValueKind == JsonValueKind.Array)
        {
            return value.EnumerateArray().SelectMany(item => ResolveReferences(item, entries));
        }

        if (value.ValueKind != JsonValueKind.Object ||
            !value.TryGetProperty("sys", out var sys) ||
            !sys.TryGetProperty("id", out var idElement) ||
            idElement.ValueKind != JsonValueKind.String)
        {
            return [];
        }

        var id = idElement.GetString();
        var entry = entries.FirstOrDefault(item =>
            item.TryGetProperty("sys", out var itemSys) &&
            itemSys.TryGetProperty("id", out var itemId) &&
            itemId.GetString() == id);
        return entry.ValueKind == JsonValueKind.Undefined ? [] : [entry];
    }

    private static int IntegerField(JsonElement fields, string name)
    {
        var value = ContentfulDeliveryClient.Field(fields, name);
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) ? number : 0;
    }

    private static IEnumerable<JsonElement> ArrayField(JsonElement fields, string name)
    {
        var value = ContentfulDeliveryClient.Field(fields, name);
        return value.ValueKind == JsonValueKind.Array ? value.EnumerateArray().ToArray() : [];
    }
}
