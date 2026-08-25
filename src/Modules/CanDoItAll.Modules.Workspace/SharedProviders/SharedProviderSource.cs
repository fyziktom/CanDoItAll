using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.Workspace;

public sealed class SharedProviderSource : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string BaseUri { get; set; } = string.Empty;

    public Guid ApiTokenSecretId { get; set; }

    public bool IsEnabled { get; set; } = true;

    public bool AllowInsecurePrivateNetwork { get; set; }

    public SharedProviderSourceStatus Status { get; set; } =
        SharedProviderSourceStatus.NeverSynchronized;

    public SharedProviderSourceInstanceId? RemoteInstanceId { get; set; }

    public SharedProviderCatalogEntityTag? LastCatalogETag { get; set; }

    public DateTimeOffset? LastSyncAtUtc { get; set; }

    public int? LastStatusCode { get; set; }

    public string LastStatusMessage { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}
