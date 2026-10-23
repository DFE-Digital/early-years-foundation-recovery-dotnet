using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

public sealed class ContentfulDeliveryClient(
    HttpClient httpClient,
    IConfiguration configuration,
    IMemoryCache cache)
{
    private const string Locale = "en-US";

    public async Task<IReadOnlyList<JsonElement>> GetEntriesAsync(
        string contentType,
        CancellationToken cancellationToken)
    {
        var space = RequiredSetting("Space");
        var environment = configuration["Contentful:Environment"] ?? "master";
        var isPreview = string.Equals(configuration["Contentful:Preview"], "true", StringComparison.OrdinalIgnoreCase);
        var token = RequiredSetting(isPreview ? "PreviewToken" : "DeliveryToken");
        var cacheKey = $"contentful:{space}:{environment}:{isPreview}:{contentType}";
        if (cache.TryGetValue(cacheKey, out JsonElement[]? cachedEntries))
        {
            return cachedEntries!;
        }

        var apiHost = isPreview ? "preview.contentful.com" : "cdn.contentful.com";
        var url = $"https://{apiHost}/spaces/{Uri.EscapeDataString(space)}/environments/{Uri.EscapeDataString(environment)}/entries?content_type={Uri.EscapeDataString(contentType)}&include=10&limit=1000";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Contentful returned HTTP {(int)response.StatusCode} for content type '{contentType}'.",
                null,
                response.StatusCode);
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var entries = new List<JsonElement>();
        if (document.RootElement.TryGetProperty("items", out var items))
        {
            entries.AddRange(items.EnumerateArray().Select(entry => entry.Clone()));
        }

        if (document.RootElement.TryGetProperty("includes", out var includes) &&
            includes.TryGetProperty("Entry", out var includedEntries))
        {
            entries.AddRange(includedEntries.EnumerateArray().Select(entry => entry.Clone()));
        }

        var result = entries.ToArray();
        cache.Set(cacheKey, result, TimeSpan.FromSeconds(isPreview ? 15 : 60));
        return result;
    }

    public static JsonElement Fields(JsonElement entry) =>
        entry.TryGetProperty("fields", out var fields) ? fields : default;

    public static JsonElement Field(JsonElement fields, string name)
    {
        if (fields.ValueKind != JsonValueKind.Object ||
            !fields.TryGetProperty(name, out var value))
        {
            return default;
        }

        return value.ValueKind == JsonValueKind.Object &&
               value.TryGetProperty(Locale, out var localized)
            ? localized
            : value;
    }

    public static string StringField(JsonElement fields, string name)
    {
        var value = Field(fields, name);
        return StringOrRichText(value).Trim();
    }

    public static string StringOrRichText(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String)
        {
            return value.GetString() ?? string.Empty;
        }

        if (value.ValueKind == JsonValueKind.Array)
        {
            return string.Concat(value.EnumerateArray().Select(StringOrRichText));
        }

        if (value.ValueKind != JsonValueKind.Object)
        {
            return string.Empty;
        }

        var text = value.TryGetProperty("value", out var textValue) &&
                   textValue.ValueKind == JsonValueKind.String
            ? textValue.GetString()
            : null;
        var children = value.TryGetProperty("content", out var content)
            ? StringOrRichText(content)
            : string.Empty;
        var nodeType = value.TryGetProperty("nodeType", out var type) ? type.GetString() : null;
        var separator = nodeType is "paragraph" or "heading-1" or "heading-2" or "heading-3" or
            "heading-4" or "heading-5" or "heading-6" or "list-item" or "blockquote"
            ? Environment.NewLine
            : string.Empty;
        return string.Concat(text, children, separator);
    }

    public static bool BooleanField(JsonElement fields, string name)
    {
        var value = Field(fields, name);
        return value.ValueKind switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            _ => false
        };
    }

    public static string ContentType(JsonElement entry) =>
        entry.TryGetProperty("sys", out var sys) &&
        sys.TryGetProperty("contentType", out var contentType) &&
        contentType.TryGetProperty("sys", out var contentTypeSys) &&
        contentTypeSys.TryGetProperty("id", out var id)
            ? id.GetString() ?? string.Empty
            : string.Empty;

    private string RequiredSetting(string name) =>
        configuration[$"Contentful:{name}"] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Contentful:{name} must be configured to load content.");
}
