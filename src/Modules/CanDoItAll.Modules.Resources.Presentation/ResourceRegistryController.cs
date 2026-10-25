using CanDoItAll.Configuration.UI;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Resources.UI;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Modules.Resources;

public enum ResourceRegistryRouteChangeKind {
    // The operator selected another resource.
    Selected,
    // The operator started a new draft.
    Cleared,
    // A new draft received its identity from its first save.
    Created,
    // The selected resource was deleted.
    Deleted
}

public enum ResourceRegistryFilterChangeKind {
    Edited,
    Reset
}

// The selected resource and the project that seeds new drafts, as the routed owner addresses them.
public readonly record struct ResourceRegistryRoute(Guid? ResourceId, Guid? ProjectId);

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
    private string search = string.Empty;
    private Guid? projectFilter;
    private string? connectorFilter;
    private ResourceValidationStatus? validationFilter;

    public event Action? Changed;
    public event Action<ResourceMutationReceipt>? Completed;
    public event Action<ResourceRegistryRoute, ResourceRegistryRouteChangeKind>? RouteChanged;
    public event Action<ResourceRegistryFilterChangeKind>? FiltersChanged;
    public ResourceRegistryDraft Draft { get; private set; } = new(new());
    public IReadOnlyList<ResourceSummary> Resources { get; private set; } = [];
    public IReadOnlyList<ResourceProjectOption> Projects { get; private set; } = [];
    public IReadOnlyList<ResourceReferenceOption> Parties { get; private set; } = [];
    public IReadOnlyList<ConfigurationSecretOption> Secrets { get; private set; } = [];
    public IReadOnlyList<ConnectorPluginManifest> Manifests { get; private set; } = [];
    public IReadOnlyList<ResourceMutationReceipt> Receipts => receipts;
    public ResourceViewAccess EditorAccess { get; private set; } = ResourceViewAccess.Loading;
    public ResourceViewAccess Access => EditorAccess == ResourceViewAccess.Ready ? catalogAccess : EditorAccess;
    public bool CanMutate => !disposed && owner.IsCurrent && EditorAccess == ResourceViewAccess.Ready && Draft.Expected is not null &&
        Draft.ReadOnlyReason is null && !Draft.EditorUnavailable;
    public bool CanSave => CanMutate && !Draft.ProjectionFailed && !IsGoverned && !IsBusy;
    public bool CanDelete => !disposed && owner.IsCurrent && EditorAccess == ResourceViewAccess.Ready &&
        Draft.Expected is ResourceMetadataExpectation.Existing && !IsBusy;
    public string? Error { get => editorError ?? catalogError ?? actionError; private set => actionError = value; }
    public IReadOnlyList<string> ReferenceErrors { get; private set; } = [];
    public bool IsLoading { get; private set; }
    public bool IsBusy => Blocked(Draft);
    public bool IsGoverned => (Draft.AcceptedSnapshot?.Values.ConnectorPluginKey ?? Draft.Editor.ConnectorPluginKey) == ResourceConnectorPluginKeys.StorageObject;
    public string Search { get => search; set => SetFilter(ref search, value ?? string.Empty); }
    public Guid? ProjectFilter { get => projectFilter; set => SetFilter(ref projectFilter, value); }
    public string? ConnectorFilter { get => connectorFilter; set => SetFilter(ref connectorFilter, string.IsNullOrWhiteSpace(value) ? null : value); }
    public ResourceValidationStatus? ValidationFilter { get => validationFilter; set => SetFilter(ref validationFilter, value); }
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
            await LoadNewDraftAsync(version);
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
        if (routeResource != id) {
            PublishRoute(id, Resources.FirstOrDefault(resource => resource.Id == id)?.ProjectId, ResourceRegistryRouteChangeKind.Selected);
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

    public async Task ReloadAsync() {
        if (disposed || !owner.IsCurrent || IsBusy || Draft.Expected is not ResourceMetadataExpectation.Existing expected) {
            return;
        }
        ReplaceDraft(new() { Id = expected.ResourceId });
        await LoadEditorAsync(expected.ResourceId, ++editorVersion);
    }

    public async Task NewAsync() {
        if (disposed || !owner.IsCurrent) {
            return;
        }
        var version = ++editorVersion;
        if (routeResource is not null) {
            PublishRoute(null, null, ResourceRegistryRouteChangeKind.Cleared);
        }
        ReplaceDraft(new());
        await LoadNewDraftAsync(version);
    }

    private async Task LoadNewDraftAsync(long version) {
        var draft = Draft;
        EditorAccess = ResourceViewAccess.Loading;
        editorError = null;
        Notify();
        try {
            var context = await owner.ReadContextAsync(lifetime.Token);
            if (!Current(draft) || version != editorVersion) {
                return;
            }
            ReplaceDraft(NewEditor(routeProject), new ResourceMetadataExpectation.Absent(context, Guid.NewGuid()));
            EditorAccess = routeProject is null || Projects.Any(p => p.Id == routeProject) ? ResourceViewAccess.Ready : ResourceViewAccess.Failed;
            editorError = EditorAccess == ResourceViewAccess.Ready ? null : "The requested project is unavailable.";
            await LoadPartiesAsync();
        } catch (Exception) {
            if (!disposed && version == editorVersion) {
                EditorAccess = ResourceViewAccess.Failed;
                editorError = "The Resource owner context could not be acquired. Explicitly open a new draft to retry.";
            }
        }
        Notify();
    }

    public async Task ChangeProjectAsync(Guid? projectId) {
        if (!CanMutate) {
            return;
        }
        var admission = Projects.FirstOrDefault(p => p.Id == projectId)?.Admission;
        if (Draft.Editor.ProjectId == projectId && Draft.Editor.ExpectedProjectAdmission == admission) {
            return;
        }
        Draft.Edited(ResourceEditorField.Project);
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
        Draft.ChangeConnector(manifest);
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
            var result = await owner.ReadAsync(id, lifetime.Token);
            if (!Current(draft) || version != editorVersion) {
                return;
            }
            if (result is ResourceMetadataReadResult.Missing missing && missing.ResourceId == id) {
                EditorAccess = ResourceViewAccess.Failed;
                editorError = "The selected resource no longer exists. This is not a new-resource draft.";
            } else if (result is ResourceMetadataReadResult.Found found && found.Snapshot.Expected.ResourceId == id) {
                var snapshot = found.Snapshot;
                ReplaceDraft(ReadOnlyEditor(snapshot), snapshot.Expected, snapshot, found.ReadOnlyReason);
                EditorAccess = ResourceViewAccess.Ready;
                if (found.ReadOnlyReason is null) {
                    try {
                        ReplaceDraft(owner.CreateEditor(snapshot), snapshot.Expected, snapshot);
                    } catch (Exception) {
                        Draft.ProjectionFailed = true;
                        Draft.EditorUnavailable = true;
                        editorError = "Stored metadata was read, but its editor could not be prepared. The exact identity remains available for deletion.";
                    }
                }
                await LoadPartiesAsync();
            } else if (result is ResourceMetadataReadResult.MetadataOnly metadata && metadata.Expected.ResourceId == id) {
                ReplaceDraft(new() { Id = id }, metadata.Expected, readOnlyReason: metadata.Reason);
                EditorAccess = ResourceViewAccess.Ready;
                Parties = [];
            } else {
                throw new InvalidOperationException("The Resource read did not match the selected identity.");
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
        if (kind == ResourceMutationKind.Save ? !CanSave : !CanDelete) {
            return;
        }
        var draft = Draft;
        var expected = draft.Expected!;
        ReplaceResourceMetadataCommand? command = null;
        ResourceEditorModel? submitted = null;
        if (kind == ResourceMutationKind.Save) {
            try {
                var values = ResourceMetadataValues.Capture(draft.Editor);
                submitted = values.ToEditorModel();
                submitted.Id = draft.Editor.Id;
                submitted.ProjectId = draft.Editor.ProjectId;
                submitted.ExpectedProjectAdmission = draft.Editor.ExpectedProjectAdmission;
                if (!Validate(submitted)) {
                    return;
                }
                command = new(expected, submitted.ExpectedProjectAdmission!, values);
            } catch (ArgumentException) {
                Error = "The Resource fields exceed the supported bounds or contain invalid values. Your draft is retained.";
                Notify();
                return;
            }
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
        var configurationGeneration = draft.ConfigurationGeneration;
        var mutationVersion = draft.AdmitMutation();
        var receipt = new ResourceMutationReceipt(draft.Origin, kind, expected, draft.Editor.Name, command);
        receipts.Add(receipt);
        Error = null;
        Notify();
        try {
            if (kind == ResourceMutationKind.Save) {
                var result = await owner.SaveAsync(command!);
                if (result is ResourceMetadataSaveOutcome.Rejected rejected) {
                    Refuse(rejected.Reason, rejected.Message);
                    return;
                }
                if (result is not ResourceMetadataSaveOutcome.Accepted accepted) {
                    Unknown();
                    return;
                }
                receipt.AcceptedSnapshot = accepted.Snapshot;
                RecordAccepted(accepted.Diagnostic);
                if (Current(draft)) {
                    draft.Accept(accepted.Snapshot);
                    if (expected is ResourceMetadataExpectation.Absent) {
                        PublishRoute(receipt.ResourceId, command!.TargetProject.ProjectId, ResourceRegistryRouteChangeKind.Created);
                    }
                    var stored = owner.CreateEditor(accepted.Snapshot);
                    if (Current(draft) && draft.MutationVersion == mutationVersion) {
                        draft.Reconcile(submitted!, stored, versions, configurationVersions, configurationGeneration);
                    }
                }
            } else {
                var result = await owner.DeleteAsync(new((ResourceMetadataExpectation.Existing)expected));
                if (result is ResourceMetadataDeleteOutcome.Rejected rejected) {
                    Refuse(rejected.Reason, rejected.Message);
                    return;
                }
                if (result is not ResourceMetadataDeleteOutcome.Accepted accepted) {
                    Unknown();
                    return;
                }
                receipt.Deleted = accepted.Deleted;
                RecordAccepted(accepted.Diagnostic);
                if (Current(draft)) {
                    Resources = Resources.Where(resource => resource.Id != receipt.ResourceId).ToArray();
                    PublishRoute(null, null, ResourceRegistryRouteChangeKind.Deleted);
                    await NewAsync();
                    await RefreshAsync();
                    if (Access != ResourceViewAccess.Ready || ReferenceErrors.Count != 0) {
                        receipt.State = ResourceEffectState.CommittedWarning;
                        receipt.Message = "The resource was deleted; follow-up reads are incomplete. The accepted deletion is retained.";
                    }
                }
            }
            if (!Current(draft) || draft.MutationVersion != mutationVersion) {
                return;
            }
            await RefreshAsync();
            if (Access != ResourceViewAccess.Ready || ReferenceErrors.Count != 0) {
                receipt.State = ResourceEffectState.CommittedWarning;
                receipt.Message = "The resource mutation committed; follow-up reads are incomplete. Refresh reads only.";
            }
        } catch (ResourceActionRefusedException exception) when (receipt.AcceptedSnapshot is null && receipt.Deleted is null) {
            receipt.State = ResourceEffectState.Refused;
            receipt.Message = exception.Message;
        } catch (Exception) {
            var committed = receipt.AcceptedSnapshot is not null || receipt.Deleted is not null;
            if (committed && Current(draft) && receipt.AcceptedSnapshot is not null) {
                draft.ProjectionFailed = true;
            }
            receipt.State = committed ? ResourceEffectState.CommittedWarning : ResourceEffectState.Unknown;
            receipt.Message = committed
                ? "The resource mutation committed; presentation or reference refresh failed. The exact accepted metadata is retained."
                : "No final owner acknowledgement was received. Current reads cannot resolve this outcome; the original target remains blocked.";
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

        void RecordAccepted(ResourceMetadataSecondaryDiagnostic? diagnostic) {
            receipt.State = diagnostic is null ? ResourceEffectState.Committed : ResourceEffectState.CommittedWarning;
            receipt.Message = kind == ResourceMutationKind.Save ? "Resource saved." : "Resource deleted.";
            if (diagnostic is not null) {
                receipt.Message += " The owner reported incomplete follow-up work; the accepted result is retained.";
            }
        }
        void Refuse(ResourceMetadataRejectionReason reason, string? message) {
            receipt.State = ResourceEffectState.Refused;
            receipt.Message = message ?? $"The Resource owner refused this request ({reason}). Reload explicitly to review current metadata.";
        }
        void Unknown() {
            receipt.State = ResourceEffectState.Unknown;
            receipt.Message = "No final owner acknowledgement was received. Current reads cannot resolve this outcome; the original target remains blocked.";
        }
    }

    public Task ReviewAsync(ResourceMutationReceipt receipt) => ReviewIdentityAsync(receipt, receipt.ResourceId);

    public async Task ReviewIdentityAsync(ResourceMutationReceipt receipt, Guid id) {
        if (disposed || !owner.IsCurrent || !receipts.Contains(receipt) || receipt.State != ResourceEffectState.Unknown || receipt.IsReviewing || receipt.ResourceId != id) {
            return;
        }
        receipt.IsReviewing = true;
        Notify();
        try {
            var stored = await owner.ReadAsync(id, lifetime.Token);
            if (disposed || !owner.IsCurrent || receipt.State != ResourceEffectState.Unknown) {
                return;
            }
            var observed = stored switch {
                ResourceMetadataReadResult.Found found => found.Snapshot.Expected.ResourceId == id && found.Snapshot.Expected.Context == receipt.Expected.Context,
                ResourceMetadataReadResult.MetadataOnly metadata => metadata.Expected.ResourceId == id && metadata.Expected.Context == receipt.Expected.Context,
                ResourceMetadataReadResult.Missing missing => missing.ResourceId == id && missing.Context == receipt.Expected.Context,
                _ => false
            };
            receipt.Observation = observed ? stored : null;
            receipt.Message = observed
                ? "Current metadata was observed. This does not prove the earlier request's outcome; its target remains blocked."
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
    private static ResourceEditorModel ReadOnlyEditor(ResourceMetadataSnapshot snapshot) {
        var values = snapshot.Values;
        var projectId = snapshot.Expected.Project switch {
            ResourceProjectBinding.Bound bound => (Guid?)bound.ProjectId,
            ResourceProjectBinding.RetainedProject retained => retained.ProjectId,
            ResourceProjectBinding.RetainedUnbound => null,
            _ => throw new InvalidOperationException("The stored Resource project binding is unsupported.")
        };
        return new() {
            Id = snapshot.Expected.ResourceId,
            ProjectId = projectId,
            ExpectedProjectAdmission = snapshot.Expected.Project is ResourceProjectBinding.Bound project
                ? new ProjectWriteAdmission(snapshot.Expected.Context.DatabaseProfileId, project.ProjectId, project.LifetimeId) : null,
            Name = values.Name,
            Description = values.Description,
            ConnectorPluginKey = values.ConnectorPluginKey,
            ConfigSchemaVersion = values.ConfigSchemaVersion,
            ConfigJson = values.ConfigJson,
            LocationOrIdentifier = values.LocationOrIdentifier,
            OwnerPartyId = values.OwnerPartyId,
            MaintainerPartyId = values.MaintainerPartyId,
            ValidationStatus = values.ValidationStatus,
            Sensitivity = values.Sensitivity,
            SupportsPreview = values.SupportsPreview,
            SupportsIndexing = values.SupportsIndexing
        };
    }
    private void ReplaceDraft(ResourceEditorModel editor, ResourceMetadataExpectation? expected = null,
        ResourceMetadataSnapshot? snapshot = null, ResourceMetadataReadOnlyReason? readOnlyReason = null) {
        Draft = new(editor, expected, snapshot, readOnlyReason);
        validation = null;
        actionError = null;
        partyVersion++;
    }
    private bool Blocked(ResourceRegistryDraft draft) => receipts.Any(receipt => receipt.BlocksDispatch &&
        draft.Expected is { } expected && receipt.ResourceId == expected.ResourceId && receipt.Expected.Context == expected.Context);
    private bool Current(ResourceRegistryDraft draft, Guid? origin = null) => !disposed && owner.IsCurrent && ReferenceEquals(Draft, draft) && (origin is null || draft.Origin == origin);
    private static async Task<(bool Success, T? Value)> ReadAsync<T>(Func<Task<T>> read) {
        try {
            return (true, await read());
        } catch (Exception) {
            return (false, default);
        }
    }
    public void ResetFilters() {
        search = string.Empty;
        projectFilter = null;
        connectorFilter = null;
        validationFilter = null;
        Notify();
        FiltersChanged?.Invoke(ResourceRegistryFilterChangeKind.Reset);
    }

    // Filters the routed owner restores from its location; restoring them is not an edit.
    public void ApplyRouteFilters(string? searchText, Guid? project, string? connector, ResourceValidationStatus? validation) {
        search = searchText ?? string.Empty;
        projectFilter = project;
        connectorFilter = string.IsNullOrWhiteSpace(connector) ? null : connector;
        validationFilter = validation;
        Notify();
    }

    private void SetFilter<T>(ref T field, T value) {
        if (EqualityComparer<T>.Default.Equals(field, value)) {
            return;
        }
        field = value;
        FiltersChanged?.Invoke(ResourceRegistryFilterChangeKind.Edited);
    }

    // A selection keeps the project seed only while the selected resource belongs to it, so the route stays a valid binding.
    private void PublishRoute(Guid? resourceId, Guid? resourceProjectId, ResourceRegistryRouteChangeKind kind) {
        routeResource = resourceId;
        routeProject = routeProject is { } seed && (resourceId is null || resourceProjectId == seed) ? seed : null;
        RouteChanged?.Invoke(new(routeResource, routeProject), kind);
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
        RouteChanged = null;
        FiltersChanged = null;
    }
}
