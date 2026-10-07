using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Modules.Resources;

public sealed record ResourceReferenceOption(Guid Id, string Name);
public sealed record ResourceProjectOption(Guid Id, string Name, ProjectWriteAdmission Admission);

public interface IResourceRegistryOwner {
    bool IsCurrent { get; }
    IReadOnlyList<ConnectorPluginManifest> ListManifests();
    string BuildLocationPreview(ResourceEditorModel editor);
    Task<IReadOnlyList<ResourceSummary>> ListAsync(CancellationToken cancellationToken = default);
    Task<ResourceEditorModel> GetAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceProjectOption>> ListProjectsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceReferenceOption>> ListSecretsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ResourceReferenceOption>> ListPartiesAsync(Guid? projectId, IReadOnlyList<Guid> retainedIds, CancellationToken cancellationToken = default);
    Task<Result<Guid>> SaveAsync(ResourceEditorModel command);
    Task DeleteAsync(Guid id, ProjectWriteAdmission? admission);
}

public sealed class ResourceActionRefusedException(string message) : InvalidOperationException(message);
