using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Resources;

public sealed record ResourceReferenceOption(Guid Id, string Name);
public sealed record ResourceProjectOption(Guid Id, string Name, ProjectWriteAdmission Admission);

public interface IResourceRegistryOwner {
    bool IsCurrent { get; }
    IReadOnlyList<ConnectorPluginManifest> ListManifests();
    string BuildLocationPreview(ResourceEditorModel editor);
    Task<IReadOnlyList<ResourceSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<ResourceMetadataContext> ReadContextAsync(CancellationToken cancellationToken = default);
    Task<ResourceMetadataReadResult> ReadAsync(Guid id, CancellationToken cancellationToken = default);
    ResourceEditorModel CreateEditor(ResourceMetadataSnapshot snapshot);
    Task<IReadOnlyList<ResourceProjectOption>> ListProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceReferenceOption>> ListSecretsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceReferenceOption>> ListPartiesAsync(Guid? projectId, IReadOnlyList<Guid> retainedIds, CancellationToken cancellationToken = default);
    Task<ResourceMetadataSaveOutcome> SaveAsync(ReplaceResourceMetadataCommand command, CancellationToken cancellationToken = default);
    Task<ResourceMetadataDeleteOutcome> DeleteAsync(DeleteResourceMetadataCommand command, CancellationToken cancellationToken = default);
}

public sealed class ResourceActionRefusedException(string message) : InvalidOperationException(message);
