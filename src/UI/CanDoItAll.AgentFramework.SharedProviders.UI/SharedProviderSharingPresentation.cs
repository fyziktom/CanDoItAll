using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.AgentFramework.SharedProviders.UI;

public enum SharedProviderOwnership { Local, Imported, RuntimeOnly }
public enum SharedProviderImportAvailability { Available, Unpublished, Missing, SourceOffline, AuthorizationFailed, SourceIdentityMismatch, IncompatibleContract }
public enum SharedProviderConfirmationKind { Unpublish, RetireImport }

public sealed record SharedProviderSharingOrigin(Guid ViewId, long Activation, Guid SnapshotId,
    Guid? ProviderId, Guid? PublicationToken, SharedProviderImportBaseline? Import);

public sealed record SharedProviderPublicationPresentation(SharedProviderPublicationId PublicId, bool IsPublished);
public sealed record SharedProviderEligibleModelPresentation(string UpstreamModelId, IReadOnlyList<SharedProviderCapability> Capabilities);
public sealed record SharedProviderEligibilityPresentation(bool IsEligible, string SanitizedReason,
    IReadOnlyList<SharedProviderEligibleModelPresentation> Models);
public sealed record SharedProviderImportPresentation(SharedProviderImportBaseline Baseline, string SourceName,
    string RemoteDisplayName, SharedProviderPurpose Purpose, SharedProviderTransport Transport,
    SharedProviderRoutingModelId DefaultModelId, bool IsSelected, SharedProviderImportAvailability Availability,
    IReadOnlyList<SharedProviderCatalogModel> Models);
public sealed record SharedProviderProfilePresentation(SharedProviderOwnership Ownership,
    SharedProviderPublicationPresentation? Publication, SharedProviderEligibilityPresentation? Eligibility,
    SharedProviderImportPresentation? Import);

public sealed record SharedProviderConfirmation(Guid Id, SharedProviderSharingOrigin Origin, SharedProviderConfirmationKind Kind) {
    public string Title => Kind == SharedProviderConfirmationKind.Unpublish ? "Unpublish this provider?" : "Retire this imported provider?";
    public string Message => Kind == SharedProviderConfirmationKind.Unpublish
        ? "New remote requests stop. The permanent public identity and deletion protection remain."
        : "The profile remains for audit and can be reactivated through a later catalog import.";
    public string ActionText => Kind == SharedProviderConfirmationKind.Unpublish ? "Unpublish" : "Retire import";
}

public sealed record SharedProviderSharingPresentation(SharedProviderSharingOrigin Origin,
    SharedProviderProfilePresentation? Profile, SharedProviderImportDraft? Draft,
    bool Loading, bool WritesBlocked, string? Warning, SharedProviderConfirmation? Confirmation);

public interface ISharedProviderSharingView {
    SharedProviderSharingPresentation Presentation { get; }
    Task RetryAsync(SharedProviderSharingOrigin origin);
    Task PublishAsync(SharedProviderSharingOrigin origin);
    Task SaveImportedAsync(SharedProviderSharingOrigin origin, SharedProviderImportSubmission submission);
    void OpenConfirmation(SharedProviderSharingOrigin origin, SharedProviderConfirmationKind kind);
    Task ConfirmAsync(SharedProviderConfirmation confirmation);
    void CloseConfirmation(SharedProviderConfirmation confirmation);
}
