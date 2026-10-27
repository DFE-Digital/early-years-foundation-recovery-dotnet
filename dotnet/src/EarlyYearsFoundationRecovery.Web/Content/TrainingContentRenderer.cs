using Ganss.Xss;
using Markdig;
using Microsoft.AspNetCore.Html;

namespace EarlyYearsFoundationRecovery.Web.Content;

public sealed class TrainingContentRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .Build();

    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    public HtmlString Render(string markdown) =>
        new(_sanitizer.Sanitize(Markdown.ToHtml(markdown, Pipeline)));

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(
        [
            "h1", "h2", "h3", "h4", "h5", "h6", "p", "a", "ul", "ol", "li",
            "strong", "em", "b", "i", "blockquote", "cite", "img", "br", "hr",
            "table", "thead", "tbody", "tfoot", "tr", "th", "td", "code", "pre",
            "span", "div", "dl", "dt", "dd", "section"
        ]);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(
        [
            "href", "src", "alt", "title", "rel", "target", "class", "id",
            "colspan", "rowspan", "aria-label", "aria-describedby", "download"
        ]);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.UnionWith(["http", "https"]);
        return sanitizer;
    }
}
