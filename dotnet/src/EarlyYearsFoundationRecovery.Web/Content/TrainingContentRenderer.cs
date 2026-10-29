using Ganss.Xss;
using Markdig;
using Markdig.Renderers;
using Microsoft.AspNetCore.Html;

namespace EarlyYearsFoundationRecovery.Web.Content;

public sealed class TrainingContentRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .UsePipeTables()
        .Build();

    private readonly HtmlSanitizer _sanitizer = CreateSanitizer();

    public HtmlString Render(string markdown)
    {
        var document = Markdown.Parse(markdown, Pipeline);
        markdown = TrainingShortcodes.SeparateMarkers(markdown, document);
        document = Markdown.Parse(markdown, Pipeline);
        TrainingShortcodes.ApplyLinks(document, markdown);
        TrainingShortcodes.Apply(document, markdown);
        TrainingColumnsLayout.Apply(document, markdown);
        using var writer = new StringWriter();
        var renderer = new HtmlRenderer(writer);
        Pipeline.Setup(renderer);
        renderer.ObjectRenderers.Insert(0, new TrainingColumnsLayout.Renderer());
        renderer.ObjectRenderers.Insert(0, new TrainingShortcodes.Renderer());
        renderer.Render(document);
        return new HtmlString(_sanitizer.Sanitize(writer.ToString()));
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(
        [
            "h1", "h2", "h3", "h4", "h5", "h6", "p", "a", "ul", "ol", "li",
            "strong", "em", "b", "i", "blockquote", "cite", "img", "br", "hr",
            "table", "thead", "tbody", "tfoot", "tr", "th", "td", "code", "pre",
            "span", "div", "dl", "dt", "dd", "section", "details", "summary"
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
