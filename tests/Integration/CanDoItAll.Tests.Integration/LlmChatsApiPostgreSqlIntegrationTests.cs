using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.Tests.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.LlmChats;

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
            Assert.Equal(HttpStatusCode.Accepted, providerFailure.StatusCode);
            Assert.NotNull(providerFailure.Headers.Location);
            var failed = await WaitForOperationTerminalAsync(
                host.Client,
                providerFailure.Headers.Location!.ToString());
            Assert.Equal("failed", failed.GetProperty("status").GetString());
            Assert.Equal(
                LlmChatErrorCodes.ProviderUnavailable,
                failed.GetProperty("failure").GetProperty("code").GetString());
            Assert.DoesNotContain(
                ControllableLlmChatInvocationPort.ProviderSecret,
                failed.GetRawText(),
                StringComparison.Ordinal);
        }

        var afterFailure = await GetJsonAsync(
            host.Client,
            $"/api/llm-conversations/{defaultConversationId:D}");
        var cancellationOperationId = Guid.Parse("72000000-0000-0000-0000-000000000007");
        using var cancellationSend = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{defaultConversationId:D}/turns",
            CreateTurnBody(
                cancellationOperationId,
                afterFailure.GetProperty("transcriptRevision").GetInt64(),
                "cancel in flight"));
        Assert.Equal(HttpStatusCode.Accepted, cancellationSend.StatusCode);
        await invocationPort.WaitUntilStartedAsync(ControllableLlmChatInvocationPort.CancellationKey);
        using (var cancellation = await host.Client.PostAsync(
                   $"/api/llm-chat-operations/{cancellationOperationId:D}/cancel",
                   null))
        {
            Assert.Equal(HttpStatusCode.Accepted, cancellation.StatusCode);
        }
        var cancelledOperation = await WaitForOperationTerminalAsync(
            host.Client,
            $"/api/llm-chat-operations/{cancellationOperationId:D}");
        Assert.Equal("cancelled", cancelledOperation.GetProperty("status").GetString());

        var afterCancellation = await GetJsonAsync(
            host.Client,
            $"/api/llm-conversations/{defaultConversationId:D}");
        var afterRecovery = afterCancellation;

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
            cancellationOperationId);
        Assert.DoesNotContain(
            Directory.EnumerateFiles(host.RootPath, "*", SearchOption.AllDirectories),
            path => Path.GetFileName(path).Contains("llmchat", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ConversationApi_PersistsServerOwnedApiOriginAndRejectsSpoofedOrigin()
    {
        var provider = CreateProvider();
        var invocationPort = new ControllableLlmChatInvocationPort();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services => ConfigureProviderBoundary(services, provider, invocationPort));
        Assert.Equal(TestDatabaseProviderKind.PostgreSql, host.ActiveProfile.Provider);
        var definition = await CreateDefinitionAsync(host.Client, FastModel, thinkingEffort: null);
        var definitionId = definition.GetProperty("id").GetGuid();
        definition = await ChangeDefinitionStatusAsync(host.Client, definitionId, "activate", definition);
        var route = $"/api/llm-chats/{definitionId:D}/conversations";

        using var spoofed = await host.Client.PostAsJsonAsync(route, new
        {
            title = "Spoofed PostgreSQL origin",
            origin = "application"
        });
        Assert.Equal(HttpStatusCode.BadRequest, spoofed.StatusCode);

        var created = await CreateConversationAsync(host.Client, definitionId, "Stored API origin");
        Assert.Equal("api", created.GetProperty("origin").GetString());

        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var stored = await dbContext.Set<LlmChatConversationRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == created.GetProperty("id").GetGuid());
        Assert.Equal(LlmChatConversationOrigin.Api, stored.Origin);
    }

    [Fact]
    public async Task Profile_switch_before_finalization_retains_committed_usage_and_blocks_later_writes()
    {
        var provider = CreateProvider();
        var invocationPort = new ControllableLlmChatInvocationPort();
        var finalizationBarrier = new LlmChatFinalizationBarrier();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                ConfigureProviderBoundary(services, provider, invocationPort);
                DecorateConversationEngine(services, finalizationBarrier);
            });
        Assert.Equal(TestDatabaseProviderKind.PostgreSql, host.ActiveProfile.Provider);

        var definition = await CreateDefinitionAsync(host.Client, FastModel, thinkingEffort: null);
        var definitionId = definition.GetProperty("id").GetGuid();
        definition = await ChangeDefinitionStatusAsync(host.Client, definitionId, "activate", definition);
        var conversation = await CreateConversationAsync(host.Client, definitionId, "Profile fence conversation");
        var conversationId = conversation.GetProperty("id").GetGuid();
        var operationId = Guid.Parse("72000000-0000-0000-0000-000000000008");
        using var send = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{conversationId:D}/turns",
            CreateTurnBody(
                operationId,
                conversation.GetProperty("transcriptRevision").GetInt64(),
                "profile switch before finalization"));
        Assert.Equal(HttpStatusCode.Accepted, send.StatusCode);
        await finalizationBarrier.WaitAsync();

        using var activeConversationResponse = await host.Client.GetAsync(
            $"/api/llm-conversations/{conversationId:D}");
        Assert.Equal(HttpStatusCode.OK, activeConversationResponse.StatusCode);
        using var activeConversation = JsonDocument.Parse(
            await activeConversationResponse.Content.ReadAsStringAsync());
        Assert.True(activeConversation.RootElement.GetProperty("hasActiveTurn").GetBoolean());
        Assert.Equal(
            operationId,
            activeConversation.RootElement.GetProperty("activeOperationId").GetGuid());

        await using var ownerScope = host.App.Services.CreateAsyncScope();
        var openedSession = await ownerScope.ServiceProvider
            .GetRequiredService<LlmChatOperationEventStreamSessionFactory>()
            .OpenAsync(new LlmChatOperationId(operationId));
        Assert.True(openedSession.IsSuccess);
        await using var durableSession = openedSession.Value!;
        var durablePage = await durableSession.ReadAsync(
            0,
            take: durableSession.MaximumPageSize,
            maximumWait: TimeSpan.FromMilliseconds(10));
        Assert.NotEmpty(durablePage.Events);

        using var eventStream = await OpenEventStreamAsync(
            host.Client,
            $"/api/llm-chat-operations/{operationId:D}/events");
        PublishProfileSwitch(host);
        finalizationBarrier.Release();

        Assert.True(durableSession.ProfileLifetime.IsCancellationRequested);
        await Assert.ThrowsAsync<LlmChatRuntimeProfileChangedException>(
            () => durableSession.ReadAsync(
                durablePage.LatestSequence,
                take: durableSession.MaximumPageSize,
                maximumWait: TimeSpan.FromMilliseconds(10)).AsTask());

        await using var eventBody = await eventStream.Content.ReadAsStreamAsync();
        using var eventReader = new StreamReader(eventBody);
        var framesBeforeSwitch = await ReadToEndAsync(eventReader).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.DoesNotContain(
            framesBeforeSwitch,
            frame => frame.Event is
                "llm.operation.succeeded" or
                "llm.operation.failed" or
                "llm.operation.cancelled" or
                "llm.operation.recovery-required");

        await WaitUntilAsync(async () =>
        {
            var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
            await using var pollingContext = await factory.CreateDbContextAsync();
            return await pollingContext.Set<LlmChatInvocationRecordRow>()
                .AnyAsync(row => row.OperationId == operationId);
        }, "the profile-fenced invocation audit");

        var factory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using var dbContext = await factory.CreateDbContextAsync();
        var operation = await dbContext.Set<LlmChatOperationRow>().SingleAsync(row => row.Id == operationId);
        var audit = await dbContext.Set<LlmChatInvocationRecordRow>().SingleAsync(row => row.OperationId == operationId);
        var transcript = await dbContext.Set<LlmChatTranscriptRow>().SingleAsync(row => row.ConversationId == conversationId);
        Assert.Equal(LlmChatOperationStatus.Running, operation.Status);
        Assert.NotNull(operation.ProviderDispatchReturnedAtUtc);
        Assert.Equal(LlmChatInvocationOutcome.Succeeded, audit.Outcome);
        Assert.Equal(10, audit.InputTokens);
        Assert.Equal(4, audit.OutputTokens);
        Assert.Equal(1, audit.CachedInputTokens);
        Assert.Equal(operationId, transcript.ActiveTurnId);
        Assert.False(await dbContext.Set<LlmChatMessageRow>().AnyAsync(row =>
            row.ConversationId == conversationId &&
            row.TurnId == operationId &&
            row.Role == LlmMessageRole.Assistant));

        using var staleQuery = await host.Client.GetAsync($"/api/llm-chat-operations/{operationId:D}");
        Assert.Equal(HttpStatusCode.Conflict, staleQuery.StatusCode);
        Assert.Contains(LlmChatErrorCodes.RuntimeProfileChanged, await staleQuery.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Request_lifetime_ends_before_provider_completion_and_does_not_cancel_durable_execution()
    {
        var provider = CreateProvider();
        var invocationPort = new ControllableLlmChatInvocationPort();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services => ConfigureProviderBoundary(services, provider, invocationPort));
        var definition = await CreateDefinitionAsync(host.Client, FastModel, thinkingEffort: null);
        definition = await ChangeDefinitionStatusAsync(
            host.Client,
            definition.GetProperty("id").GetGuid(),
            "activate",
            definition);
        var conversation = await CreateConversationAsync(
            host.Client,
            definition.GetProperty("id").GetGuid(),
            "Detached request lifetime");
        var conversationId = conversation.GetProperty("id").GetGuid();
        var operationId = Guid.Parse("72000000-0000-0000-0000-000000000009");
        using var requestCancellation = new CancellationTokenSource();
        var send = host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{conversationId:D}/turns",
            CreateTurnBody(
                operationId,
                conversation.GetProperty("transcriptRevision").GetInt64(),
                "request disconnect in flight"),
            requestCancellation.Token);

        await invocationPort.WaitUntilStartedAsync(ControllableLlmChatInvocationPort.RequestDisconnectKey);
        using var admitted = await send.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Equal(HttpStatusCode.Accepted, admitted.StatusCode);
        requestCancellation.Cancel();
        invocationPort.ReleaseRequestDisconnect();

        var completed = await WaitForOperationTerminalAsync(
            host.Client,
            $"/api/llm-chat-operations/{operationId:D}");
        Assert.Equal("succeeded", completed.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Object, completed.GetProperty("assistantMessage").ValueKind);
        using var completedConversationResponse = await host.Client.GetAsync(
            $"/api/llm-conversations/{conversationId:D}");
        Assert.Equal(HttpStatusCode.OK, completedConversationResponse.StatusCode);
        using var completedConversation = JsonDocument.Parse(
            await completedConversationResponse.Content.ReadAsStringAsync());
        Assert.False(completedConversation.RootElement.GetProperty("hasActiveTurn").GetBoolean());
        Assert.False(completedConversation.RootElement.TryGetProperty("activeOperationId", out _));
    }

    [Fact]
    public async Task Configured_workers_never_exceed_concurrency_cap()
    {
        var invocationPort = new CapacityTrackingLlmChatInvocationPort();
        await using var host = await CreateCapacityHostAsync(invocationPort, workerCount: 2);
        var definition = await CreateActiveDefinitionAsync(host.Client);
        var operationIds = new List<Guid>();
        for (var index = 1; index <= 3; index++)
        {
            var conversation = await CreateConversationAsync(
                host.Client,
                definition.GetProperty("id").GetGuid(),
                $"Capacity {index}");
            var operationId = Guid.NewGuid();
            operationIds.Add(operationId);
            await AdmitCapacityTurnAsync(host.Client, conversation, operationId, $"hold-cap-{index}");
        }

        await invocationPort.WaitForStartedCountAsync(2);
        Assert.Equal(2, invocationPort.ActiveCount);
        Assert.Equal(2, invocationPort.MaximumActiveCount);
        Assert.Equal(2, invocationPort.StartedKeys.Count);

        invocationPort.Release(invocationPort.StartedKeys[0]);
        await invocationPort.WaitForStartedCountAsync(3);
        Assert.Equal(2, invocationPort.MaximumActiveCount);
        invocationPort.ReleaseAll();
        foreach (var operationId in operationIds)
        {
            var terminal = await WaitForOperationTerminalAsync(
                host.Client,
                $"/api/llm-chat-operations/{operationId:D}");
            Assert.Equal("succeeded", terminal.GetProperty("status").GetString());
        }
    }

    [Fact]
    public async Task Slow_conversation_does_not_starve_unrelated_conversation()
    {
        var invocationPort = new CapacityTrackingLlmChatInvocationPort();
        await using var host = await CreateCapacityHostAsync(invocationPort, workerCount: 2);
        var definition = await CreateActiveDefinitionAsync(host.Client);
        var slowConversation = await CreateConversationAsync(
            host.Client,
            definition.GetProperty("id").GetGuid(),
            "Slow conversation");
        var fastConversation = await CreateConversationAsync(
            host.Client,
            definition.GetProperty("id").GetGuid(),
            "Fast conversation");
        var slowOperationId = Guid.NewGuid();
        await AdmitCapacityTurnAsync(host.Client, slowConversation, slowOperationId, "hold-slow");
        await invocationPort.WaitUntilStartedAsync("hold-slow");

        var fastOperationId = Guid.NewGuid();
        await AdmitCapacityTurnAsync(host.Client, fastConversation, fastOperationId, "fast-independent");
        var fastCompleted = await WaitForOperationTerminalAsync(
            host.Client,
            $"/api/llm-chat-operations/{fastOperationId:D}");

        Assert.Equal("succeeded", fastCompleted.GetProperty("status").GetString());
        Assert.True(invocationPort.IsActive("hold-slow"));
        invocationPort.Release("hold-slow");
        var slowCompleted = await WaitForOperationTerminalAsync(
            host.Client,
            $"/api/llm-chat-operations/{slowOperationId:D}");
        Assert.Equal("succeeded", slowCompleted.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Workers_never_execute_two_active_turns_for_one_conversation()
    {
        var invocationPort = new CapacityTrackingLlmChatInvocationPort();
        await using var host = await CreateCapacityHostAsync(invocationPort, workerCount: 2);
        var definition = await CreateActiveDefinitionAsync(host.Client);
        var conversation = await CreateConversationAsync(
            host.Client,
            definition.GetProperty("id").GetGuid(),
            "Serialized conversation");
        var conversationId = conversation.GetProperty("id").GetGuid();
        var firstOperationId = Guid.NewGuid();
        await AdmitCapacityTurnAsync(host.Client, conversation, firstOperationId, "hold-same-conversation");
        await invocationPort.WaitUntilStartedAsync("hold-same-conversation");
        var current = await GetJsonAsync(host.Client, $"/api/llm-conversations/{conversationId:D}");

        using var second = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{conversationId:D}/turns",
            CreateTurnBody(
                Guid.NewGuid(),
                current.GetProperty("transcriptRevision").GetInt64(),
                "second same conversation"));

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(1, invocationPort.MaximumActiveFor("hold-same-conversation"));
        invocationPort.Release("hold-same-conversation");
        _ = await WaitForOperationTerminalAsync(
            host.Client,
            $"/api/llm-chat-operations/{firstOperationId:D}");
    }

    [Fact]
    public async Task Shutdown_drains_all_started_workers()
    {
        var invocationPort = new CapacityTrackingLlmChatInvocationPort();
        await using var host = await CreateCapacityHostAsync(invocationPort, workerCount: 2);
        var definition = await CreateActiveDefinitionAsync(host.Client);
        for (var index = 1; index <= 2; index++)
        {
            var conversation = await CreateConversationAsync(
                host.Client,
                definition.GetProperty("id").GetGuid(),
                $"Shutdown {index}");
            await AdmitCapacityTurnAsync(
                host.Client,
                conversation,
                Guid.NewGuid(),
                $"hold-shutdown-{index}");
        }

        await invocationPort.WaitForStartedCountAsync(2);
        await host.App.StopAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(0, invocationPort.ActiveCount);
        Assert.Equal(2, invocationPort.CancellationCount);
    }

    [Fact]
    public Task Full_retention_emits_gap_with_durable_high_water_then_closes_terminal()
        => DurableSse_ReconnectsAfterDeltaWithoutRedispatchAndClosesAfterOneTerminalEvent();

    [Fact]
    public async Task DurableSse_ReconnectsAfterDeltaWithoutRedispatchAndClosesAfterOneTerminalEvent()
    {
        var provider = CreateProvider();
        var invocationPort = new ControllableLlmChatInvocationPort();
        await using var host = await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services => ConfigureProviderBoundary(services, provider, invocationPort));
        Assert.Equal(TestDatabaseProviderKind.PostgreSql, host.ActiveProfile.Provider);

        var definition = await CreateDefinitionAsync(host.Client, FastModel, thinkingEffort: null);
        definition = await ChangeDefinitionStatusAsync(
            host.Client,
            definition.GetProperty("id").GetGuid(),
            "activate",
            definition);
        var conversation = await CreateConversationAsync(
            host.Client,
            definition.GetProperty("id").GetGuid(),
            "Durable SSE reconnect");
        var conversationId = conversation.GetProperty("id").GetGuid();
        var operationId = Guid.Parse("72000000-0000-0000-0000-000000000010");
        var requestBody = CreateTurnBody(
            operationId,
            conversation.GetProperty("transcriptRevision").GetInt64(),
            "stream reconnect proof");

        using var admission = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{conversationId:D}/turns",
            requestBody);
        Assert.Equal(HttpStatusCode.Accepted, admission.StatusCode);
        using var admissionDocument = JsonDocument.Parse(await admission.Content.ReadAsStringAsync());
        var admissionBody = admissionDocument.RootElement;
        Assert.False(admissionBody.GetProperty("replayed").GetBoolean());
        Assert.Equal(
            $"/api/llm-chat-operations/{operationId:D}",
            admissionBody.GetProperty("statusUrl").GetString());
        var eventsUrl = admissionBody.GetProperty("eventsUrl").GetString();
        Assert.Equal(
            $"/api/llm-chat-operations/{operationId:D}/events",
            eventsUrl);

        await invocationPort.WaitUntilStartedAsync(ControllableLlmChatInvocationPort.StreamReconnectKey);
        long firstDeltaSequence;
        using (var firstStream = await OpenEventStreamAsync(host.Client, eventsUrl!))
        {
            Assert.Equal("text/event-stream; charset=utf-8", firstStream.Content.Headers.ContentType?.ToString());
            Assert.True(firstStream.Headers.CacheControl?.NoCache == true);
            Assert.True(firstStream.Headers.CacheControl?.NoStore == true);
            Assert.Equal("no", Assert.Single(firstStream.Headers.GetValues("X-Accel-Buffering")));
            await using var body = await firstStream.Content.ReadAsStreamAsync();
            using var reader = new StreamReader(body);
            var firstDelta = await ReadUntilEventAsync(reader, "llm.response.delta");
            firstDeltaSequence = Assert.IsType<long>(firstDelta.Id);
            using var deltaDocument = JsonDocument.Parse(firstDelta.Data);
            Assert.Equal("First ", deltaDocument.RootElement.GetProperty("payload").GetProperty("text").GetString());
            await ReadHeartbeatAsync(reader).WaitAsync(TimeSpan.FromSeconds(10));
        }

        invocationPort.ReleaseStreamReconnect();
        var completed = await WaitForOperationTerminalAsync(
            host.Client,
            $"/api/llm-chat-operations/{operationId:D}");
        Assert.Equal("succeeded", completed.GetProperty("status").GetString());

        using var reconnectRequest = new HttpRequestMessage(HttpMethod.Get, eventsUrl);
        reconnectRequest.Headers.TryAddWithoutValidation("Last-Event-ID", firstDeltaSequence.ToString());
        using var reconnect = await host.Client.SendAsync(
            reconnectRequest,
            HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, reconnect.StatusCode);
        await using var reconnectBody = await reconnect.Content.ReadAsStreamAsync();
        using var reconnectReader = new StreamReader(reconnectBody);
        var resumedFrames = await ReadToEndAsync(reconnectReader);
        Assert.DoesNotContain(
            resumedFrames,
            frame => frame.Event == "llm.response.delta" && frame.Data.Contains("First ", StringComparison.Ordinal));
        Assert.Contains(
            resumedFrames,
            frame => frame.Event == "llm.response.delta" && frame.Data.Contains("second.", StringComparison.Ordinal));
        var terminal = Assert.Single(resumedFrames, frame => frame.Event is
            "llm.operation.succeeded" or
            "llm.operation.failed" or
            "llm.operation.cancelled" or
            "llm.operation.recovery-required");
        Assert.True(terminal.Id > firstDeltaSequence);
        Assert.Equal(1, invocationPort.StreamInvocationCount);

        using var replay = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{conversationId:D}/turns",
            requestBody);
        Assert.Equal(HttpStatusCode.Accepted, replay.StatusCode);
        using var replayDocument = JsonDocument.Parse(await replay.Content.ReadAsStringAsync());
        Assert.True(replayDocument.RootElement.GetProperty("replayed").GetBoolean());
        Assert.Equal(1, invocationPort.StreamInvocationCount);
        Assert.Equal(
            terminal.Id,
            replayDocument.RootElement.GetProperty("lastEventSequence").GetInt64());
        var replayJson = replayDocument.RootElement.GetRawText();
        Assert.DoesNotContain("Review the supplied design carefully.", replayJson, StringComparison.Ordinal);
        Assert.DoesNotContain("stream reconnect proof", replayJson, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE_PROVIDER_KEY", replayJson, StringComparison.Ordinal);
        Assert.DoesNotContain("provider.invalid", replayJson, StringComparison.OrdinalIgnoreCase);

        using (var conflictingCursor = new HttpRequestMessage(HttpMethod.Get, $"{eventsUrl}?after=1"))
        {
            conflictingCursor.Headers.TryAddWithoutValidation("Last-Event-ID", "2");
            using var invalidCursor = await host.Client.SendAsync(conflictingCursor);
            Assert.Equal(HttpStatusCode.BadRequest, invalidCursor.StatusCode);
            Assert.Contains(
                LlmChatErrorCodes.StreamCursorInvalid,
                await invalidCursor.Content.ReadAsStringAsync(),
                StringComparison.Ordinal);
        }

        var eventFactory = host.App.Services.GetRequiredService<IDbContextFactory<AppDbContext>>();
        await using (var eventContext = await eventFactory.CreateDbContextAsync())
        {
            await eventContext.Set<LlmChatOperationEventRow>()
                .Where(row => row.OperationId == operationId)
                .ExecuteDeleteAsync();
        }

        using var gapStream = await OpenEventStreamAsync(host.Client, eventsUrl!);
        await using var gapBody = await gapStream.Content.ReadAsStreamAsync();
        using var gapReader = new StreamReader(gapBody);
        var gapFrames = await ReadToEndAsync(gapReader).WaitAsync(TimeSpan.FromSeconds(10));
        var gapFrame = Assert.Single(gapFrames, frame => frame.Event == "stream.gap");
        using var gapDocument = JsonDocument.Parse(gapFrame.Data);
        Assert.Equal(
            $"/api/llm-chat-operations/{operationId:D}",
            gapDocument.RootElement.GetProperty("snapshotUrl").GetString());
        Assert.Equal(
            "cursorBeforeRetention",
            gapDocument.RootElement.GetProperty("reason").GetString());
        var authoritative = await GetJsonAsync(
            host.Client,
            $"/api/llm-chat-operations/{operationId:D}");
        Assert.Equal("succeeded", authoritative.GetProperty("status").GetString());

        var cancellationOperationId = Guid.Parse("72000000-0000-0000-0000-000000000011");
        var currentConversation = await GetJsonAsync(
            host.Client,
            $"/api/llm-conversations/{conversationId:D}");
        using var cancellationAdmission = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{conversationId:D}/turns",
            CreateTurnBody(
                cancellationOperationId,
                currentConversation.GetProperty("transcriptRevision").GetInt64(),
                "cancel in flight"));
        Assert.Equal(HttpStatusCode.Accepted, cancellationAdmission.StatusCode);
        await invocationPort.WaitUntilStartedAsync(ControllableLlmChatInvocationPort.CancellationKey);
        using var cancellationStream = await OpenEventStreamAsync(
            host.Client,
            $"/api/llm-chat-operations/{cancellationOperationId:D}/events");
        using var cancellation = await host.Client.PostAsync(
            $"/api/llm-chat-operations/{cancellationOperationId:D}/cancel",
            null);
        Assert.True(
            cancellation.StatusCode is HttpStatusCode.OK or HttpStatusCode.Accepted,
            $"Unexpected cancellation status {(int)cancellation.StatusCode}.");
        await using var cancellationBody = await cancellationStream.Content.ReadAsStreamAsync();
        using var cancellationReader = new StreamReader(cancellationBody);
        var cancellationFrames = await ReadToEndAsync(cancellationReader).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Single(cancellationFrames, frame => frame.Event == "llm.operation.cancelled");
        var cancelled = await GetJsonAsync(
            host.Client,
            $"/api/llm-chat-operations/{cancellationOperationId:D}");
        Assert.Equal("cancelled", cancelled.GetProperty("status").GetString());

        var failureOperationId = Guid.Parse("72000000-0000-0000-0000-000000000012");
        currentConversation = await GetJsonAsync(
            host.Client,
            $"/api/llm-conversations/{conversationId:D}");
        using var failureAdmission = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{conversationId:D}/turns",
            CreateTurnBody(
                failureOperationId,
                currentConversation.GetProperty("transcriptRevision").GetInt64(),
                "provider failure"));
        Assert.Equal(HttpStatusCode.Accepted, failureAdmission.StatusCode);
        var failed = await WaitForOperationTerminalAsync(
            host.Client,
            $"/api/llm-chat-operations/{failureOperationId:D}");
        Assert.Equal("failed", failed.GetProperty("status").GetString());
        using var failureStream = await OpenEventStreamAsync(
            host.Client,
            $"/api/llm-chat-operations/{failureOperationId:D}/events");
        await using var failureBody = await failureStream.Content.ReadAsStreamAsync();
        using var failureReader = new StreamReader(failureBody);
        var failureFrames = await ReadToEndAsync(failureReader).WaitAsync(TimeSpan.FromSeconds(10));
        var failureFrame = Assert.Single(failureFrames, frame => frame.Event == "llm.operation.failed");
        Assert.DoesNotContain(
            ControllableLlmChatInvocationPort.ProviderSecret,
            failureFrame.Data,
            StringComparison.Ordinal);
    }

    private static async Task<ApiTestHost> CreateCapacityHostAsync(
        CapacityTrackingLlmChatInvocationPort invocationPort,
        int workerCount)
    {
        var provider = CreateProvider();
        return await ApiTestHost.CreateAsync(
            jwtEnabled: false,
            configureServices: services =>
            {
                services.RemoveAll<LlmChatExecutionLeaseOptions>();
                services.AddSingleton(new LlmChatExecutionLeaseOptions
                {
                    PollInterval = TimeSpan.FromMilliseconds(100),
                    HeartbeatInterval = TimeSpan.FromMilliseconds(100),
                    LeaseDuration = TimeSpan.FromSeconds(1),
                    CandidateBatchSize = 16,
                    WorkerCount = workerCount,
                    MaximumQueuedAge = TimeSpan.FromMinutes(5),
                    MaximumOperationDuration = TimeSpan.FromMinutes(30)
                });
                ConfigureProviderBoundary(services, provider, invocationPort);
            });
    }

    private static async Task<JsonElement> CreateActiveDefinitionAsync(HttpClient client)
    {
        var definition = await CreateDefinitionAsync(client, FastModel, thinkingEffort: null);
        return await ChangeDefinitionStatusAsync(
            client,
            definition.GetProperty("id").GetGuid(),
            "activate",
            definition);
    }

    private static async Task AdmitCapacityTurnAsync(
        HttpClient client,
        JsonElement conversation,
        Guid operationId,
        string message)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/llm-conversations/{conversation.GetProperty("id").GetGuid():D}/turns",
            CreateTurnBody(
                operationId,
                conversation.GetProperty("transcriptRevision").GetInt64(),
                message));
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
    }

    private static void ConfigureProviderBoundary<TInvocationPort>(
        IServiceCollection services,
        ProviderProfile provider,
        TInvocationPort invocationPort)
        where TInvocationPort : class, ILlmInvocationPort, ILlmStreamingInvocationPort
    {
        services.RemoveAll<IProviderRuntimeProfileSource>();
        services.RemoveAll<ILlmInvocationPort>();
        services.RemoveAll<ILlmStreamingInvocationPort>();
        services.AddSingleton<IProviderRuntimeProfileSource>(new StaticProviderSource(provider));
        services.AddSingleton<ILlmInvocationPort>(invocationPort);
        services.AddSingleton<ILlmStreamingInvocationPort>(invocationPort);
    }

    private static async Task<HttpResponseMessage> OpenEventStreamAsync(
        HttpClient client,
        string eventsUrl)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, eventsUrl);
        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response;
    }

    private static async Task<SseFrame> ReadUntilEventAsync(StreamReader reader, string eventName)
    {
        while (true)
        {
            var frame = await ReadFrameAsync(reader)
                ?? throw new EndOfStreamException($"The SSE stream closed before '{eventName}'.");
            if (frame.Event == eventName)
            {
                return frame;
            }
        }
    }

    private static async Task ReadHeartbeatAsync(StreamReader reader)
    {
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.StartsWith(": heartbeat ", StringComparison.Ordinal))
            {
                return;
            }
        }

        throw new EndOfStreamException("The SSE stream closed before a heartbeat was received.");
    }

    private static async Task<IReadOnlyList<SseFrame>> ReadToEndAsync(StreamReader reader)
    {
        var frames = new List<SseFrame>();
        while (await ReadFrameAsync(reader) is { } frame)
        {
            frames.Add(frame);
        }

        return frames;
    }

    private static async Task<SseFrame?> ReadFrameAsync(StreamReader reader)
    {
        long? id = null;
        string? eventName = null;
        string? data = null;
        while (await reader.ReadLineAsync() is { } line)
        {
            if (line.Length == 0)
            {
                if (eventName is not null && data is not null)
                {
                    return new SseFrame(id, eventName, data);
                }

                continue;
            }

            if (line.StartsWith("id: ", StringComparison.Ordinal))
            {
                id = long.Parse(line.AsSpan(4), System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (line.StartsWith("event: ", StringComparison.Ordinal))
            {
                eventName = line[7..];
            }
            else if (line.StartsWith("data: ", StringComparison.Ordinal))
            {
                data = line[6..];
            }
        }

        return null;
    }

    private static void DecorateConversationEngine(
        IServiceCollection services,
        LlmChatFinalizationBarrier barrier)
    {
        var descriptor = services.Last(item => item.ServiceType == typeof(ILlmChatConversationEngine));
        var factory = descriptor.ImplementationFactory
            ?? throw new InvalidOperationException("The LLM Chat conversation engine must use its scoped factory.");
        services.Remove(descriptor);
        services.Add(ServiceDescriptor.Describe(
            typeof(ILlmChatConversationEngine),
            serviceProvider => new BarrierLlmChatConversationEngine(
                (ILlmChatConversationEngine)factory(serviceProvider),
                barrier),
            descriptor.Lifetime));
    }

    private static ProviderProfile CreateProvider()
        => new(
            ProviderId,
            "Private chat provider",
            ProviderKind.AzureOpenAi,
            "https://provider.invalid",
            "PRIVATE_PROVIDER_KEY",
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
        Assert.DoesNotContain("PRIVATE_PROVIDER_KEY", json, StringComparison.Ordinal);
        Assert.DoesNotContain("provider.invalid", json, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task AssertOpenApiAsync(HttpClient client)
    {
        var openApi = await GetJsonAsync(client, "/swagger/v1/swagger.json");
        var paths = openApi.GetProperty("paths");
        var send = paths.GetProperty("/api/llm-conversations/{conversationId}/turns").GetProperty("post");
        Assert.Equal("SendLlmChatTurn", send.GetProperty("operationId").GetString());
        Assert.True(send.GetProperty("responses").TryGetProperty("202", out var accepted));
        Assert.True(accepted.GetProperty("content").TryGetProperty("application/json", out _));
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
            "StreamLlmChatOperationEvents",
            paths.GetProperty("/api/llm-chat-operations/{operationId}/events")
                .GetProperty("get").GetProperty("operationId").GetString());
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
            name = "Architecture assistant",
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
            tags = new[] { "streaming-api", "architecture" },
            revisionReason = $"API {model} {thinkingEffort ?? "provider-default"}",
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
            new { title });
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
            response.StatusCode == HttpStatusCode.Accepted,
            $"Expected an admitted turn but received {(int)response.StatusCode}: {responseBody}");
        Assert.NotNull(response.Headers.Location);
        return await WaitForOperationTerminalAsync(client, response.Headers.Location!.ToString());
    }

    private static async Task<JsonElement> WaitForOperationTerminalAsync(HttpClient client, string location)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            var operation = await GetJsonAsync(client, location);
            var status = operation.GetProperty("status").GetString();
            if (status is "succeeded" or "failed" or "cancelled" or "recoveryRequired")
            {
                return operation;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        throw new TimeoutException($"LLM Chat operation at '{location}' did not become terminal.");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> predicate, string description)
    {
        for (var attempt = 0; attempt < 200; attempt++)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25));
        }

        throw new TimeoutException($"Timed out waiting for {description}.");
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
            DisplayName = "Switched profile",
            ProviderKind = current.Profile.ProviderKind,
            SourceKind = current.Profile.SourceKind,
            Runtime = new DatabaseProfileRuntimeMetadata
            {
                Fingerprint = $"{current.Profile.Runtime.Fingerprint}-profile-switch"
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
        Guid cancellationOperationId)
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
        var operations = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .ToDictionaryAsync(row => row.Id);
        Assert.Equal(LlmChatOperationStatus.Succeeded, operations[firstOperationId].Status);
        Assert.Equal(LlmChatOperationStatus.Succeeded, operations[secondOperationId].Status);
        Assert.Equal(LlmChatOperationStatus.Failed, operations[providerFailureOperationId].Status);
        Assert.Equal(LlmChatOperationStatus.Cancelled, operations[cancellationOperationId].Status);
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
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            $"Expected 200 from '{path}' but received {(int)response.StatusCode}: {body}");
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    private sealed record ConversationCounts(int MessageCount, int InvocationCount);

    private sealed record SseFrame(long? Id, string Event, string Data);

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

internal sealed class CapacityTrackingLlmChatInvocationPort : ILlmInvocationPort, ILlmStreamingInvocationPort
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> starts = [];
    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> releases = [];
    private readonly ConcurrentDictionary<string, int> activeByKey = [];
    private readonly ConcurrentDictionary<string, int> maximumActiveByKey = [];
    private readonly object startedGate = new();
    private readonly List<string> startedKeys = [];
    private TaskCompletionSource<bool> startedChanged = CreateSignal();
    private int activeCount;
    private int maximumActiveCount;
    private int cancellationCount;

    public int ActiveCount => Volatile.Read(ref activeCount);

    public int MaximumActiveCount => Volatile.Read(ref maximumActiveCount);

    public int CancellationCount => Volatile.Read(ref cancellationCount);

    public IReadOnlyList<string> StartedKeys
    {
        get
        {
            lock (startedGate)
            {
                return [.. startedKeys];
            }
        }
    }

    public Task<LlmInvocationResult> InvokeAsync(
        LlmInvocationRequest request,
        CancellationToken cancellationToken = default)
        => Task.FromResult(new LlmInvocationResult(request.Model, "unused", LlmUsage.Zero));

    public async IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var key = request.Messages.Last(message => message.Role == LlmMessageRole.User).Text;
        var now = DateTimeOffset.UtcNow;
        yield return new LlmStreamingAttemptStarted(
            1,
            request.Provider.Id,
            request.Provider.Kind,
            request.Model,
            LlmStreamingDeliveryMode.Incremental,
            now);
        Enter(key);
        try
        {
            if (key.StartsWith("hold-", StringComparison.Ordinal))
            {
                await releases.GetOrAdd(key, static _ => CreateSignal()).Task
                    .WaitAsync(cancellationToken);
            }

            yield return new LlmStreamingTextDelta(1, $"Completed {key}", 1);
            yield return new LlmStreamingCompleted(
                1,
                request.Model,
                "stop",
                new LlmUsage(2, 2),
                LlmStreamingDeliveryMode.Incremental,
                DateTimeOffset.UtcNow);
        }
        finally
        {
            if (cancellationToken.IsCancellationRequested)
            {
                Interlocked.Increment(ref cancellationCount);
            }

            activeByKey.AddOrUpdate(key, 0, static (_, count) => count - 1);
            Interlocked.Decrement(ref activeCount);
        }
    }

    public bool IsActive(string key)
        => activeByKey.GetValueOrDefault(key) > 0;

    public int MaximumActiveFor(string key)
        => maximumActiveByKey.GetValueOrDefault(key);

    public Task WaitUntilStartedAsync(string key)
        => starts.GetOrAdd(key, static _ => CreateSignal()).Task.WaitAsync(TimeSpan.FromSeconds(10));

    public async Task WaitForStartedCountAsync(int count)
    {
        while (true)
        {
            Task changed;
            lock (startedGate)
            {
                if (startedKeys.Count >= count)
                {
                    return;
                }

                changed = startedChanged.Task;
            }

            await changed.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    public void Release(string key)
        => releases.GetOrAdd(key, static _ => CreateSignal()).TrySetResult(true);

    public void ReleaseAll()
    {
        foreach (var key in StartedKeys)
        {
            Release(key);
        }
    }

    private void Enter(string key)
    {
        starts.GetOrAdd(key, static _ => CreateSignal()).TrySetResult(true);
        var keyActive = activeByKey.AddOrUpdate(key, 1, static (_, count) => count + 1);
        maximumActiveByKey.AddOrUpdate(key, keyActive, (_, maximum) => Math.Max(maximum, keyActive));
        var active = Interlocked.Increment(ref activeCount);
        UpdateMaximum(ref maximumActiveCount, active);
        lock (startedGate)
        {
            startedKeys.Add(key);
            startedChanged.TrySetResult(true);
            startedChanged = CreateSignal();
        }
    }

    private static void UpdateMaximum(ref int target, int candidate)
    {
        var observed = Volatile.Read(ref target);
        while (candidate > observed)
        {
            var replaced = Interlocked.CompareExchange(ref target, candidate, observed);
            if (replaced == observed)
            {
                return;
            }

            observed = replaced;
        }
    }

    private static TaskCompletionSource<bool> CreateSignal()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);
}

internal sealed class ControllableLlmChatInvocationPort : ILlmInvocationPort, ILlmStreamingInvocationPort
{
    public const string CancellationKey = "cancel";
    public const string ProfileSwitchKey = "profile-switch";
    public const string RequestDisconnectKey = "request-disconnect";
    public const string StreamReconnectKey = "stream-reconnect";
    public const string ProviderSecret = "provider-secret-must-not-leak";

    private readonly ConcurrentDictionary<string, TaskCompletionSource<bool>> starts = [];
    private readonly TaskCompletionSource<bool> profileSwitchRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> requestDisconnectRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> streamReconnectRelease =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int streamInvocationCount;

    public int StreamInvocationCount => Volatile.Read(ref streamInvocationCount);

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

        if (userText.Contains("request disconnect in flight", StringComparison.Ordinal))
        {
            Signal(RequestDisconnectKey);
            await requestDisconnectRelease.Task;
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

    public void ReleaseRequestDisconnect()
        => requestDisconnectRelease.TrySetResult(true);

    public void ReleaseStreamReconnect()
        => streamReconnectRelease.TrySetResult(true);

    public async IAsyncEnumerable<LlmStreamingUpdate> StreamAsync(
        LlmInvocationRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref streamInvocationCount);
        var now = DateTimeOffset.UtcNow;
        yield return new LlmStreamingAttemptStarted(
            1,
            request.Provider.Id,
            request.Provider.Kind,
            request.Model,
            LlmStreamingDeliveryMode.Incremental,
            now);
        var userText = request.Messages.Last(message => message.Role == LlmMessageRole.User).Text;
        if (userText.Contains("provider failure", StringComparison.Ordinal))
        {
            yield return new LlmStreamingFailed(
                1,
                LlmInvocationFailureKind.ProviderFailure,
                new LlmUsage(7, 0),
                false,
                DateTimeOffset.UtcNow);
            yield break;
        }

        if (userText.Contains("cancel in flight", StringComparison.Ordinal))
        {
            Signal(CancellationKey);
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        if (userText.Contains("profile switch in flight", StringComparison.Ordinal))
        {
            Signal(ProfileSwitchKey);
            await profileSwitchRelease.Task.WaitAsync(cancellationToken);
        }

        if (userText.Contains("request disconnect in flight", StringComparison.Ordinal))
        {
            Signal(RequestDisconnectKey);
            await requestDisconnectRelease.Task.WaitAsync(cancellationToken);
        }

        if (userText.Contains("stream reconnect proof", StringComparison.Ordinal))
        {
            yield return new LlmStreamingTextDelta(1, "First ", 1);
            Signal(StreamReconnectKey);
            await streamReconnectRelease.Task.WaitAsync(cancellationToken);
            yield return new LlmStreamingTextDelta(1, "second.", 2);
        }
        else
        {
            yield return new LlmStreamingTextDelta(1, $"Assistant response to: {userText}", 1);
        }

        yield return new LlmStreamingCompleted(
            1,
            request.Model,
            "stop",
            new LlmUsage(10, 4, 1),
            LlmStreamingDeliveryMode.Incremental,
            DateTimeOffset.UtcNow);
    }

    private void Signal(string key)
        => starts.GetOrAdd(
                key,
                static _ => new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously))
            .TrySetResult(true);
}

internal sealed class LlmChatFinalizationBarrier
{
    private readonly TaskCompletionSource<bool> reached =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource<bool> released =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task WaitAsync()
        => reached.Task.WaitAsync(TimeSpan.FromSeconds(10));

    public async Task WaitForReleaseAsync()
    {
        reached.TrySetResult(true);
        await released.Task.ConfigureAwait(false);
    }

    public void Release()
        => released.TrySetResult(true);
}

internal sealed class BarrierLlmChatConversationEngine(
    ILlmChatConversationEngine inner,
    LlmChatFinalizationBarrier barrier) : ILlmChatConversationEngine
{
    public Task<LlmChatConversationEngineState> CreateAsync(
        LlmChatConversationId conversationId,
        LlmChatDefinitionRevision definitionRevision,
        string title,
        CancellationToken cancellationToken = default)
        => inner.CreateAsync(conversationId, definitionRevision, title, cancellationToken);

    public Task<LlmChatConversationEngineState?> TryGetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
        => inner.TryGetAsync(conversationId, cancellationToken);

    public Task<LlmChatTranscriptPage?> TryGetTranscriptPageAsync(
        LlmChatConversationId conversationId,
        int take,
        LlmChatTranscriptCursor? cursor,
        CancellationToken cancellationToken = default)
        => inner.TryGetTranscriptPageAsync(conversationId, take, cursor, cancellationToken);

    public Task<LlmChatConversationEngineState> RenameAsync(
        LlmChatConversationId conversationId,
        string title,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
        => inner.RenameAsync(conversationId, title, expectedTranscriptRevision, cancellationToken);

    public Task<LlmConversationTurnAdmission> AdmitTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
        => inner.AdmitTurnAsync(
            conversationId,
            operationId,
            definition,
            definitionRevision,
            userText,
            expectedTranscriptRevision,
            cancellationToken);

    public async IAsyncEnumerable<LlmStreamingUpdate> StreamTurnAsync(
        LlmConversationTurnAdmission admission,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await foreach (var update in inner.StreamTurnAsync(admission, cancellationToken).ConfigureAwait(false))
        {
            if (update is LlmStreamingCompleted)
            {
                await barrier.WaitForReleaseAsync().ConfigureAwait(false);
            }

            yield return update;
        }
    }

    public Task<LlmConversationTurnAdmission> ResumeAdmittedTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        CancellationToken cancellationToken = default)
        => inner.ResumeAdmittedTurnAsync(
            conversationId,
            operationId,
            definition,
            definitionRevision,
            cancellationToken);

    public Task<LlmChatConversationEngineTurnResult> CompleteTurnAsync(
        LlmConversationTurnAdmission admission,
        LlmInvocationResult invocationResult,
        CancellationToken cancellationToken = default)
        => inner.CompleteTurnAsync(admission, invocationResult, cancellationToken);

    public Task<LlmChatConversationEngineState> CompensateTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => inner.CompensateTurnAsync(conversationId, operationId, cancellationToken);

    public Task<LlmChatConversationTurnEvidence?> InspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => inner.InspectTurnAsync(conversationId, operationId, cancellationToken);

    public Task<LlmChatConversationEngineState> AbandonActiveTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => inner.AbandonActiveTurnAsync(conversationId, operationId, cancellationToken);
}
