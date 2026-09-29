using CanDoItAll.Configuration.UI;
using CanDoItAll.Resources.UI;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.Resources;

public sealed class ResourceRegistryController(IResourceRegistryOwner owner) : IResourceRegistryWorkspace, IDisposable {
    private readonly List<ResourceMutationReceipt> receipts = [];
    private readonly CancellationTokenSource lifetime = new();
    private long readVersion;
    private long editorVersion;
    private long partyVersion;
    private bool disposed;
    private bool initialized;
    private Guid? routeResource;
    private Guid? routeProject;
    private ValidationMessageStore? validation;
    private ResourceViewAccess catalogAccess = ResourceViewAccess.Loading;
    private string? catalogError;
    private string? editorError;
    private string? actionError;

    public event Action? Changed;
    public event Action<ResourceMutationReceipt>? Completed;
    public ResourceRegistryDraft Draft { get; private set; } = new(new());
    public IReadOnlyList<ResourceSummary> Resources { get; private set; } = [];
    public IReadOnlyList<ResourceProjectOption> Projects { get; private set; } = [];
    public IReadOnlyList<ResourceReferenceOption> Parties { get; private set; } = [];
    public IReadOnlyList<ConfigurationSecretOption> Secrets { get; private set; } = [];
    public IReadOnlyList<ConnectorPluginManifest> Manifests { get; private set; } = [];
    public IReadOnlyList<ResourceMutationReceipt> Receipts => receipts;
    public ResourceViewAccess EditorAccess { get; private set; } = ResourceViewAccess.Loading;
    public ResourceViewAccess Access => EditorAccess == ResourceViewAccess.Ready ? catalogAccess : EditorAccess;
    public bool CanMutate => !disposed && owner.IsCurrent && EditorAccess == ResourceViewAccess.Ready;
    public string? Error { get => editorError ?? catalogError ?? actionError; private set => actionError = value; }
    public IReadOnlyList<string> ReferenceErrors { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public bool IsBusy => Blocked(Draft);
    public bool IsGoverned => Draft.Editor.ConnectorPluginKey == ResourceConnectorPluginKeys.StorageObject;
    public string Search { get; set; } = string.Empty;
    public Guid? ProjectFilter { get; set; }
    public string? ConnectorFilter { get; set; }
    public ResourceValidationStatus? ValidationFilter { get; set; }
    public ConnectorPluginManifest? SelectedManifest => Manifests.FirstOrDefault(m => string.Equals(m.PluginKey, Draft.Editor.ConnectorPluginKey, StringComparison.OrdinalIgnoreCase));
    public string? SelectedProjectName => Projects.FirstOrDefault(p => p.Admission == Draft.Editor.ExpectedProjectAdmission)?.Name;
    public string PreviewLocation => SelectedManifest is null ? Draft.Editor.LocationOrIdentifier : owner.BuildLocationPreview(Draft.Editor);
    public string EditorTitle => Draft.Editor.Id.HasValue ? Draft.Editor.Name : "New resource";
    public IReadOnlyList<ResourceSummary> FilteredResources => Resources.Where(r =>
        (string.IsNullOrWhiteSpace(Search) || new[] { r.Name, r.ProjectName, r.LocationOrIdentifier, r.ConnectorDisplayName }.Any(value => value.Contains(Search, StringComparison.OrdinalIgnoreCase))) &&
        (ProjectFilter is null || r.ProjectId == ProjectFilter && Projects.Any(p => p.Id == r.ProjectId && p.Admission.LifetimeId == r.ProjectLifetimeId)) &&
        (string.IsNullOrWhiteSpace(ConnectorFilter) || r.ConnectorPluginKey == ConnectorFilter) &&
        (ValidationFilter is null || r.ValidationStatus == ValidationFilter)).OrderBy(r => r.ProjectName).ThenBy(r => r.Name).ToArray();

    public async Task LoadRouteAsync(Guid? resourceId, Guid? projectId) {
        if (initialized && routeResource == resourceId && routeProject == projectId) {
            return;
        }
        initialized = true;
        routeResource = resourceId;
        routeProject = projectId;
        ReplaceDraft(new() { Id = resourceId, ProjectId = projectId });
        EditorAccess = ResourceViewAccess.Loading;
        editorError = null;
        var draft = Draft;
        var version = ++editorVersion;
        await RefreshAsync();
        if (!Current(draft) || version != editorVersion) {
            return;
        }
        if (!ResolveRoute(resourceId, projectId)) {
            EditorAccess = ResourceViewAccess.Failed;
            editorError = "The requested editor has not been acquired. Refresh references and retry the exact selection, or explicitly start a new resource.";
            Notify();
            return;
        }
        if (resourceId is { } id) {
            await LoadEditorAsync(id, version);
        } else {
            ReplaceDraft(NewEditor(projectId));
            EditorAccess = ResourceViewAccess.Ready;
            await LoadPartiesAsync();
        }
        Notify();
    }

    public async Task RefreshAsync() {
        if (disposed) {
            return;
        }
        var version = ++readVersion;
        IsLoading = true;
        Notify();
        var resources = ReadAsync(() => owner.ListAsync(lifetime.Token));
        var projects = ReadAsync(() => owner.ListProjectsAsync(lifetime.Token));
        var secrets = ReadAsync(() => owner.ListSecretsAsync(lifetime.Token));
        await Task.WhenAll(resources, projects, secrets);
        if (disposed || version != readVersion) {
            return;
        }
        IsLoading = false;
        if (!owner.IsCurrent) {
            catalogAccess = ResourceViewAccess.Failed;
            catalogError = "The database profile changed. Open a new Resources workspace.";
            Notify();
            return;
        }
        Manifests = owner.ListManifests();
        var warnings = new List<string>();
        if (projects.Result.Success) {
            Projects = projects.Result.Value!;
        } else {
            warnings.Add("Project references could not be refreshed. Retained choices may be stale.");
        }
        if (secrets.Result.Success) {
            Secrets = secrets.Result.Value!.Select(s => new ConfigurationSecretOption(s.Id, s.Name)).ToArray();
        } else {
            warnings.Add("Secret references could not be refreshed. No secret was selected automatically.");
        }
        ReferenceErrors = warnings;
        if (resources.Result.Success) {
            Resources = resources.Result.Value!;
            catalogAccess = ResourceViewAccess.Ready;
            catalogError = null;
            if (Draft.Editor.Id is { } id && !Resources.Any(r => r.Id == id)) {
                catalogAccess = ResourceViewAccess.Failed;
                catalogError = "The selected resource is missing. Its draft and identity are retained.";
            }
        } else {
            catalogAccess = ResourceViewAccess.Failed;
            catalogError = "Resources could not be refreshed. Displayed rows are stale; your draft is retained.";
        }
        if (routeProject is not null && (!projects.Result.Success || !Projects.Any(p => p.Id == routeProject))) {
            catalogAccess = ResourceViewAccess.Failed;
            catalogError = "The requested project is unavailable. No current project was substituted.";
        }
        if (routeResource is not null && Draft.Editor.Id == routeResource) {
            ResolveRoute(routeResource, routeProject);
        }
        Notify();
    }

    public async Task SelectAsync(Guid id) {
        if (disposed || !owner.IsCurrent || Draft.Editor.Id == id && EditorAccess == ResourceViewAccess.Ready) {
            return;
        }
        if (routeResource == id && !ResolveRoute(id, routeProject)) {
            Notify();
            return;
        }
        ReplaceDraft(new() { Id = id });
        await LoadEditorAsync(id, ++editorVersion);
    }

    public async Task RefreshAfterPromotionAsync() {
        await RefreshAsync();
        if (catalogAccess == ResourceViewAccess.Failed || ReferenceErrors.Count != 0) {
            throw new InvalidOperationException("The resource was promoted, but registry references could not be refreshed.");
        }
    }

    public async Task NewAsync() {
        if (disposed || !owner.IsCurrent) {
            return;
        }
        editorVersion++;
        ReplaceDraft(NewEditor(routeProject));
        EditorAccess = routeProject is null || Projects.Any(p => p.Id == routeProject) ? ResourceViewAccess.Ready : ResourceViewAccess.Failed;
        editorError = EditorAccess == ResourceViewAccess.Ready ? null : "The requested project is unavailable.";
        Notify();
        await LoadPartiesAsync();
    }

    public async Task ChangeProjectAsync(Guid? projectId) {
        if (!CanMutate) {
            return;
        }
        var admission = Projects.FirstOrDefault(p => p.Id == projectId)?.Admission;
        if (Draft.Editor.ProjectId == projectId && Draft.Editor.ExpectedProjectAdmission == admission) {
            return;
        }
        Draft.ChangeTarget();
        Draft.Editor.ProjectId = projectId;
        Draft.Editor.ExpectedProjectAdmission = admission;
        editorVersion++;
        Notify();
        await LoadPartiesAsync();
    }

    public Task ChangeConnectorAsync(string key) {
        if (!CanMutate) {
            return Task.CompletedTask;
        }
        if (Draft.Editor.ConnectorPluginKey == key && SelectedManifest?.ConfigurationSchema.Version == Draft.Editor.ConfigSchemaVersion) {
            return Task.CompletedTask;
        }
        var manifest = Manifests.FirstOrDefault(m => m.PluginKey == key && m.PluginKey != ResourceConnectorPluginKeys.StorageObject);
        if (manifest is null) {
            Error = "The selected connector is unavailable.";
            Notify();
            return Task.CompletedTask;
        }
        var editor = Draft.Editor.Capture();
        editor.ConnectorPluginKey = manifest.PluginKey;
        editor.ConfigSchemaVersion = manifest.ConfigurationSchema.Version;
        editor.Configuration.KeepOnly(manifest.ConfigurationSchema.Fields.Select(f => f.Key));
        editor.ConfigJson = "{}";
        ReplaceDraft(editor);
        editorVersion++;
        Notify();
        return Task.CompletedTask;
    }

    private async Task LoadEditorAsync(Guid id, long version) {
        var draft = Draft;
        EditorAccess = ResourceViewAccess.Loading;
        editorError = null;
        Notify();
        try {
            var editor = await owner.GetAsync(id, lifetime.Token);
            if (!Current(draft) || version != editorVersion) {
                return;
            }
            if (editor.Id != id) {
                EditorAccess = ResourceViewAccess.Failed;
                editorError = "The selected resource no longer exists. This is not a new-resource draft.";
            } else {
                ReplaceDraft(editor);
                EditorAccess = ResourceViewAccess.Ready;
                editorError = null;
                await LoadPartiesAsync();
            }
        } catch (Exception) {
            if (Current(draft) && version == editorVersion) {
                EditorAccess = ResourceViewAccess.Failed;
                editorError = "The selected resource could not be read. Retry the exact selection.";
            }
        }
        Notify();
    }

    private async Task LoadPartiesAsync() {
        var draft = Draft;
        var origin = draft.Origin;
        var version = ++partyVersion;
        var editor = draft.Editor;
        var ids = new[] { editor.OwnerPartyId, editor.MaintainerPartyId }.OfType<Guid>().Distinct().ToArray();
        var read = await ReadAsync(() => owner.ListPartiesAsync(editor.ProjectId, ids, lifetime.Token));
        if (!Current(draft, origin) || version != partyVersion) {
            return;
        }
        if (read.Success) {
            Parties = read.Value!;
        } else {
            ReferenceErrors = ReferenceErrors.Append("Owner and maintainer references could not be read. Existing IDs are retained.").Distinct().ToArray();
        }
        Notify();
    }

    public Task SaveAsync() => MutateAsync(ResourceMutationKind.Save);
    public Task DeleteAsync() => MutateAsync(ResourceMutationKind.Delete);

    private async Task MutateAsync(ResourceMutationKind kind) {
        if (!CanMutate || Blocked(Draft)) {
            return;
        }
        var draft = Draft;
        var origin = draft.Origin;
        var command = draft.Editor.Capture();
        if (kind == ResourceMutationKind.Delete && command.Id is null) {
            return;
        }
        if (kind == ResourceMutationKind.Save && !Validate(command)) {
            return;
        }
        if (receipts.Count >= 32) {
            var completed = receipts.FirstOrDefault(r => !r.BlocksDispatch && !r.IsReviewing);
            if (completed is null) {
                Error = "Resolve existing pending actions before submitting another resource.";
                Notify();
                return;
            }
            receipts.Remove(completed);
        }
        var versions = draft.CaptureVersions();
        var configurationVersions = draft.Configuration.CaptureVersions();
        var mutationVersion = draft.AdmitMutation();
        var receipt = new ResourceMutationReceipt(origin, kind, command);
        receipts.Add(receipt);
        Error = null;
        Notify();
        try {
            if (kind == ResourceMutationKind.Save) {
                var result = await owner.SaveAsync(command.Capture());
                if (!result.IsSuccess) {
                    receipt.State = ResourceEffectState.Refused;
                    receipt.Message = string.Join(" ", result.Errors.Select(e => e.Message));
                    return;
                }
                receipt.ResourceId = result.Value;
            } else {
                await owner.DeleteAsync(command.Id!.Value, command.ExpectedProjectAdmission);
            }
            receipt.State = ResourceEffectState.Committed;
            receipt.Message = kind == ResourceMutationKind.Save ? "Resource saved." : "Resource deleted.";
            var followUp = Current(draft, origin);
            RetainCommit();
            if (!followUp) {
                return;
            }
            if (kind == ResourceMutationKind.Save) {
                var stored = await owner.GetAsync(receipt.ResourceId!.Value, lifetime.Token);
                if (stored.Id != receipt.ResourceId) {
                    throw new InvalidOperationException("The committed resource is missing during read-back.");
                }
                if (Current(draft, origin) && draft.MutationVersion == mutationVersion) {
                    draft.Reconcile(command, stored, versions, configurationVersions);
                }
            }
            if (kind == ResourceMutationKind.Save && (!Current(draft, origin) || draft.MutationVersion != mutationVersion)) {
                return;
            }
            await RefreshAsync();
            if (Access != ResourceViewAccess.Ready || ReferenceErrors.Count != 0) {
                receipt.State = ResourceEffectState.CommittedWarning;
                receipt.Message = "The resource mutation committed; follow-up reads are incomplete. Refresh reads only.";
            }
        } catch (ResourceCommittedMutationException exception) {
            receipt.ResourceId = exception.ResourceId;
            receipt.State = ResourceEffectState.CommittedWarning;
            receipt.Message = exception.Message;
            RetainCommit();
        } catch (ResourceActionRefusedException exception) {
            receipt.State = ResourceEffectState.Refused;
            receipt.Message = exception.Message;
        } catch (Exception) {
            var committed = receipt.State is ResourceEffectState.Committed or ResourceEffectState.CommittedWarning;
            receipt.State = committed ? ResourceEffectState.CommittedWarning : ResourceEffectState.Unknown;
            receipt.Message = committed
                ? "The resource mutation committed; a follow-up read failed. The confirmed identity is retained."
                : "No final owner acknowledgement was received. Review exact stored identity before another dispatch; no action was replayed.";
        } finally {
            try {
                Completed?.Invoke(receipt);
            } catch (Exception) {
                if (receipt.State == ResourceEffectState.Committed) {
                    receipt.State = ResourceEffectState.CommittedWarning;
                }
                receipt.Message += " Outcome notification failed; this receipt retains the owner result.";
            }
            Notify();
        }

        void RetainCommit() {
            if (!Current(draft, origin)) {
                return;
            }
            if (kind == ResourceMutationKind.Save) {
                draft.Editor.Id = receipt.ResourceId;
            } else {
                Resources = Resources.Where(r => r.Id != receipt.ResourceId).ToArray();
                ReplaceDraft(NewEditor(routeProject));
                Parties = [];
            }
        }
    }

    public Task ReviewAsync(ResourceMutationReceipt receipt) => receipt.ResourceId is { } id ? ReviewIdentityAsync(receipt, id) : Task.CompletedTask;

    public async Task ReviewIdentityAsync(ResourceMutationReceipt receipt, Guid id) {
        if (disposed || !owner.IsCurrent || !receipts.Contains(receipt) || receipt.State != ResourceEffectState.Unknown || receipt.IsReviewing || id == Guid.Empty || receipt.ResourceId is { } known && known != id) {
            return;
        }
        receipt.IsReviewing = true;
        Notify();
        try {
            var stored = await owner.GetAsync(id, lifetime.Token);
            if (disposed || !owner.IsCurrent || receipt.State != ResourceEffectState.Unknown) {
                return;
            }
            var observed = receipt.Kind == ResourceMutationKind.Delete ? stored.Id is null : stored.Id == id && stored.ExpectedProjectAdmission == receipt.Command.ExpectedProjectAdmission;
            receipt.ReviewedResource = observed && stored.Id is not null ? stored.Capture() : null;
            receipt.State = observed && receipt.ResourceId is not null ? ResourceEffectState.Reviewed : ResourceEffectState.Unknown;
            receipt.Message = observed
                ? receipt.ResourceId is null ? "The exact candidate exists. This does not establish the original create outcome. Open the observed resource to inspect and edit it; this unacknowledged create stays blocked."
                    : "Exact stored identity was observed. This read is not proof of the earlier command's outcome; inspect current values before resubmitting."
                : "The exact target could not be verified. The outcome remains unresolved.";
        } catch (Exception) {
            receipt.Message = "Exact-identity review failed. No command was replayed.";
        } finally {
            receipt.IsReviewing = false;
            Notify();
        }
    }

    private bool Validate(ResourceEditorModel command) {
        validation ??= new(Draft.EditContext);
        validation.Clear();
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(command.Name)) {
            errors.Add("Resource name is required.");
        }
        if (command.ExpectedProjectAdmission is null || command.ExpectedProjectAdmission.ProjectId != command.ProjectId) {
            errors.Add("Select a current project before saving.");
        }
        if (IsGoverned) {
            errors.Add("Governed storage objects can only be saved through Browse promotion.");
        } else if (SelectedManifest is { } manifest) {
            errors.AddRange(new ConfigurationSchemaValidator().Validate(manifest.ConfigurationSchema, command.Configuration).Issues.Select(i => i.Message));
            if (command.ConfigSchemaVersion != manifest.ConfigurationSchema.Version) {
                errors.Add("The stored connector schema differs from the available schema. Select a connector explicitly to change it.");
            }
        } else {
            errors.Add("The exact connector is unavailable. Its configuration has been retained.");
        }
        foreach (var error in errors) {
            validation.Add(new FieldIdentifier(Draft.Editor, string.Empty), error);
        }
        Draft.EditContext.NotifyValidationStateChanged();
        Error = errors.Count > 0 ? string.Join(" ", errors) : null;
        Notify();
        return errors.Count == 0;
    }

    private bool ResolveRoute(Guid? id, Guid? projectId) {
        var selection = Pages.ResourceRouteContextSelection.Resolve(id, projectId, Resources, Projects);
        if (!selection.IsResolved) {
            catalogAccess = ResourceViewAccess.Failed;
            catalogError = "The requested resource/project binding is unavailable or belongs to a different project lifetime.";
            return false;
        }
        return catalogAccess == ResourceViewAccess.Ready;
    }

    private ResourceEditorModel NewEditor(Guid? projectId) {
        var manifest = Manifests.FirstOrDefault(m => m.PluginKey != ResourceConnectorPluginKeys.StorageObject);
        return new() { ProjectId = projectId, ExpectedProjectAdmission = Projects.FirstOrDefault(p => p.Id == projectId)?.Admission,
            ConnectorPluginKey = manifest?.PluginKey ?? ResourceConnectorPluginKeys.Repository, ConfigSchemaVersion = manifest?.ConfigurationSchema.Version ?? string.Empty };
    }
    private void ReplaceDraft(ResourceEditorModel editor) {
        Draft = new(editor);
        validation = null;
        actionError = null;
        partyVersion++;
    }
    private bool Blocked(ResourceRegistryDraft draft) => receipts.Any(r => r.BlocksDispatch &&
        (r.ResourceId is { } id ? draft.Editor.Id == id : r.Origin == draft.Origin));
    private bool Current(ResourceRegistryDraft draft, Guid? origin = null) => !disposed && owner.IsCurrent && ReferenceEquals(Draft, draft) && (origin is null || draft.Origin == origin);
    private static async Task<(bool Success, T? Value)> ReadAsync<T>(Func<Task<T>> read) {
        try {
            return (true, await read());
        } catch (Exception) {
            return (false, default);
        }
    }
    public void ResetFilters() {
        Search = string.Empty;
        ProjectFilter = null;
        ConnectorFilter = null;
        ValidationFilter = null;
        Notify();
    }
    private void Notify() {
        if (!disposed) {
            Changed?.Invoke();
        }
    }
    public void Dispose() {
        if (disposed) {
            return;
        }
        disposed = true;
        lifetime.Cancel();
        lifetime.Dispose();
        Changed = null;
        Completed = null;
    }
}
