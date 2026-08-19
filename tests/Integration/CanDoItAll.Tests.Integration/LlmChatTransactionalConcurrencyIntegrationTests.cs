using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Runtime;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.ReadModels;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Repositories;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed class LlmChatTransactionalConcurrencyIntegrationTests
{
    [Fact]
    public async Task Concurrent_definition_update_loser_returns_stable_conflict()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatdefinitionupdatecas");
        var definitionId = await SeedDefinitionAsync(database);
        var barrier = new TwoPartyAsyncBarrier();
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var first = CreateDefinitionService(firstContext, new BarrierDefinitionRepository(
            new EfLlmChatDefinitionRepository(firstContext), barrier));
        var second = CreateDefinitionService(secondContext, new BarrierDefinitionRepository(
            new EfLlmChatDefinitionRepository(secondContext), barrier));

        var results = await Task.WhenAll(
            first.UpdateAsync(CreateUpdateCommand(definitionId, "first")),
            second.UpdateAsync(CreateUpdateCommand(definitionId, "second")));

        Assert.Single(results, result => result.IsSuccess);
        var conflict = Assert.Single(results, result => result.IsFailure);
        Assert.Equal(LlmChatErrorCodes.DefinitionConcurrencyConflict, Assert.Single(conflict.Errors).Code);
        await using var assertionContext = database.CreateDbContext();
        Assert.Equal(1, await assertionContext.Set<LlmChatDefinitionRow>().CountAsync());
        Assert.Equal(2, await assertionContext.Set<LlmChatDefinitionRevisionRow>().CountAsync());
        Assert.Equal(1, await assertionContext.Set<LlmChatDefinitionRow>()
            .Select(row => row.ConcurrencyToken)
            .SingleAsync());
    }

    [Fact]
    public async Task Concurrent_definition_status_loser_returns_stable_conflict()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatdefinitionstatuscas");
        var definitionId = await SeedDefinitionAsync(database);
        var barrier = new TwoPartyAsyncBarrier();
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var first = CreateDefinitionService(firstContext, new BarrierDefinitionRepository(
            new EfLlmChatDefinitionRepository(firstContext), barrier));
        var second = CreateDefinitionService(secondContext, new BarrierDefinitionRepository(
            new EfLlmChatDefinitionRepository(secondContext), barrier));

        var results = await Task.WhenAll(
            first.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
                definitionId,
                LlmChatDefinitionStatus.Suspended,
                0)),
            second.ChangeStatusAsync(new ChangeLlmChatDefinitionStatusCommand(
                definitionId,
                LlmChatDefinitionStatus.Archived,
                0)));

        Assert.Single(results, result => result.IsSuccess);
        var conflict = Assert.Single(results, result => result.IsFailure);
        Assert.Equal(LlmChatErrorCodes.DefinitionConcurrencyConflict, Assert.Single(conflict.Errors).Code);
        await using var assertionContext = database.CreateDbContext();
        var row = await assertionContext.Set<LlmChatDefinitionRow>().AsNoTracking().SingleAsync();
        Assert.Equal(1, row.ConcurrencyToken);
        Assert.Contains(row.Status, new[] { LlmChatDefinitionStatus.Suspended, LlmChatDefinitionStatus.Archived });
    }

    [Fact]
    public async Task Concurrent_conversation_rename_loser_returns_stable_conflict()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatconversationrenamecas");
        var definitionId = await SeedDefinitionAsync(database);
        var conversationId = await SeedConversationAsync(database, definitionId);
        var barrier = new TwoPartyAsyncBarrier();
        await using var firstContext = database.CreateDbContext();
        await using var secondContext = database.CreateDbContext();
        var first = CreateConversationService(firstContext, new BarrierConversationRepository(
            new EfLlmChatConversationRepository(firstContext), barrier));
        var second = CreateConversationService(secondContext, new BarrierConversationRepository(
            new EfLlmChatConversationRepository(secondContext), barrier));

        var results = await Task.WhenAll(
            first.RenameAsync(new RenameLlmChatConversationCommand(conversationId, "First", 0, 0)),
            second.RenameAsync(new RenameLlmChatConversationCommand(conversationId, "Second", 0, 0)));

        Assert.Single(results, result => result.IsSuccess);
        var conflict = Assert.Single(results, result => result.IsFailure);
        Assert.Equal(LlmChatErrorCodes.StorageConflict, Assert.Single(conflict.Errors).Code);
        await using var assertionContext = database.CreateDbContext();
        var row = await assertionContext.Set<LlmChatConversationRow>().AsNoTracking().SingleAsync();
        Assert.Equal(1, row.ConcurrencyToken);
        Assert.Contains(row.Title, new[] { "First", "Second" });
    }

    [Fact]
    public async Task Conversation_create_cannot_pin_concurrently_archived_definition()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatcreatearchiverace");
        var definitionId = await SeedDefinitionAsync(database);
        await using var createContext = database.CreateDbContext();
        var gate = new DefinitionReadGate();
        var service = CreateConversationService(
            createContext,
            new EfLlmChatConversationRepository(createContext),
            new GatedDefinitionRepository(new EfLlmChatDefinitionRepository(createContext), gate));
        var create = service.CreateAsync(new CreateLlmChatConversationCommand(
            definitionId,
            "Concurrent archive",
            LlmChatConversationOrigin.Api));
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var archiveContext = database.CreateDbContext())
        {
            var affected = await archiveContext.Set<LlmChatDefinitionRow>()
                .Where(row => row.Id == definitionId.Value && row.ConcurrencyToken == 0)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(row => row.Status, LlmChatDefinitionStatus.Archived)
                    .SetProperty(row => row.ConcurrencyToken, 1));
            Assert.Equal(1, affected);
        }

        gate.Release();
        var result = await create;

        Assert.True(result.IsFailure);
        Assert.Equal(LlmChatErrorCodes.DefinitionNotActive, Assert.Single(result.Errors).Code);
        await using var assertionContext = database.CreateDbContext();
        Assert.Equal(0, await assertionContext.Set<LlmChatConversationRow>().CountAsync());
    }

    [Fact]
    public async Task Conversation_create_pins_one_committed_current_revision()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatcreaterevisionrace");
        var definitionId = await SeedDefinitionAsync(database);
        await using var createContext = database.CreateDbContext();
        var gate = new DefinitionReadGate();
        var service = CreateConversationService(
            createContext,
            new EfLlmChatConversationRepository(createContext),
            new GatedDefinitionRepository(new EfLlmChatDefinitionRepository(createContext), gate));
        var create = service.CreateAsync(new CreateLlmChatConversationCommand(
            definitionId,
            "Concurrent revision",
            LlmChatConversationOrigin.Api));
        await gate.Entered.WaitAsync(TimeSpan.FromSeconds(10));
        await using (var updateContext = database.CreateDbContext())
        {
            var now = DateTimeOffset.UtcNow;
            updateContext.Add(CreateRevisionRow(definitionId, 2, now));
            var row = await updateContext.Set<LlmChatDefinitionRow>().SingleAsync();
            row.CurrentRevision = 2;
            row.ConcurrencyToken = 1;
            row.UpdatedAtUtc = now;
            await updateContext.SaveChangesAsync();
        }

        gate.Release();
        var result = await create;

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Value!.Conversation.DefinitionRevision.Value);
        Assert.Equal(2, result.Value.Transcript.TranscriptRevision);
        await using var assertionContext = database.CreateDbContext();
        var conversation = await assertionContext.Set<LlmChatConversationRow>().AsNoTracking().SingleAsync();
        Assert.Equal(2, conversation.DefinitionRevision);
    }

    [Fact]
    public async Task Reconcile_live_owner_rejects_without_mutating_persisted_evidence()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatreconcileliveowner");
        var seeded = await SeedRecoverableOperationAsync(database, liveLease: true);
        await using var dbContext = database.CreateDbContext();
        var harness = CreateReconciliationHarness(dbContext);

        var result = await harness.Service.ReconcileAsync(seeded.OperationId);

        Assert.True(result.IsFailure);
        Assert.Equal(LlmChatErrorCodes.ActiveTurnConflict, Assert.Single(result.Errors).Code);
        var operation = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == seeded.OperationId.Value);
        var transcript = await dbContext.Set<LlmChatTranscriptRow>()
            .AsNoTracking()
            .SingleAsync(row => row.ConversationId == seeded.ConversationId.Value);
        Assert.Equal(LlmChatOperationStatus.Running, operation.Status);
        Assert.Equal(seeded.OperationId.Value, operation.ExecutionOwnerId);
        Assert.Equal(seeded.OperationId.Value, transcript.ActiveTurnId);
        Assert.Equal(0, harness.DispatchSignal.SignalCount);
    }

    [Fact]
    public async Task Reconcile_persisted_failed_attempt_settles_operation_and_compensates_transcript()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatreconcilefailedattempt");
        var seeded = await SeedRecoverableOperationAsync(
            database,
            invocationOutcome: LlmChatInvocationOutcome.Failed);
        await using var dbContext = database.CreateDbContext();
        var harness = CreateReconciliationHarness(dbContext);

        var result = await harness.Service.ReconcileAsync(seeded.OperationId);

        Assert.True(result.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.Failed, result.Value!.Operation.Status);
        Assert.Equal(LlmChatErrorCodes.ProviderUnavailable, result.Value.Operation.FailureCode);
        var operation = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == seeded.OperationId.Value);
        var transcript = await dbContext.Set<LlmChatTranscriptRow>()
            .AsNoTracking()
            .SingleAsync(row => row.ConversationId == seeded.ConversationId.Value);
        Assert.Equal(LlmChatOperationStatus.Failed, operation.Status);
        Assert.Null(transcript.ActiveTurnId);
        Assert.Null(transcript.PendingUserEntryId);
        Assert.Equal(0, transcript.EntryCount);
        Assert.Empty(await dbContext.Set<LlmChatMessageRow>()
            .AsNoTracking()
            .Where(row => row.TurnId == seeded.OperationId.Value)
            .ToArrayAsync());
        Assert.Single(await dbContext.Set<LlmChatInvocationRecordRow>()
            .AsNoTracking()
            .Where(row => row.OperationId == seeded.OperationId.Value)
            .ToArrayAsync());
        Assert.Contains(
            await dbContext.Set<LlmChatOperationEventRow>()
                .AsNoTracking()
                .Where(row => row.OperationId == seeded.OperationId.Value)
                .ToArrayAsync(),
            row => row.Status == LlmChatOperationStatus.Failed);
        Assert.Equal(0, harness.DispatchSignal.SignalCount);
    }

    [Fact]
    public async Task Reconcile_ambiguous_post_dispatch_evidence_stays_recovery_required_without_redispatch()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync("llmchatreconcileambiguous");
        var seeded = await SeedRecoverableOperationAsync(database);
        await using var dbContext = database.CreateDbContext();
        var harness = CreateReconciliationHarness(dbContext);

        var result = await harness.Service.ReconcileAsync(seeded.OperationId);

        Assert.True(result.IsSuccess);
        Assert.Equal(LlmChatOperationStatus.RecoveryRequired, result.Value!.Operation.Status);
        var operation = await dbContext.Set<LlmChatOperationRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == seeded.OperationId.Value);
        var transcript = await dbContext.Set<LlmChatTranscriptRow>()
            .AsNoTracking()
            .SingleAsync(row => row.ConversationId == seeded.ConversationId.Value);
        Assert.Equal(LlmChatOperationStatus.RecoveryRequired, operation.Status);
        Assert.Equal(seeded.OperationId.Value, transcript.ActiveTurnId);
        Assert.Empty(await dbContext.Set<LlmChatInvocationRecordRow>()
            .AsNoTracking()
            .Where(row => row.OperationId == seeded.OperationId.Value)
            .ToArrayAsync());
        Assert.Equal(0, harness.DispatchSignal.SignalCount);
    }

    private static LlmChatDefinitionApplicationService CreateDefinitionService(
        AppDbContext dbContext,
        ILlmChatDefinitionRepository repository)
        => new(
            repository,
            new EfLlmChatDefinitionReadStore(dbContext),
            new EfLlmChatUnitOfWork(dbContext, UnfencedLlmChatCommitFence.Instance),
            FixedProviderResolver.Instance,
            TimeProvider.System);

    private static ReconciliationHarness CreateReconciliationHarness(AppDbContext dbContext)
    {
        var timeProvider = TimeProvider.System;
        var unitOfWork = new EfLlmChatUnitOfWork(dbContext, UnfencedLlmChatCommitFence.Instance);
        var operationRepository = new EfLlmChatOperationRepository(dbContext);
        var invocationRepository = new EfLlmChatInvocationRecordRepository(dbContext);
        var conversationEngine = new PostgreSqlReconciliationConversationEngine(dbContext);
        var operationScope = new LlmChatOperationScopeAccessor();
        var eventJournal = new LlmChatOperationEventJournal(
            operationRepository,
            new EfLlmChatOperationEventRepository(dbContext),
            unitOfWork,
            new LlmChatOperationEventSignal(timeProvider),
            operationScope,
            new LlmChatStreamingOptions(),
            timeProvider);
        var evidenceService = new LlmChatOperationEvidenceService(
            operationRepository,
            invocationRepository,
            unitOfWork,
            operationScope,
            timeProvider,
            eventJournal);
        var detailsReader = new LlmChatOperationDetailsReader(
            operationRepository,
            new EfLlmChatOperationReadStore(dbContext),
            conversationEngine);
        var stateMachine = new LlmChatOperationStateMachine(
            operationRepository,
            invocationRepository,
            unitOfWork,
            conversationEngine,
            evidenceService,
            detailsReader,
            timeProvider,
            NullLogger<LlmChatOperationStateMachine>.Instance);
        var dispatchSignal = new RecordingOperationDispatchSignal();
        var admissionService = new LlmChatOperationAdmissionService(
            new EfLlmChatDefinitionRepository(dbContext),
            new EfLlmChatConversationRepository(dbContext),
            operationRepository,
            new EfLlmChatTurnStateRepository(dbContext),
            dispatchSignal,
            unitOfWork,
            conversationEngine,
            evidenceService,
            timeProvider,
            eventJournal);
        var service = new LlmChatOperationApplicationService(
            admissionService,
            stateMachine,
            detailsReader,
            new LlmChatOperationCancellationRegistry(),
            dispatchSignal,
            NullLogger<LlmChatOperationApplicationService>.Instance);
        return new(service, dispatchSignal);
    }

    private static LlmChatConversationApplicationService CreateConversationService(
        AppDbContext dbContext,
        ILlmChatConversationRepository conversationRepository,
        ILlmChatDefinitionRepository? definitionRepository = null)
        => new(
            definitionRepository ?? new EfLlmChatDefinitionRepository(dbContext),
            conversationRepository,
            new EfLlmChatConversationReadStore(dbContext),
            ExistingTurnStateRepository.Instance,
            new EfLlmChatUnitOfWork(dbContext, UnfencedLlmChatCommitFence.Instance),
            new DeterministicConversationEngine(dbContext),
            TimeProvider.System);

    private static UpdateLlmChatDefinitionCommand CreateUpdateCommand(
        LlmChatDefinitionId definitionId,
        string suffix)
        => new(
            definitionId,
            $"Definition {suffix}",
            "Summary",
            string.Empty,
            "System",
            FixedProviderResolver.ProviderId,
            "model",
            new LlmModelSettings(0.2, "{}"),
            TimeSpan.FromMinutes(1),
            null,
            $"revision {suffix}",
            0);

    private static async Task<LlmChatDefinitionId> SeedDefinitionAsync(
        LlmChatsPostgreSqlTestDatabase database)
    {
        await using var dbContext = database.CreateDbContext();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.Add(new LlmChatDefinitionRow
        {
            Id = id,
            Name = "Definition",
            Summary = "Summary",
            AvatarImageUrl = string.Empty,
            Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        dbContext.Add(CreateRevisionRow(new LlmChatDefinitionId(id), 1, now));
        await dbContext.SaveChangesAsync();
        return new LlmChatDefinitionId(id);
    }

    private static async Task<LlmChatConversationId> SeedConversationAsync(
        LlmChatsPostgreSqlTestDatabase database,
        LlmChatDefinitionId definitionId)
    {
        await using var dbContext = database.CreateDbContext();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        dbContext.Add(new LlmChatTranscriptRow
        {
            ConversationId = id,
            ProviderId = FixedProviderResolver.ProviderId,
            ProviderName = "Provider",
            ProviderKind = ProviderKind.OpenAi,
            Model = "model",
            TranscriptRevision = 0,
            EntryCount = 0
        });
        dbContext.Add(new LlmChatConversationRow
        {
            Id = id,
            DefinitionId = definitionId.Value,
            DefinitionRevision = 1,
            Title = "Conversation",
            Status = LlmChatConversationStatus.Active,
            Origin = LlmChatConversationOrigin.Api,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
            ConcurrencyToken = 0
        });
        await dbContext.SaveChangesAsync();
        return new LlmChatConversationId(id);
    }

    private static async Task<RecoverableOperationSeed> SeedRecoverableOperationAsync(
        LlmChatsPostgreSqlTestDatabase database,
        LlmChatInvocationOutcome? invocationOutcome = null,
        bool liveLease = false)
    {
        var definitionId = await SeedDefinitionAsync(database);
        var conversationId = await SeedConversationAsync(database, definitionId);
        await using var dbContext = database.CreateDbContext();
        var operationId = LlmChatOperationId.New();
        var pendingUserEntryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var transcript = await dbContext.Set<LlmChatTranscriptRow>()
            .SingleAsync(row => row.ConversationId == conversationId.Value);
        transcript.ActiveTurnId = operationId.Value;
        transcript.PendingUserEntryId = pendingUserEntryId;
        transcript.TurnAdmittedAtUtc = now.AddSeconds(-3);
        transcript.TurnAdmittedRevision = 0;
        transcript.EntryCount = 1;
        dbContext.Add(new LlmChatMessageRow
        {
            EntryId = pendingUserEntryId,
            ConversationId = conversationId.Value,
            Sequence = 1,
            TurnId = operationId.Value,
            Role = LlmMessageRole.User,
            Text = "reconcile",
            CreatedAtUtc = now.AddSeconds(-3)
        });
        dbContext.Add(new LlmChatOperationRow
        {
            Id = operationId.Value,
            ConversationId = conversationId.Value,
            Kind = LlmChatOperationKind.SendTurn,
            RequestFingerprint = new string('a', 64),
            ExpectedTranscriptRevision = 0,
            Status = liveLease
                ? LlmChatOperationStatus.Running
                : LlmChatOperationStatus.RecoveryRequired,
            ExecutionOwnerId = liveLease ? operationId.Value : null,
            ExecutionEpoch = liveLease ? 1 : 0,
            ClaimedAtUtc = liveLease ? now.AddSeconds(-2) : null,
            HeartbeatAtUtc = liveLease ? now : null,
            LeaseExpiresAtUtc = liveLease ? now.AddMinutes(5) : null,
            DispatchPhase = invocationOutcome is null
                ? LlmChatDispatchPhase.ProviderDispatchStarted
                : LlmChatDispatchPhase.ProviderDispatchReturned,
            TurnAdmittedAtUtc = now.AddSeconds(-3),
            ProviderDispatchStartedAtUtc = now.AddSeconds(-2),
            ProviderDispatchReturnedAtUtc = invocationOutcome is null ? null : now.AddSeconds(-1),
            StartedAtUtc = now.AddSeconds(-4),
            FailureCode = liveLease ? string.Empty : LlmChatErrorCodes.OperationRecoveryRequired,
            ConcurrencyToken = 0
        });
        if (invocationOutcome is { } outcome)
        {
            dbContext.Add(new LlmChatInvocationRecordRow
            {
                OperationId = operationId.Value,
                Ordinal = 1,
                ProviderProfileId = FixedProviderResolver.ProviderId,
                ProviderKind = ProviderKind.OpenAi,
                ProviderName = "Provider",
                Model = "model",
                InputTokens = 3,
                OutputTokens = 0,
                CachedInputTokens = 0,
                Outcome = outcome,
                FailureCode = LlmChatErrorCodes.ProviderUnavailable,
                StartedAtUtc = now.AddSeconds(-2),
                CompletedAtUtc = now.AddSeconds(-1),
                CorrelationId = "reconciliation-proof"
            });
        }

        await dbContext.SaveChangesAsync();
        return new(operationId, conversationId);
    }

    private static LlmChatDefinitionRevisionRow CreateRevisionRow(
        LlmChatDefinitionId definitionId,
        int revision,
        DateTimeOffset createdAtUtc)
        => LlmChatPersistenceMapper.ToRow(new LlmChatDefinitionRevision(
            definitionId,
            new LlmChatDefinitionRevisionNumber(revision),
            $"Revision {revision}",
            "Summary",
            string.Empty,
            "System",
            FixedProviderResolver.ProviderId,
            ProviderKind.OpenAi,
            "Provider",
            "model",
            new LlmModelSettings(0.2, "{}")
            {
                ThinkingEffort = AgentReasoningEffortLevel.Medium
            },
            TimeSpan.FromMinutes(1),
            null,
            createdAtUtc,
            "test"));

    private sealed record ReconciliationHarness(
        LlmChatOperationApplicationService Service,
        RecordingOperationDispatchSignal DispatchSignal);

    private sealed record RecoverableOperationSeed(
        LlmChatOperationId OperationId,
        LlmChatConversationId ConversationId);
}

internal sealed class TwoPartyAsyncBarrier
{
    private readonly TaskCompletionSource reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrivals;

    public async Task SignalAndWaitAsync()
    {
        if (Interlocked.Increment(ref arrivals) == 2)
        {
            reached.TrySetResult();
        }

        await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }
}

internal sealed class DefinitionReadGate
{
    private readonly TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Entered => entered.Task;

    public async Task WaitAsync()
    {
        entered.TrySetResult();
        await released.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    public void Release()
        => released.TrySetResult();
}

internal sealed class BarrierDefinitionRepository(
    ILlmChatDefinitionRepository inner,
    TwoPartyAsyncBarrier barrier) : ILlmChatDefinitionRepository
{
    public async Task<LlmChatDefinition?> TryGetAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default)
    {
        var value = await inner.TryGetAsync(id, cancellationToken);
        await barrier.SignalAndWaitAsync();
        return value;
    }

    public Task<LlmChatDefinition?> TryGetForUpdateAsync(
        LlmChatDefinitionId id,
        CancellationToken cancellationToken = default)
        => inner.TryGetForUpdateAsync(id, cancellationToken);

    public Task<LlmChatDefinitionRevision?> TryGetRevisionAsync(LlmChatDefinitionId id, LlmChatDefinitionRevisionNumber revision, CancellationToken cancellationToken = default)
        => inner.TryGetRevisionAsync(id, revision, cancellationToken);

    public Task<IReadOnlyList<string>> ListTagsAsync(LlmChatDefinitionId id, CancellationToken cancellationToken = default)
        => inner.ListTagsAsync(id, cancellationToken);

    public Task ReplaceTagsAsync(LlmChatDefinitionId id, IReadOnlyList<string> tags, CancellationToken cancellationToken = default)
        => inner.ReplaceTagsAsync(id, tags, cancellationToken);

    public Task CreateAsync(LlmChatDefinition definition, LlmChatDefinitionRevision revision, CancellationToken cancellationToken = default)
        => inner.CreateAsync(definition, revision, cancellationToken);

    public Task ReplaceAsync(LlmChatDefinition definition, long expectedConcurrencyToken, LlmChatDefinitionRevision? appendedRevision, CancellationToken cancellationToken = default)
        => inner.ReplaceAsync(definition, expectedConcurrencyToken, appendedRevision, cancellationToken);
}

internal sealed class GatedDefinitionRepository(
    ILlmChatDefinitionRepository inner,
    DefinitionReadGate gate) : ILlmChatDefinitionRepository
{
    public async Task<LlmChatDefinition?> TryGetAsync(LlmChatDefinitionId id, CancellationToken cancellationToken = default)
    {
        var value = await inner.TryGetAsync(id, cancellationToken);
        await gate.WaitAsync();
        return value;
    }

    public async Task<LlmChatDefinition?> TryGetForUpdateAsync(LlmChatDefinitionId id, CancellationToken cancellationToken = default)
    {
        await gate.WaitAsync();
        return await inner.TryGetForUpdateAsync(id, cancellationToken);
    }

    public Task<LlmChatDefinitionRevision?> TryGetRevisionAsync(LlmChatDefinitionId id, LlmChatDefinitionRevisionNumber revision, CancellationToken cancellationToken = default)
        => inner.TryGetRevisionAsync(id, revision, cancellationToken);

    public Task<IReadOnlyList<string>> ListTagsAsync(LlmChatDefinitionId id, CancellationToken cancellationToken = default)
        => inner.ListTagsAsync(id, cancellationToken);

    public Task ReplaceTagsAsync(LlmChatDefinitionId id, IReadOnlyList<string> tags, CancellationToken cancellationToken = default)
        => inner.ReplaceTagsAsync(id, tags, cancellationToken);

    public Task CreateAsync(LlmChatDefinition definition, LlmChatDefinitionRevision revision, CancellationToken cancellationToken = default)
        => inner.CreateAsync(definition, revision, cancellationToken);

    public Task ReplaceAsync(LlmChatDefinition definition, long expectedConcurrencyToken, LlmChatDefinitionRevision? appendedRevision, CancellationToken cancellationToken = default)
        => inner.ReplaceAsync(definition, expectedConcurrencyToken, appendedRevision, cancellationToken);
}

internal sealed class BarrierConversationRepository(
    ILlmChatConversationRepository inner,
    TwoPartyAsyncBarrier barrier) : ILlmChatConversationRepository
{
    public async Task<LlmChatConversation?> TryGetAsync(LlmChatConversationId id, CancellationToken cancellationToken = default)
    {
        var value = await inner.TryGetAsync(id, cancellationToken);
        await barrier.SignalAndWaitAsync();
        return value;
    }

    public Task CreateAsync(LlmChatConversation conversation, CancellationToken cancellationToken = default)
        => inner.CreateAsync(conversation, cancellationToken);

    public Task ReplaceAsync(LlmChatConversation conversation, long expectedConcurrencyToken, CancellationToken cancellationToken = default)
        => inner.ReplaceAsync(conversation, expectedConcurrencyToken, cancellationToken);
}

internal sealed class FixedProviderResolver : ILlmChatProviderResolver
{
    public static readonly Guid ProviderId = new("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static FixedProviderResolver Instance { get; } = new();

    public Task<Result<LlmChatResolvedProvider>> ResolveAsync(Guid providerProfileId, string model, AgentReasoningEffortLevel? thinkingEffort, CancellationToken cancellationToken = default)
        => Task.FromResult(Result<LlmChatResolvedProvider>.Success(new(
            providerProfileId,
            "Provider",
            ProviderKind.OpenAi,
            model,
            new ProviderModelThinkingEffortCapability(
                model,
                AgentThinkingEffortSupportStatus.Supported,
                AgentThinkingEffortCapabilitySource.Defined,
                Enum.GetValues<AgentReasoningEffortLevel>()),
            AgentReasoningEffortLevel.Medium)));

    public Task<Result<IReadOnlyList<LlmChatProviderOption>>> ListOptionsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(Result<IReadOnlyList<LlmChatProviderOption>>.Success([]));
}

internal sealed class ExistingTurnStateRepository : ILlmChatTurnStateRepository
{
    public static ExistingTurnStateRepository Instance { get; } = new();

    public Task<LlmChatConversationTurnState> LockAsync(LlmChatConversationId conversationId, CancellationToken cancellationToken = default)
        => Task.FromResult(new LlmChatConversationTurnState(true, false, false));
}

internal sealed class RecordingOperationDispatchSignal : ILlmChatOperationDispatchSignal
{
    public bool HasAvailableExecutor => false;

    public LlmChatDispatchAvailability Availability => new(0, 0);

    public int SignalCount { get; private set; }

    public IDisposable RegisterExecutor()
        => throw new NotSupportedException();

    public IDisposable BeginProgress()
        => throw new NotSupportedException();

    public void Signal()
        => SignalCount++;

    public ValueTask WaitAsync(
        TimeSpan maximumDelay,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class PostgreSqlReconciliationConversationEngine(AppDbContext dbContext)
    : ILlmChatConversationEngine
{
    private readonly EfLlmChatConversationReadStore readStore = new(dbContext);

    public Task<LlmChatConversationTurnEvidence?> InspectTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => readStore.TryInspectTurnAsync(conversationId, operationId, cancellationToken);

    public async Task<LlmChatConversationEngineState> CompensateTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
    {
        var transcript = await dbContext.Set<LlmChatTranscriptRow>()
            .SingleAsync(
                row => row.ConversationId == conversationId.Value,
                cancellationToken);
        if (transcript.ActiveTurnId != operationId.Value)
        {
            throw new InvalidOperationException("The reconciliation target is not the persisted active turn.");
        }

        if (transcript.PendingUserEntryId is { } pendingUserEntryId)
        {
            var pendingMessage = await dbContext.Set<LlmChatMessageRow>()
                .SingleAsync(
                    row => row.EntryId == pendingUserEntryId,
                    cancellationToken);
            dbContext.Remove(pendingMessage);
            transcript.EntryCount = checked(transcript.EntryCount - 1);
        }

        transcript.ActiveTurnId = null;
        transcript.PendingUserEntryId = null;
        transcript.TurnAdmittedAtUtc = null;
        transcript.TurnAdmittedRevision = null;
        var conversation = await dbContext.Set<LlmChatConversationRow>()
            .AsNoTracking()
            .SingleAsync(row => row.Id == conversationId.Value, cancellationToken);
        return new(
            conversationId,
            transcript.TranscriptRevision,
            null,
            conversation.CreatedAtUtc,
            conversation.UpdatedAtUtc);
    }

    public Task<LlmChatConversationEngineState> CreateAsync(
        LlmChatConversationId conversationId,
        LlmChatDefinitionRevision definitionRevision,
        string title,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationEngineState?> TryGetAsync(
        LlmChatConversationId conversationId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatTranscriptPage?> TryGetTranscriptPageAsync(
        LlmChatConversationId conversationId,
        int take,
        LlmChatTranscriptCursor? cursor,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationEngineState> RenameAsync(
        LlmChatConversationId conversationId,
        string title,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmConversationTurnAdmission> AdmitTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        string userText,
        long expectedTranscriptRevision,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmConversationTurnAdmission> ResumeAdmittedTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        LlmChatDefinition definition,
        LlmChatDefinitionRevision definitionRevision,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<LlmStreamingUpdate> StreamTurnAsync(
        LlmConversationTurnAdmission admission,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationEngineTurnResult> CompleteTurnAsync(
        LlmConversationTurnAdmission admission,
        LlmInvocationResult invocationResult,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationEngineState> AbandonActiveTurnAsync(
        LlmChatConversationId conversationId,
        LlmChatOperationId operationId,
        CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}

internal sealed class DeterministicConversationEngine(AppDbContext dbContext) : ILlmChatConversationEngine
{
    public Task<LlmChatConversationEngineState> CreateAsync(LlmChatConversationId conversationId, LlmChatDefinitionRevision definitionRevision, string title, CancellationToken cancellationToken = default)
    {
        dbContext.Add(new LlmChatTranscriptRow
        {
            ConversationId = conversationId.Value,
            ProviderId = definitionRevision.ProviderProfileId,
            ProviderName = definitionRevision.ProviderName,
            ProviderKind = definitionRevision.ProviderKind,
            Model = definitionRevision.Model,
            TranscriptRevision = 0,
            EntryCount = 0
        });
        return Task.FromResult(State(conversationId, definitionRevision.Revision.Value));
    }

    public Task<LlmChatConversationEngineState> RenameAsync(LlmChatConversationId conversationId, string title, long expectedTranscriptRevision, CancellationToken cancellationToken = default)
        => Task.FromResult(State(conversationId, expectedTranscriptRevision));

    public Task<LlmChatConversationEngineState?> TryGetAsync(LlmChatConversationId conversationId, CancellationToken cancellationToken = default)
        => Task.FromResult<LlmChatConversationEngineState?>(State(conversationId, 0));

    public Task<LlmChatTranscriptPage?> TryGetTranscriptPageAsync(LlmChatConversationId conversationId, int take, LlmChatTranscriptCursor? cursor, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmConversationTurnAdmission> AdmitTurnAsync(LlmChatConversationId conversationId, LlmChatOperationId operationId, LlmChatDefinition definition, LlmChatDefinitionRevision definitionRevision, string userText, long expectedTranscriptRevision, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmConversationTurnAdmission> ResumeAdmittedTurnAsync(LlmChatConversationId conversationId, LlmChatOperationId operationId, LlmChatDefinition definition, LlmChatDefinitionRevision definitionRevision, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public IAsyncEnumerable<LlmStreamingUpdate> StreamTurnAsync(LlmConversationTurnAdmission admission, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationEngineTurnResult> CompleteTurnAsync(LlmConversationTurnAdmission admission, LlmInvocationResult invocationResult, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationEngineState> CompensateTurnAsync(LlmChatConversationId conversationId, LlmChatOperationId operationId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationTurnEvidence?> InspectTurnAsync(LlmChatConversationId conversationId, LlmChatOperationId operationId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    public Task<LlmChatConversationEngineState> AbandonActiveTurnAsync(LlmChatConversationId conversationId, LlmChatOperationId operationId, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();

    private static LlmChatConversationEngineState State(LlmChatConversationId conversationId, long revision)
    {
        var now = DateTimeOffset.UtcNow;
        return new LlmChatConversationEngineState(conversationId, revision, null, now, now);
    }
}
