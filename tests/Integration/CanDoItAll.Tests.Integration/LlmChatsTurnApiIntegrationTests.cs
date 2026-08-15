using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
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

    internal static Task<ApiTestHost> CreateHostAsync(StubLlmChatOperationApplicationService service)
        => ApiTestHost.CreateAsync(
            jwtEnabled: false,
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
        Assert.Equal(
            firstJson.RootElement.GetProperty("requestFingerprint").GetString(),
            retryJson.RootElement.GetProperty("requestFingerprint").GetString());
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

public sealed class LlmChatsRecoveryApiIntegrationTests
{
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
        => GetAsync(operationId, cancellationToken);

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
