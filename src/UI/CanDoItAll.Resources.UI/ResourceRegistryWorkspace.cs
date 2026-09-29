using CanDoItAll.Configuration.UI;
using CanDoItAll.Modules.Resources;
using CanDoItAll.SharedKernel.Configuration;
using Microsoft.AspNetCore.Components.Forms;

namespace CanDoItAll.Resources.UI;

public enum ResourceEditorField { Name, Description, Owner, Maintainer, Secret, Validation, Sensitivity, Preview, Indexing }
public enum ResourceEffectState { Pending, Refused, Committed, CommittedWarning, Unknown, Reviewed }

public sealed class ResourceMutationReceipt(Guid origin, ResourceMutationKind kind, ResourceEditorModel command) {
    public Guid Id { get; } = Guid.NewGuid();
    public Guid Origin { get; } = origin;
    public ResourceMutationKind Kind { get; } = kind;
    public ResourceEditorModel Command { get; } = command;
    public Guid? ResourceId { get; set; } = command.Id;
    public ResourceEffectState State { get; set; } = ResourceEffectState.Pending;
    public string Message { get; set; } = "Waiting for the owner.";
    public bool IsReviewing { get; set; }
    public ResourceEditorModel? ReviewedResource { get; set; }
    public bool BlocksDispatch => State is ResourceEffectState.Pending or ResourceEffectState.Unknown;
}

public sealed class ResourceRegistryDraft(ResourceEditorModel editor) {
    private readonly Dictionary<ResourceEditorField, long> versions = [];
    public Guid Origin { get; private set; } = Guid.NewGuid();
    public ResourceEditorModel Editor { get; } = editor;
    public EditContext EditContext { get; } = new(editor);
    public ConfigurationInputDraft Configuration { get; } = new(editor.Configuration);
    public long MutationVersion { get; private set; }
    public long AdmitMutation() => ++MutationVersion;
    public void ChangeTarget() => Origin = Guid.NewGuid();
    public void Edited(ResourceEditorField field) => versions[field] = Version(field) + 1;
    public long Version(ResourceEditorField field) => versions.GetValueOrDefault(field);
    public IReadOnlyDictionary<ResourceEditorField, long> CaptureVersions() => new Dictionary<ResourceEditorField, long>(versions);

    public void Reconcile(ResourceEditorModel submitted, ResourceEditorModel stored, IReadOnlyDictionary<ResourceEditorField, long> submittedVersions,
        IReadOnlyDictionary<string, long> configurationVersions) {
        Apply(ResourceEditorField.Name, submitted.Name, stored.Name, () => Editor.Name, value => Editor.Name = value);
        Apply(ResourceEditorField.Description, submitted.Description, stored.Description, () => Editor.Description, value => Editor.Description = value);
        Apply(ResourceEditorField.Owner, submitted.OwnerPartyId, stored.OwnerPartyId, () => Editor.OwnerPartyId, value => Editor.OwnerPartyId = value);
        Apply(ResourceEditorField.Maintainer, submitted.MaintainerPartyId, stored.MaintainerPartyId, () => Editor.MaintainerPartyId, value => Editor.MaintainerPartyId = value);
        Apply(ResourceEditorField.Secret, submitted.LinkedSecretId, stored.LinkedSecretId, () => Editor.LinkedSecretId, value => Editor.LinkedSecretId = value);
        Apply(ResourceEditorField.Validation, submitted.ValidationStatus, stored.ValidationStatus, () => Editor.ValidationStatus, value => Editor.ValidationStatus = value);
        Apply(ResourceEditorField.Sensitivity, submitted.Sensitivity, stored.Sensitivity, () => Editor.Sensitivity, value => Editor.Sensitivity = value);
        Apply(ResourceEditorField.Preview, submitted.SupportsPreview, stored.SupportsPreview, () => Editor.SupportsPreview, value => Editor.SupportsPreview = value);
        Apply(ResourceEditorField.Indexing, submitted.SupportsIndexing, stored.SupportsIndexing, () => Editor.SupportsIndexing, value => Editor.SupportsIndexing = value);
        Configuration.Reconcile(submitted.Configuration, stored.Configuration, configurationVersions);
        Editor.ConfigJson = stored.ConfigJson;
        Editor.LocationOrIdentifier = stored.LocationOrIdentifier;

        void Apply<T>(ResourceEditorField field, T oldValue, T storedValue, Func<T> current, Action<T> assign) {
            if (Version(field) == submittedVersions.GetValueOrDefault(field) && EqualityComparer<T>.Default.Equals(current(), oldValue)) {
                assign(storedValue);
            }
        }
    }
}

public interface IResourceRegistryWorkspace {
    event Action? Changed;
    ResourceRegistryDraft Draft { get; }
    IReadOnlyList<ResourceSummary> Resources { get; }
    IReadOnlyList<ResourceSummary> FilteredResources { get; }
    IReadOnlyList<ResourceProjectOption> Projects { get; }
    IReadOnlyList<ResourceReferenceOption> Parties { get; }
    IReadOnlyList<ConfigurationSecretOption> Secrets { get; }
    IReadOnlyList<ConnectorPluginManifest> Manifests { get; }
    IReadOnlyList<ResourceMutationReceipt> Receipts { get; }
    ConnectorPluginManifest? SelectedManifest { get; }
    ResourceViewAccess Access { get; }
    string? Error { get; }
    IReadOnlyList<string> ReferenceErrors { get; }
    string? SelectedProjectName { get; }
    string PreviewLocation { get; }
    bool IsBusy { get; }
    bool IsLoading { get; }
    bool IsGoverned { get; }
    string Search { get; set; }
    Guid? ProjectFilter { get; set; }
    string? ConnectorFilter { get; set; }
    ResourceValidationStatus? ValidationFilter { get; set; }
    Task RefreshAsync();
    Task SelectAsync(Guid id);
    Task NewAsync();
    Task ChangeProjectAsync(Guid? projectId);
    Task ChangeConnectorAsync(string key);
    Task SaveAsync();
    Task DeleteAsync();
    Task ReviewAsync(ResourceMutationReceipt receipt);
    Task ReviewIdentityAsync(ResourceMutationReceipt receipt, Guid resourceId);
    void ResetFilters();
}
