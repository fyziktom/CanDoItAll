using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.Workspace;

public sealed class SharedProviderInvocationRecord : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string RequestId { get; set; } = string.Empty;

    public SharedProviderPublicationId PublicationId { get; set; }

    public Guid ProviderProfileId { get; set; }

    public string AuthenticatedSubject { get; set; } = string.Empty;

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

    public SharedProviderMetadataCompleteness PricingCompleteness { get; set; } =
        SharedProviderMetadataCompleteness.Unavailable;

    public DateTimeOffset DeleteAfterUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}
