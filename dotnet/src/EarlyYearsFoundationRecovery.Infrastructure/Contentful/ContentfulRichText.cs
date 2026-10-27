using System.Text;
using System.Text.Json;

namespace EarlyYearsFoundationRecovery.Infrastructure.Contentful;

internal static class ContentfulRichText
{
    public static string ToMarkdown(JsonElement node, IReadOnlyList<JsonElement> entries)
    {
        var type = node.GetProperty("nodeType").GetString();
        if (type == "text")
        {
            return Text(node);
        }

        if (type == "embedded-asset-block")
        {
            return Image(node, entries);
        }
        if (type == "table")
        {
            return Table(node, entries);
        }

        var children = node.TryGetProperty("content", out var content)
            ? content.EnumerateArray().Select(child => ToMarkdown(child, entries)).ToArray()
            : [];
        var body = string.Concat(children);
        return type switch
        {
            "document" => body.Trim(),
            "paragraph" => body + "\n\n",
            "heading-1" => "# " + body + "\n\n",
            "heading-2" => "## " + body + "\n\n",
            "heading-3" => "### " + body + "\n\n",
            "heading-4" => "#### " + body + "\n\n",
            "heading-5" => "##### " + body + "\n\n",
            "heading-6" => "###### " + body + "\n\n",
            "unordered-list" => List(children, ordered: false),
            "ordered-list" => List(children, ordered: true),
            "list-item" => body,
            "blockquote" => string.Join("\n", body.TrimEnd().Split('\n').Select(line => "> " + line)) + "\n\n",
            "hr" => "---\n\n",
            "hyperlink" => $"[{body}](<{Destination(node.GetProperty("data").GetProperty("uri").GetString() ?? "")}>)",
            _ => throw new InvalidDataException($"Unsupported Contentful rich-text node '{type}'.")
        };
    }

    private static string Text(JsonElement node)
    {
        var value = node.GetProperty("value").GetString() ?? "";
        var marks = node.TryGetProperty("marks", out var markArray)
            ? markArray.EnumerateArray().Select(mark => mark.GetProperty("type").GetString()).ToArray()
            : [];
        if (marks.Any(mark => mark is not ("bold" or "italic" or "code")))
        {
            throw new InvalidDataException("Unsupported Contentful rich-text text mark.");
        }

        string text;
        if (marks.Contains("code"))
        {
            var longestRun = 0;
            var run = 0;
            foreach (var character in value)
            {
                run = character == '`' ? run + 1 : 0;
                longestRun = Math.Max(longestRun, run);
            }
            var delimiter = new string('`', longestRun + 1);
            text = delimiter + " " + value.Replace('\n', ' ').Replace('\r', ' ') + " " + delimiter;
        }
        else
        {
            text = Escape(value).Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n').Replace("\n", "  \n", StringComparison.Ordinal);
        }

        // Whitespace must sit outside emphasis delimiters for CommonMark to recognize them.
        var leading = text[..(text.Length - text.TrimStart().Length)];
        var trailing = text[text.TrimEnd().Length..];
        text = text.Trim();
        if (text.Length == 0)
        {
            return value;
        }

        if (marks.Contains("bold"))
        {
            text = "**" + text + "**";
        }
        if (marks.Contains("italic"))
        {
            text = "*" + text + "*";
        }
        return leading + text + trailing;
    }

    private static string List(string[] items, bool ordered)
    {
        return string.Join("\n", items.Select((item, index) =>
        {
            var marker = ordered ? $"{index + 1}. " : "- ";
            var lines = item.TrimEnd().Split('\n');
            return marker + lines[0] + string.Concat(lines.Skip(1).Select(line => "\n" + new string(' ', marker.Length) + line));
        })) + "\n\n";
    }

    private static string Table(JsonElement node, IReadOnlyList<JsonElement> entries)
    {
        var rows = node.GetProperty("content").EnumerateArray().ToArray();
        if (rows.Length == 0)
        {
            throw new InvalidDataException("Contentful rich-text tables require a header row.");
        }
        var lines = new List<string>();
        var columnCount = 0;
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index];
            var cells = row.GetProperty("content").EnumerateArray().ToArray();
            if (index == 0)
            {
                columnCount = cells.Length;
            }
            if (row.GetProperty("nodeType").GetString() != "table-row" || columnCount == 0 ||
                cells.Length != columnCount)
            {
                throw new InvalidDataException("Contentful rich-text tables require consistently sized rows.");
            }

            var values = cells.Select(cell =>
            {
                var paragraphs = cell.GetProperty("content").EnumerateArray().ToArray();
                if (cell.GetProperty("nodeType").GetString() != (index == 0 ? "table-header-cell" : "table-cell") ||
                    paragraphs.Length != 1 || paragraphs[0].GetProperty("nodeType").GetString() != "paragraph")
                {
                    throw new InvalidDataException("Contentful rich-text tables support a header row and single-paragraph cells.");
                }
                var value = ToMarkdown(paragraphs[0], entries).TrimEnd();
                if (value.Contains('\n'))
                {
                    throw new InvalidDataException("Contentful rich-text table cells must contain a single line.");
                }
                return value;
            });
            lines.Add("| " + string.Join(" | ", values) + " |");
            if (index == 0)
            {
                lines.Add("| " + string.Join(" | ", Enumerable.Repeat("---", columnCount)) + " |");
            }
        }
        return string.Join("\n", lines) + "\n\n";
    }

    private static string Image(JsonElement node, IReadOnlyList<JsonElement> entries)
    {
        var id = node.GetProperty("data").GetProperty("target").GetProperty("sys").GetProperty("id").GetString();
        var asset = entries.FirstOrDefault(entry =>
            entry.GetProperty("sys").GetProperty("id").GetString() == id &&
            entry.GetProperty("sys").TryGetProperty("type", out var type) && type.GetString() == "Asset");
        if (asset.ValueKind == JsonValueKind.Undefined)
        {
            throw new InvalidDataException($"Contentful image asset '{id}' could not be resolved.");
        }

        var fields = ContentfulDeliveryClient.Fields(asset);
        var file = ContentfulDeliveryClient.Field(fields, "file");
        var contentType = ContentfulDeliveryClient.StringField(file, "contentType");
        if (contentType is not ("image/png" or "image/jpeg" or "image/gif" or "image/webp" or "image/avif"))
        {
            throw new InvalidDataException($"Unsupported Contentful image type '{contentType}' for asset '{id}'.");
        }

        var url = Destination(ContentfulDeliveryClient.StringField(file, "url"));
        var description = ContentfulDeliveryClient.StringField(fields, "description");
        return $"![{Escape(description)}](<{url}>)\n\n";
    }

    private static string Destination(string value)
    {
        if (value.Any(char.IsControl) || value.Contains('\\'))
        {
            throw new InvalidDataException("Contentful rich-text URLs must not contain control characters or backslashes.");
        }
        if (value.StartsWith("//", StringComparison.Ordinal))
        {
            value = "https:" + value;
        }
        if (value.StartsWith('/'))
        {
            return value.Replace(" ", "%20", StringComparison.Ordinal).Replace("<", "%3C", StringComparison.Ordinal)
                .Replace(">", "%3E", StringComparison.Ordinal);
        }
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http") || uri.UserInfo.Length > 0)
        {
            throw new InvalidDataException("Contentful rich-text links and images require an HTTP(S) or root-relative URL.");
        }
        return uri.AbsoluteUri.Replace("<", "%3C", StringComparison.Ordinal).Replace(">", "%3E", StringComparison.Ordinal);
    }

    private static string Escape(string value)
    {
        var result = new StringBuilder();
        foreach (var character in value)
        {
            if ("\\`*_{}[]<>()#+-.!|&~=".Contains(character))
            {
                result.Append('\\');
            }
            result.Append(character);
        }
        return result.ToString();
    }
}
