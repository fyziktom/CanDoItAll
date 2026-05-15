using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;

namespace CanDoItAll.Tests.Integration;

public sealed class EmailPluginClientTests
{
    [Fact]
    public void Gmail_download_payload_uses_resolved_connection_id()
    {
        var connectionId = new PluginConnectionId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var payload = InvokeGmailDownloadPayloadFactory(
            new WorkflowNodeInput("""
            {
              "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "runContext": {
                "workflowNodeId": "node-1"
              }
            }
            """),
            new GmailWorkflowExecutorSettings
            {
                ConnectionId = string.Empty,
                Label = "CanDoItAllSummaryTest",
                ProcessedLabel = "CanDoItAllSummaryTestProcessed",
                MaxMessages = 1
            },
            connectionId,
            new PluginEmailMessageBatch(
                "gmail",
                "label",
                "CanDoItAllSummaryTest",
                1,
                [CreateMessage("msg-1")]));

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal(connectionId.ToString(), root.GetProperty("gmailProcessing").GetProperty("connectionId").GetString());
        Assert.Equal(connectionId.ToString(), root.GetProperty("runContext").GetProperty("gmailProcessing").GetProperty("connectionId").GetString());
    }

    [Fact]
    public void Office365_download_payload_uses_resolved_connection_id()
    {
        var connectionId = new PluginConnectionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var payload = InvokeOffice365DownloadPayloadFactory(
            new WorkflowNodeInput("""
            {
              "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "runContext": {
                "workflowNodeId": "node-1"
              }
            }
            """),
            new Office365WorkflowExecutorSettings
            {
                ConnectionId = string.Empty,
                Category = "CanDoItAllSummaryTest",
                ProcessedCategory = "CanDoItAllSummaryTestProcessed",
                MaxMessages = 1
            },
            connectionId,
            new PluginEmailMessageBatch(
                "office365",
                "category",
                "CanDoItAllSummaryTest",
                1,
                [CreateMessage("graph-1")]));

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal(connectionId.ToString(), root.GetProperty("office365Processing").GetProperty("connectionId").GetString());
        Assert.Equal(connectionId.ToString(), root.GetProperty("runContext").GetProperty("office365Processing").GetProperty("connectionId").GetString());
    }

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

    [Fact]
    public async Task Office365_client_marks_message_processed_by_moving_categories_and_creating_processed_category()
    {
        var createCalled = false;
        var patchCalled = false;
        var client = new Office365GraphClient(new FakeHttpClientFactory(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("graph-token", request.Headers.Authorization?.Parameter);
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get &&
                pathAndQuery == "/v1.0/me/outlook/masterCategories?$select=displayName,color")
            {
                return Json(new
                {
                    value = new[]
                    {
                        new { displayName = "CanDoItAllSummaryTest", color = "preset1" }
                    }
                });
            }

            if (request.Method == HttpMethod.Post &&
                pathAndQuery == "/v1.0/me/outlook/masterCategories")
            {
                createCalled = true;
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                Assert.Contains("CanDoItAllSummaryTestProcessed", body, StringComparison.Ordinal);
                Assert.Contains("preset0", body, StringComparison.Ordinal);
                return Json(new { displayName = "CanDoItAllSummaryTestProcessed", color = "preset0" }, HttpStatusCode.Created);
            }

            if (request.Method == HttpMethod.Get &&
                pathAndQuery == "/v1.0/me/messages/graph-1?$select=id,categories")
            {
                return Json(new
                {
                    id = "graph-1",
                    categories = new[] { "CanDoItAllSummaryTest", "Existing" }
                });
            }

            if (request.Method == HttpMethod.Patch &&
                pathAndQuery == "/v1.0/me/messages/graph-1")
            {
                patchCalled = true;
                var body = request.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
                using var document = JsonDocument.Parse(body);
                var categories = document.RootElement.GetProperty("categories")
                    .EnumerateArray()
                    .Select(item => item.GetString())
                    .ToArray();
                Assert.DoesNotContain("CanDoItAllSummaryTest", categories);
                Assert.Contains("CanDoItAllSummaryTestProcessed", categories);
                Assert.Contains("Existing", categories);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var result = await client.MarkMessageProcessedAsync(
            "graph-token",
            "graph-1",
            "CanDoItAllSummaryTest",
            "CanDoItAllSummaryTestProcessed");

        Assert.Equal("graph-1", result.MessageId);
        Assert.True(result.SourceCategoryRemoved);
        Assert.True(result.ProcessedCategoryAdded);
        Assert.True(result.ProcessedCategoryCreated);
        Assert.Contains("CanDoItAllSummaryTestProcessed", result.Categories);
        Assert.True(createCalled);
        Assert.True(patchCalled);
    }

    private static HttpResponseMessage Json(object payload, HttpStatusCode statusCode = HttpStatusCode.OK)
        => new(statusCode)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };

    private static string Base64Url(string value)
        => Convert.ToBase64String(Encoding.UTF8.GetBytes(value))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private static PluginEmailMessage CreateMessage(string id)
        => new(
            id,
            "thread-1",
            "Subject line",
            "sender@example.test",
            "2026-05-13T10:00:00Z",
            "snippet",
            "Email body",
            [],
            "https://mail.example.test/message");

    private static string InvokeGmailDownloadPayloadFactory(
        WorkflowNodeInput input,
        GmailWorkflowExecutorSettings settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
        => InvokeDownloadPayloadFactory<GmailDownloadByLabelWorkflowExecutor>(
            input,
            settings,
            connectionId,
            batch);

    private static string InvokeOffice365DownloadPayloadFactory(
        WorkflowNodeInput input,
        Office365WorkflowExecutorSettings settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
        => InvokeDownloadPayloadFactory<Office365DownloadByCategoryWorkflowExecutor>(
            input,
            settings,
            connectionId,
            batch);

    private static string InvokeDownloadPayloadFactory<TExecutor>(
        WorkflowNodeInput input,
        object settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
        => (string)(typeof(TExecutor).GetMethod(
                "CreatePayload",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?.Invoke(null, [input, settings, connectionId, batch])
            ?? throw new InvalidOperationException($"Could not invoke payload factory for {typeof(TExecutor).Name}."));

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
