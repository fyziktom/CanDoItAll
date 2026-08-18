using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Usage;

public sealed class SimpleChatProviderUsageProjectionSource(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ILogger<SimpleChatProviderUsageProjectionSource> logger) : IProviderUsageProjectionSource
{
    public const string SourceIdentity = "simple-chats-ef";

    public string SourceName => SourceIdentity;

    public ProviderUsageWorkloadKind WorkloadKind => ProviderUsageWorkloadKind.SimpleChat;

    public async ValueTask<ProviderUsageSourceResult> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory
                .CreateDbContextAsync(cancellationToken)
                .ConfigureAwait(false);
            var rows = await (
                    from invocation in dbContext.Set<LlmChatInvocationRecordRow>().AsNoTracking()
                    join operation in dbContext.Set<LlmChatOperationRow>().AsNoTracking()
                        on invocation.OperationId equals operation.Id
                    join conversation in dbContext.Set<LlmChatConversationRow>().AsNoTracking()
                        on operation.ConversationId equals conversation.Id
                    join revision in dbContext.Set<LlmChatDefinitionRevisionRow>().AsNoTracking()
                        on new { conversation.DefinitionId, Revision = conversation.DefinitionRevision }
                        equals new { revision.DefinitionId, revision.Revision }
                    select new SimpleChatUsageRow(
                        invocation.OperationId,
                        invocation.Ordinal,
                        conversation.DefinitionId,
                        revision.Name,
                        invocation.ProviderProfileId,
                        invocation.ProviderName,
                        invocation.ProviderKind,
                        invocation.Model,
                        operation.Status,
                        invocation.UsageStatus,
                        invocation.PricingStatus,
                        invocation.InputTokens,
                        invocation.CachedInputTokens,
                        invocation.OutputTokens,
                        invocation.ProviderCostUsd,
                        invocation.CalculatedCostUsd,
                        invocation.CompletedAtUtc))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var updatedAtUtc = rows.Count == 0
                ? DateTimeOffset.UnixEpoch
                : rows.Max(row => row.CompletedAtUtc);
            return new(
                SourceIdentity,
                ProviderUsageWorkloadKind.SimpleChat,
                ProviderUsageSourceState.Complete,
                rows.Select(Map).ToList(),
                updatedAtUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Failed to read the Simple Chat provider usage projection.");
            return ProviderUsageSourceResult.Failed(
                SourceIdentity,
                ProviderUsageWorkloadKind.SimpleChat,
                "simple_chat_usage_read_failed",
                "Simple Chat usage could not be read from the database.",
                DateTimeOffset.UtcNow);
        }
    }

    private static ProviderUsageContribution Map(SimpleChatUsageRow row)
    {
        return new(
            $"{row.OperationId:N}:{row.Ordinal}",
            ProviderUsageWorkloadKind.SimpleChat,
            ProviderUsageConsumerKind.SimpleChatDefinition,
            row.DefinitionId.ToString("D"),
            row.DefinitionName,
            row.ProviderProfileId,
            row.ProviderName,
            row.ProviderKind,
            row.Model,
            row.OperationId.ToString("D"),
            MapOutcome(row.OperationStatus),
            MapUsage(row),
            MapPricing(row.PricingStatus),
            new(
                row.InputTokens,
                row.CachedInputTokens,
                CacheWriteTokens: 0,
                row.OutputTokens,
                ReasoningTokens: 0,
                checked(row.InputTokens + row.OutputTokens)),
            row.ProviderCostUsd ?? row.CalculatedCostUsd,
            row.CompletedAtUtc);
    }

    private static ProviderUsageExecutionOutcome MapOutcome(LlmChatOperationStatus status)
    {
        return status switch
        {
            LlmChatOperationStatus.Succeeded => ProviderUsageExecutionOutcome.Succeeded,
            LlmChatOperationStatus.Failed => ProviderUsageExecutionOutcome.Failed,
            LlmChatOperationStatus.Cancelled => ProviderUsageExecutionOutcome.Cancelled,
            _ => ProviderUsageExecutionOutcome.Unknown
        };
    }

    private static ProviderUsageCompleteness MapUsage(SimpleChatUsageRow row)
    {
        return row.UsageStatus switch
        {
            LlmChatInvocationUsageEvidenceStatus.Observed => ProviderUsageCompleteness.Observed,
            LlmChatInvocationUsageEvidenceStatus.MissingAfterProviderActivity =>
                ProviderUsageCompleteness.MissingAfterProviderActivity,
            LlmChatInvocationUsageEvidenceStatus.UsageUnavailable => ProviderUsageCompleteness.UsageUnavailable,
            LlmChatInvocationUsageEvidenceStatus.LegacyKnownTokens
                when row.InputTokens > 0 || row.OutputTokens > 0 || row.CachedInputTokens > 0 =>
                ProviderUsageCompleteness.LegacyKnownTokens,
            LlmChatInvocationUsageEvidenceStatus.LegacyKnownTokens => ProviderUsageCompleteness.UsageUnavailable,
            _ => throw new ArgumentOutOfRangeException(
                nameof(row.UsageStatus),
                row.UsageStatus,
                "Unknown Simple Chat usage status.")
        };
    }

    private static ProviderUsagePricingCompleteness MapPricing(
        LlmChatInvocationPricingEvidenceStatus status)
    {
        return status switch
        {
            LlmChatInvocationPricingEvidenceStatus.ProviderReported =>
                ProviderUsagePricingCompleteness.ProviderReported,
            LlmChatInvocationPricingEvidenceStatus.CalculatedAtExecution =>
                ProviderUsagePricingCompleteness.CalculatedAtExecution,
            LlmChatInvocationPricingEvidenceStatus.Unpriced => ProviderUsagePricingCompleteness.Unpriced,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown Simple Chat pricing status.")
        };
    }

    private sealed record SimpleChatUsageRow(
        Guid OperationId,
        int Ordinal,
        Guid DefinitionId,
        string DefinitionName,
        Guid ProviderProfileId,
        string ProviderName,
        ProviderKind ProviderKind,
        string Model,
        LlmChatOperationStatus OperationStatus,
        LlmChatInvocationUsageEvidenceStatus UsageStatus,
        LlmChatInvocationPricingEvidenceStatus PricingStatus,
        int InputTokens,
        int CachedInputTokens,
        int OutputTokens,
        decimal? ProviderCostUsd,
        decimal? CalculatedCostUsd,
        DateTimeOffset CompletedAtUtc);
}
