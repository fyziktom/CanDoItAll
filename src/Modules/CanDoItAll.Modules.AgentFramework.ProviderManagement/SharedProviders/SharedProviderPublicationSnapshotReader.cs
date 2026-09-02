using System.Text;
using System.Text.Json;
using CanDoItAll.SharedProviders.Abstractions;

namespace CanDoItAll.Modules.AgentFramework.ProviderManagement;

internal static class SharedProviderPublicationSnapshotReader {
    public static bool TryRead(SharedProviderImport import, out SharedProviderCatalogPublication publication) {
        publication = null!;
        try {
            if (string.IsNullOrEmpty(import.RemoteCatalogSnapshotJson) ||
                Encoding.UTF8.GetByteCount(import.RemoteCatalogSnapshotJson) > SharedProviderRemotePublicationState.MaximumSnapshotBytes) {
                return false;
            }

            var snapshot = JsonSerializer.Deserialize<SharedProviderRemotePublicationSnapshot>(
                import.RemoteCatalogSnapshotJson, SharedProviderProtocolJson.Options);
            if (snapshot is null || snapshot.SchemaVersion != SharedProviderProtocolVersion.Current || snapshot.Publication is null) {
                return false;
            }

            var candidate = snapshot.Publication;
            var revision = SharedProviderCanonicalRevision.ComputePublication(candidate);
            if (candidate.Revision != revision || candidate.PublicationId != import.RemotePublicationId ||
                candidate.Revision != import.RemoteRevision ||
                !string.Equals(candidate.DisplayName, import.RemoteDisplayName, StringComparison.Ordinal) ||
                candidate.Purpose != import.RemotePurpose || candidate.Transport != import.RemoteTransport ||
                candidate.DefaultModelId != import.RemoteDefaultModelId) {
                return false;
            }

            publication = candidate;
            return true;
        } catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException or NotSupportedException) {
            return false;
        }
    }
}
