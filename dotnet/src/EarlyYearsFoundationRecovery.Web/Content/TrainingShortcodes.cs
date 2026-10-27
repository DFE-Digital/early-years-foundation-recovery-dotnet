using System.Text;
using System.Text.RegularExpressions;
using Markdig;
using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace EarlyYearsFoundationRecovery.Web.Content;

internal sealed partial class TrainingShortcodes(string kind) : ContainerBlock(null)
{
    public string Kind { get; } = kind;

    [GeneratedRegex(@"\{/?(?:brain|book|info|quote|two_thirds)\}")]
    private static partial Regex BlockMarker();

    public static string SeparateMarkers(string source, MarkdownDocument document)
    {
        var positions = document.Descendants<LiteralInline>()
            .SelectMany(literal => BlockMarker().Matches(source.Substring(literal.Span.Start, literal.Span.Length))
                .Select(match => (Start: literal.Span.Start + match.Index, match.Length)))
            .Where(marker => !IsEscaped(source, marker.Start))
            .OrderByDescending(marker => marker.Start);
        var result = new StringBuilder(source);
        foreach (var (start, length) in positions)
        {
            result.Insert(start + length, "\n\n");
            result.Insert(start, "\n\n");
        }
        return result.ToString();
    }

    public static void Apply(ContainerBlock container, string source)
    {
        for (var index = 0; index < container.Count; index++)
        {
            var marker = TrainingColumnsLayout.Marker(container[index], source);
            if (marker is "{/brain}" or "{/book}" or "{/info}" or "{/quote}")
            {
                throw new InvalidDataException($"Contentful shortcode '{marker}' has no opening marker.");
            }
            if (marker is not ("{brain}" or "{book}" or "{info}" or "{quote}"))
            {
                if (container[index] is ContainerBlock nested)
                {
                    Apply(nested, source);
                }
                continue;
            }

            var kind = marker[1..^1];
            var end = index + 1;
            var depth = 1;
            for (; end < container.Count; end++)
            {
                var candidate = TrainingColumnsLayout.Marker(container[end], source);
                if (candidate == marker)
                {
                    depth++;
                }
                else if (candidate == $"{{/{kind}}}" && --depth == 0)
                {
                    break;
                }
            }
            if (end == container.Count || end == index + 1)
            {
                throw new InvalidDataException($"Contentful shortcode '{kind}' requires content and a matching closing marker.");
            }
            var blocks = container.Skip(index + 1).Take(end - index - 1).ToArray();
            if (kind == "quote" && (blocks.Length < 2 || blocks[^1] is not ParagraphBlock))
            {
                throw new InvalidDataException("A Contentful quote requires a separate final citation paragraph.");
            }
            for (var remove = end; remove >= index; remove--)
            {
                container.RemoveAt(remove);
            }
            var shortcode = new TrainingShortcodes(kind);
            foreach (var block in blocks)
            {
                shortcode.Add(block);
            }
            Apply(shortcode, source);
            container.Insert(index, shortcode);
        }
    }

    public static void ApplyLinks(MarkdownDocument document, string source)
    {
        foreach (var literal in document.Descendants<LiteralInline>().ToArray())
        {
            foreach (var kind in new[] { "button", "external" })
            {
                var opening = $"{{{kind}}}";
                var closing = $"{{/{kind}}}";
                var text = literal.Content.ToString();
                if (!text.EndsWith(opening, StringComparison.Ordinal) ||
                    IsEscaped(source, literal.Span.End - opening.Length + 1))
                {
                    continue;
                }
                if (literal.NextSibling is not LinkInline { IsImage: false } link ||
                    link.NextSibling is not LiteralInline after ||
                    !after.Content.ToString().StartsWith(closing, StringComparison.Ordinal) ||
                    IsEscaped(source, after.Span.Start))
                {
                    throw new InvalidDataException($"Contentful shortcode '{kind}' must wrap a single Markdown link.");
                }
                literal.Content = new Markdig.Helpers.StringSlice(text[..^opening.Length]);
                after.Content = new Markdig.Helpers.StringSlice(after.Content.ToString()[closing.Length..]);
                var attributes = link.GetAttributes();
                if (kind == "button")
                {
                    attributes.AddClass("button");
                }
                else
                {
                    attributes.AddProperty("target", "_blank");
                    attributes.AddProperty("rel", "noopener noreferrer");
                    link.AppendChild(new LiteralInline(" (opens in a new tab)"));
                }
            }
        }
        foreach (var literal in document.Descendants<LiteralInline>())
        {
            foreach (var marker in new[] { "{button}", "{/button}", "{external}", "{/external}" })
            {
                var sourceText = source.AsSpan(literal.Span.Start, literal.Span.Length);
                var markerOffset = sourceText.IndexOf(marker, StringComparison.Ordinal);
                if (literal.Content.ToString().Contains(marker, StringComparison.Ordinal) &&
                    markerOffset >= 0 && !IsEscaped(source, literal.Span.Start + markerOffset))
                {
                    throw new InvalidDataException($"Invalid Contentful link shortcode '{marker}'.");
                }
            }
        }
    }

    private static bool IsEscaped(string source, int position)
    {
        var slashes = 0;
        while (position > 0 && source[--position] == '\\')
        {
            slashes++;
        }
        return slashes % 2 != 0;
    }

    internal sealed class Renderer : HtmlObjectRenderer<TrainingShortcodes>
    {
        protected override void Write(HtmlRenderer renderer, TrainingShortcodes block)
        {
            if (block.Kind == "quote")
            {
                renderer.WriteLine("<blockquote class=\"training-quote\">");
                for (var index = 0; index < block.Count - 1; index++)
                {
                    renderer.Write(block[index]);
                }
                renderer.Write("<cite>");
                renderer.WriteLeafInline((ParagraphBlock)block[^1]);
                renderer.WriteLine("</cite></blockquote>");
                return;
            }
            var heading = block.Kind switch
            {
                "brain" => "Reflection point",
                "book" => "Further reading",
                "info" => "In your setting",
                _ => throw new InvalidDataException($"Unsupported learning prompt '{block.Kind}'.")
            };
            renderer.Write("<section class=\"training-prompt training-prompt--").Write(block.Kind).WriteLine("\">");
            renderer.Write("<h2>").Write(heading).WriteLine("</h2>");
            renderer.WriteChildren(block);
            renderer.WriteLine("</section>");
        }
    }
}
