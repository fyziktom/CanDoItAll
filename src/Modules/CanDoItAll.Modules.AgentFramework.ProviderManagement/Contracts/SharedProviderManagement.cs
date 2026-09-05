using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

public enum SharedProviderProfileOwnership
{
    Local,
    Imported,
    RuntimeOnly
}

public sealed record SharedProviderImportedProfileSnapshot(
    Guid ImportId,
    Guid SourceId,
    string SourceName,
    SharedProviderPublicationId RemotePublicationId,
    Guid ProviderProfileId,
    string LocalAlias,
    bool IsEnabled,
    string RemoteDisplayName,
    SharedProviderPurpose Purpose,
    SharedProviderTransport Transport,
    SharedProviderRoutingModelId DefaultModelId,
    SharedProviderSelectionState SelectionState,
    SharedProviderAvailabilityState AvailabilityState,
    IReadOnlyList<SharedProviderCatalogModel> Models,
    Guid ImportConcurrencyToken,
    Guid ProviderConcurrencyToken);

public sealed record SharedProviderProfileSharingSnapshot(
    Guid ProviderProfileId,
    SharedProviderProfileOwnership Ownership,
    SharedProviderPublicationWriteResult? Publication,
    SharedProviderPublicationEligibility? Eligibility,
    SharedProviderImportedProfileSnapshot? Import) {
    public SharedProviderChange? Change { get; init; }
}

public sealed record SharedProviderSourceManagementSnapshot(
    SharedProviderSourceSnapshot Source,
    IReadOnlyList<SharedProviderImportedProfileSnapshot> Imports);

public sealed record SharedProviderSourceEditorRequest(
    Guid? Id,
    Guid? ExpectedConcurrencyToken,
    string Name,
    Uri BaseUri,
    Guid ApiTokenSecretId,
    bool IsEnabled,
    bool AllowInsecurePrivateNetwork);

public sealed record SharedProviderImportedProfileUpdateRequest(
    Guid ImportId,
    Guid ProviderProfileId,
    string LocalAlias,
    bool IsEnabled,
    Guid ExpectedImportConcurrencyToken,
    Guid ExpectedProviderConcurrencyToken);

public sealed record SharedProviderImportedProfileRetireRequest(
    Guid ImportId,
    Guid ProviderProfileId,
    Guid ExpectedImportConcurrencyToken,
    Guid ExpectedProviderConcurrencyToken);

public interface ISharedProviderManagementService
{
    Task<SharedProviderProfileSharingSnapshot> GetProfileSharingAsync(
        Guid providerProfileId,
        CancellationToken cancellationToken = default);

    Task<SharedProviderProfileSharingSnapshot> SetPublicationAsync(
        Guid providerProfileId,
        SharedProviderPublicationAction action,
        Guid? expectedConcurrencyToken,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SharedProviderSourceManagementSnapshot>> ListSourcesAsync(
        CancellationToken cancellationToken = default);

    Task<SharedProviderSourceWriteResult> SaveSourceAsync(
        SharedProviderSourceEditorRequest request,
        CancellationToken cancellationToken = default);

    Task<SharedProviderSourceWriteResult> SetSourceEnabledAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        bool isEnabled,
        CancellationToken cancellationToken = default);

    Task<SharedProviderSourceDeleteResult> DeleteSourceAsync(
        Guid sourceId,
        Guid expectedConcurrencyToken,
        CancellationToken cancellationToken = default);

    Task<SharedProviderSourceOperationResult> TestSourceAsync(
        Guid sourceId,
        CancellationToken cancellationToken = default);

    Task<SharedProviderSourceOperationResult> SynchronizeSourceAsync(
        Guid sourceId,
        IReadOnlySet<SharedProviderPublicationId> selectedPublicationIds,
        CancellationToken cancellationToken = default);

    Task<SharedProviderProfileSharingSnapshot> UpdateImportedProfileAsync(
        SharedProviderImportedProfileUpdateRequest request,
        CancellationToken cancellationToken = default);

    Task<SharedProviderProfileSharingSnapshot> RetireImportedProfileAsync(
        SharedProviderImportedProfileRetireRequest request,
        CancellationToken cancellationToken = default);
}
