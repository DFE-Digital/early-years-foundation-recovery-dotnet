using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using EarlyYearsFoundationRecovery.Application.Contracts;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using EarlyYearsFoundationRecovery.Web.Content;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class ContentfulTrainingContentProviderTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task MapsLocalizedReflectionNotesFlag(bool enabled)
    {
        var response = JsonNode.Parse(ContentfulResponse)!;
        var fields = response["includes"]!["Entry"]![0]!["fields"]!;
        fields["page_type"] = "text_page";
        fields["notes"] = new JsonObject { ["en-US"] = enabled };

        var page = await MapPageResponseAsync(response.ToJsonString());

        Assert.Equal(enabled, page.Notes);
        Assert.Equal(enabled, page.SupportsNotes);
    }

    [Fact]
    public async Task MapsUpcomingTextForNamedModuleWithoutPublishedPages()
    {
        var response = JsonNode.Parse(ContentfulResponse)!;
        var fields = response["items"]![0]!["fields"]!;
        fields["pages"] = new JsonArray();
        fields["upcoming"] = new JsonObject { ["en-US"] = "Learn about individual needs in a future module." };

        var module = await MapModuleResponseAsync(response.ToJsonString());

        Assert.True(module.IsDraft);
        Assert.Empty(module.Pages);
        Assert.Equal("Learn about individual needs in a future module.", module.Upcoming);
        Assert.Equal("module-one", module.Name);
    }

    [Fact]
    public async Task MissingUpcomingTextDoesNotMakeWorkingModuleUnavailable()
    {
        var module = await MapModuleResponseAsync(ContentfulResponse);

        Assert.Empty(module.Upcoming);
        Assert.False(module.IsDraft);
    }

    [Fact]
    public async Task UnresolvedPageReferencesKeepFutureModuleUnavailable()
    {
        var response = JsonNode.Parse(ContentfulResponse)!;
        response["includes"]!["Entry"] = new JsonArray();
        response["items"]![0]!["fields"]!["upcoming"] = "Planned module.";

        var module = await MapModuleResponseAsync(response.ToJsonString());

        Assert.True(module.IsDraft);
        Assert.Empty(module.Pages);
        Assert.Equal("Planned module.", module.Upcoming);
    }

    [Theory]
    [InlineData("description")]
    [InlineData("outcomes")]
    public async Task MapsLocalizedModuleOverviewRichText(string field)
    {
        var response = JsonNode.Parse(ContentfulResponse)!;
        response["items"]![0]!["fields"]![field] = JsonNode.Parse("""
            {"en-US":{"nodeType":"document","content":[
              {"nodeType":"paragraph","content":[{"nodeType":"text","value":"Understand individual needs.","marks":[{"type":"bold"}]}]}
            ]}}
            """);

        var module = await MapModuleResponseAsync(response.ToJsonString());

        Assert.Contains("<strong>Understand individual needs.</strong>",
            new TrainingContentRenderer().Render(field == "description" ? module.Description : module.Outcomes).Value!);
    }

    [Fact]
    public async Task MapsModuleOverviewMarkdownWithoutChangingPageAvailability()
    {
        var response = JsonNode.Parse(ContentfulResponse)!;
        response["items"]![0]!["fields"]!["description"] = "Learn about **development**.";
        response["items"]![0]!["fields"]!["outcomes"] = "- Observe children\n- Support learning";

        var module = await MapModuleResponseAsync(response.ToJsonString());

        Assert.Equal("Learn about **development**.", module.Description);
        Assert.Equal("- Observe children\n- Support learning", module.Outcomes);
        Assert.False(module.IsDraft);
        Assert.Single(module.Pages);
    }

    [Fact]
    public async Task MissingModuleOverviewMetadataRemainsOptional()
    {
        var module = await MapModuleResponseAsync(ContentfulResponse);

        Assert.Empty(module.Description);
        Assert.Empty(module.Outcomes);
        Assert.False(module.IsDraft);
    }

    [Theory]
    [InlineData("confidence")]
    [InlineData("pre_confidence")]
    public async Task MapsLocalizedConfidenceDescriptionAndHint(string pageType)
    {
        var response = JsonNode.Parse(ContentfulResponse)!;
        var fields = response["includes"]!["Entry"]![0]!["fields"]!;
        fields["page_type"] = new JsonObject { ["en-US"] = pageType };
        fields["description"] = JsonNode.Parse("""
            {"en-US":{"nodeType":"document","content":[
              {"nodeType":"paragraph","content":[{"nodeType":"text","value":"Reflect on your learning.","marks":[{"type":"bold"}]}]}
            ]}}
            """);
        fields["hint"] = new JsonObject { ["en-US"] = "Choose the response that best reflects your confidence." };

        var question = await MapPageResponseAsync(response.ToJsonString());

        Assert.Contains("<strong>Reflect on your learning.</strong>",
            new TrainingContentRenderer().Render(question.Description).Value!);
        Assert.Equal("Choose the response that best reflects your confidence.", question.Hint);
    }

    [Fact]
    public async Task MissingOptionalConfidenceGuidanceRemainsEmpty()
    {
        var question = await MapPageResponseAsync(ContentfulResponse);

        Assert.Empty(question.Description);
        Assert.Empty(question.Hint);
    }

    [Theory]
    [InlineData("failure_message")]
    [InlineData("success_message")]
    public async Task MapsRichTextAssessmentFeedbackThroughTheSameSanitizedPipeline(string field)
    {
        var response = JsonNode.Parse(ContentfulResponse)!;
        response["includes"]!["Entry"]![0]!["fields"]![field] = JsonNode.Parse("""
            {"en-US":{"nodeType":"document","content":[
              {"nodeType":"paragraph","content":[{"nodeType":"text","value":"Try observing individual needs.","marks":[{"type":"bold"}]}]}
            ]}}
            """);

        var question = await MapPageResponseAsync(response.ToJsonString());

        Assert.Contains("<strong>Try observing individual needs.</strong>",
            new TrainingContentRenderer().Render(field == "failure_message" ? question.FailureMessage : question.SuccessMessage).Value!);
    }

    [Fact]
    public async Task RichTextIsUsedForCertificateCriteriaAndVideoTranscriptsToo()
    {
        const string richText = """
            {"nodeType":"document","content":[
              {"nodeType":"heading-2","content":[{"nodeType":"text","value":"Child development"}]},
              {"nodeType":"paragraph","content":[{"nodeType":"text","value":"<script>alert(1)</script>"}]}
            ]}
            """;
        var response = JsonNode.Parse(VideoResponse)!;
        response["items"]![0]!["fields"]!["criteria"] = JsonNode.Parse(richText);
        response["includes"]!["Entry"]![0]!["fields"]!["transcript"] = JsonNode.Parse(richText);
        response["includes"]!["Entry"]![0]!["fields"]!["video_id"] = "";
        var module = await MapModuleResponseAsync(response.ToJsonString());

        var renderer = new TrainingContentRenderer();
        foreach (var markdown in new[] { module.Criteria, Assert.Single(module.Pages).Video!.Transcript })
        {
            var html = renderer.Render(markdown).Value!;
            Assert.Contains("<h2>Child development</h2>", html);
            Assert.Contains("&lt;script&gt;", html);
            Assert.DoesNotContain("<script", html);
        }
    }

    [Fact]
    public async Task RichTextPreservesTableHeadersAndCells()
    {
        var page = await MapRichTextPageAsync("""
            {"nodeType":"document","content":[{"nodeType":"table","content":[
              {"nodeType":"table-row","content":[
                {"nodeType":"table-header-cell","content":[{"nodeType":"paragraph","content":[{"nodeType":"text","value":"Topic"}]}]},
                {"nodeType":"table-header-cell","content":[{"nodeType":"paragraph","content":[{"nodeType":"text","value":"Description"}]}]}
              ]},
              {"nodeType":"table-row","content":[
                {"nodeType":"table-cell","content":[{"nodeType":"paragraph","content":[{"nodeType":"text","value":"Learning | development"}]}]},
                {"nodeType":"table-cell","content":[{"nodeType":"paragraph","content":[{"nodeType":"text","value":"Child development"}]}]}
              ]}
            ]}]}
            """);

        var html = new TrainingContentRenderer().Render(page.Body).Value!;
        Assert.Contains("<table>", html);
        Assert.Contains("<th>Topic</th>", html);
        Assert.Contains("<td>Learning | development</td>", html);
        Assert.Contains("<td>Child development</td>", html);
    }

    [Theory]
    [InlineData("code", "a `value` and **literal**", "<code>a `value` and **literal**</code>")]
    [InlineData("bold", " padded ", " <strong>padded</strong> ")]
    public async Task RichTextPreservesLiteralCodeAndWhitespaceAroundEmphasis(string mark, string value, string expected)
    {
        var document = new JsonObject
        {
            ["nodeType"] = "document",
            ["content"] = new JsonArray(new JsonObject
            {
                ["nodeType"] = "paragraph",
                ["content"] = new JsonArray(
                    new JsonObject { ["nodeType"] = "text", ["value"] = "Before" },
                    new JsonObject
                    {
                        ["nodeType"] = "text", ["value"] = value,
                        ["marks"] = new JsonArray(new JsonObject { ["type"] = mark })
                    },
                    new JsonObject { ["nodeType"] = "text", ["value"] = "after" })
            })
        };
        var page = await MapRichTextPageAsync(document.ToJsonString());

        Assert.Contains(expected, new TrainingContentRenderer().Render(page.Body).Value!);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,unsafe")]
    [InlineData("https://example.test\\path")]
    public async Task UnsafeRichTextLinksRaiseAnExplicitContentError(string destination)
    {
        var document = JsonNode.Parse("""
            {"nodeType":"document","content":[{"nodeType":"hyperlink","data":{"uri":""},"content":[{"nodeType":"text","value":"link"}]}]}
            """)!;
        document["content"]![0]!["data"]!["uri"] = destination;

        await Assert.ThrowsAsync<InvalidDataException>(() => MapRichTextPageAsync(document.ToJsonString()));
    }

    [Fact]
    public async Task UnsupportedRichTextDoesNotSilentlyDisappear()
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => MapRichTextPageAsync("""
            {"nodeType":"document","content":[{"nodeType":"embedded-entry-block","content":[]}]}
            """));
    }

    [Fact]
    public async Task RichTextPreservesHeadingsMarksLinksAndNestedLists()
    {
        var page = await MapRichTextPageAsync("""
            {"nodeType":"document","content":[
              {"nodeType":"heading-2","content":[{"nodeType":"text","value":"How children learn","marks":[]}]},
              {"nodeType":"paragraph","content":[
                {"nodeType":"text","value":"Read *literally* ","marks":[]},
                {"nodeType":"text","value":"carefully","marks":[{"type":"bold"},{"type":"italic"}]},
                {"nodeType":"text","value":" and ","marks":[]},
                {"nodeType":"hyperlink","data":{"uri":"https://example.test/guide?q=one&next=two"},"content":[{"nodeType":"text","value":"this guide","marks":[]}]}
              ]},
              {"nodeType":"unordered-list","content":[
                {"nodeType":"list-item","content":[
                  {"nodeType":"paragraph","content":[{"nodeType":"text","value":"First topic","marks":[]}]},
                  {"nodeType":"ordered-list","content":[{"nodeType":"list-item","content":[{"nodeType":"paragraph","content":[{"nodeType":"text","value":"Nested topic","marks":[]}]}]}]}
                ]}
              ]}
            ]}
            """);

        var html = new TrainingContentRenderer().Render(page.Body).Value!;
        Assert.Contains("<h2>How children learn</h2>", html);
        Assert.Contains("Read *literally*", html);
        Assert.Contains("<em><strong>carefully</strong></em>", html);
        Assert.Contains("href=\"https://example.test/guide?q=one&amp;next=two\"", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<ol>", html);
        Assert.Contains("Nested topic", html);
    }

    [Fact]
    public async Task RichTextResolvesLocalizedLinkedImagesWithDescriptionAsAlternativeText()
    {
        var page = await MapRichTextPageAsync("""
            {"nodeType":"document","content":[
              {"nodeType":"embedded-asset-block","data":{"target":{"sys":{"id":"brain-image","type":"Link","linkType":"Asset"}}},"content":[]}
            ]}
            """, """
            [{"sys":{"id":"brain-image","type":"Asset"},"fields":{
              "description":{"en-US":"Diagram of a child's brain"},
              "title":{"en-US":"Brain"},
              "file":{"en-US":{"url":"//images.ctfassets.net/space/brain.png","contentType":"image/png"}}
            }}]
            """);

        var html = new TrainingContentRenderer().Render(page.Body).Value!;
        Assert.Contains("<img", html);
        Assert.Contains("src=\"https://images.ctfassets.net/space/brain.png\"", html);
        Assert.Contains("alt=\"Diagram of a child's brain\"", html);
    }

    [Theory]
    [InlineData("missing-image", "[]")]
    [InlineData("brain-image", """[{"sys":{"id":"brain-image","type":"Asset"},"fields":{"file":{"url":"javascript:alert(1)","contentType":"image/png"}}}]""")]
    [InlineData("brain-image", """[{"sys":{"id":"brain-image","type":"Asset"},"fields":{"file":{"url":"https://example.test/document.pdf","contentType":"application/pdf"}}}]""")]
    public async Task BrokenOrInvalidEmbeddedImagesRaiseAnExplicitContentError(string id, string assets)
    {
        var body = """
            {"nodeType":"document","content":[
              {"nodeType":"embedded-asset-block","data":{"target":{"sys":{"id":"ASSET_ID","type":"Link","linkType":"Asset"}}},"content":[]}
            ]}
            """.Replace("ASSET_ID", id, StringComparison.Ordinal);

        await Assert.ThrowsAsync<InvalidDataException>(() => MapRichTextPageAsync(body, assets));
    }

    private static async Task<EarlyYearsFoundationRecovery.Application.Contracts.TrainingPage> MapRichTextPageAsync(
        string body,
        string assets = "[]")
    {
        var response = JsonNode.Parse(VideoResponse)!;
        var fields = response["includes"]!["Entry"]![0]!["fields"]!;
        response["includes"]!["Entry"]![0]!["sys"]!["contentType"]!["sys"]!["id"] = "page";
        fields["body"] = JsonNode.Parse(body);
        response["includes"]!["Asset"] = JsonNode.Parse(assets);
        return await MapPageResponseAsync(response.ToJsonString());
    }

    [Theory]
    [InlineData("youtube", "XnP6jaK7ZAY", "https://www.youtube.com/embed/XnP6jaK7ZAY")]
    [InlineData("YouTube", "XnP6jaK7ZAY", "https://www.youtube.com/embed/XnP6jaK7ZAY")]
    [InlineData("vimeo", "743243040", "https://player.vimeo.com/video/743243040")]
    public async Task MapsVideoProviderTitleAndTranscript(string provider, string id, string expectedUrl)
    {
        var page = await MapVideoPageAsync(provider, id);

        Assert.Equal("video_page", page.PageType);
        Assert.Equal("Video heading", page.Title);
        Assert.NotNull(page.Video);
        Assert.Equal(expectedUrl, page.Video.EmbedUrl);
        Assert.Equal("Video title", page.Video.Title);
        Assert.Equal("## Transcript\n\nAn expert explains child development.", page.Video.Transcript);
    }

    [Fact]
    public async Task VideoWithoutAnIdStillProvidesATranscript()
    {
        var page = await MapVideoPageAsync("youtube", "");

        Assert.NotNull(page.Video);
        Assert.Null(page.Video.EmbedUrl);
        Assert.NotEmpty(page.Video.Transcript);
    }

    [Theory]
    [InlineData("unknown", "video-id")]
    [InlineData("youtube", "../untrusted")]
    [InlineData("youtube", "https://untrusted.test/video")]
    [InlineData("vimeo", "123?autoplay=1")]
    public async Task InvalidVideoMetadataRaisesAnExplicitContentError(string provider, string id)
    {
        await Assert.ThrowsAsync<InvalidDataException>(() => MapVideoPageAsync(provider, id));
    }

    private static async Task<EarlyYearsFoundationRecovery.Application.Contracts.TrainingPage> MapVideoPageAsync(
        string provider,
        string id)
    {
        var response = VideoResponse
            .Replace("VIDEO_PROVIDER", provider, StringComparison.Ordinal)
            .Replace("VIDEO_ID", id, StringComparison.Ordinal);
        return await MapPageResponseAsync(response);
    }

    private static async Task<TrainingPage> MapPageResponseAsync(string response) =>
        Assert.Single((await MapModuleResponseAsync(response)).Pages);

    private static async Task<TrainingModule> MapModuleResponseAsync(string response)
    {
        using var httpClient = new HttpClient(new StubHttpMessageHandler(response));
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Contentful:Space"] = "test-space",
                ["Contentful:DeliveryToken"] = "test-token"
            })
            .Build();
        var client = new ContentfulDeliveryClient(httpClient, configuration, cache);
        var modules = await new ContentfulTrainingContentProvider(client).GetModulesAsync(CancellationToken.None);
        return Assert.Single(modules);
    }

    private const string VideoResponse = """
        {
          "items": [{
            "sys": {"id": "module-one", "contentType": {"sys": {"id": "trainingModule"}}},
            "fields": {
              "name": "module-one",
              "title": "Module one",
              "pages": [{"sys": {"id": "video-one", "type": "Link", "linkType": "Entry"}}]
            }
          }],
          "includes": {
            "Entry": [{
              "sys": {"id": "video-one", "contentType": {"sys": {"id": "video"}}},
              "fields": {
                "name": "video-one",
                "heading": "Video heading",
                "title": "Video title",
                "video_id": "VIDEO_ID",
                "video_provider": "VIDEO_PROVIDER",
                "transcript": "## Transcript\n\nAn expert explains child development."
              }
            }]
          }
        }
        """;

    [Fact]
    public async Task MapsLinkedModulePagesAndSummativeAnswers()
    {
        var handler = new StubHttpMessageHandler(ContentfulResponse);
        using var httpClient = new HttpClient(handler);
        using var cache = new MemoryCache(new MemoryCacheOptions());
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Contentful:Space"] = "test-space",
                ["Contentful:Environment"] = "test",
                ["Contentful:DeliveryToken"] = "test-token"
            })
            .Build();
        var contentful = new ContentfulDeliveryClient(httpClient, configuration, cache);
        var provider = new ContentfulTrainingContentProvider(contentful);

        var modules = await provider.GetModulesAsync(CancellationToken.None);
        var module = Assert.Single(modules);
        var question = Assert.Single(module.Pages);

        Assert.Equal("module-one", module.Name);
        Assert.Equal("- Understand child development", module.Criteria);
        Assert.False(module.IsDraft);
        Assert.Equal("summative", question.PageType);
        Assert.Equal("Which answer is correct?", question.Body);
        Assert.Equal("Read [the topic](/training/module-one/content/topic) again.", question.FailureMessage);
        Assert.Equal("Well done: **observe individual needs**.", question.SuccessMessage);
        Assert.Equal(["2"], question.CorrectAnswerIds);
        Assert.Equal("Yes", question.Options![1].Label);
        Assert.True(question.AllowsMultipleAnswers is false);
        Assert.Equal("Bearer", handler.AuthorizationScheme);
        Assert.Equal("test-token", handler.AuthorizationParameter);
    }

    private const string ContentfulResponse = """
        {
          "items": [
            {
              "sys": {
                "id": "module-one",
                "contentType": { "sys": { "id": "trainingModule" } }
              },
              "fields": {
                "name": { "en-US": "module-one" },
                "title": { "en-US": "Module one" },
                "criteria": { "en-US": "- Understand child development" },
                "position": { "en-US": 1 },
                "pages": { "en-US": [{ "sys": { "type": "Link", "linkType": "Entry", "id": "question-one" } }] }
              }
            }
          ],
          "includes": {
            "Entry": [
              {
                "sys": {
                  "id": "question-one",
                  "contentType": { "sys": { "id": "question" } }
                },
                "fields": {
                  "name": { "en-US": "question-one" },
                  "heading": { "en-US": "Question one" },
                  "page_type": { "en-US": "summative" },
                  "body": {
                    "en-US": {
                      "nodeType": "document",
                      "content": [
                        { "nodeType": "paragraph", "content": [{ "nodeType": "text", "value": "Which answer is correct?" }] }
                      ]
                    }
                  },
                  "answers": { "en-US": [["No", false], ["Yes", true]] },
                  "failure_message": { "en-US": "Read [the topic](/training/module-one/content/topic) again." },
                  "success_message": { "en-US": "Well done: **observe individual needs**." }
                }
              }
            ]
          }
        }
        """;

    private sealed class StubHttpMessageHandler(string responseBody) : HttpMessageHandler
    {
        public string? AuthorizationScheme { get; private set; }

        public string? AuthorizationParameter { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            AuthorizationScheme = request.Headers.Authorization?.Scheme;
            AuthorizationParameter = request.Headers.Authorization?.Parameter;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseBody, Encoding.UTF8, "application/json")
            });
        }
    }
}
