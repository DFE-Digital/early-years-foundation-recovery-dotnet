using Markdig.Renderers;
using Markdig.Renderers.Html;
using Markdig.Syntax;
using Markdig.Syntax.Inlines;

namespace EarlyYearsFoundationRecovery.Web.Content;

internal sealed class TrainingColumnsLayout(string cssClass) : ContainerBlock(null)
{
    public string CssClass { get; } = cssClass;

    public static void Apply(ContainerBlock container, string source)
    {
        for (var index = 0; index < container.Count; index++)
        {
            var marker = Marker(container[index], source);
            if (marker == "{/two_thirds}")
            {
                throw new InvalidDataException("A Contentful two_thirds layout has no opening marker.");
            }
            if (marker != "{two_thirds}")
            {
                if (container[index] is ContainerBlock nested)
                {
                    Apply(nested, source);
                }
                continue;
            }

            var end = index + 1;
            while (end < container.Count && Marker(container[end], source) != "{/two_thirds}")
            {
                if (Marker(container[end], source) == "{two_thirds}")
                {
                    throw new InvalidDataException("Nested Contentful two_thirds layouts are not supported.");
                }
                end++;
            }
            if (end == container.Count)
            {
                throw new InvalidDataException("A Contentful two_thirds layout has no closing marker.");
            }
            if (container[end - 1] is ParagraphBlock
                {
                    Inline.LastChild: LinkInline { IsImage: true, PreviousSibling: LineBreakInline lineBreak } trailingImage
                } && lineBreak.PreviousSibling is not null)
            {
                trailingImage.Remove();
                lineBreak.Remove();
                var imageParagraph = new ParagraphBlock(null) { Inline = new ContainerInline() };
                imageParagraph.Inline.AppendChild(trailingImage);
                container.Insert(end, imageParagraph);
                end++;
            }
            if (end <= index + 2 || container[end - 1] is not ParagraphBlock
                {
                    Inline.FirstChild: LinkInline { IsImage: true, NextSibling: null }
                })
            {
                throw new InvalidDataException("A Contentful two_thirds layout requires text followed by a standalone image.");
            }

            var blocks = container.Skip(index + 1).Take(end - index - 1).ToArray();
            for (var remove = end; remove >= index; remove--)
            {
                container.RemoveAt(remove);
            }
            var layout = new TrainingColumnsLayout("training-columns");
            var text = new TrainingColumnsLayout("training-columns__text");
            var image = new TrainingColumnsLayout("training-columns__image");
            foreach (var block in blocks[..^1])
            {
                text.Add(block);
            }
            image.Add(blocks[^1]);
            layout.Add(text);
            layout.Add(image);
            container.Insert(index, layout);
        }
    }

    internal static string? Marker(Block block, string source) =>
        block is ParagraphBlock { Inline.FirstChild: LiteralInline { NextSibling: null } literal } paragraph &&
        !source.AsSpan(paragraph.Span.Start, paragraph.Span.Length).TrimStart().StartsWith("\\", StringComparison.Ordinal)
            ? literal.Content.ToString().Trim()
            : null;

    internal sealed class Renderer : HtmlObjectRenderer<TrainingColumnsLayout>
    {
        protected override void Write(HtmlRenderer renderer, TrainingColumnsLayout block)
        {
            renderer.Write("<div class=\"").Write(block.CssClass).WriteLine("\">");
            renderer.WriteChildren(block);
            renderer.WriteLine("</div>");
        }
    }
}
