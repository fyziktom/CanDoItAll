using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Plugins.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class EmailPluginClientTests
{
    [Fact]
    public void Office365_plugin_descriptor_and_di_register_address_executor()
    {
        var services = new ServiceCollection();

        services.AddCanDoItAllOffice365Plugin();

        using var provider = services.BuildServiceProvider();
        var plugin = Assert.Single(provider.GetServices<ICanDoItAllPlugin>(), item => item.Descriptor.Id == Office365PluginConstants.PluginId);
        Assert.Contains(
            plugin.Descriptor.WorkflowExecutors,
            executor => executor.ExecutorId == Office365PluginConstants.DownloadByAddressExecutorId &&
                        executor.Name == "Office365 unprocessed message by address" &&
                        executor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.ReadsExternalData) &&
                        executor.SideEffects.Kind == WorkflowExecutorSideEffectKind.ExternalRead &&
                        executor.DeterministicTestMode.IsSupported);
        Assert.Contains(
            plugin.Descriptor.WorkflowExecutors,
            executor => executor.ExecutorId == Office365PluginConstants.MarkProcessedExecutorId &&
                        executor.PermissionPolicy.RequiredCapabilities.HasFlag(WorkflowExecutorCapabilityFlags.IdempotentExternalMarker) &&
                        executor.SideEffects.ExternalMutationKind == WorkflowExecutorExternalMutationKind.ProcessedMarker &&
                        executor.SideEffects.AllowsIdempotentRetry);
        Assert.Contains(
            services,
            descriptor => descriptor.ServiceType == typeof(IWorkflowExecutorContribution) &&
                          descriptor.ImplementationType?.GenericTypeArguments.Contains(
                              typeof(Office365DownloadByAddressWorkflowExecutor)) == true);
    }

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
        var processing = root.GetProperty("gmailProcessing");
        Assert.Equal(connectionId.ToString(), processing.GetProperty("connectionId").GetString());
        Assert.Equal("msg-1", processing.GetProperty("selectedMessageId").GetString());
        Assert.Equal("gmail:msg-1", processing.GetProperty("idempotencyKey").GetString());
        Assert.Equal(connectionId.ToString(), root.GetProperty("runContext").GetProperty("gmailProcessing").GetProperty("connectionId").GetString());
        Assert.Equal(
            "gmail:msg-1",
            root.GetProperty("runContext").GetProperty("gmailProcessing").GetProperty("idempotencyKey").GetString());
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
    public void Office365_address_download_payload_preserves_scheduler_context_and_idempotency()
    {
        var connectionId = new PluginConnectionId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var payload = InvokeOffice365AddressDownloadPayloadFactory(
            new WorkflowNodeInput("""
            {
              "emailAddress": "sender@example.test",
              "projectId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
              "nodeId": "node-1",
              "runContext": {
                "workflowNodeId": "scheduler-node"
              }
            }
            """),
            new Office365MessageAddressWorkflowExecutorSettings
            {
                ConnectionId = string.Empty,
                ProcessedCategory = "CanDoItAllProcessed"
            },
            connectionId,
            new PluginEmailMessageBatch(
                "office365",
                "emailAddress",
                "sender@example.test",
                1,
                [CreateMessage("graph-1")]));

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var processing = root.GetProperty("office365Processing");
        Assert.Equal(connectionId.ToString(), processing.GetProperty("connectionId").GetString());
        Assert.Equal("graph-1", processing.GetProperty("selectedMessageId").GetString());
        Assert.Equal("office365:graph-1", processing.GetProperty("idempotencyKey").GetString());
        Assert.Equal("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa", root.GetProperty("projectId").GetString());
        Assert.Equal("node-1", root.GetProperty("nodeId").GetString());
        Assert.Equal(
            "office365:graph-1",
            root.GetProperty("runContext").GetProperty("office365Processing").GetProperty("idempotencyKey").GetString());
    }

    [Fact]
    public void Office365_address_download_payload_marks_no_message_as_success_route()
    {
        var payload = InvokeOffice365AddressDownloadPayloadFactory(
            new WorkflowNodeInput("""{"emailAddress":"sender@example.test"}"""),
            new Office365MessageAddressWorkflowExecutorSettings
            {
                ProcessedCategory = "CanDoItAllProcessed"
            },
            new PluginConnectionId(Guid.Parse("22222222-2222-2222-2222-222222222222")),
            new PluginEmailMessageBatch(
                "office365",
                "emailAddress",
                "sender@example.test",
                0,
                []));

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        Assert.Equal(0, root.GetProperty("count").GetInt32());
        Assert.True(root.GetProperty("noMessages").GetBoolean());
        Assert.Equal("no_messages", root.GetProperty("route").GetString());
        Assert.Empty(root.GetProperty("messages").EnumerateArray());
        Assert.Empty(root.GetProperty("office365Processing").GetProperty("messageIds").EnumerateArray());
        Assert.Equal(string.Empty, root.GetProperty("office365Processing").GetProperty("selectedMessageId").GetString());
    }

    [Fact]
    public void Gmail_mark_processed_payload_includes_commit_side_effect_receipts()
    {
        var payload = InvokeMarkProcessedPayloadFactory<GmailMarkProcessedWorkflowExecutor, GmailMessageLabelMutationResult>(
            new WorkflowNodeInput("""{"runContext":{"gmailProcessing":{"idempotencyKey":"gmail:msg-1"}}}"""),
            new GmailMessageLabelMutationResult(
                "gmail",
                "msg-1",
                "CanDoItAllSummaryTest",
                "CanDoItAllSummaryTestProcessed",
                SourceLabelRemoved: true,
                ProcessedLabelAdded: true));

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var receipt = root.GetProperty("externalSideEffectReceipt");
        Assert.Equal("Commit", root.GetProperty("sideEffectMode").GetString());
        Assert.False(root.GetProperty("dryRun").GetBoolean());
        Assert.True(root.GetProperty("committed").GetBoolean());
        Assert.Equal("workflow-email-processed-marker/v1", receipt.GetProperty("schemaVersion").GetString());
        Assert.Equal("gmail", receipt.GetProperty("provider").GetString());
        Assert.Equal("gmail:msg-1", receipt.GetProperty("idempotencyKey").GetString());
        Assert.True(receipt.GetProperty("mutationApplied").GetBoolean());
        Assert.True(root.GetProperty("idempotencyRecord").GetProperty("retrySafe").GetBoolean());
        Assert.Equal("CanDoItAllSummaryTestProcessed", root.GetProperty("processedMarker").GetProperty("processedMarkerName").GetString());
    }

    [Fact]
    public void Office365_mark_processed_payload_includes_commit_side_effect_receipts()
    {
        var payload = InvokeMarkProcessedPayloadFactory<Office365MarkProcessedWorkflowExecutor, Office365MessageCategoryMutationResult>(
            new WorkflowNodeInput("""{"runContext":{"office365Processing":{"idempotencyKey":"office365:graph-1"}}}"""),
            new Office365MessageCategoryMutationResult(
                "office365",
                "graph-1",
                "CanDoItAllSummaryTest",
                "CanDoItAllSummaryTestProcessed",
                SourceCategoryRemoved: true,
                ProcessedCategoryAdded: true,
                ProcessedCategoryCreated: false,
                ["CanDoItAllSummaryTestProcessed"]));

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var receipt = root.GetProperty("externalSideEffectReceipt");
        Assert.Equal("Commit", root.GetProperty("sideEffectMode").GetString());
        Assert.False(root.GetProperty("dryRun").GetBoolean());
        Assert.True(root.GetProperty("committed").GetBoolean());
        Assert.Equal("workflow-email-processed-marker/v1", receipt.GetProperty("schemaVersion").GetString());
        Assert.Equal("office365", receipt.GetProperty("provider").GetString());
        Assert.Equal("office365:graph-1", receipt.GetProperty("idempotencyKey").GetString());
        Assert.True(receipt.GetProperty("mutationApplied").GetBoolean());
        Assert.True(root.GetProperty("idempotencyRecord").GetProperty("retrySafe").GetBoolean());
        Assert.Equal("CanDoItAllSummaryTestProcessed", root.GetProperty("processedMarker").GetProperty("processedMarkerName").GetString());
    }

    [Fact]
    public void Office365_address_filter_settings_resolve_scheduler_input_paths()
    {
        var settings = new Office365MessageAddressWorkflowExecutorSettings
        {
            EmailAddressJsonPath = "$.email",
            ProcessedCategory = string.Empty,
            ProcessedCategoryJsonPath = "$.processedCategory",
            LookbackHours = 336,
            LookbackHoursJsonPath = "$.lookbackHours"
        };

        var filter = InvokeOffice365AddressFilterSettingsFactory(
            settings,
            new WorkflowNodeInput("""
            {
              "email": "Sender@Example.Test",
              "processedCategory": "Office365SchedulerProcessed",
              "lookbackHours": 2
            }
            """));

        Assert.Equal("Sender@Example.Test", filter.EmailAddress);
        Assert.Equal("Office365SchedulerProcessed", filter.ProcessedCategory);
        Assert.Equal(2, filter.LookbackHours);
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
        var labelStateRead = false;
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

            if (request.Method == HttpMethod.Get &&
                pathAndQuery == "/gmail/v1/users/me/messages/msg-1?format=minimal")
            {
                labelStateRead = true;
                return Json(new
                {
                    id = "msg-1",
                    labelIds = new[] { "Label_1" }
                });
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
        Assert.True(labelStateRead);
        Assert.True(modifyCalled);
    }

    [Fact]
    public async Task Gmail_client_skips_modify_when_message_already_has_processed_marker()
    {
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
                        new { id = "Label_1", name = "CanDoItAllSummaryTest" },
                        new { id = "Label_Processed", name = "CanDoItAllSummaryTestProcessed" }
                    }
                });
            }

            if (request.Method == HttpMethod.Get &&
                pathAndQuery == "/gmail/v1/users/me/messages/msg-1?format=minimal")
            {
                return Json(new
                {
                    id = "msg-1",
                    labelIds = new[] { "Label_Processed" }
                });
            }

            if (request.Method == HttpMethod.Post &&
                pathAndQuery == "/gmail/v1/users/me/messages/msg-1/modify")
            {
                modifyCalled = true;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var result = await client.MarkMessageProcessedAsync(
            "gmail-token",
            "msg-1",
            "CanDoItAllSummaryTest",
            "CanDoItAllSummaryTestProcessed");

        Assert.Equal("msg-1", result.MessageId);
        Assert.False(result.SourceLabelRemoved);
        Assert.False(result.ProcessedLabelAdded);
        Assert.False(modifyCalled);
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
    public async Task Office365_client_downloads_one_unprocessed_message_by_address_with_processed_category_exclusion()
    {
        var client = new Office365GraphClient(new FakeHttpClientFactory(request =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("graph-token", request.Headers.Authorization?.Parameter);
            var decoded = WebUtility.UrlDecode(request.RequestUri!.Query);
            Assert.Contains("$top=1", decoded, StringComparison.Ordinal);
            Assert.Contains("from/emailAddress/address eq 'sender@example.test'", decoded, StringComparison.Ordinal);
            Assert.Contains("sender/emailAddress/address eq 'sender@example.test'", decoded, StringComparison.Ordinal);
            Assert.Contains("not(categories/any(c:c eq 'CanDoItAllProcessed'))", decoded, StringComparison.Ordinal);
            return Json(new
            {
                value = new[]
                {
                    new
                    {
                        id = "graph-1",
                        conversationId = "conversation-1",
                        internetMessageId = "internet-1",
                        subject = "Graph subject",
                        from = new
                        {
                            emailAddress = new
                            {
                                name = "Sender",
                                address = "sender@example.test"
                            }
                        },
                        sender = new
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
                        categories = Array.Empty<string>(),
                        webLink = "https://outlook.office.test/message"
                    }
                }
            });
        }));

        var batch = await client.DownloadOneUnprocessedMessageByAddressAsync(
            "graph-token",
            new Office365MessageAddressFilterSettings
            {
                EmailAddress = "Sender@Example.Test",
                ProcessedCategory = "CanDoItAllProcessed",
                MaxCandidateMessages = 5
            });

        var message = Assert.Single(batch.Messages);
        Assert.Equal("office365", batch.Provider);
        Assert.Equal("emailAddress", batch.FilterKind);
        Assert.Equal("sender@example.test", batch.FilterValue);
        Assert.Equal("conversation-1", message.ThreadId);
        Assert.Equal("Graph subject", message.Subject);
        Assert.Equal("sender@example.test", message.From);
        Assert.Equal("Graph body", message.BodyText);
    }

    [Fact]
    public async Task Office365_client_uses_bounded_fallback_and_ignores_processed_or_wrong_address_messages()
    {
        var requestNumber = 0;
        var client = new Office365GraphClient(new FakeHttpClientFactory(request =>
        {
            requestNumber++;
            var decoded = WebUtility.UrlDecode(request.RequestUri!.Query);
            if (requestNumber == 1)
            {
                Assert.Contains("from/emailAddress/address eq 'sender@example.test'", decoded, StringComparison.Ordinal);
                return Json(new
                {
                    error = new
                    {
                        code = "Request_UnsupportedQuery",
                        message = "Complex filter is unsupported."
                    }
                }, HttpStatusCode.BadRequest);
            }

            Assert.Contains("$top=3", decoded, StringComparison.Ordinal);
            Assert.Contains("not(categories/any(c:c eq 'CanDoItAllProcessed'))", decoded, StringComparison.Ordinal);
            return Json(new
            {
                value = new[]
                {
                    new
                    {
                        id = "graph-processed",
                        subject = "Already processed",
                        from = new { emailAddress = new { name = "Sender", address = "sender@example.test" } },
                        sender = new { emailAddress = new { name = "Sender", address = "sender@example.test" } },
                        receivedDateTime = "2026-05-13T12:00:00Z",
                        bodyPreview = "processed",
                        body = new { contentType = "text", content = "Processed body" },
                        categories = new[] { "CanDoItAllProcessed" },
                        webLink = "https://outlook.office.test/processed"
                    },
                    new
                    {
                        id = "graph-other",
                        subject = "Wrong sender",
                        from = new { emailAddress = new { name = "Other", address = "other@example.test" } },
                        sender = new { emailAddress = new { name = "Other", address = "other@example.test" } },
                        receivedDateTime = "2026-05-13T11:00:00Z",
                        bodyPreview = "other",
                        body = new { contentType = "text", content = "Other body" },
                        categories = Array.Empty<string>(),
                        webLink = "https://outlook.office.test/other"
                    },
                    new
                    {
                        id = "graph-good",
                        subject = "Good sender",
                        from = new { emailAddress = new { name = "Delegate", address = "delegate@example.test" } },
                        sender = new { emailAddress = new { name = "Sender", address = "sender@example.test" } },
                        receivedDateTime = "2026-05-13T10:00:00Z",
                        bodyPreview = "good",
                        body = new { contentType = "text", content = "Good body" },
                        categories = Array.Empty<string>(),
                        webLink = "https://outlook.office.test/good"
                    }
                }
            });
        }));

        var batch = await client.DownloadOneUnprocessedMessageByAddressAsync(
            "graph-token",
            new Office365MessageAddressFilterSettings
            {
                EmailAddress = "sender@example.test",
                ProcessedCategory = "CanDoItAllProcessed",
                MaxCandidateMessages = 3
            });

        var message = Assert.Single(batch.Messages);
        Assert.Equal("graph-good", message.Id);
        Assert.Equal(2, requestNumber);
    }

    [Fact]
    public async Task Office365_client_download_by_address_returns_empty_batch_when_no_candidate_matches()
    {
        var client = new Office365GraphClient(new FakeHttpClientFactory(_ => Json(new { value = Array.Empty<object>() })));

        var batch = await client.DownloadOneUnprocessedMessageByAddressAsync(
            "graph-token",
            new Office365MessageAddressFilterSettings
            {
                EmailAddress = "sender@example.test",
                ProcessedCategory = "CanDoItAllProcessed"
            });

        Assert.Equal(0, batch.Count);
        Assert.Empty(batch.Messages);
    }

    [Fact]
    public async Task Office365_client_rejects_invalid_address_before_graph_call()
    {
        var client = new Office365GraphClient(new FakeHttpClientFactory(_ => throw new InvalidOperationException("Graph should not be called.")));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => client.DownloadOneUnprocessedMessageByAddressAsync(
            "graph-token",
            new Office365MessageAddressFilterSettings
            {
                EmailAddress = "Sender <sender@example.test>",
                ProcessedCategory = "CanDoItAllProcessed"
            }));

        Assert.Contains("single address", exception.Message, StringComparison.Ordinal);
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

    [Fact]
    public async Task Office365_client_can_mark_processed_by_adding_category_without_source_category()
    {
        var patchCalled = false;
        var client = new Office365GraphClient(new FakeHttpClientFactory(request =>
        {
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get &&
                pathAndQuery == "/v1.0/me/outlook/masterCategories?$select=displayName,color")
            {
                return Json(new
                {
                    value = new[]
                    {
                        new { displayName = "CanDoItAllProcessed", color = "preset1" }
                    }
                });
            }

            if (request.Method == HttpMethod.Get &&
                pathAndQuery == "/v1.0/me/messages/graph-1?$select=id,categories")
            {
                return Json(new
                {
                    id = "graph-1",
                    categories = new[] { "Existing" }
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
                Assert.Contains("Existing", categories);
                Assert.Contains("CanDoItAllProcessed", categories);
                return new HttpResponseMessage(HttpStatusCode.NoContent);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var result = await client.MarkMessageProcessedAsync(
            "graph-token",
            "graph-1",
            string.Empty,
            "CanDoItAllProcessed");

        Assert.Equal("graph-1", result.MessageId);
        Assert.False(result.SourceCategoryRemoved);
        Assert.True(result.ProcessedCategoryAdded);
        Assert.False(result.ProcessedCategoryCreated);
        Assert.True(patchCalled);
    }

    [Fact]
    public async Task Office365_client_skips_patch_when_message_already_has_processed_category()
    {
        var patchCalled = false;
        var client = new Office365GraphClient(new FakeHttpClientFactory(request =>
        {
            var pathAndQuery = request.RequestUri!.PathAndQuery;
            if (request.Method == HttpMethod.Get &&
                pathAndQuery == "/v1.0/me/outlook/masterCategories?$select=displayName,color")
            {
                return Json(new
                {
                    value = new[]
                    {
                        new { displayName = "CanDoItAllSummaryTestProcessed", color = "preset1" }
                    }
                });
            }

            if (request.Method == HttpMethod.Get &&
                pathAndQuery == "/v1.0/me/messages/graph-1?$select=id,categories")
            {
                return Json(new
                {
                    id = "graph-1",
                    categories = new[] { "Existing", "CanDoItAllSummaryTestProcessed" }
                });
            }

            if (request.Method == HttpMethod.Patch &&
                pathAndQuery == "/v1.0/me/messages/graph-1")
            {
                patchCalled = true;
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound);
        }));

        var result = await client.MarkMessageProcessedAsync(
            "graph-token",
            "graph-1",
            "CanDoItAllSummaryTest",
            "CanDoItAllSummaryTestProcessed");

        Assert.Equal("graph-1", result.MessageId);
        Assert.False(result.SourceCategoryRemoved);
        Assert.False(result.ProcessedCategoryAdded);
        Assert.False(result.ProcessedCategoryCreated);
        Assert.Contains("CanDoItAllSummaryTestProcessed", result.Categories);
        Assert.False(patchCalled);
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

    private static string InvokeOffice365AddressDownloadPayloadFactory(
        WorkflowNodeInput input,
        Office365MessageAddressWorkflowExecutorSettings settings,
        PluginConnectionId connectionId,
        PluginEmailMessageBatch batch)
        => InvokeDownloadPayloadFactory<Office365DownloadByAddressWorkflowExecutor>(
            input,
            settings,
            connectionId,
            batch);

    private static Office365MessageAddressFilterSettings InvokeOffice365AddressFilterSettingsFactory(
        Office365MessageAddressWorkflowExecutorSettings settings,
        WorkflowNodeInput input)
        => (Office365MessageAddressFilterSettings)(typeof(Office365DownloadByAddressWorkflowExecutor).GetMethod(
                "CreateFilterSettings",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?.Invoke(null, [settings, input])
            ?? throw new InvalidOperationException("Could not invoke Office365 address filter settings factory."));

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

    private static string InvokeMarkProcessedPayloadFactory<TExecutor, TResult>(
        WorkflowNodeInput input,
        TResult result)
        => (string)(typeof(TExecutor).GetMethod(
                "CreatePayload",
                BindingFlags.NonPublic | BindingFlags.Static)
            ?.Invoke(null, [input, result])
            ?? throw new InvalidOperationException($"Could not invoke mark-processed payload factory for {typeof(TExecutor).Name}."));

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
