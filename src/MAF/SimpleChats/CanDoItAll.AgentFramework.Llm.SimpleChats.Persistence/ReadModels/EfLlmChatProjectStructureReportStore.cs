using CanDoItAll.AgentFramework.Llm.SimpleChats.Application;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Common;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.Entities;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Persistence.ReadModels;

public sealed class EfLlmChatProjectStructureReportStore(
    IDbContextFactory<AppDbContext> dbContextFactory) : ILlmChatProjectStructureReportStore
{
    public async Task<LlmChatProjectStructureReport> QueryProjectStructureReportAsync(
        LlmChatProjectStructureReportQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        await using var dbContext = await dbContextFactory
            .CreateDbContextAsync(cancellationToken)
            .ConfigureAwait(false);
        var projectScopeKeys = query.ProjectIds
            .Select(static projectId => WorkspaceScopeDescriptor.Project(projectId.ToString("D")).Key)
            .ToArray();
        var reportOperations =
            from operation in dbContext.Set<LlmChatOperationRow>().AsNoTracking()
            join conversation in dbContext.Set<LlmChatConversationRow>().AsNoTracking()
                on operation.ConversationId equals conversation.Id
            join revision in dbContext.Set<LlmChatDefinitionRevisionRow>().AsNoTracking()
                on new { conversation.DefinitionId, Revision = conversation.DefinitionRevision }
                equals new { revision.DefinitionId, revision.Revision }
            where operation.AttributionScopeKind == WorkspaceScopeKind.Project &&
                  projectScopeKeys.Contains(operation.AttributionScopeKey)
            select new
            {
                operation.Id,
                operation.ConversationId,
                conversation.DefinitionId,
                conversation.DefinitionRevision,
                operation.Status,
                ConversationTitle = conversation.Title,
                DefinitionName = revision.Name,
                revision.ProviderName,
                revision.Model,
                operation.StartedAtUtc,
                operation.ProviderDispatchStartedAtUtc,
                ActivityAtUtc = EF.Functions.Greatest(
                    operation.CompletedAtUtc ?? operation.StartedAtUtc,
                    operation.CancellationRequestedAtUtc ?? operation.StartedAtUtc,
                    operation.HeartbeatAtUtc ?? operation.StartedAtUtc,
                    operation.ProviderDispatchReturnedAtUtc ?? operation.StartedAtUtc,
                    operation.ProviderDispatchStartedAtUtc ?? operation.StartedAtUtc,
                    operation.TranscriptCompletedAtUtc ?? operation.StartedAtUtc,
                    operation.TurnAdmittedAtUtc ?? operation.StartedAtUtc,
                    operation.ClaimedAtUtc ?? operation.StartedAtUtc,
                    operation.StartedAtUtc)
            };

        if (query.ActivityFromUtc is { } activityFromUtc)
        {
            reportOperations = reportOperations.Where(operation =>
                operation.ActivityAtUtc >= activityFromUtc);
        }

        reportOperations = reportOperations.Where(operation =>
            operation.ActivityAtUtc <= query.ActivityToUtc);
        if (query.Statuses.Count > 0)
        {
            var statuses = query.Statuses.ToArray();
            reportOperations = reportOperations.Where(operation => statuses.Contains(operation.Status));
        }

        var offset = checked(query.PageIndex * query.PageSize);
        var pageOperations = reportOperations
            .OrderByDescending(operation => operation.ActivityAtUtc)
            .ThenByDescending(operation => operation.Id)
            .Skip(offset)
            .Take(query.PageSize);
        var pageOperationIds = pageOperations.Select(operation => operation.Id);
        var pageUsageByOperation = dbContext.Set<LlmChatInvocationRecordRow>()
            .AsNoTracking()
            .Where(invocation => pageOperationIds.Contains(invocation.OperationId))
            .GroupBy(invocation => invocation.OperationId)
            .Select(group => new
            {
                OperationId = group.Key,
                InvocationCount = group.Count(),
                UnpricedInvocationCount = group.Count(invocation =>
                    invocation.PricingStatus == LlmChatInvocationPricingEvidenceStatus.Unpriced),
                KnownCostUsd = group.Sum(invocation =>
                    invocation.ProviderCostUsd ?? invocation.CalculatedCostUsd ?? 0m)
            });
        var pageReportRows =
            from operation in pageOperations
            join usage in pageUsageByOperation on operation.Id equals usage.OperationId into usageMatches
            from usage in usageMatches.DefaultIfEmpty()
            select new
            {
                operation.Id,
                operation.ConversationId,
                operation.DefinitionId,
                operation.DefinitionRevision,
                operation.Status,
                operation.ConversationTitle,
                operation.DefinitionName,
                operation.ProviderName,
                operation.Model,
                operation.StartedAtUtc,
                operation.ProviderDispatchStartedAtUtc,
                operation.ActivityAtUtc,
                InvocationCount = usage == null ? null : (int?)usage.InvocationCount,
                UnpricedInvocationCount = usage == null
                    ? null
                    : (int?)usage.UnpricedInvocationCount,
                KnownCostUsd = usage == null ? null : (decimal?)usage.KnownCostUsd
            };
        var pageRows = await pageReportRows
            .OrderByDescending(row => row.ActivityAtUtc)
            .ThenByDescending(row => row.Id)
            .ToArrayAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalCount = 0;
        var knownCostUsd = 0m;
        var unknownCostRunCount = 0;
        var totalDurationMilliseconds = 0L;
        LlmChatProjectStructureDailyCost[] dailyCost = [];
        if (query.IncludeAggregate)
        {
            var aggregateOperationIds = reportOperations.Select(operation => operation.Id);
            var aggregateUsageByOperation = dbContext.Set<LlmChatInvocationRecordRow>()
                .AsNoTracking()
                .Where(invocation => aggregateOperationIds.Contains(invocation.OperationId))
                .GroupBy(invocation => invocation.OperationId)
                .Select(group => new
                {
                    OperationId = group.Key,
                    InvocationCount = group.Count(),
                    UnpricedInvocationCount = group.Count(invocation =>
                        invocation.PricingStatus == LlmChatInvocationPricingEvidenceStatus.Unpriced),
                    KnownCostUsd = group.Sum(invocation =>
                        invocation.ProviderCostUsd ?? invocation.CalculatedCostUsd ?? 0m)
                });
            var aggregateRows =
                from operation in reportOperations
                join usage in aggregateUsageByOperation on operation.Id equals usage.OperationId into usageMatches
                from usage in usageMatches.DefaultIfEmpty()
                select new
                {
                    operation.StartedAtUtc,
                    operation.ProviderDispatchStartedAtUtc,
                    operation.ActivityAtUtc,
                    InvocationCount = usage == null ? null : (int?)usage.InvocationCount,
                    UnpricedInvocationCount = usage == null
                        ? null
                        : (int?)usage.UnpricedInvocationCount,
                    KnownCostUsd = usage == null ? null : (decimal?)usage.KnownCostUsd
                };
            var totals = await aggregateRows
                .GroupBy(_ => 1)
                .Select(group => new
                {
                    TotalCount = group.Count(),
                    KnownCostUsd = group.Sum(row => row.KnownCostUsd ?? 0m),
                    UnknownCostRunCount = group.Count(row =>
                        (row.UnpricedInvocationCount ?? 0) > 0 ||
                        !row.InvocationCount.HasValue && row.ProviderDispatchStartedAtUtc.HasValue),
                    DurationMilliseconds = group.Sum(row =>
                        row.ActivityAtUtc > row.StartedAtUtc
                            ? (row.ActivityAtUtc - row.StartedAtUtc).TotalMilliseconds
                            : 0d)
                })
                .SingleOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
            totalCount = totals?.TotalCount ?? 0;
            knownCostUsd = NormalizeKnownCost(totals?.KnownCostUsd ?? 0m);
            unknownCostRunCount = totals?.UnknownCostRunCount ?? 0;
            totalDurationMilliseconds = NormalizeDurationMilliseconds(
                totals?.DurationMilliseconds ?? 0d);

            var dailyCostRows = await aggregateRows
                .Where(row =>
                    row.ActivityAtUtc >= query.ChartFromUtc &&
                    row.ActivityAtUtc <= query.ActivityToUtc)
                .GroupBy(row => row.ActivityAtUtc.Date)
                .Select(group => new
                {
                    Date = group.Key,
                    KnownCostUsd = group.Sum(row => row.KnownCostUsd ?? 0m)
                })
                .OrderBy(row => row.Date)
                .ToArrayAsync(cancellationToken)
                .ConfigureAwait(false);
            dailyCost = dailyCostRows
                .Select(static row => new LlmChatProjectStructureDailyCost(
                    DateOnly.FromDateTime(row.Date),
                    NormalizeKnownCost(row.KnownCostUsd)))
                .ToArray();
        }

        return new LlmChatProjectStructureReport(
            pageRows
                .Select(static row => new LlmChatProjectStructureReportRun(
                    new LlmChatOperationId(row.Id),
                    new LlmChatConversationId(row.ConversationId),
                    new LlmChatDefinitionId(row.DefinitionId),
                    row.DefinitionRevision,
                    row.Status,
                    row.ConversationTitle,
                    row.DefinitionName,
                    row.ProviderName,
                    row.Model,
                    row.ActivityAtUtc,
                    NormalizeDurationMilliseconds(
                        row.ActivityAtUtc > row.StartedAtUtc
                            ? (row.ActivityAtUtc - row.StartedAtUtc).TotalMilliseconds
                            : 0d),
                    NormalizeKnownCost(row.KnownCostUsd ?? 0m),
                    (row.UnpricedInvocationCount ?? 0) > 0 ||
                    !row.InvocationCount.HasValue && row.ProviderDispatchStartedAtUtc.HasValue))
                .ToArray(),
            query.PageIndex,
            query.PageSize,
            totalCount,
            knownCostUsd,
            unknownCostRunCount,
            totalDurationMilliseconds,
            dailyCost);
    }

    private static decimal NormalizeKnownCost(decimal value)
        => decimal.Round(value, 6, MidpointRounding.AwayFromZero);

    private static long NormalizeDurationMilliseconds(double value)
    {
        if (value <= 0d || double.IsNaN(value))
        {
            return 0L;
        }

        return value >= long.MaxValue
            ? long.MaxValue
            : (long)value;
    }
}
