using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Modules.LlmChats.Persistence.Repositories;
using CanDoItAll.Modules.LlmChats.Persistence.ReadModels;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed class LlmChatApiValidationIntegrationTests
{
    private static readonly Guid Empty = Guid.Empty;

    [Fact]
    public async Task Empty_definition_conversation_operation_and_filter_ids_return_stable_invalid_request()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        var routes = new[]
        {
            $"/api/llm-chats/{Empty:D}",
            $"/api/llm-chats/{Empty:D}/editor",
            $"/api/llm-conversations/{Empty:D}",
            $"/api/llm-chat-operations/{Empty:D}",
            $"/api/llm-conversations?definitionId={Empty:D}"
        };

        foreach (var route in routes)
        {
            using var response = await host.Client.GetAsync(route);
            await AssertInvalidRequestAsync(response);
        }
    }

    [Fact]
    public async Task Explicit_invalid_page_sizes_return_stable_invalid_request()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        var conversationId = Guid.NewGuid();
        var routes = new[]
        {
            "/api/llm-chats?take=0",
            "/api/llm-chats?take=-1",
            "/api/llm-chats?take=101",
            "/api/llm-conversations?take=0",
            "/api/llm-conversations?take=-1",
            "/api/llm-conversations?take=101",
            $"/api/llm-conversations/{conversationId:D}?messageTake=0",
            $"/api/llm-conversations/{conversationId:D}?messageTake=-1",
            $"/api/llm-conversations/{conversationId:D}?messageTake=101"
        };

        foreach (var route in routes)
        {
            using var response = await host.Client.GetAsync(route);
            await AssertInvalidRequestAsync(response);
        }
    }

    [Fact]
    public async Task Unknown_members_return_stable_invalid_request_problem_details()
    {
        var operations = new StubLlmChatOperationApplicationService();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                services.RemoveAll<ILlmChatDefinitionApplicationService>();
                services.RemoveAll<ILlmChatConversationApplicationService>();
                services.RemoveAll<ILlmChatOperationApplicationService>();
                services.AddSingleton<ILlmChatDefinitionApplicationService, StubLlmChatDefinitionApplicationService>();
                services.AddSingleton<ILlmChatConversationApplicationService, StubLlmChatConversationApplicationService>();
                services.AddSingleton<ILlmChatOperationApplicationService>(operations);
            },
            useInMemoryDatabase: true);
        var definitionRoute = "/api/llm-chats";
        var conversationRoute =
            $"/api/llm-chats/{StubLlmChatDefinitionApplicationService.DefinitionId.Value:D}/conversations";
        var turnRoute =
            $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}/turns";

        using var definition = await PostJsonAsync(host.Client, definitionRoute, """
            {
              "name": "Assistant",
              "summary": "Summary",
              "avatarImageUrl": "",
              "systemPrompt": "Prompt",
              "providerProfileId": "cccccccc-cccc-cccc-cccc-cccccccccccc",
              "model": "reasoning-model",
              "thinkingEffort": "high",
              "revisionReason": "Initial",
              "unknown": true
            }
            """);
        using var conversation = await PostJsonAsync(host.Client, conversationRoute, """
            { "title": "Conversation", "origin": "application" }
            """);
        using var turn = await PostJsonAsync(host.Client, turnRoute, $$"""
            {
              "operationId": "{{Guid.NewGuid():D}}",
              "expectedTranscriptRevision": {{StubLlmChatOperationApplicationService.TranscriptRevision}},
              "message": "Hello",
              "unknown": true
            }
            """);

        await AssertInvalidRequestAsync(definition);
        await AssertInvalidRequestAsync(conversation);
        await AssertInvalidRequestAsync(turn);
    }

    private static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string route, string json)
        => client.PostAsync(
            route,
            new StringContent(json, Encoding.UTF8, "application/json"));

    internal static async Task AssertInvalidRequestAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(
            LlmChatErrorCodes.InvalidRequest,
            body.RootElement.GetProperty("code").GetString());
        Assert.Equal(400, body.RootElement.GetProperty("status").GetInt32());
    }
}

public sealed class LlmChatApiPrivacyIntegrationTests
{
    [Fact]
    public async Task Read_scope_excludes_system_messages_while_provider_context_retains_prompt()
    {
        const string systemPrompt = "SYSTEM-PROMPT-SENTINEL-7f3a";
        var conversationId = Guid.NewGuid();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: false);

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var accessor = scope.ServiceProvider.GetRequiredService<IDatabaseProfileRuntimeAccessor>();
            var factory = scope.ServiceProvider.GetRequiredService<IProfileAppDbContextFactory>();
            await using var dbContext = await factory.CreateDbContextForProfileAsync(accessor.ResolveCurrentProfile());
            var document = LlmChatsPostgreSqlTestDatabase.CreateDocument(conversationId);
            LlmChatsPostgreSqlTestDatabase.SeedConversationRoot(dbContext, document);
            dbContext.Add(new LlmChatTranscriptRow
            {
                ConversationId = conversationId,
                ProviderId = Guid.NewGuid(),
                ProviderName = "Provider",
                ProviderKind = CanDoItAll.AgentFramework.Models.ProviderKind.OpenAi,
                Model = "model",
                TranscriptRevision = 2,
                EntryCount = 2
            });
            dbContext.Add(new LlmChatMessageRow
            {
                EntryId = Guid.NewGuid(),
                ConversationId = conversationId,
                Sequence = 1,
                TurnId = Guid.NewGuid(),
                Role = LlmMessageRole.System,
                Text = systemPrompt,
                CreatedAtUtc = document.CreatedAtUtc,
                Model = ""
            });
            dbContext.Add(new LlmChatMessageRow
            {
                EntryId = Guid.NewGuid(),
                ConversationId = conversationId,
                Sequence = 2,
                TurnId = Guid.NewGuid(),
                Role = LlmMessageRole.User,
                Text = "Visible user message",
                CreatedAtUtc = document.CreatedAtUtc.AddSeconds(1),
                Model = ""
            });
            await dbContext.SaveChangesAsync();

            var providerSnapshot = await new EfLlmConversationTurnStore(dbContext)
                .TryGetAsync(conversationId, 20);
            Assert.NotNull(providerSnapshot);
            Assert.Contains(providerSnapshot.ContextEntries, entry =>
                entry.Role == LlmMessageRole.System && entry.Text == systemPrompt);
        }

        using var response = await host.Client.GetAsync(
            $"/api/llm-conversations/{conversationId:D}?messageTake=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(systemPrompt, json, StringComparison.Ordinal);
        using var body = JsonDocument.Parse(json);
        var messages = body.RootElement.GetProperty("messages");
        Assert.Single(messages.EnumerateArray());
        Assert.Equal("user", messages[0].GetProperty("role").GetString());
        Assert.Equal("Visible user message", messages[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Manage_editor_returns_authoritative_prompt_without_provider_secrets()
    {
        await using var host = await CreateStubHostAsync(jwtEnabled: false);
        using var response = await host.Client.GetAsync(
            $"/api/llm-chats/{StubLlmChatDefinitionApplicationService.DefinitionId.Value:D}/editor");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Headers.ETag);
        var json = await response.Content.ReadAsStringAsync();
        using var body = JsonDocument.Parse(json);
        Assert.Equal("Review carefully.", body.RootElement.GetProperty("systemPrompt").GetString());
        Assert.Equal("reasoning-model", body.RootElement.GetProperty("model").GetString());
        Assert.Equal(3, body.RootElement.GetProperty("revision").GetInt32());
        Assert.DoesNotContain("credential", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("apiKey", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("endpoint", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("localPath", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Editor_requires_manage_scope()
    {
        await using var host = await CreateStubHostAsync(jwtEnabled: true);
        var tokenService = host.App.Services.GetRequiredService<IApiTokenService>();
        var route = $"/api/llm-chats/{StubLlmChatDefinitionApplicationService.DefinitionId.Value:D}/editor";

        SetBearer(host, tokenService, ApiAccessScopeNames.ReadLlmChats);
        using var read = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, read.StatusCode);

        SetBearer(host, tokenService, ApiAccessScopeNames.ManageLlmChats);
        using var manage = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, manage.StatusCode);
    }

    [Fact]
    public async Task Operation_response_omits_internal_request_fingerprint()
    {
        var operations = new StubLlmChatOperationApplicationService();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                services.RemoveAll<ILlmChatOperationApplicationService>();
                services.AddSingleton<ILlmChatOperationApplicationService>(operations);
            },
            useInMemoryDatabase: true);
        var operationId = Guid.NewGuid();
        using var response = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}/turns",
            new
            {
                operationId,
                expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision,
                message = "Hello"
            });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("requestFingerprint", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(new string('a', 64), json, StringComparison.Ordinal);

        using var persisted = await host.Client.GetAsync($"/api/llm-chat-operations/{operationId:D}");
        Assert.Equal(HttpStatusCode.OK, persisted.StatusCode);
        Assert.DoesNotContain(
            "requestFingerprint",
            await persisted.Content.ReadAsStringAsync(),
            StringComparison.OrdinalIgnoreCase);
    }

    private static Task<ApiTestHost> CreateStubHostAsync(bool jwtEnabled)
        => ApiTestHost.CreateAsync(
            jwtEnabled,
            configureServices: services =>
            {
                services.RemoveAll<ILlmChatDefinitionApplicationService>();
                services.RemoveAll<ILlmChatConversationApplicationService>();
                services.RemoveAll<ILlmChatProviderResolver>();
                services.AddSingleton<ILlmChatDefinitionApplicationService, StubLlmChatDefinitionApplicationService>();
                services.AddSingleton<ILlmChatConversationApplicationService, StubLlmChatConversationApplicationService>();
                services.AddSingleton<ILlmChatProviderResolver, StubLlmChatProviderResolver>();
            },
            useInMemoryDatabase: true);

    private static void SetBearer(ApiTestHost host, IApiTokenService tokenService, string scope)
    {
        var token = tokenService.IssueToken(new ApiTokenIssueRequest
        {
            Subject = $"llm-chat-{scope}",
            DisplayName = "LLM Chat API hardening client",
            Scopes = [scope]
        });
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.Token);
    }
}

public sealed class LlmChatApiMetadataIntegrationTests
{
    [Fact]
    public async Task Endpoint_split_preserves_routes_names_and_policies()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: true,
            useInMemoryDatabase: true);
        var endpoints = host.App.Services.GetRequiredService<EndpointDataSource>()
            .Endpoints
            .OfType<RouteEndpoint>()
            .Where(endpoint => endpoint.RoutePattern.RawText?.StartsWith("/api/llm-", StringComparison.Ordinal) == true)
            .ToDictionary(
                endpoint => endpoint.Metadata.GetMetadata<IEndpointNameMetadata>()?.EndpointName ?? string.Empty,
                StringComparer.Ordinal);
        var expected = new Dictionary<string, (string Route, string Method, string Policy)>(StringComparer.Ordinal)
        {
            ["ListLlmChatProviderOptions"] = ("/api/llm-chats/provider-options", "GET", ApiAuthorizationPolicies.ReadLlmChats),
            ["ListLlmChatDefinitions"] = ("/api/llm-chats/", "GET", ApiAuthorizationPolicies.ReadLlmChats),
            ["CreateLlmChatDefinition"] = ("/api/llm-chats/", "POST", ApiAuthorizationPolicies.ManageLlmChats),
            ["GetLlmChatDefinition"] = ("/api/llm-chats/{definitionId:guid}", "GET", ApiAuthorizationPolicies.ReadLlmChats),
            ["GetLlmChatDefinitionEditor"] = ("/api/llm-chats/{definitionId:guid}/editor", "GET", ApiAuthorizationPolicies.ManageLlmChats),
            ["UpdateLlmChatDefinition"] = ("/api/llm-chats/{definitionId:guid}", "PUT", ApiAuthorizationPolicies.ManageLlmChats),
            ["ActivateLlmChatDefinition"] = ("/api/llm-chats/{definitionId:guid}/activate", "POST", ApiAuthorizationPolicies.ManageLlmChats),
            ["SuspendLlmChatDefinition"] = ("/api/llm-chats/{definitionId:guid}/suspend", "POST", ApiAuthorizationPolicies.ManageLlmChats),
            ["ArchiveLlmChatDefinition"] = ("/api/llm-chats/{definitionId:guid}/archive", "POST", ApiAuthorizationPolicies.ManageLlmChats),
            ["CreateLlmChatConversation"] = ("/api/llm-chats/{definitionId:guid}/conversations", "POST", ApiAuthorizationPolicies.ManageLlmChats),
            ["ListLlmChatConversations"] = ("/api/llm-conversations/", "GET", ApiAuthorizationPolicies.ReadLlmChats),
            ["GetLlmChatConversation"] = ("/api/llm-conversations/{conversationId:guid}", "GET", ApiAuthorizationPolicies.ReadLlmChats),
            ["RenameLlmChatConversation"] = ("/api/llm-conversations/{conversationId:guid}/title", "PATCH", ApiAuthorizationPolicies.ManageLlmChats),
            ["ArchiveLlmChatConversation"] = ("/api/llm-conversations/{conversationId:guid}/archive", "POST", ApiAuthorizationPolicies.ManageLlmChats)
        };

        foreach (var (name, contract) in expected)
        {
            var endpoint = Assert.Contains(name, endpoints);
            Assert.Equal(contract.Route, endpoint.RoutePattern.RawText);
            Assert.Contains(contract.Method, endpoint.Metadata.GetMetadata<IHttpMethodMetadata>()!.HttpMethods);
            Assert.Contains(endpoint.Metadata.GetOrderedMetadata<IAuthorizeData>(), item => item.Policy == contract.Policy);
        }
    }

    [Fact]
    public async Task OpenApi_declares_every_implemented_llm_chat_problem_status()
    {
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            useInMemoryDatabase: true);
        using var response = await host.Client.GetAsync("/swagger/v1/swagger.json");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var paths = document.RootElement.GetProperty("paths");
        var expected = new Dictionary<(string Path, string Method), string[]>
        {
            [("/api/llm-chats/provider-options", "get")] = ["422", "500"],
            [("/api/llm-chats", "get")] = ["400"],
            [("/api/llm-chats", "post")] = ["400", "422"],
            [("/api/llm-chats/{definitionId}", "get")] = ["400", "404"],
            [("/api/llm-chats/{definitionId}/editor", "get")] = ["400", "404"],
            [("/api/llm-chats/{definitionId}", "put")] = ["400", "404", "409", "422"],
            [("/api/llm-chats/{definitionId}/activate", "post")] = ["400", "404", "409"],
            [("/api/llm-chats/{definitionId}/suspend", "post")] = ["400", "404", "409"],
            [("/api/llm-chats/{definitionId}/archive", "post")] = ["400", "404", "409"],
            [("/api/llm-chats/{definitionId}/conversations", "post")] = ["400", "404", "409"],
            [("/api/llm-conversations", "get")] = ["400"],
            [("/api/llm-conversations/{conversationId}", "get")] = ["400", "404"],
            [("/api/llm-conversations/{conversationId}/title", "patch")] = ["400", "404", "409"],
            [("/api/llm-conversations/{conversationId}/archive", "post")] = ["400", "404", "409"],
            [("/api/llm-conversations/{conversationId}/turns", "post")] = ["400", "404", "409", "422", "503", "504"],
            [("/api/llm-chat-operations/{operationId}", "get")] = ["400", "404"],
            [("/api/llm-chat-operations/{operationId}/events", "get")] = ["400", "404", "409"],
            [("/api/llm-chat-operations/{operationId}/cancel", "post")] = ["400", "404"],
            [("/api/llm-chat-operations/{operationId}/reconcile", "post")] = ["400", "404", "409"]
        };

        foreach (var (endpoint, statuses) in expected)
        {
            var responses = paths
                .GetProperty(endpoint.Path)
                .GetProperty(endpoint.Method)
                .GetProperty("responses");
            foreach (var status in statuses)
            {
                Assert.True(
                    responses.TryGetProperty(status, out _),
                    $"{endpoint.Method.ToUpperInvariant()} {endpoint.Path} does not declare {status}.");
            }
        }
    }
}

public sealed class LlmChatOperationStorageContractIntegrationTests
{
    [Fact]
    public async Task Unknown_persisted_operation_kind_fails_as_storage_corrupted()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatinvalidoperationkind");
        await using var dbContext = database.CreateDbContext();
        var conversationId = Guid.NewGuid();
        var document = LlmChatsPostgreSqlTestDatabase.CreateDocument(conversationId);
        LlmChatsPostgreSqlTestDatabase.SeedConversationRoot(dbContext, document);
        dbContext.Add(LlmConversationPersistenceMapper.ToRow(document));
        dbContext.Add(new LlmChatOperationRow
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Kind = (LlmChatOperationKind)99,
            RequestFingerprint = new string('a', 64),
            ExpectedTranscriptRevision = 0,
            Status = LlmChatOperationStatus.Pending,
            StartedAtUtc = DateTimeOffset.UtcNow,
            ConcurrencyToken = 0
        });
        await dbContext.SaveChangesAsync();
        var operationId = new LlmChatOperationId(
            await dbContext.Set<LlmChatOperationRow>().Select(row => row.Id).SingleAsync());
        dbContext.ChangeTracker.Clear();

        var exception = await Assert.ThrowsAsync<InvalidDataException>(() =>
            new EfLlmChatOperationReadStore(dbContext).TryGetAsync(operationId));
        Assert.Contains("operation kind", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
