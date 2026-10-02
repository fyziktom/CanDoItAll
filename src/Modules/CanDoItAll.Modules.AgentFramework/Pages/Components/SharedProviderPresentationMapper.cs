using CanDoItAll.AgentFramework.SharedProviders.UI;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Modules.AgentFramework.Pages.Components;

internal static class SharedProviderPresentationMapper {
    public static SharedProviderImportBaseline Baseline(SharedProviderImportedProfileSnapshot import) =>
        new(import.ImportId, import.ProviderProfileId, import.SourceId, import.RemotePublicationId,
            import.ImportConcurrencyToken, import.ProviderConcurrencyToken, new(import.LocalAlias, import.IsEnabled));

    public static SharedProviderProfilePresentation? Sharing(SharedProviderProfileSharingSnapshot? state) => state is null ? null :
        new(state.Ownership switch {
            SharedProviderProfileOwnership.Local => SharedProviderOwnership.Local,
            SharedProviderProfileOwnership.Imported => SharedProviderOwnership.Imported,
            SharedProviderProfileOwnership.RuntimeOnly => SharedProviderOwnership.RuntimeOnly,
            _ => throw new ArgumentOutOfRangeException(nameof(state))
        }, state.Publication is { } publication ? new(publication.PublicId, publication.IsPublished) : null,
            state.Eligibility is { } eligibility ? new(eligibility.IsEligible, eligibility.SanitizedReason,
                eligibility.Models.Select(model => new SharedProviderEligibleModelPresentation(model.UpstreamModelId, model.Capabilities)).ToArray()) : null,
            state.Import is { } import ? new(Baseline(import), import.SourceName, import.RemoteDisplayName, import.Purpose,
                import.Transport, import.DefaultModelId, import.SelectionState == SharedProviderSelectionState.Selected,
                Availability(import.AvailabilityState), import.Models) : null);

    private static SharedProviderImportAvailability Availability(SharedProviderAvailabilityState availability) => availability switch {
        SharedProviderAvailabilityState.Available => SharedProviderImportAvailability.Available,
        SharedProviderAvailabilityState.Unpublished => SharedProviderImportAvailability.Unpublished,
        SharedProviderAvailabilityState.Missing => SharedProviderImportAvailability.Missing,
        SharedProviderAvailabilityState.SourceOffline => SharedProviderImportAvailability.SourceOffline,
        SharedProviderAvailabilityState.AuthorizationFailed => SharedProviderImportAvailability.AuthorizationFailed,
        SharedProviderAvailabilityState.SourceIdentityMismatch => SharedProviderImportAvailability.SourceIdentityMismatch,
        SharedProviderAvailabilityState.IncompatibleContract => SharedProviderImportAvailability.IncompatibleContract,
        _ => throw new ArgumentOutOfRangeException(nameof(availability))
    };
}
