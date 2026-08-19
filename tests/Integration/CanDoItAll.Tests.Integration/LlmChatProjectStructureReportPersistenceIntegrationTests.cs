using CanDoItAll.AgentFramework.Llm.Abstractions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Conversations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Definitions;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.ReadModels;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Tests.Integration.LlmChats;

public sealed class LlmChatProjectStructureReportPersistenceIntegrationTests
{
    private static readonly DateTimeOffset UtcMidnight =
        new(2026, 8, 18, 0, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task PostgreSql_filters_project_attribution_and_aggregates_invocation_costs()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync(
            "llmchatprojectreport");
        var projectId = Guid.NewGuid();
        var otherProjectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var previousDayOperation = CreateOperation(
            conversationId,
            projectId,
            LlmChatOperationStatus.Succeeded,
            UtcMidnight.AddMinutes(-6),
            UtcMidnight.AddMinutes(-1));
        var knownOperation = CreateOperation(
            conversationId,
            projectId,
            LlmChatOperationStatus.Succeeded,
            UtcMidnight.AddMinutes(5),
            UtcMidnight.AddMinutes(10));
        var unknownOperation = CreateOperation(
            conversationId,
            projectId,
            LlmChatOperationStatus.Failed,
            UtcMidnight.AddMinutes(15),
            UtcMidnight.AddMinutes(20));
        var otherProjectOperation = CreateOperation(
            conversationId,
            otherProjectId,
            LlmChatOperationStatus.Succeeded,
            UtcMidnight.AddMinutes(25),
            UtcMidnight.AddMinutes(30));

        await using (var dbContext = database.CreateDbContext())
        {
            SeedConversationRoot(dbContext, definitionId, conversationId);
            dbContext.Set<LlmChatOperationRow>().AddRange(
                previousDayOperation,
                knownOperation,
                unknownOperation,
                otherProjectOperation);
            dbContext.Set<LlmChatInvocationRecordRow>().AddRange(
                CreateInvocation(
                    previousDayOperation,
                    ordinal: 1,
                    LlmChatInvocationPricingEvidenceStatus.ProviderReported,
                    providerCostUsd: 1.25m),
                CreateInvocation(
                    knownOperation,
                    ordinal: 1,
                    LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution,
                    calculatedCostUsd: 2.50m),
                CreateInvocation(
                    knownOperation,
                    ordinal: 2,
                    LlmChatInvocationPricingEvidenceStatus.ProviderReported,
                    providerCostUsd: 0.75m),
                CreateInvocation(
                    unknownOperation,
                    ordinal: 1,
                    LlmChatInvocationPricingEvidenceStatus.Unpriced),
                CreateInvocation(
                    otherProjectOperation,
                    ordinal: 1,
                    LlmChatInvocationPricingEvidenceStatus.ProviderReported,
                    providerCostUsd: 99m));
            await dbContext.SaveChangesAsync();
        }

        var store = new EfLlmChatProjectStructureReportStore(
            new TestDbContextFactory(database));
        var report = await store.QueryProjectStructureReportAsync(
            new LlmChatProjectStructureReportQuery(
                [projectId],
                UtcMidnight.AddHours(-1),
                UtcMidnight.AddHours(1),
                UtcMidnight.AddHours(-1),
                [LlmChatOperationStatus.Succeeded, LlmChatOperationStatus.Failed],
                pageSize: 10));

        Assert.Equal(3, report.TotalCount);
        Assert.Equal(4.50m, report.KnownCostUsd);
        Assert.Equal(1, report.UnknownCostRunCount);
        Assert.Equal(900_000L, report.TotalDurationMilliseconds);
        Assert.Equal(
            [unknownOperation.Id, knownOperation.Id, previousDayOperation.Id],
            report.Runs.Select(static run => run.OperationId.Value));
        var knownRun = Assert.Single(
            report.Runs,
            run => run.OperationId.Value == knownOperation.Id);
        Assert.Equal(3.25m, knownRun.KnownCostUsd);
        Assert.False(knownRun.HasUnknownCost);
        var unknownRun = Assert.Single(
            report.Runs,
            run => run.OperationId.Value == unknownOperation.Id);
        Assert.Equal(0m, unknownRun.KnownCostUsd);
        Assert.True(unknownRun.HasUnknownCost);
        Assert.Equal(
            [
                new LlmChatProjectStructureDailyCost(
                    DateOnly.FromDateTime(UtcMidnight.AddDays(-1).UtcDateTime),
                    1.25m),
                new LlmChatProjectStructureDailyCost(
                    DateOnly.FromDateTime(UtcMidnight.UtcDateTime),
                    3.25m)
            ],
            report.DailyCost);
    }

    [Fact]
    public async Task PostgreSql_uses_later_provider_activity_after_cancellation_request()
    {
        await using var database = await LlmChatsPostgreSqlTestDatabase.CreateAsync(
            "llmchatprojectreportactivity");
        var projectId = Guid.NewGuid();
        var definitionId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var startedAtUtc = UtcMidnight.AddMinutes(5);
        var cancellationRequestedAtUtc = startedAtUtc.AddMinutes(2);
        var providerReturnedAtUtc = startedAtUtc.AddMinutes(5);
        var scope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        var operation = new LlmChatOperationRow
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Kind = LlmChatOperationKind.SendTurn,
            RequestFingerprint = new string('b', 64),
            ExpectedTranscriptRevision = 0,
            Status = LlmChatOperationStatus.CancellationRequested,
            AttributionScopeKind = scope.Kind,
            AttributionScopeKey = scope.Key,
            CancellationRequestedAtUtc = cancellationRequestedAtUtc,
            CancellationGeneration = 1,
            ExecutionOwnerId = Guid.NewGuid(),
            ExecutionEpoch = 1,
            DispatchPhase = LlmChatDispatchPhase.ProviderDispatchReturned,
            TurnAdmittedAtUtc = startedAtUtc,
            ClaimedAtUtc = startedAtUtc.AddMinutes(1),
            HeartbeatAtUtc = startedAtUtc.AddMinutes(4),
            LeaseExpiresAtUtc = providerReturnedAtUtc.AddMinutes(1),
            ProviderDispatchStartedAtUtc = startedAtUtc.AddMinutes(1),
            ProviderDispatchReturnedAtUtc = providerReturnedAtUtc,
            StartedAtUtc = startedAtUtc,
            LastEventSequence = 1,
            ConcurrencyToken = 0
        };

        await using (var dbContext = database.CreateDbContext())
        {
            SeedConversationRoot(dbContext, definitionId, conversationId);
            dbContext.Set<LlmChatOperationRow>().Add(operation);
            await dbContext.SaveChangesAsync();
        }

        var store = new EfLlmChatProjectStructureReportStore(
            new TestDbContextFactory(database));
        var report = await store.QueryProjectStructureReportAsync(
            new LlmChatProjectStructureReportQuery(
                [projectId],
                providerReturnedAtUtc.AddSeconds(-1),
                providerReturnedAtUtc.AddSeconds(1),
                startedAtUtc,
                [LlmChatOperationStatus.CancellationRequested],
                pageSize: 10));

        var run = Assert.Single(report.Runs);
        Assert.Equal(providerReturnedAtUtc, run.ActivityAtUtc);
        Assert.Equal(300_000L, run.DurationMilliseconds);
        Assert.Equal(1, report.TotalCount);
        Assert.Equal(300_000L, report.TotalDurationMilliseconds);
    }

    private static void SeedConversationRoot(
        AppDbContext dbContext,
        Guid definitionId,
        Guid conversationId)
    {
        dbContext.Set<LlmChatDefinitionRow>().Add(new LlmChatDefinitionRow
        {
            Id = definitionId,
            Name = "Project report",
            Summary = "Simple Chat project reporting integration fixture.",
            AvatarImageUrl = "",
            Status = LlmChatDefinitionStatus.Active,
            CurrentRevision = 1,
            CreatedAtUtc = UtcMidnight.AddDays(-1),
            UpdatedAtUtc = UtcMidnight.AddDays(-1),
            ConcurrencyToken = 0
        });
        dbContext.Set<LlmChatDefinitionRevisionRow>().Add(
            LlmChatsPostgreSqlTestDatabase.CreateRevisionRow(
                definitionId,
                revision: 1,
                thinkingEffort: null,
                UtcMidnight.AddDays(-1)));
        dbContext.Set<LlmChatTranscriptRow>().Add(
            LlmChatsPostgreSqlTestDatabase.CreateTranscriptRow(
                conversationId,
                UtcMidnight.AddDays(-1)));
        dbContext.Set<LlmChatConversationRow>().Add(new LlmChatConversationRow
        {
            Id = conversationId,
            DefinitionId = definitionId,
            DefinitionRevision = 1,
            Title = "Project conversation",
            Status = LlmChatConversationStatus.Active,
            Origin = LlmChatConversationOrigin.Application,
            CreatedAtUtc = UtcMidnight.AddDays(-1),
            UpdatedAtUtc = UtcMidnight,
            ConcurrencyToken = 0
        });
    }

    private static LlmChatOperationRow CreateOperation(
        Guid conversationId,
        Guid projectId,
        LlmChatOperationStatus status,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        var scope = WorkspaceScopeDescriptor.Project(projectId.ToString("D"));
        return new LlmChatOperationRow
        {
            Id = Guid.NewGuid(),
            ConversationId = conversationId,
            Kind = LlmChatOperationKind.SendTurn,
            RequestFingerprint = new string('a', 64),
            ExpectedTranscriptRevision = 0,
            Status = status,
            AttributionScopeKind = scope.Kind,
            AttributionScopeKey = scope.Key,
            DispatchPhase = LlmChatDispatchPhase.ProviderDispatchReturned,
            TurnAdmittedAtUtc = startedAtUtc,
            ProviderDispatchStartedAtUtc = startedAtUtc.AddMinutes(1),
            ProviderDispatchReturnedAtUtc = completedAtUtc,
            TranscriptCompletedAtUtc = completedAtUtc,
            StartedAtUtc = startedAtUtc,
            CompletedAtUtc = completedAtUtc,
            ResultingTranscriptRevision = 1,
            LastEventSequence = 1,
            ConcurrencyToken = 0
        };
    }

    private static LlmChatInvocationRecordRow CreateInvocation(
        LlmChatOperationRow operation,
        int ordinal,
        LlmChatInvocationPricingEvidenceStatus pricingStatus,
        decimal? providerCostUsd = null,
        decimal? calculatedCostUsd = null)
        => new()
        {
            OperationId = operation.Id,
            Ordinal = ordinal,
            ProviderProfileId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"),
            ProviderKind = ProviderKind.OpenAi,
            ProviderName = "Provider",
            Model = "model",
            DeliveryMode = LlmStreamingDeliveryMode.CompletedFallback,
            FinishReason = "stop",
            InputTokens = 10,
            OutputTokens = 5,
            CachedInputTokens = 0,
            UsageStatus = LlmChatInvocationUsageEvidenceStatus.Observed,
            PricingStatus = pricingStatus,
            ProviderCostUsd = providerCostUsd,
            CalculatedCostUsd = calculatedCostUsd,
            PricingProfileHash = "",
            PricingVersion = "",
            Outcome = operation.Status == LlmChatOperationStatus.Failed
                ? LlmChatInvocationOutcome.Failed
                : LlmChatInvocationOutcome.Succeeded,
            FailureCode = operation.Status == LlmChatOperationStatus.Failed
                ? "provider-failed"
                : "",
            StartedAtUtc = operation.ProviderDispatchStartedAtUtc!.Value,
            CompletedAtUtc = operation.ProviderDispatchReturnedAtUtc!.Value,
            CorrelationId = $"project-report-{operation.Id:N}-{ordinal}"
        };

    private sealed class TestDbContextFactory(LlmChatsPostgreSqlTestDatabase database) :
        IDbContextFactory<AppDbContext>
    {
        public AppDbContext CreateDbContext() => database.CreateDbContext();

        public Task<AppDbContext> CreateDbContextAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(CreateDbContext());
        }
    }
}
