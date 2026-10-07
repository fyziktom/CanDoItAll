using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Resources;

public sealed class ResourceRegistryOwner(ResourcesService resources, ProjectWriteSelectionQuery projects,
    SecretService secrets, IProjectPartyIntegrationBridge parties, ICanonicalRuntimeDatabase canonical) : IResourceRegistryOwner {
    private readonly Guid profile = canonical.Profile.Profile.Id;
    private readonly long generation = canonical.Generation;
    public bool IsCurrent => canonical.Profile.Profile.Id == profile && canonical.Generation == generation;
    public IReadOnlyList<ConnectorPluginManifest> ListManifests() => resources.ListConnectorManifests();
    public string BuildLocationPreview(ResourceEditorModel editor) => resources.BuildLocationPreview(editor);
    public Task<IReadOnlyList<ResourceSummary>> ListAsync(CancellationToken cancellationToken = default) => resources.ListAsync(cancellationToken);
    public Task<ResourceEditorModel> GetAsync(Guid id, CancellationToken cancellationToken = default) => resources.GetAsync(id, cancellationToken);
    public async Task<IReadOnlyList<ResourceProjectOption>> ListProjectsAsync(CancellationToken cancellationToken = default) =>
        (await projects.ListAsync(cancellationToken: cancellationToken)).Select(p => new ResourceProjectOption(p.Id, p.Name, p.Admission)).ToArray();
    public async Task<IReadOnlyList<ResourceReferenceOption>> ListSecretsAsync(CancellationToken cancellationToken = default) =>
        (await secrets.ListForPickerAsync(cancellationToken)).Select(s => new ResourceReferenceOption(s.Id, s.Name)).ToArray();
    public async Task<IReadOnlyList<ResourceReferenceOption>> ListPartiesAsync(Guid? projectId, IReadOnlyList<Guid> retainedIds, CancellationToken cancellationToken = default) {
        var options = projectId is { } id ? (await parties.ListPartyOptionsAsync(id, cancellationToken)).ToList() : [];
        foreach (var missing in retainedIds.Where(id => options.All(p => p.PartyId != id)).Distinct()) {
            if (await parties.GetPartyOptionAsync(missing, cancellationToken) is { } option) {
                options.Add(option);
            }
        }
        return options.OrderBy(p => p.DisplayName).Select(p => new ResourceReferenceOption(p.PartyId, p.DisplayName)).ToArray();
    }
    public async Task<Result<Guid>> SaveAsync(ResourceEditorModel command) {
        RequireCurrent();
        try {
            return await resources.SaveAsync(command);
        } catch (ProjectWriteAdmissionRejectedException) {
            throw new ResourceActionRefusedException("The selected project lifetime no longer admits this write. Reload references and select a current project explicitly.");
        }
    }
    public Task DeleteAsync(Guid id, ProjectWriteAdmission? admission) {
        RequireCurrent();
        return resources.DeleteAsync(id, admission);
    }
    private void RequireCurrent() {
        if (!IsCurrent) {
            throw new ResourceActionRefusedException("The database profile changed. Open a new Resources workspace.");
        }
    }
}
