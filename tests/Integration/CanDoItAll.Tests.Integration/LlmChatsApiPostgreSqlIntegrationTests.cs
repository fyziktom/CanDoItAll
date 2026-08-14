using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Conversations;
using CanDoItAll.Modules.LlmChats.Definitions;
using CanDoItAll.Modules.LlmChats.Operations;
using CanDoItAll.Modules.LlmChats.Persistence.Entities;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class LlmChatsApiPostgreSqlIntegrationTests
{
    private const string FastModel = "model-fast";
    private const string DeepModel = "model-deep";
    private static readonly Guid ProviderId = new("71000000-0000-0000-0000-000000000001");

    [Fact]
    public async Task RealHostPostgreSqlApi_PreservesRevisionsIdempotencyEffortAuditCancellationAndRecovery()
    {
        var provider = CreateProvider();
        var invocationPort = new ControllableLlmChatInvocationPort();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services => ConfigureProviderBoundary(services, provider, invocationPort));
        Assert.Equal(TestDatabaseProviderKind.PostgreSql, host.ActiveProfile.Provider);

        await AssertProviderOptionsAsync(host.Client);
        await AssertOpenApiAsync(host.Client);

        var definition = await CreateDefinitionAsync(host.Client, FastModel, thinkingEffort: null);
        var definitionId = definition.GetProperty("id").GetGuid();
        definition = await ChangeDefinitionStatusAsync(host.Client, definitionId, "activate", definition);
        var defaultConversation = await CreateConversationAsync(
            host.Client,
            definitionId,
            "Default effort conversation");
        Assert.Equal(1, defaultConversation.GetProperty("definitionRevision").GetInt32());

        var defaultConversationId = defaultConversation.GetProperty("id").GetGuid();
        var firstOperationId = Guid.Parse("72000000-0000-0000-0000-000000000001");
        var firstOperation = await SendSucceededTurnAsync(
            host.Client,
            defaultConversationId,
            firstOperationId,
            defaultConversation.GetProperty("transcriptRevision").GetInt64(),
            "First default-effort turn");
        var secondOperationId = Guid.Parse("72000000-0000-0000-0000-000000000002");
        var secondRequest = CreateTurnBody(
            secondOperationId,
            firstOperation.GetProperty("resultingTranscriptRevision").GetInt64(),
            "Second default-effort turn");
        var secondOperation = await SendSucceededTurnAsync(
            host.Client,
            defaultConversationId,
            secondRequest);

        var countsBeforeReplay = await ReadConversationCountsAsync(host, defaultConversationId);
        var replay = await SendSucceededTurnAsync(host.Client, defaultConversationId, secondRequest);
        Assert.Equal(secondOperation.GetRawText(), replay.GetRawText());
        Assert.Equal(countsBeforeReplay, await ReadConversationCountsAsync(host, defaultConversationId));

        using (var conflict = await host.Client.PostAsJsonAsync(
                   $"/api/llm-conversations/{defaultConversationId:D}/turns",
                   CreateTurnBody(
                       secondOperationId,
                       firstOperation.GetProperty("resultingTranscriptRevision").GetInt64(),
                       "A conflicting paid request")))
        {
            Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
            Assert.Contains(LlmChatErrorCodes.OperationIdConflict, await conflict.Content.ReadAsStringAsync());
        }
        Assert.Equal(countsBeforeReplay, await ReadConversationCountsAsync(host, defaultConversationId));

        using (var stale = await host.Client.PostAsJsonAsync(
                   $"/api/llm-conversations/{defaultConversationId:D}/turns",
                   CreateTurnBody(
                       Guid.Parse("72000000-0000-0000-0000-000000000003"),
                       expectedTranscriptRevision: 0,
                       message: "A stale turn")))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Contains(LlmChatErrorCodes.TranscriptRevisionConflict, await stale.Content.ReadAsStringAsync());
        }

        await AssertMessagePaginationAsync(host.Client, defaultConversationId);

        using (var unsupported = await PutDefinitionAsync(
                   host.Client,
                   definitionId,
                   definition,
                   FastModel,
                   "high"))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupported.StatusCode);
            Assert.Contains(LlmChatErrorCodes.ThinkingEffortNotSupported, await unsupported.Content.ReadAsStringAsync());
        }

        definition = await UpdateDefinitionAsync(
            host.Client,
            definitionId,
            definition,
            FastModel,
            "none");
        Assert.Equal(2, definition.GetProperty("currentRevision").GetInt32());
        var noneConversation = await CreateConversationAsync(
            host.Client,
            definitionId,
            "Explicit none conversation");
        Assert.Equal(2, noneConversation.GetProperty("definitionRevision").GetInt32());
        var noneOperationId = Guid.Parse("72000000-0000-0000-0000-000000000004");
        await SendSucceededTurnAsync(
            host.Client,
            noneConversation.GetProperty("id").GetGuid(),
            noneOperationId,
            noneConversation.GetProperty("transcriptRevision").GetInt64(),
            "Explicit none turn");

        definition = await UpdateDefinitionAsync(
            host.Client,
            definitionId,
            definition,
            DeepModel,
            "high");
        Assert.Equal(3, definition.GetProperty("currentRevision").GetInt32());
        var deepConversation = await CreateConversationAsync(
            host.Client,
            definitionId,
            "Deep model conversation");
        Assert.Equal(3, deepConversation.GetProperty("definitionRevision").GetInt32());
        var deepOperationId = Guid.Parse("72000000-0000-0000-0000-000000000005");
        await SendSucceededTurnAsync(
            host.Client,
            deepConversation.GetProperty("id").GetGuid(),
            deepOperationId,
            deepConversation.GetProperty("transcriptRevision").GetInt64(),
            "Deep high-effort turn");

        var pinnedDefaultConversation = await GetJsonAsync(
            host.Client,
            $"/api/llm-conversations/{defaultConversationId:D}");
        Assert.Equal(1, pinnedDefaultConversation.GetProperty("definitionRevision").GetInt32());

        var providerFailureOperationId = Guid.Parse("72000000-0000-0000-0000-000000000006");
        using (var providerFailure = await host.Client.PostAsJsonAsync(
                   $"/api/llm-conversations/{defaultConversationId:D}/turns",
                   CreateTurnBody(
                       providerFailureOperationId,
                       pinnedDefaultConversation.GetProperty("transcriptRevision").GetInt64(),
                       "provider failure")))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, providerFailure.StatusCode);
            var body = await providerFailure.Content.ReadAsStringAsync();
            Assert.Contains(LlmChatErrorCodes.ProviderUnavailable, body, StringComparison.Ordinal);
            Assert.Contains(providerFailureOperationId.ToString("D"), body, StringComparison.Ordinal);
            Assert.DoesNotContain(ControllableLlmChatInvocationPort.ProviderSecret, body, StringComparison.Ordinal);
        }

        var afterFailure = await GetJsonAsync(
            host.Client,
            $"/api/llm-conversations/{defaultConversationId:D}");
        var cancellationOperationId = Guid.Parse("72000000-0000-0000-0000-000000000007");
        var cancellationSend = host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{defaultConversationId:D}/turns",
            CreateTurnBody(
                cancellationOperationId,
                afterFailure.GetProperty("transcriptRevision").GetInt64(),
                "cancel in flight"));
        await invocationPort.WaitUntilStartedAsync(ControllableLlmChatInvocationPort.CancellationKey);
        using (var cancellation = await host.Client.PostAsync(
                   $"/api/llm-chat-operations/{cancellationOperationId:D}/cancel",
                   null))
        {
            Assert.Equal(HttpStatusCode.Accepted, cancellation.StatusCode);
        }
        using (var cancelledSend = await cancellationSend)
        {
            Assert.Equal(HttpStatusCode.Conflict, cancelledSend.StatusCode);
            Assert.Contains(LlmChatErrorCodes.Cancelled, await cancelledSend.Content.ReadAsStringAsync());
        }
        var cancelledOperation = await GetJsonAsync(
            host.Client,
            $"/api/llm-chat-operations/{cancellationOperationId:D}");
        Assert.Equal("cancelled", cancelledOperation.GetProperty("status").GetString());

        var afterCancellation = await GetJsonAsync(
            host.Client,
            $"/api/llm-conversations/{defaultConversationId:D}");
        var recoveryOperationId = Guid.Parse("72000000-0000-0000-0000-000000000008");
        var recoverySend = host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{defaultConversationId:D}/turns",
            CreateTurnBody(
                recoveryOperationId,
                afterCancellation.GetProperty("transcriptRevision").GetInt64(),
                "profile switch in flight"));
        await invocationPort.WaitUntilStartedAsync(ControllableLlmChatInvocationPort.ProfileSwitchKey);
        PublishProfileSwitch(host);
        invocationPort.ReleaseProfileSwitch();
        using (var recoveryResponse = await recoverySend)
        {
            Assert.Equal(HttpStatusCode.Conflict, recoveryResponse.StatusCode);
            Assert.Contains(LlmChatErrorCodes.RuntimeProfileChanged, await recoveryResponse.Content.ReadAsStringAsync());
        }
        var recoveryOperation = await GetJsonAsync(
            host.Client,
            $"/api/llm-chat-operations/{recoveryOperationId:D}");
        Assert.Equal("recoveryRequired", recoveryOperation.GetProperty("status").GetString());

        using (var abandon = await host.Client.PostAsync(
                   $"/api/llm-conversations/{defaultConversationId:D}" +
                   $"/active-turns/{recoveryOperationId:D}/abandon",
                   null))
        {
            Assert.Equal(HttpStatusCode.OK, abandon.StatusCode);
        }
        var afterRecovery = await GetJsonAsync(
            host.Client,
            $"/api/llm-conversations/{defaultConversationId:D}");
        Assert.False(afterRecovery.GetProperty("hasActiveTurn").GetBoolean());

        using (var rename = await host.Client.PatchAsJsonAsync(
                   $"/api/llm-conversations/{defaultConversationId:D}/title",
                   new
                   {
                       title = "Renamed default conversation",
                       expectedTranscriptRevision = afterRecovery.GetProperty("transcriptRevision").GetInt64(),
                       expectedConcurrencyToken = afterRecovery.GetProperty("concurrencyToken").GetInt64()
                   }))
        {
            Assert.Equal(HttpStatusCode.OK, rename.StatusCode);
            afterRecovery = await ReadJsonAsync(rename);
        }
        using (var archiveConversation = await host.Client.PostAsJsonAsync(
                   $"/api/llm-conversations/{defaultConversationId:D}/archive",
                   new
                   {
                       expectedConcurrencyToken = afterRecovery.GetProperty("concurrencyToken").GetInt64()
                   }))
        {
            Assert.Equal(HttpStatusCode.OK, archiveConversation.StatusCode);
        }

        await AssertConversationPaginationAsync(host.Client);
        definition = await ChangeDefinitionStatusAsync(host.Client, definitionId, "suspend", definition);
        definition = await ChangeDefinitionStatusAsync(host.Client, definitionId, "activate", definition);
        definition = await ChangeDefinitionStatusAsync(host.Client, definitionId, "archive", definition);
        Assert.Equal("archived", definition.GetProperty("status").GetString());

        await AssertPostgreSqlGraphAsync(
            host,
            definitionId,
            defaultConversationId,
            firstOperationId,
            secondOperationId,
            noneOperationId,
            deepOperationId,
            providerFailureOperationId,
            cancellationOperationId,
            recoveryOperationId);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(host.RootPath, "*", SearchOption.AllDirectories),
            path => Path.GetFileName(path).Contains("llmchat", StringComparison.OrdinalIgnoreCase));
    }

    private static void ConfigureProviderBoundary(
        IServiceCollection services,
        ProviderProfile provider,
        ControllableLlmChatInvocationPort invocationPort)
    {
        services.RemoveAll<IProviderRuntimeProfileSource>();
        services.RemoveAll<ILlmInvocationPort>();
        services.AddSingleton<IProviderRuntimeProfileSource>(new StaticProviderSource(provider));
        services.AddSingleton<ILlmInvocationPort>(invocationPort);
    }

    private static ProviderProfile CreateProvider()
        => new(
            ProviderId,
            "SB09 private provider",
            ProviderKind.AzureOpenAi,
            "https://provider.invalid",
            "SB09_PROVIDER_KEY",
            FastModel,
            ProviderTransportKind.Responses,
            true,
            true,
            true,
            false,
            false,
            AgentThinkingEffortPolicy.WriteProviderDefault("{}", AgentReasoningEffortLevel.Low),
            string.Empty,
            "Healthy",
            null,
            [FastModel, DeepModel])
        {
            IsPrivateProvider = true,
            ModelThinkingEffortCapabilities =
            [
                new ProviderModelThinkingEffortCapability(
                    FastModel,
                    AgentThinkingEffortSupportStatus.Supported,
                    AgentThinkingEffortCapabilitySource.Discovered,
                    [AgentReasoningEffortLevel.None, AgentReasoningEffortLevel.Low],
                    ControlMode: AgentThinkingEffortControlMode.EffortLevels),
                new ProviderModelThinkingEffortCapability(
                    DeepModel,
                    AgentThinkingEffortSupportStatus.Supported,
                    AgentThinkingEffortCapabilitySource.Discovered,
                    [
                        AgentReasoningEffortLevel.Low,
                        AgentReasoningEffortLevel.Medium,
                        AgentReasoningEffortLevel.High
                    ],
                    ControlMode: AgentThinkingEffortControlMode.EffortLevels)
            ]
        };

    private static async Task AssertProviderOptionsAsync(HttpClient client)
    {
        var options = await GetJsonAsync(client, "/api/llm-chats/provider-options");
        var provider = Assert.Single(options.EnumerateArray().ToArray());
        Assert.Equal(ProviderId, provider.GetProperty("providerProfileId").GetGuid());
        var models = provider.GetProperty("models").EnumerateArray().ToDictionary(
            item => item.GetProperty("model").GetString()!,
            StringComparer.Ordinal);
        Assert.Equal(
            ["none", "low"],
            models[FastModel].GetProperty("thinkingEffort").GetProperty("allowedEfforts")
                .EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            ["low", "medium", "high"],
            models[DeepModel].GetProperty("thinkingEffort").GetProperty("allowedEfforts")
                .EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(
            "low",
            models[FastModel].GetProperty("thinkingEffort").GetProperty("providerDefault").GetString());
        var json = options.GetRawText();
        Assert.DoesNotContain("SB09_PROVIDER_KEY", json, StringComparison.Ordinal);
        Assert.DoesNotContain("provider.invalid", json, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertOpenApiAsync(HttpClient client)
    {
        var openApi = await GetJsonAsync(client, "/swagger/v1/swagger.json");
        var paths = openApi.GetProperty("paths");
        var send = paths.GetProperty("/api/llm-conversations/{conversationId}/turns").GetProperty("post");
        Assert.Equal("SendLlmChatTurn", send.GetProperty("operationId").GetString());
        Assert.True(send.GetProperty("responses").TryGetProperty("200", out var ok));
        Assert.True(send.GetProperty("responses").TryGetProperty("202", out var accepted));
        Assert.Equal(
            ok.GetProperty("content").GetProperty("application/json").GetProperty("schema").GetRawText(),
            accepted.GetProperty("content").GetProperty("application/json").GetProperty("schema").GetRawText());
        Assert.True(send.GetProperty("responses").TryGetProperty("409", out _));
        Assert.True(send.GetProperty("responses").TryGetProperty("422", out _));
        Assert.True(send.GetProperty("responses").TryGetProperty("503", out _));
        Assert.True(send.GetProperty("responses").TryGetProperty("504", out _));
        Assert.Equal(
            "GetLlmChatOperation",
            paths.GetProperty("/api/llm-chat-operations/{operationId}")
                .GetProperty("get").GetProperty("operationId").GetString());
        Assert.Equal(
            "CancelLlmChatOperation",
            paths.GetProperty("/api/llm-chat-operations/{operationId}/cancel")
                .GetProperty("post").GetProperty("operationId").GetString());
        Assert.Equal(
            "AbandonLlmChatActiveTurn",
            paths.GetProperty("/api/llm-conversations/{conversationId}/active-turns/{turnId}/abandon")
                .GetProperty("post").GetProperty("operationId").GetString());

        var schemas = openApi.GetProperty("components").GetProperty("schemas");
        var turnRequest = schemas.GetProperty("SendLlmChatTurnApiRequest");
        var required = turnRequest.GetProperty("required").EnumerateArray()
            .Select(item => item.GetString()).ToArray();
        Assert.Contains("operationId", required);
        Assert.Contains("expectedTranscriptRevision", required);
        Assert.Contains("message", required);
        var schemaJson = schemas.GetRawText();
        Assert.Contains("thinkingEffort", schemaJson, StringComparison.Ordinal);
        Assert.Contains("allowedEfforts", schemaJson, StringComparison.Ordinal);
        Assert.Contains("providerDefault", schemaJson, StringComparison.Ordinal);
    }

    private static async Task<JsonElement> CreateDefinitionAsync(
        HttpClient client,
        string model,
        string? thinkingEffort)
    {
        using var response = await client.PostAsJsonAsync(
            "/api/llm-chats",
            CreateDefinitionBody(model, thinkingEffort, expectedConcurrencyToken: null));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> UpdateDefinitionAsync(
        HttpClient client,
        Guid definitionId,
        JsonElement current,
        string model,
        string? thinkingEffort)
    {
        using var response = await PutDefinitionAsync(
            client,
            definitionId,
            current,
            model,
            thinkingEffort);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static Task<HttpResponseMessage> PutDefinitionAsync(
        HttpClient client,
        Guid definitionId,
        JsonElement current,
        string model,
        string? thinkingEffort)
        => client.PutAsJsonAsync(
            $"/api/llm-chats/{definitionId:D}",
            CreateDefinitionBody(
                model,
                thinkingEffort,
                current.GetProperty("concurrencyToken").GetInt64()));

    private static object CreateDefinitionBody(
        string model,
        string? thinkingEffort,
        long? expectedConcurrencyToken)
        => new
        {
            name = "SB09 architecture assistant",
            summary = "Real PostgreSQL API proof",
            avatarImageUrl = string.Empty,
            systemPrompt = "Review the supplied design carefully.",
            providerProfileId = ProviderId,
            model,
            thinkingEffort,
            modelSettings = new
            {
                temperature = 0.2,
                modelParameterConfiguration = new { maxOutputTokens = 200 },
                timeoutSeconds = 20
            },
            tags = new[] { "sb09", "architecture" },
            revisionReason = $"SB09 {model} {thinkingEffort ?? "provider-default"}",
            expectedConcurrencyToken
        };

    private static async Task<JsonElement> ChangeDefinitionStatusAsync(
        HttpClient client,
        Guid definitionId,
        string action,
        JsonElement current)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/llm-chats/{definitionId:D}/{action}",
            new
            {
                expectedConcurrencyToken = current.GetProperty("concurrencyToken").GetInt64()
            });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> CreateConversationAsync(
        HttpClient client,
        Guid definitionId,
        string title)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/llm-chats/{definitionId:D}/conversations",
            new { title, origin = "api" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static object CreateTurnBody(Guid operationId, long expectedTranscriptRevision, string message)
        => new { operationId, expectedTranscriptRevision, message };

    private static Task<JsonElement> SendSucceededTurnAsync(
        HttpClient client,
        Guid conversationId,
        Guid operationId,
        long expectedTranscriptRevision,
        string message)
        => SendSucceededTurnAsync(
            client,
            conversationId,
            CreateTurnBody(operationId, expectedTranscriptRevision, message));

    private static async Task<JsonElement> SendSucceededTurnAsync(
        HttpClient client,
        Guid conversationId,
        object request)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/llm-conversations/{conversationId:D}/turns",
            request);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected a successful turn but received {(int)response.StatusCode}: {responseBody}");
        Assert.NotNull(response.Headers.Location);
        using var document = JsonDocument.Parse(responseBody);
        return document.RootElement.Clone();
    }

    private static async Task AssertMessagePaginationAsync(HttpClient client, Guid conversationId)
    {
        var firstPage = await GetJsonAsync(
            client,
            $"/api/llm-conversations/{conversationId:D}?messageTake=1");
        Assert.Single(firstPage.GetProperty("messages").EnumerateArray().ToArray());
        var cursor = firstPage.GetProperty("nextMessageCursor").GetString();
        Assert.False(string.IsNullOrWhiteSpace(cursor));
        var secondPage = await GetJsonAsync(
            client,
            $"/api/llm-conversations/{conversationId:D}?messageTake=1&messageCursor={Uri.EscapeDataString(cursor!)}");
        Assert.Single(secondPage.GetProperty("messages").EnumerateArray().ToArray());
    }

    private static async Task AssertConversationPaginationAsync(HttpClient client)
    {
        var firstPage = await GetJsonAsync(client, "/api/llm-conversations?take=1");
        Assert.Single(firstPage.GetProperty("items").EnumerateArray().ToArray());
        Assert.False(string.IsNullOrWhiteSpace(firstPage.GetProperty("nextCursor").GetString()));
    }

    private static async Task<ConversationCounts> ReadConversationCountsAsync(
        ApiTestHost host,
        Guid conversationId)
    {
        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        return new ConversationCounts(
            await dbContext.Set<LlmChatMessageRow>().CountAsync(row => row.ConversationId == conversationId),
            await dbContext.Set<LlmChatInvocationRecordRow>().CountAsync(row =>
                dbContext.Set<LlmChatOperationRow>()
                    .Where(operation => operation.ConversationId == conversationId)
                    .Select(operation => operation.Id)
                    .Contains(row.OperationId)));
    }

    private static void PublishProfileSwitch(ApiTestHost host)
    {
        var runtimeState = Assert.IsType<DatabaseRuntimeState>(
            host.App.Services.GetRequiredService<IDatabaseRuntimeState>());
        var current = host.App.Services.GetRequiredService<IDatabaseProfileRuntimeAccessor>()
            .ResolveCurrentProfile();
        var switched = new DatabaseProfileRecord
        {
            Id = Guid.Parse("73000000-0000-0000-0000-000000000001"),
            DisplayName = "SB09 switched profile",
            ProviderKind = current.Profile.ProviderKind,
            SourceKind = current.Profile.SourceKind,
            Runtime = new DatabaseProfileRuntimeMetadata
            {
                Fingerprint = $"{current.Profile.Runtime.Fingerprint}-sb09-switch"
            }
        };
        runtimeState.PublishRestartObserved(
            runtimeState.GetSnapshot(),
            new ResolvedDatabaseProfile(
                switched,
                DatabaseProfileResolutionSource.ExplicitOverride,
                current.ConnectionString));
    }

    private static async Task AssertPostgreSqlGraphAsync(
        ApiTestHost host,
        Guid definitionId,
        Guid defaultConversationId,
        Guid firstOperationId,
        Guid secondOperationId,
        Guid noneOperationId,
        Guid deepOperationId,
        Guid providerFailureOperationId,
        Guid cancellationOperationId,
        Guid recoveryOperationId)
    {
        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var revisions = await dbContext.Set<LlmChatDefinitionRevisionRow>()
            .AsNoTracking()
            .Where(row => row.DefinitionId == definitionId)
            .OrderBy(row => row.Revision)
            .ToArrayAsync();
        Assert.Equal(3, revisions.Length);
        Assert.Null(revisions[0].ThinkingEffort);
        Assert.Equal(FastModel, revisions[0].Model);
        Assert.Equal(AgentReasoningEffortLevel.None, revisions[1].ThinkingEffort);
        Assert.Equal(FastModel, revisions[1].Model);
        Assert.Equal(AgentReasoningEffortLevel.High, revisions[2].ThinkingEffort);
        Assert.Equal(DeepModel, revisions[2].Model);

        var messages = await dbContext.Set<LlmChatMessageRow>()
            .AsNoTracking()
            .Where(row => row.ConversationId == defaultConversationId)
            .OrderBy(row => row.Sequence)
            .ToArrayAsync();
        Assert.Equal(5, messages.Length);
        Assert.Equal(5, messages.Select(row => row.EntryId).Distinct().Count());
        Assert.Equal(3, messages.Select(row => row.TurnId).Distinct().Count());
        Assert.Equal(LlmMessageRole.System, messages[0].Role);
        Assert.Equal(2, messages.Count(row => row.Role == LlmMessageRole.User));
        Assert.Equal(2, messages.Count(row => row.Role == LlmMessageRole.Assistant));

        var audits = await dbContext.Set<LlmChatInvocationRecordRow>()
            .AsNoTracking()
            .ToDictionaryAsync(row => row.OperationId);
        AssertAudit(audits[firstOperationId], null, AgentReasoningEffortLevel.Low, LlmChatInvocationOutcome.Succeeded);
        AssertAudit(audits[secondOperationId], null, AgentReasoningEffortLevel.Low, LlmChatInvocationOutcome.Succeeded);
        AssertAudit(
            audits[noneOperationId],
            AgentReasoningEffortLevel.None,
            AgentReasoningEffortLevel.None,
            LlmChatInvocationOutcome.Succeeded);
        AssertAudit(
            audits[deepOperationId],
            AgentReasoningEffortLevel.High,
            AgentReasoningEffortLevel.High,
            LlmChatInvocationOutcome.Succeeded);
        AssertAudit(
            audits[providerFailureOperationId],
            null,
            AgentReasoningEffortLevel.Low,
            LlmChatInvocationOutcome.Failed);
        AssertAudit(
            audits[cancellationOperationId],
            null,
            AgentReasoningEffortLevel.Low,
            LlmChatInvocationOutcome.Cancelled);
        AssertAudit(
            audits[recoveryOperationId],
            null,
            AgentReasoningEffortLevel.Low,
            LlmChatInvocationOutcome.Succeeded);

        var operations = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .ToDictionaryAsync(row => row.Id);
        Assert.Equal(LlmChatOperationStatus.Succeeded, operations[firstOperationId].Status);
        Assert.Equal(LlmChatOperationStatus.Succeeded, operations[secondOperationId].Status);
        Assert.Equal(LlmChatOperationStatus.Failed, operations[providerFailureOperationId].Status);
        Assert.Equal(LlmChatOperationStatus.Cancelled, operations[cancellationOperationId].Status);
        Assert.Equal(LlmChatOperationStatus.Failed, operations[recoveryOperationId].Status);
        Assert.Equal(
            LlmChatErrorCodes.RuntimeProfileChanged,
            operations[recoveryOperationId].FailureCode);
        Assert.False(await dbContext.Set<LlmChatTranscriptRow>()
            .AnyAsync(row => row.ActiveTurnId != null));
    }

    private static void AssertAudit(
        LlmChatInvocationRecordRow row,
        AgentReasoningEffortLevel? requested,
        AgentReasoningEffortLevel? effective,
        LlmChatInvocationOutcome outcome)
    {
        Assert.Equal(requested, row.RequestedThinkingEffort);
        Assert.Equal(effective, row.EffectiveThinkingEffort);
        Assert.Equal(outcome, row.Outcome);
    }

    private static async Task<JsonElement> GetJsonAsync(HttpClient client, string path)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadJsonAsync(response);
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed record ConversationCounts(int MessageCount, int InvocationCount);

    private sealed class StaticProviderSource(ProviderProfile provider) : IProviderRuntimeProfileSource
    {
        public Task<IReadOnlyList<ProviderProfile>> ListProvidersAsync(
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<ProviderProfile>>([provider]);

        public Task<ProviderProfile?> GetProviderAsync(
            Guid providerId,
            CancellationToken cancellationToken = default)
            => Task.FromResult(provider.Id == providerId ? provider : null);
    }
}

internal sealed class ControllableLlmChatInvocationPort : ILlmInvocationPort
{
    public const string CancellationKey = "cancel";
    public const string ProfileSwitchKey = "profile-switch";
    public const string ProviderSecret = "provider-secret-must-not-leak";

    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> starts = [];
    private readonly TaskCompletionSource<bool> profileSwitchRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public async Task<LlmInvocationResult> InvokeAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default)
    {
        var userText = request.Messages.Last(message => message.Role == LlmMessageRole.User).Text;
        if (userText.Contains("provider failure", StringComparison.Ordinal))
        {
            throw new LlmInvocationException(
                LlmInvocationFailureKind.ProviderFailure,
                request.Provider.Name,
                request.Model,
                request.CorrelationId,
                new InvalidOperationException(ProviderSecret),
                new LlmUsage(7, 0));
        }

        if (userText.Contains("cancel in flight", StringComparison.Ordinal))
        {
            Signal(CancellationKey);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (userText.Contains("profile switch in flight", StringComparison.Ordinal))
        {
            Signal(ProfileSwitchKey);
            await profileSwitchRelease.Task;
        }

        return new LlmInvocationResult(request.Model, $"Assistant response to: {userText}", new LlmUsage(10, 4, 1));
    }

    public Task WaitUntilStartedAsync(string key)
        => starts.GetOrAdd(
                key,
                static _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously))
            .Task
            .WaitAsync(TimeSpan.FromSeconds(10));

    public void ReleaseProfileSwitch()
        => profileSwitchRelease.TrySetResult(true);

    private void Signal(string key)
        => starts.GetOrAdd(
                key,
                static _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously))
            .TrySetResult(true);
}
