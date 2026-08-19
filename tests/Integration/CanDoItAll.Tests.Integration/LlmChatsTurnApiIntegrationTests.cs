using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed class LlmChatsTurnApiIntegrationTests
{
    [Fact]
    public async Task TurnApi_UsesOneOperationResourceAndRejectsStaleUnknownAndProviderFailureSafely()
    {
        var service = new StubLlmChatOperationApplicationService();
        await using var host = await CreateHostAsync(service);

        using var accepted = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}/turns",
            new
            {
                operationId = Guid.Parse("10000000-0000-0000-0000-000000000001"),
                expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision,
                message = "running"
            });
        Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
        Assert.Equal(
            "/api/llm-chat-operations/10000000-0000-0000-0000-000000000001",
            accepted.Headers.Location?.OriginalString);
        using var acceptedJson = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync());
        Assert.Equal(
            "candoitall.llm-chat-operation.v1",
            acceptedJson.RootElement.GetProperty("schema").GetString());
        Assert.Equal("running", acceptedJson.RootElement.GetProperty("status").GetString());

        using var stale = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}/turns",
            new
            {
                operationId = Guid.Parse("10000000-0000-0000-0000-000000000002"),
                expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision - 1,
                message = "stale"
            });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains(LlmChatErrorCodes.TranscriptRevisionConflict, await stale.Content.ReadAsStringAsync());

        using var unknown = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}/turns",
            new
            {
                operationId = Guid.Parse("10000000-0000-0000-0000-000000000003"),
                expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision,
                message = "ignored",
                context = new[] { "must-not-be-ignored" },
                attachments = Array.Empty<object>(),
                channel = "external",
                model = "override"
            });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        using var providerFailure = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}/turns",
            new
            {
                operationId = Guid.Parse("10000000-0000-0000-0000-000000000004"),
                expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision,
                message = "provider-failure"
            });
        Assert.Equal(HttpStatusCode.Accepted, providerFailure.StatusCode);
        using var failureJson = JsonDocument.Parse(await providerFailure.Content.ReadAsStringAsync());
        Assert.Equal(
            LlmChatErrorCodes.ProviderUnavailable,
            failureJson.RootElement.GetProperty("failure").GetProperty("code").GetString());
        Assert.Equal(
            Guid.Parse("10000000-0000-0000-0000-000000000004"),
            failureJson.RootElement.GetProperty("operationId").GetGuid());
        Assert.True(failureJson.RootElement.GetProperty("failure").GetProperty("retryable").GetBoolean());
        Assert.DoesNotContain("provider-secret", failureJson.RootElement.GetRawText(), StringComparison.Ordinal);
    }

    internal static Task<ApiTestHost> CreateHostAsync(
        StubLlmChatOperationApplicationService service,
        bool jwtEnabled = false)
        => ApiTestHost.CreateAsync(
            jwtEnabled,
            configureServices: collection =>
            {
                collection.RemoveAll<ILlmChatOperationApplicationService>();
                collection.AddSingleton<ILlmChatOperationApplicationService>(service);
            },
            useInMemoryDatabase: true);
}

public sealed class LlmChatsIdempotencyApiIntegrationTests
{
    [Fact]
    public async Task TurnApi_SameOperationRetryReturnsExistingResultAndConflictDoesNotDispatch()
    {
        var service = new StubLlmChatOperationApplicationService();
        await using var host = await LlmChatsTurnApiIntegrationTests.CreateHostAsync(service);
        var operationId = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var route = $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}/turns";
        var request = new
        {
            operationId,
            expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision,
            message = "Review this design."
        };

        using var first = await host.Client.PostAsJsonAsync(route, request);
        using var retry = await host.Client.PostAsJsonAsync(route, request);
        using var conflict = await host.Client.PostAsJsonAsync(route, new
        {
            operationId,
            expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision,
            message = "A different paid request."
        });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, retry.StatusCode);
        using var firstJson = JsonDocument.Parse(await first.Content.ReadAsStringAsync());
        using var retryJson = JsonDocument.Parse(await retry.Content.ReadAsStringAsync());
        Assert.False(firstJson.RootElement.GetProperty("replayed").GetBoolean());
        Assert.True(retryJson.RootElement.GetProperty("replayed").GetBoolean());
        Assert.False(firstJson.RootElement.TryGetProperty("requestFingerprint", out _));
        Assert.False(retryJson.RootElement.TryGetProperty("requestFingerprint", out _));
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictBody = await conflict.Content.ReadAsStringAsync();
        Assert.Contains(LlmChatErrorCodes.OperationIdConflict, conflictBody);
        Assert.DoesNotContain("requestFingerprint", conflictBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Review this design.", conflictBody, StringComparison.Ordinal);
        Assert.DoesNotContain("A different paid request.", conflictBody, StringComparison.Ordinal);
        Assert.Equal(1, service.ProviderDispatchCount);
    }
}

public sealed class LlmChatsCancellationApiIntegrationTests
{
    [Fact]
    public async Task CancellationApi_PersistsCancellationAndOperationStatusReturnsIt()
    {
        var service = new StubLlmChatOperationApplicationService();
        await using var host = await LlmChatsTurnApiIntegrationTests.CreateHostAsync(service);
        var operationId = Guid.Parse("30000000-0000-0000-0000-000000000001");
        using var send = await host.Client.PostAsJsonAsync(
            $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}/turns",
            new
            {
                operationId,
                expectedTranscriptRevision = StubLlmChatOperationApplicationService.TranscriptRevision,
                message = "running"
            });
        Assert.Equal(HttpStatusCode.Accepted, send.StatusCode);

        using var cancel = await host.Client.PostAsync($"/api/llm-chat-operations/{operationId:D}/cancel", null);
        Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        using var cancelled = JsonDocument.Parse(await cancel.Content.ReadAsStringAsync());
        Assert.Equal("cancelled", cancelled.RootElement.GetProperty("status").GetString());

        using var get = await host.Client.GetAsync($"/api/llm-chat-operations/{operationId:D}");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);
        using var persisted = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal("cancelled", persisted.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, service.CancellationCount);
    }
}

public sealed class LlmChatOperationAuditApiIntegrationTests
{
    [Fact]
    public async Task Operation_status_returns_bounded_sanitized_invocation_attempts()
    {
        var service = new StubLlmChatOperationApplicationService();
        var operationId = LlmChatOperationId.New();
        service.SeedInvocationAudit(operationId, LlmChatOperationDetails.MaximumInvocationRecords);
        await using var host = await LlmChatsTurnApiIntegrationTests.CreateHostAsync(service);

        using var response = await host.Client.GetAsync($"/api/llm-chat-operations/{operationId.Value:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var attempts = document.RootElement.GetProperty("invocationAttempts").EnumerateArray().ToArray();
        Assert.Equal(LlmChatOperationDetails.MaximumInvocationRecords, attempts.Length);
        Assert.Equal(
            Enumerable.Range(1, LlmChatOperationDetails.MaximumInvocationRecords),
            attempts.Select(attempt => attempt.GetProperty("ordinal").GetInt32()));
    }

    [Fact]
    public async Task Invocation_projection_excludes_profile_name_id_correlation_and_raw_failure()
    {
        var service = new StubLlmChatOperationApplicationService();
        var operationId = LlmChatOperationId.New();
        service.SeedInvocationAudit(operationId, 1);
        await using var host = await LlmChatsTurnApiIntegrationTests.CreateHostAsync(service);

        using var response = await host.Client.GetAsync($"/api/llm-chat-operations/{operationId.Value:D}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        var attempt = Assert.Single(document.RootElement.GetProperty("invocationAttempts").EnumerateArray());
        Assert.Equal((int)ProviderKind.OpenAi, attempt.GetProperty("providerKind").GetInt32());
        Assert.Equal(
            LlmChatErrorCodes.StorageCorrupted,
            attempt.GetProperty("failure").GetProperty("code").GetString());
        Assert.DoesNotContain("provider-profile-secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("correlation-secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("raw-provider-secret", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("requestFingerprint", body, StringComparison.OrdinalIgnoreCase);
    }
}

public sealed class LlmChatsRecoveryApiIntegrationTests
{
    [Fact]
    public async Task Reconcile_route_requires_manage_scope_and_returns_stable_errors()
    {
        var service = new StubLlmChatOperationApplicationService();
        var operationId = new LlmChatOperationId(Guid.Parse("40000000-0000-0000-0000-000000000002"));
        service.SeedRecoveryRequired(operationId, hasLiveOwner: false);
        await using var host = await LlmChatsTurnApiIntegrationTests.CreateHostAsync(service, jwtEnabled: true);
        var tokenService = host.App.Services.GetRequiredService<IApiTokenService>();
        var route = $"/api/llm-chat-operations/{operationId.Value:D}/reconcile";

        using var unauthenticated = await host.Client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);

        SetScope(host, tokenService, ApiAccessScopeNames.ExecuteLlmChats);
        using var wrongScope = await host.Client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.Forbidden, wrongScope.StatusCode);

        SetScope(host, tokenService, ApiAccessScopeNames.ManageLlmChats);
        using var reconciled = await host.Client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.OK, reconciled.StatusCode);
        Assert.Equal(1, service.ReconciliationCount);

        using var invalid = await host.Client.PostAsync(
            $"/api/llm-chat-operations/{Guid.Empty:D}/reconcile",
            null);
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Contains(LlmChatErrorCodes.InvalidRequest, await invalid.Content.ReadAsStringAsync());

        using var missing = await host.Client.PostAsync(
            $"/api/llm-chat-operations/{Guid.NewGuid():D}/reconcile",
            null);
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains(LlmChatErrorCodes.OperationNotFound, await missing.Content.ReadAsStringAsync());

        using var swagger = await host.Client.GetAsync("/swagger/v1/swagger.json");
        using var document = JsonDocument.Parse(await swagger.Content.ReadAsStringAsync());
        var responses = document.RootElement
            .GetProperty("paths")
            .GetProperty("/api/llm-chat-operations/{operationId}/reconcile")
            .GetProperty("post")
            .GetProperty("responses");
        Assert.True(responses.TryGetProperty("400", out _));
        Assert.True(responses.TryGetProperty("404", out _));
        Assert.True(responses.TryGetProperty("409", out _));
    }

    [Fact]
    public async Task RecoveryApi_RejectsLiveOwnerThenPersistsExactTurnAbandonment()
    {
        var service = new StubLlmChatOperationApplicationService();
        var operationId = new LlmChatOperationId(Guid.Parse("40000000-0000-0000-0000-000000000001"));
        service.SeedRecoveryRequired(operationId, hasLiveOwner: true);
        await using var host = await LlmChatsTurnApiIntegrationTests.CreateHostAsync(service);
        var route =
            $"/api/llm-conversations/{StubLlmChatOperationApplicationService.ConversationId.Value:D}" +
            $"/active-turns/{operationId.Value:D}/abandon";

        using var liveOwner = await host.Client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.Conflict, liveOwner.StatusCode);
        Assert.Contains(LlmChatErrorCodes.OperationRecoveryRequired, await liveOwner.Content.ReadAsStringAsync());
        Assert.Equal(0, service.AbandonmentCount);

        service.ReleaseLiveOwner(operationId);
        using var abandon = await host.Client.PostAsync(route, null);
        Assert.Equal(HttpStatusCode.OK, abandon.StatusCode);
        using var abandoned = JsonDocument.Parse(await abandon.Content.ReadAsStringAsync());
        Assert.Equal("failed", abandoned.RootElement.GetProperty("status").GetString());
        Assert.Equal(1, service.AbandonmentCount);

        using var get = await host.Client.GetAsync($"/api/llm-chat-operations/{operationId.Value:D}");
        using var persisted = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
        Assert.Equal("failed", persisted.RootElement.GetProperty("status").GetString());
    }

    private static void SetScope(ApiTestHost host, IApiTokenService tokenService, string scope)
    {
        var token = tokenService.IssueToken(new ApiTokenIssueRequest
        {
            Subject = $"llm-chat-reconcile-{scope}",
            DisplayName = "LLM Chat reconcile client",
            Scopes = [scope]
        });
        host.Client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(token.TokenType, token.Token);
    }
}

internal sealed class StubLlmChatOperationApplicationService : ILlmChatOperationApplicationService
{
    public static readonly LlmChatConversationId ConversationId =
        new(new Guid("50000000-0000-0000-0000-000000000001"));
    public const long TranscriptRevision = 5;

    private static readonly DateTimeOffset Now = new(2026, 8, 14, 0, 0, 0, TimeSpan.Zero);
    private static readonly LlmChatRequestFingerprint Fingerprint = new(new string('a', 64));
    private readonly Dictionary<LlmChatOperationId, SendLlmChatTurnCommand> commands = [];
    private readonly Dictionary<LlmChatOperationId, LlmChatOperationDetails> operations = [];
    private readonly HashSet<LlmChatOperationId> liveOwners = [];

    public int ProviderDispatchCount { get; private set; }

    public int CancellationCount { get; private set; }

    public int AbandonmentCount { get; private set; }

    public int ReconciliationCount { get; private set; }

    public Task<Result<LlmChatOperationDetails>> SendAsync(
        SendLlmChatTurnCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.ExpectedTranscriptRevision != TranscriptRevision)
        {
            return Failure(LlmChatErrorCodes.TranscriptRevisionConflict);
        }

        if (operations.TryGetValue(command.OperationId, out var existing))
        {
            return commands[command.OperationId] == command
                ? Success(existing with { Replayed = true })
                : Failure(LlmChatErrorCodes.OperationIdConflict);
        }

        ProviderDispatchCount++;
        commands.Add(command.OperationId, command);
        var details = command.Message switch
        {
            "running" => CreateDetails(command, LlmChatOperationStatus.Running),
            "provider-failure" => CreateDetails(
                command,
                LlmChatOperationStatus.Failed,
                LlmChatErrorCodes.ProviderUnavailable),
            _ => CreateDetails(command, LlmChatOperationStatus.Succeeded, assistant: true)
        };
        operations.Add(command.OperationId, details);
        return Success(details);
    }

    public Task<Result<LlmChatOperationDetails>> GetAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => operations.TryGetValue(operationId, out var details)
            ? Success(details)
            : Failure(LlmChatErrorCodes.OperationNotFound);

    public Task<Result<LlmChatOperationDetails>> CancelAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        if (!operations.TryGetValue(operationId, out var details))
        {
            return Failure(LlmChatErrorCodes.OperationNotFound);
        }

        CancellationCount++;
        var cancelled = details with
        {
            Operation = details.Operation with
            {
                Status = LlmChatOperationStatus.Cancelled,
                CancellationRequestedAtUtc = Now,
                CompletedAtUtc = Now,
                FailureCode = LlmChatErrorCodes.Cancelled,
                ConcurrencyToken = details.Operation.ConcurrencyToken + 1
            }
        };
        operations[operationId] = cancelled;
        return Success(cancelled);
    }

    public Task<Result<LlmChatOperationDetails>> ReconcileAsync(
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        if (liveOwners.Contains(operationId))
        {
            return Failure(LlmChatErrorCodes.ActiveTurnConflict);
        }

        ReconciliationCount++;
        return GetAsync(operationId, cancellationToken);
    }

    public Task<Result<LlmChatOperationDetails>> AbandonActiveTurnAsync(
        AbandonLlmChatActiveTurnCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!operations.TryGetValue(command.TurnId, out var details))
        {
            return Failure(LlmChatErrorCodes.OperationNotFound);
        }

        if (details.Operation.ConversationId != command.ConversationId ||
            details.Operation.Status != LlmChatOperationStatus.RecoveryRequired ||
            liveOwners.Contains(command.TurnId))
        {
            return Failure(LlmChatErrorCodes.OperationRecoveryRequired);
        }

        AbandonmentCount++;
        var failed = details with
        {
            Operation = details.Operation with
            {
                Status = LlmChatOperationStatus.Failed,
                CompletedAtUtc = Now,
                FailureCode = LlmChatErrorCodes.OperationRecoveryRequired,
                ConcurrencyToken = details.Operation.ConcurrencyToken + 1
            }
        };
        operations[command.TurnId] = failed;
        return Success(failed);
    }

    public void SeedRecoveryRequired(LlmChatOperationId operationId, bool hasLiveOwner)
    {
        var command = new SendLlmChatTurnCommand(operationId, ConversationId, TranscriptRevision, "recover");
        commands[operationId] = command;
        operations[operationId] = CreateDetails(
            command,
            LlmChatOperationStatus.RecoveryRequired,
            LlmChatErrorCodes.OperationRecoveryRequired);
        if (hasLiveOwner)
        {
            liveOwners.Add(operationId);
        }
    }

    public void ReleaseLiveOwner(LlmChatOperationId operationId)
        => liveOwners.Remove(operationId);

    public void SeedInvocationAudit(LlmChatOperationId operationId, int count)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(count, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(
            count,
            LlmChatOperationDetails.MaximumInvocationRecords);
        var command = new SendLlmChatTurnCommand(operationId, ConversationId, TranscriptRevision, "audit");
        var invocations = Enumerable.Range(1, count)
            .Select(ordinal => new LlmChatInvocationRecord(
                operationId,
                Guid.Parse("aaaaaaaa-1111-2222-3333-bbbbbbbbbbbb"),
                ProviderKind.OpenAi,
                "provider-profile-secret",
                "safe-model",
                AgentReasoningEffortLevel.None,
                AgentReasoningEffortLevel.Medium,
                ordinal,
                new LlmUsage(ordinal, 1),
                ordinal == count
                    ? LlmChatInvocationOutcome.Failed
                    : LlmChatInvocationOutcome.Succeeded,
                ordinal == count ? "raw-provider-secret" : string.Empty,
                Now.AddSeconds(ordinal),
                Now.AddSeconds(ordinal + 1),
                "correlation-secret",
                LlmStreamingDeliveryMode.Incremental,
                ordinal == count ? string.Empty : "stop"))
            .ToArray();
        commands[operationId] = command;
        operations[operationId] = CreateDetails(
            command,
            LlmChatOperationStatus.Failed,
            LlmChatErrorCodes.StorageCorrupted) with
        {
            Invocations = invocations
        };
    }

    private static LlmChatOperationDetails CreateDetails(
        SendLlmChatTurnCommand command,
        LlmChatOperationStatus status,
        string failureCode = "",
        bool assistant = false)
    {
        var operation = new LlmChatOperation(
            command.OperationId,
            command.ConversationId,
            LlmChatOperationKind.SendTurn,
            Fingerprint,
            command.ExpectedTranscriptRevision,
            status,
            Now,
            1) with
        {
            CompletedAtUtc = status is LlmChatOperationStatus.Succeeded or LlmChatOperationStatus.Failed
                ? Now
                : null,
            ResultingTranscriptRevision = assistant ? TranscriptRevision + 2 : null,
            AssistantEntryId = assistant ? Guid.Parse("60000000-0000-0000-0000-000000000001") : null,
            FailureCode = failureCode
        };
        var message = assistant
            ? new LlmChatAssistantMessage(
                operation.AssistantEntryId!.Value,
                command.OperationId,
                "The design review result.",
                "reasoning-model",
                new LlmUsage(100, 40),
                Now)
            : null;
        return new LlmChatOperationDetails(operation, message, []);
    }

    private static Task<Result<LlmChatOperationDetails>> Success(LlmChatOperationDetails details)
        => Task.FromResult(Result<LlmChatOperationDetails>.Success(details));

    private static Task<Result<LlmChatOperationDetails>> Failure(string code)
        => Task.FromResult(Result<LlmChatOperationDetails>.Failure(
            Error.Failure("A sanitized LLM Chat failure occurred.", code)));
}
