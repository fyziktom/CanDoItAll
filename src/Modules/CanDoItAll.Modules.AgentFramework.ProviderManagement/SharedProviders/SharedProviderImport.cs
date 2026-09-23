using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public sealed class SharedProviderImport : IHasConcurrencyToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid SourceId { get; set; }

    public SharedProviderPublicationId RemotePublicationId { get; set; }

    public Guid ProviderProfileId { get; set; }

    public string RemoteDisplayName { get; set; } = string.Empty;

    public SharedProviderPublicRevision RemoteRevision { get; set; }

    public SharedProviderPurpose RemotePurpose { get; set; }

    public SharedProviderTransport RemoteTransport { get; set; }

    public SharedProviderRoutingModelId RemoteDefaultModelId { get; set; }

    public string RemoteCatalogSnapshotJson { get; set; } = string.Empty;

    public SharedProviderSelectionState SelectionState { get; set; }

    public SharedProviderAvailabilityState AvailabilityState { get; set; }

    public DateTimeOffset? LastSeenAtUtc { get; set; }

    public DateTimeOffset? LastSyncAtUtc { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }

    public Guid ConcurrencyToken { get; set; }
}
