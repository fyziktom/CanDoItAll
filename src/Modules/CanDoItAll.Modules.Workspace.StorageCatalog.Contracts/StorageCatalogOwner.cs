using System.Collections.Immutable;

namespace CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

public enum CatalogEffect { Save, Test, Delete }
public enum CatalogWrite { NotAttempted, Committed, Unknown }
public enum CatalogRouting { NotAttempted, Complete, PossiblyPartial }
public enum CatalogActivity { NotAttempted, Complete, Failed }
public enum CatalogDriver { NotAttempted, Completed, Unknown }
public enum CatalogDiagnostic { None, Invalid, Missing, Protected, Retired, ReadUnavailable, CredentialDenied, DriverMissing, DriverFailed, PersistenceFailed, RoutingFailed, ActivityFailed }

public sealed record CatalogCommand(Guid OperationId, Guid Origin, CatalogContext Context, CatalogEffect Effect, CatalogEdit Draft);
public sealed record CatalogOutcome {
    public Guid? CatalogId { get; init; }
    public CatalogWrite Write { get; init; }
    public CatalogRouting Routing { get; init; }
    public CatalogActivity Activity { get; init; }
    public CatalogDriver Driver { get; init; }
    public CatalogHealthFact? Health { get; init; }
    public CatalogDiagnostic Diagnostic { get; init; }
    public bool DiagnosticsUnavailable { get; init; }
    public bool IsUnknown => Write == CatalogWrite.Unknown || Driver == CatalogDriver.Unknown;
    public bool NeedsReview => IsUnknown || Routing == CatalogRouting.PossiblyPartial;
}

public interface IStorageCatalogOwner {
    CatalogContext Context { get; }
    bool IsCurrent { get; }
    CatalogChoices Choices { get; }
    Task<ImmutableArray<CatalogRow>> ReadCatalogAsync(CancellationToken cancellationToken);
    Task<ImmutableArray<CatalogSecret>> ReadSecretsAsync(CancellationToken cancellationToken);
    Task<ImmutableArray<CatalogRoute>> ReadRoutesAsync(CancellationToken cancellationToken);
    Task<CatalogEdit?> ReadEditorAsync(Guid id, CancellationToken cancellationToken);
    Task<CatalogOutcome> ExecuteAsync(CatalogCommand command, CancellationToken cancellationToken);
}
