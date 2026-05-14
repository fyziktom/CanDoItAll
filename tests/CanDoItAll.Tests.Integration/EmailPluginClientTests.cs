using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.Modules.Plugins;

namespace CanDoItAll.Tests.Integration;

public sealed class EmailPluginClientTests
{
    [Fact]
    public async Task Gmail_client_downloads_messages_by_label()
    {
        var client = new GmailApiClient(new FakeHttpClientFactory(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("gmail-token", request.Headers.Authorization?.Parameter);
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            if (pathAndQuery == "/gmail/v1/users/me/labels")
            {
                return Json(new
                {
                    labels = new[]
                    {
                        new { id = "Label_1", name = "CanDoItAll-Test" }
                    }
                });
            }

            if (pathAndQuery.Contains("/gmail/v1/users/me/messages?", StringComparison.Ordinal))
            {
                Assert.Contains("labelIds=Label_1", pathAndQuery, StringComparison.Ordinal);
                return Json(new
                {
                    messages = new[]
                    {
                        new { id = "msg-1" }
                    }
                });
            }

            if (pathAndQuery == "/gmail/v1/users/me/messages/msg-1?format=full")
            {
                return Json(new
                {
                    id = "msg-1",
                    threadId = "thread-1",
                    snippet = "snippet",
                    labelIds = new[] { "Label_1" },
                    payload = new
                    {
                        mimeType = "text/plain",
                        headers = new[]
                        {
                            new { name = "Received", value = "by mx-1.example.test" },
                            new { name = "Received", value = "by mx-2.example.test" },
                            new { name = "Subject", value = "Subject line" },
                            new { name = "From", value = "sender@example.test" },
                            new { name = "Date", value = "Wed, 13 May 2026 10:00:00 +0000" }
                        },
                        body = new
                        {
                            data = Base64Url("Email body")
                        },
                        parts = Array.Empty<object>()
                    }
                });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var batch = await client.DownloadMessagesByLabelAsync("gmail-token", "CanDoItAll-Test", 5);

        var message = Assert.Single(batch.Messages);
        Assert.Equal("gmail", batch.Provider);
        Assert.Equal("label", batch.FilterKind);
        Assert.Equal("Subject line", message.Subject);
        Assert.Equal("sender@example.test", message.From);
        Assert.Equal("Email body", message.BodyText);
    }

    [Fact]
    public async Task Gmail_client_marks_message_processed_by_moving_labels()
    {
        var createCalled = false;
        var modifyCalled = false;
        var client = new GmailApiClient(new FakeHttpClientFactory(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("gmail-token", request.Headers.Authorization?.Parameter);
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get &&
                pathAndQuery == "/gmail/v1/users/me/labels")
            {
                return Json(new
                {
                    labels = new[]
                    {
                        new { id = "Label_1", name = "CanDoItAllSummaryTest" }
                    }
                });
            }

            if (request.Method == HttpMethod.Post &&
                pathAndQuery == "/gmail/v1/users/me/labels")
            {
                createCalled = true;
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                Assert.Contains("CanDoItAllSummaryTestProcessed", body, StringComparison.Ordinal);
                return Json(new { id = "Label_Processed", name = "CanDoItAllSummaryTestProcessed" });
            }

            if (request.Method == HttpMethod.Post &&
                pathAndQuery == "/gmail/v1/users/me/messages/msg-1/modify")
            {
                modifyCalled = true;
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                Assert.Contains("Label_Processed", body, StringComparison.Ordinal);
                Assert.Contains("Label_1", body, StringComparison.Ordinal);
                return Json(new { id = "msg-1" });
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var result = await client.MarkMessageProcessedAsync(
            "gmail-token",
            "msg-1",
            "CanDoItAllSummaryTest",
            "CanDoItAllSummaryTestProcessed");

        Assert.Equal("msg-1", result.MessageId);
        Assert.True(result.SourceLabelRemoved);
        Assert.True(result.ProcessedLabelAdded);
        Assert.True(createCalled);
        Assert.True(modifyCalled);
    }

    [Fact]
    public async Task Office365_client_filters_messages_by_category()
    {
        var client = new Office365GraphClient(new FakeHttpClientFactory(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("graph-token", request.Headers.Authorization?.Parameter);
            var decoded = WebUtility.UrlDecode(request.RequestUri!.Query);
            Assert.Contains("categories/any(c:c eq 'CanDoItAll-Test')", decoded, StringComparison.Ordinal);
            return Json(new
            {
                value = new[]
                {
                    new
                    {
                        id = "graph-1",
                        subject = "Graph subject",
                        from = new
                        {
                            emailAddress = new
                            {
                                name = "Sender",
                                address = "sender@example.test"
                            }
                        },
                        receivedDateTime = "2026-05-13T10:00:00Z",
                        bodyPreview = "preview",
                        body = new
                        {
                            contentType = "text",
                            content = "Graph body"
                        },
                        categories = new[] { "CanDoItAll-Test" },
                        webLink = "https://outlook.office.test/message"
                    }
                }
            });
        }));

        var batch = await client.DownloadMessagesByCategoryAsync("graph-token", "CanDoItAll-Test", 5);

        var message = Assert.Single(batch.Messages);
        Assert.Equal("office365", batch.Provider);
        Assert.Equal("category", batch.FilterKind);
        Assert.Equal("Graph subject", message.Subject);
        Assert.Equal("sender@example.test", message.From);
        Assert.Equal("Graph body", message.BodyText);
        Assert.Contains("CanDoItAll-Test", message.Labels);
    }

    private static HttpResponseMessage Json(object payload)
        => new(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

    private static string Base64Url(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private sealed class FakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name)
            => new(new FakeHandler(handler))
            {
                BaseAddress = new Uri("https://example.test")
            };
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> handler) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(handler(request));
    }
}
