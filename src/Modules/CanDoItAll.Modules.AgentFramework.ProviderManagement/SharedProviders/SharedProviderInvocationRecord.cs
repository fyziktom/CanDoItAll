using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderInvocationRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RequestId { get; set; } = string.Empty;

    public SharedProviderPublicationId PublicationId { get; set; }

    public Guid ProviderProfileId { get; set; }

    public string AuthenticatedSubject { get; set; } = string.Empty;
    public SharedProviderCallerIdentity? CallerIdentity { get; set; }
    public bool FinalizationRecovered { get; set; }
    public long HistoryVersion { get; set; } = 1;
    public string ProviderNameSnapshot { get; set; } = string.Empty;
    public ProviderKind? ProviderKindSnapshot { get; set; }

    public AccessContextReference? AccessContextReference { get; set; }

    public string TraceId { get; set; } = string.Empty;

    public string CorrelationId { get; set; } = string.Empty;

    public SharedProviderRelayOperation Operation { get; set; }

    public SharedProviderRoutingModelId PublicModelId { get; set; }

    public string UpstreamModelId { get; set; } = string.Empty;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public long? DurationMilliseconds { get; set; }

    public SharedProviderInvocationOutcome Outcome { get; set; } =
        SharedProviderInvocationOutcome.InProgress;

    public SharedProviderFailureCategory? FailureCategory { get; set; }

    public long? InputTokenCount { get; set; }

    public long? OutputTokenCount { get; set; }

    public int? ImageCount { get; set; }

    public SharedProviderMetadataCompleteness UsageCompleteness { get; set; } =
        SharedProviderMetadataCompleteness.Unavailable;

    public decimal? Price { get; set; }

    public ProviderExecutionTariff? PricingSnapshot { get; set; }
    public ProviderExecutionPrice? PriceEvidence { get; set; }
    public long? CachedInputTokenCount { get; set; }
    public long? CacheWriteTokenCount { get; set; }
    public long? ReasoningTokenCount { get; set; }

    public SharedProviderMetadataCompleteness PricingCompleteness { get; set; } =
        SharedProviderMetadataCompleteness.Unavailable;

    public DateTimeOffset DeleteAfterUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}
