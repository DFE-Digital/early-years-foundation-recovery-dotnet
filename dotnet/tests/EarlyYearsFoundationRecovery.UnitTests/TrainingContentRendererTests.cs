using EarlyYearsFoundationRecovery.Web.Content;
using Microsoft.Playwright;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class TrainingContentRendererTests
{
    private readonly TrainingContentRenderer _renderer = new();

    [Fact]
    public void RendersInsetTextWithNestedMarkdown()
    {
        var html = _renderer.Render("""
            {inset-text}
            Important **guidance**.

            - First point
            - Second point
            {/inset-text}
            """).Value!;

        Assert.Contains("<div class=\"training-inset-text\">", html);
        Assert.Contains("<strong>guidance</strong>", html);
        Assert.Contains("<li>First point</li>", html);
        Assert.DoesNotContain("{inset-text}", html);
    }

    [Theory]
    [InlineData("Why does this matter? Read **more**.", "Why does this matter?")]
    [InlineData("More information. Read **more**.", "More information")]
    public void DetailsUsesFirstQuestionMarkOrFullStopAsSummaryBoundary(string content, string summary)
    {
        var html = _renderer.Render($"{{details}}{content}{{/details}}").Value!;

        Assert.Contains("<details class=\"training-details\">", html);
        Assert.Contains($"<summary>{summary}</summary>", html);
        Assert.Contains("<strong>more</strong>", html);
        Assert.DoesNotContain(" open", html);
        Assert.DoesNotContain("{details}", html);
    }

    [Fact]
    public void DetailsCanContainInsetTextAndLists()
    {
        var html = _renderer.Render("""
            {details}
            What should I do?

            - Observe
            - Reflect

            {inset-text}
            Keep **notes**.
            {/inset-text}
            {/details}
            """).Value!;

        Assert.Contains("<summary>What should I do?</summary>", html);
        Assert.Contains("<li>Observe</li>", html);
        Assert.Contains("class=\"training-inset-text\"", html);
        Assert.Contains("<strong>notes</strong>", html);
    }

    [Theory]
    [InlineData("{details}Missing punctuation and body{/details}")]
    [InlineData("{details}Summary only?{/details}")]
    [InlineData("{details}Unclosed? Body")]
    [InlineData("{/details}")]
    [InlineData("{inset-text}Unclosed")]
    [InlineData("{inset-text}{/inset-text}")]
    [InlineData("{/inset-text}")]
    public void MalformedInsetTextAndDetailsRaiseExplicitContentErrors(string markdown)
    {
        Assert.Throws<InvalidDataException>(() => _renderer.Render(markdown));
    }

    [Fact]
    public void DetailsAndInsetMarkersInsideCodeOrEscapedTextRemainLiteral()
    {
        var html = _renderer.Render("""
            `{details}Summary? Body{/details}`

            ```text
            {inset-text}Literal{/inset-text}
            ```

            \{details}

            \{inset-text}
            """).Value!;

        Assert.Contains("{details}", html);
        Assert.Contains("{inset-text}", html);
        Assert.DoesNotContain("<details", html);
        Assert.DoesNotContain("class=\"training-inset-text\"", html);
    }

    [Fact]
    public void DetailsSummaryAndBodyDoNotEnableRawHtmlOrUnsafeLinks()
    {
        var html = _renderer.Render("""
            {details}
            <img src=x onerror=alert(1)>?

            <script>alert(1)</script>

            [Unsafe](javascript:alert%281%29)
            {/details}
            """).Value!;

        Assert.Contains("<summary>", html);
        Assert.DoesNotContain("<img", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("href=", html);
    }

    [Fact]
    public void DetailsPreservesSummaryFormattingAndNestedDetails()
    {
        var html = _renderer.Render("""
            {details}
            **More** information?

            Outer content.

            {details}Another question? Inner content.{/details}
            {/details}
            """).Value!;

        Assert.Contains("<summary><strong>More</strong> information?</summary>", html);
        Assert.Contains("<summary>Another question?</summary>", html);
        Assert.Contains("Inner content.", html);
        Assert.DoesNotContain("{details}", html);
    }

    [Fact]
    public async Task RenderedDetailsOpensAndClosesWithKeyboardWithoutJavaScript()
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        await using var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            JavaScriptEnabled = false,
            ViewportSize = new ViewportSize { Width = 375, Height = 812 }
        });
        var page = await context.NewPageAsync();
        await page.SetContentAsync(_renderer.Render("{details}More information? Hidden guidance.{/details}").Value!);
        var summary = page.Locator("summary");
        var body = page.Locator(".training-details__text");

        Assert.False(await body.IsVisibleAsync());
        await summary.FocusAsync();
        await page.Keyboard.PressAsync("Enter");
        Assert.True(await body.IsVisibleAsync());
        Assert.Equal("Hidden guidance.", (await body.InnerTextAsync()).Trim());
        await page.Keyboard.PressAsync("Space");
        Assert.False(await body.IsVisibleAsync());
    }

    [Theory]
    [InlineData("brain", "Reflection point")]
    [InlineData("book", "Further reading")]
    [InlineData("info", "In your setting")]
    public void RendersLearningPromptsWithNestedMarkdown(string shortcode, string heading)
    {
        var html = _renderer.Render($"{{{shortcode}}}\nRead **carefully**.\n\n- First topic\n- Second topic\n{{/{shortcode}}}").Value!;

        Assert.Contains($"class=\"training-prompt training-prompt--{shortcode}\"", html);
        Assert.Contains($"<h2>{heading}</h2>", html);
        Assert.Contains("<strong>carefully</strong>", html);
        Assert.Contains("<li>First topic</li>", html);
        Assert.DoesNotContain($"{{{shortcode}}}", html);
        Assert.DoesNotContain($"{{/{shortcode}}}", html);
    }

    [Fact]
    public void RendersQuoteWithSeparateCitation()
    {
        var html = _renderer.Render("""
            {quote}
            Learning takes **practice**.

            An early years practitioner
            {/quote}
            """).Value!;

        Assert.Contains("<blockquote class=\"training-quote\">", html);
        Assert.Contains("<strong>practice</strong>", html);
        Assert.Contains("<cite>An early years practitioner</cite>", html);
        Assert.DoesNotContain("{quote}", html);
    }

    [Theory]
    [InlineData("button", "/training", "class=\"button\"")]
    [InlineData("external", "https://example.test/guide?q=one&next=two", "rel=\"noopener noreferrer\"")]
    public void RendersShortcodeLinks(string shortcode, string url, string expected)
    {
        var html = _renderer.Render($"Before {{{shortcode}}}[Read **more**]({url}){{/{shortcode}}} after.").Value!;

        Assert.Contains(expected, html);
        Assert.Contains("<strong>more</strong>", html);
        Assert.Contains("Before ", html);
        Assert.Contains(" after.", html);
        Assert.DoesNotContain($"{{{shortcode}}}", html);
        if (shortcode == "external")
        {
            Assert.Contains("target=\"_blank\"", html);
            Assert.Contains("(opens in a new tab)", html);
        }
    }

    [Theory]
    [InlineData("{brain}\nMissing closing marker.")]
    [InlineData("{/info}")]
    [InlineData("{brain}\nText\n{/book}")]
    [InlineData("{quote}\nNo separate citation.\n{/quote}")]
    [InlineData("{button}Not a link{/button}")]
    public void InvalidShortcodesRaiseExplicitContentErrors(string markdown)
    {
        Assert.Throws<InvalidDataException>(() => _renderer.Render(markdown));
    }

    [Fact]
    public void ShortcodesInCodeAndEscapedTextRemainLiteral()
    {
        var html = _renderer.Render("""
            `{brain}`

            ```text
            {info}
            Keep this code.
            {/info}
            ```

            \{book}

            \{external}[literal](/training)\{/external}
            """).Value!;

        Assert.Contains("<code>{brain}</code>", html);
        Assert.Contains("{info}", html);
        Assert.Contains("{book}", html);
        Assert.Contains("{external}", html);
        Assert.DoesNotContain("class=\"training-prompt", html);
        Assert.DoesNotContain("target=\"_blank\"", html);
    }

    [Fact]
    public void ShortcodeLinksStillRejectUnsafeSchemes()
    {
        var html = _renderer.Render("{external}[Unsafe](javascript:alert%281%29){/external}").Value!;

        Assert.DoesNotContain("href=", html);
        Assert.DoesNotContain("javascript:", html);
    }

    [Fact]
    public void RendersNestedPromptsAndConsecutiveShortcodeLinks()
    {
        var html = _renderer.Render("""
            {brain}
            First reflection.
            {info}
            Try this in your setting.
            {/info}
            {/brain}

            {button}[First](/first){/button} and {external}[Second](https://example.test/second){/external}
            """).Value!;

        Assert.Contains("training-prompt--brain", html);
        Assert.Contains("training-prompt--info", html);
        Assert.Contains("href=\"/first\" class=\"button\"", html);
        Assert.Contains("href=\"https://example.test/second\"", html);
        Assert.DoesNotContain("{/", html);
    }

    [Fact]
    public void RendersTwoThirdsLayoutWithoutBlankLinesAroundMarkers()
    {
        var html = _renderer.Render("""
            {two_thirds}
            Description of an image
            ![Diagram](https://example.test/image.png)
            {/two_thirds}
            """).Value!;

        Assert.Contains("class=\"training-columns\"", html);
        Assert.Contains("Description of an image", html);
        Assert.Contains("alt=\"Diagram\"", html);
    }

    [Fact]
    public void RawHtmlInsidePromptsRemainsDisabled()
    {
        var html = _renderer.Render("""
            {info}
            <script>alert(1)</script>

            [Unsafe](javascript:alert%281%29)
            {/info}
            """).Value!;

        Assert.Contains("In your setting", html);
        Assert.DoesNotContain("<script", html);
        Assert.DoesNotContain("href=", html);
    }

    [Fact]
    public void RendersRailsTwoThirdsLayoutWithoutExposingCmsMarkers()
    {
        var html = _renderer.Render("""
            {two_thirds}

            ## The developing brain

            Read **carefully**.

            ![Brain diagram](https://example.test/brain.png)

            {/two_thirds}
            """).Value!;

        Assert.Contains("<div class=\"training-columns\">", html);
        Assert.Contains("<div class=\"training-columns__text\">", html);
        Assert.Contains("<div class=\"training-columns__image\">", html);
        Assert.Contains("<h2>The developing brain</h2>", html);
        Assert.Contains("<strong>carefully</strong>", html);
        Assert.Contains("alt=\"Brain diagram\"", html);
        Assert.DoesNotContain("{two_thirds}", html);
        Assert.DoesNotContain("{/two_thirds}", html);
    }

    [Fact]
    public void LayoutMarkersInsideCodeStayLiteral()
    {
        var html = _renderer.Render("""
            `{two_thirds}`

            ```text
            {two_thirds}
            ![image](https://example.test/image.png)
            {/two_thirds}
            ```
            """).Value!;

        Assert.Contains("<code>{two_thirds}</code>", html);
        Assert.DoesNotContain("class=\"training-columns\"", html);
    }

    [Fact]
    public void EscapedLayoutMarkersStayLiteral()
    {
        var html = _renderer.Render("""
            \{two_thirds}

            Text.

            ![Image](https://example.test/image.png)

            \{/two_thirds}
            """).Value!;

        Assert.Contains("{two_thirds}", html);
        Assert.DoesNotContain("class=\"training-columns\"", html);
    }

    [Theory]
    [InlineData("{two_thirds}\n\nText without a closing marker.")]
    [InlineData("{/two_thirds}")]
    [InlineData("{two_thirds}\n\nNo image.\n\n{/two_thirds}")]
    public void InvalidLayoutsRaiseAnExplicitContentError(string markdown)
    {
        Assert.Throws<InvalidDataException>(() => _renderer.Render(markdown));
    }

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
