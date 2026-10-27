using EarlyYearsFoundationRecovery.Web.Content;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class TrainingContentRendererTests
{
    private readonly TrainingContentRenderer _renderer = new();

    [Fact]
    public void RendersHeadingsParagraphsListsAndLinks()
    {
        var html = _renderer.Render("""
            ## What to expect

            Read **carefully** and visit [resources](/wifi-and-data).

            - First topic
            - Second topic

            [External resource](https://example.test/resources)
            """).Value!;

        Assert.Contains("<h2>What to expect</h2>", html);
        Assert.Contains("<strong>carefully</strong>", html);
        Assert.Contains("<a href=\"/wifi-and-data\">resources</a>", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<li>First topic</li>", html);
        Assert.Contains("href=\"https://example.test/resources\"", html);
    }

    [Fact]
    public void RendersTables()
    {
        var html = _renderer.Render("""
            | Topic | Description |
            | --- | --- |
            | Learning | Child development |
            """).Value!;

        Assert.Contains("<table>", html);
        Assert.Contains("<th>Topic</th>", html);
        Assert.Contains("<td>Child development</td>", html);
    }

    [Theory]
    [InlineData("[Unsafe](javascript:alert%281%29)")]
    [InlineData("[Unsafe](vbscript:msgbox%281%29)")]
    [InlineData("![Unsafe](data:text/html;base64,PHNjcmlwdD4=)")]
    public void RemovesUnsafeLinkAndImageSchemes(string markdown)
    {
        var html = _renderer.Render(markdown).Value!;

        Assert.DoesNotContain("href=", html);
        Assert.DoesNotContain("src=", html);
    }

    [Fact]
    public void DoesNotRenderRawHtml()
    {
        var html = _renderer.Render("""
            <script>alert(1)</script>

            <img src="https://example.test/image.png" onerror="alert(1)">
            """).Value!;

        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("<img", html);
    }
}
