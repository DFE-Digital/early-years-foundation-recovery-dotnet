using System.Net;
using System.Text;
using EarlyYearsFoundationRecovery.Infrastructure.Contentful;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace EarlyYearsFoundationRecovery.UnitTests;

public sealed class ContentfulTrainingContentProviderTests
{
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
        Assert.False(module.IsDraft);
        Assert.Equal("summative", question.PageType);
        Assert.Equal("Which answer is correct?", question.Body);
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
                  "answers": { "en-US": [["No", false], ["Yes", true]] }
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
