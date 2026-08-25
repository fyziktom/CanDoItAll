using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.SharedProviders.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

using ProviderProfileMapper = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfileMapper;

internal sealed class SharedProviderRelayUsageProjectionSource(
    IDbContextFactory<AppDbContext> dbContextFactory,
    ProviderProfileMapper providerMapper,
    ILogger<SharedProviderRelayUsageProjectionSource> logger) :
    IProviderUsageProjectionSource
{
    public const string SourceIdentity = "shared-provider-relay";

    public string SourceName => SourceIdentity;

    public ProviderUsageWorkloadKind WorkloadKind => ProviderUsageWorkloadKind.SharedProviderRelay;

    public async ValueTask<ProviderUsageSourceResult> ReadAsync(
        CancellationToken cancellationToken = default)
    {
        try
        {
            await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
            var rows = await (
                from invocation in dbContext.Set<SharedProviderInvocationRecord>().AsNoTracking()
                join profile in dbContext.Set<CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile>().AsNoTracking()
                    on invocation.ProviderProfileId equals profile.Id
                orderby invocation.StartedAtUtc, invocation.Id
                select new UsageRow(invocation, profile))
                .ToListAsync(cancellationToken);
            var providers = rows
                .Select(row => row.Profile)
                .GroupBy(profile => profile.Id)
                .ToDictionary(
                    group => group.Key,
                    group => providerMapper.Map(group.First()));
            var contributions = rows
                .Select(row => Map(row, providers[row.Profile.Id]))
                .ToArray();
            var updatedAtUtc = contributions.Length == 0
                ? DateTimeOffset.UnixEpoch
                : contributions.Max(contribution => contribution.OccurredAtUtc);
            return new ProviderUsageSourceResult(
                SourceIdentity,
                ProviderUsageWorkloadKind.SharedProviderRelay,
                ProviderUsageSourceState.Complete,
                Array.AsReadOnly(contributions),
                updatedAtUtc);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            logger.LogError("Failed to read the shared-provider relay usage projection.");
            return ProviderUsageSourceResult.Failed(
                SourceIdentity,
                ProviderUsageWorkloadKind.SharedProviderRelay,
                "shared_provider_usage_read_failed",
                "Shared-provider relay usage could not be read from the workspace store.",
                DateTimeOffset.UtcNow);
        }
    }

    private static ProviderUsageContribution Map(
        UsageRow row,
        CanDoItAll.AgentFramework.Models.ProviderProfile provider)
    {
        var usage = MapUsage(row.Invocation);
        return new ProviderUsageContribution(
            row.Invocation.Id.ToString("D"),
            ProviderUsageWorkloadKind.SharedProviderRelay,
            ProviderUsageConsumerKind.SharedProviderRelay,
            row.Invocation.PublicationId.ToString(),
            row.Profile.Name,
            row.Invocation.ProviderProfileId,
            row.Profile.Name,
            provider.Kind,
            row.Invocation.UpstreamModelId,
            row.Invocation.RequestId,
            MapOutcome(row.Invocation.Outcome),
            usage.Completeness,
            MapPricing(row.Invocation),
            usage.Tokens,
            row.Invocation.Price,
            row.Invocation.CompletedAtUtc ?? row.Invocation.StartedAtUtc)
        {
            ImageCount = usage.ImageCount
        };
    }

    private static ProjectedUsage MapUsage(SharedProviderInvocationRecord invocation)
    {
        if (invocation.InputTokenCount is < 0 ||
            invocation.OutputTokenCount is < 0 ||
            invocation.ImageCount is <= 0 or > SharedProviderRelaySupportDescriptor.MaximumAllowedImageCount)
        {
            throw InvalidUsage(invocation);
        }

        return invocation.Operation switch
        {
            SharedProviderRelayOperation.ChatCompletions or SharedProviderRelayOperation.Responses =>
                MapTokenUsage(invocation),
            SharedProviderRelayOperation.ImageGenerations => MapImageUsage(invocation),
            _ => throw InvalidUsage(invocation)
        };
    }

    private static ProjectedUsage MapTokenUsage(SharedProviderInvocationRecord invocation)
    {
        if (invocation.ImageCount.HasValue)
        {
            throw InvalidUsage(invocation);
        }

        var hasInputTokens = invocation.InputTokenCount.HasValue;
        var hasOutputTokens = invocation.OutputTokenCount.HasValue;
        if (invocation.UsageCompleteness == SharedProviderMetadataCompleteness.Unavailable &&
            !hasInputTokens &&
            !hasOutputTokens)
        {
            return ProjectedUsage.Unavailable;
        }

        if (invocation.UsageCompleteness == SharedProviderMetadataCompleteness.Partial &&
            hasInputTokens != hasOutputTokens)
        {
            return ProjectedUsage.Unavailable;
        }

        if (invocation.UsageCompleteness != SharedProviderMetadataCompleteness.Complete ||
            !hasInputTokens ||
            !hasOutputTokens)
        {
            throw InvalidUsage(invocation);
        }

        if (!TryMapCompleteTokenUsage(invocation, out var tokens))
        {
            return ProjectedUsage.Unavailable;
        }

        return new ProjectedUsage(
            ProviderUsageCompleteness.Observed,
            tokens,
            ImageCount: null);
    }

    private static ProjectedUsage MapImageUsage(SharedProviderInvocationRecord invocation)
    {
        if (invocation.UsageCompleteness == SharedProviderMetadataCompleteness.Unavailable &&
            !invocation.InputTokenCount.HasValue &&
            !invocation.OutputTokenCount.HasValue &&
            !invocation.ImageCount.HasValue)
        {
            return ProjectedUsage.Unavailable;
        }

        if (invocation.UsageCompleteness == SharedProviderMetadataCompleteness.Complete &&
            !invocation.InputTokenCount.HasValue &&
            !invocation.OutputTokenCount.HasValue &&
            invocation.ImageCount is { } imageCount)
        {
            return new ProjectedUsage(
                ProviderUsageCompleteness.Observed,
                ProviderUsageTokenCounts.Empty,
                imageCount);
        }

        throw InvalidUsage(invocation);
    }

    private static InvalidOperationException InvalidUsage(SharedProviderInvocationRecord invocation)
        => new(
            $"Shared-provider invocation '{invocation.Id:D}' has usage incompatible with operation '{invocation.Operation}'.");

    private static bool TryMapCompleteTokenUsage(
        SharedProviderInvocationRecord invocation,
        out ProviderUsageTokenCounts tokens)
    {
        if (invocation.UsageCompleteness != SharedProviderMetadataCompleteness.Complete ||
            invocation.InputTokenCount is not { } inputTokens ||
            invocation.OutputTokenCount is not { } outputTokens ||
            inputTokens > int.MaxValue ||
            outputTokens > int.MaxValue ||
            inputTokens > int.MaxValue - outputTokens)
        {
            tokens = ProviderUsageTokenCounts.Empty;
            return false;
        }

        tokens = new ProviderUsageTokenCounts(
            (int)inputTokens,
            CachedInputTokens: 0,
            CacheWriteTokens: 0,
            (int)outputTokens,
            ReasoningTokens: 0,
            (int)(inputTokens + outputTokens));
        return true;
    }

    private static ProviderUsageExecutionOutcome MapOutcome(
        SharedProviderInvocationOutcome outcome)
        => outcome switch
        {
            SharedProviderInvocationOutcome.Succeeded => ProviderUsageExecutionOutcome.Succeeded,
            SharedProviderInvocationOutcome.Failed => ProviderUsageExecutionOutcome.Failed,
            SharedProviderInvocationOutcome.Cancelled => ProviderUsageExecutionOutcome.Cancelled,
            SharedProviderInvocationOutcome.InProgress => ProviderUsageExecutionOutcome.Unknown,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };

    private static ProviderUsagePricingCompleteness MapPricing(
        SharedProviderInvocationRecord invocation)
        => invocation.Price.HasValue &&
            invocation.PricingCompleteness != SharedProviderMetadataCompleteness.Unavailable
            ? ProviderUsagePricingCompleteness.CalculatedAtExecution
            : ProviderUsagePricingCompleteness.Unpriced;

    private sealed record UsageRow(
        SharedProviderInvocationRecord Invocation,
        CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderProfile Profile);

    private sealed record ProjectedUsage(
        ProviderUsageCompleteness Completeness,
        ProviderUsageTokenCounts Tokens,
        int? ImageCount)
    {
        public static ProjectedUsage Unavailable { get; } = new(
            ProviderUsageCompleteness.UsageUnavailable,
            ProviderUsageTokenCounts.Empty,
            ImageCount: null);
    }
}
