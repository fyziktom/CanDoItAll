using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using Microsoft.AspNetCore.Components;

namespace CanDoItAll.AgentFramework.Editor.UI;

public enum AgentEditorAccessFlag { ProjectStructureRead, ProjectCreation, SubprojectCreation, ProjectStructureNonTaskWrite, ProjectStructureTaskWrite, ProjectStructureWrite, ProjectStructureAllowAll, ProcessRead, ProcessWrite, ProcessAllowAll, WorkspaceFileRead, WorkspaceFileWrite, WorkspaceValidationCommands, WorkspaceScaffoldProjects, WorkspaceManagePaths, WorkspaceTransformArtifacts, StorageRead, StorageWrite, StorageAllowAll, WorkspaceLocalScripts, ScriptsReadEnvironment }
public enum CapabilityDialogAssignmentFilter { All, Attached, Available }
public enum CapabilityDialogKindFilter { All, Tool, Skill, Mcp }

public abstract record AgentEditorAccessIntent {
    private AgentEditorAccessIntent() { }
    public sealed record Flag(AgentEditorAccessFlag Kind, bool Value) : AgentEditorAccessIntent;
    public sealed record Profile(AgentWorkspaceToolProfileKind Value) : AgentEditorAccessIntent;
    public sealed record Project(Guid Id, bool Selected) : AgentEditorAccessIntent;
    public sealed record Secret(AgentEditorSecret Reference, bool Selected) : AgentEditorAccessIntent;
    public sealed record SelectAllProjects : AgentEditorAccessIntent;
    public sealed record ClearProjects : AgentEditorAccessIntent;
    public sealed record LoadProjects : AgentEditorAccessIntent;
    public sealed record RetrySecrets : AgentEditorAccessIntent;
    public sealed record CreateCapability(CapabilityKind Kind) : AgentEditorAccessIntent;
    public sealed record ReviewCreatedCapability : AgentEditorAccessIntent;
    public sealed record AssignCreatedCapability : AgentEditorAccessIntent;
}

public sealed class AgentEditorAccessState(AgentEditorOrigin origin, AgentEditorModel draft) {
    public AgentEditorOrigin Origin { get; } = origin;
    public AgentEditorModel Draft { get; set; } = draft;
    private AgentEditorModel editorModel => Draft;
    public IReadOnlyList<AgentEditorProject> Projects { get; set; } = [];
    public bool ProjectsLoaded { get; set; }
    public bool ProjectsLoading { get; set; }
    public bool ProjectsRequested { get; set; }
    public string? ProjectsError { get; set; }
    public IReadOnlyList<AgentEditorSecret> Secrets { get; set; } = [];
    public bool SecretsLoaded { get; set; }
    public bool SecretsLoading { get; set; }
    public string? SecretsError { get; set; }
    public bool ConfirmingWorkspaceRisk { get; set; }
    public int WorkspaceRiskInputVersion { get; set; }
    public bool OpeningCapabilityWizard { get; set; }
    public bool MutationBlocked { get; set; }
    public IReadOnlyList<CapabilityCatalogItem> Capabilities { get; set; } = [];
    public string CapabilitySearch { get; set; } = string.Empty;
    public CapabilityDialogAssignmentFilter CapabilityAssignmentFilter { get; set; }
    public CapabilityDialogKindFilter CapabilityKindFilter { get; set; }
    public Guid? CreatedCapabilityId { get; set; }
    public string? CreatedCapabilityMessage { get; set; }
    public bool CreatedCapabilityNeedsRead { get; set; }
    public EventCallback<AgentEditorAccessIntent> Changed { get; set; }

    public Task ChangeProfileAsync(object? value) {
        return Enum.TryParse<AgentWorkspaceToolProfileKind>(value?.ToString(), out var profile) && Enum.IsDefined(profile)
            ? Changed.InvokeAsync(new AgentEditorAccessIntent.Profile(profile))
            : Task.CompletedTask;
    }

    public void Apply(AgentEditorAccessIntent intent) {
        switch (intent) {
            case AgentEditorAccessIntent.Flag flag:
                ApplyFlag(flag.Kind, flag.Value);
                break;
            case AgentEditorAccessIntent.Profile profile:
                ChangeWorkspaceToolProfile(profile.Value);
                break;
            case AgentEditorAccessIntent.Project project:
                ToggleProjectStructureProject(project.Id, project.Selected);
                break;
            case AgentEditorAccessIntent.Secret secret:
                ToggleAllowedSecret(secret.Reference, secret.Selected);
                break;
            case AgentEditorAccessIntent.SelectAllProjects:
                SelectAllProjectStructureProjects();
                break;
            case AgentEditorAccessIntent.ClearProjects:
                ClearProjectStructureProjects();
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(intent), intent, "This intent requires its effect owner.");
        }
    }

    private void ApplyFlag(AgentEditorAccessFlag kind, bool value) {
        switch (kind) {
            case AgentEditorAccessFlag.ProjectStructureRead:
                ToggleProjectStructureRead(value);
                break;
            case AgentEditorAccessFlag.ProjectCreation:
                ToggleProjectCreation(value);
                break;
            case AgentEditorAccessFlag.SubprojectCreation:
                ToggleSubprojectCreation(value);
                break;
            case AgentEditorAccessFlag.ProjectStructureNonTaskWrite:
                ToggleProjectStructureNonTaskWrite(value);
                break;
            case AgentEditorAccessFlag.ProjectStructureTaskWrite:
                ToggleProjectStructureTaskWrite(value);
                break;
            case AgentEditorAccessFlag.ProjectStructureWrite:
                ToggleProjectStructureWrite(value);
                break;
            case AgentEditorAccessFlag.ProjectStructureAllowAll:
                ToggleProjectStructureAllowAll(value);
                break;
            case AgentEditorAccessFlag.ProcessRead:
                ToggleProcessRead(value);
                break;
            case AgentEditorAccessFlag.ProcessWrite:
                ToggleProcessWrite(value);
                break;
            case AgentEditorAccessFlag.ProcessAllowAll:
                ToggleProcessAllowAll(value);
                break;
            case AgentEditorAccessFlag.WorkspaceFileRead:
                ToggleWorkspaceFileRead(value);
                break;
            case AgentEditorAccessFlag.WorkspaceFileWrite:
                ToggleWorkspaceFileWrite(value);
                break;
            case AgentEditorAccessFlag.WorkspaceValidationCommands:
                ToggleWorkspaceValidationCommands(value);
                break;
            case AgentEditorAccessFlag.WorkspaceScaffoldProjects:
                ToggleWorkspaceScaffoldProjects(value);
                break;
            case AgentEditorAccessFlag.WorkspaceManagePaths:
                ToggleWorkspaceManagePaths(value);
                break;
            case AgentEditorAccessFlag.WorkspaceTransformArtifacts:
                ToggleWorkspaceTransformArtifacts(value);
                break;
            case AgentEditorAccessFlag.StorageRead:
                ToggleStorageRead(value);
                break;
            case AgentEditorAccessFlag.StorageWrite:
                ToggleStorageWrite(value);
                break;
            case AgentEditorAccessFlag.StorageAllowAll:
                ToggleStorageAllowAll(value);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "This permission requires confirmation.");
        }
    }
    public int CountAllowedProjectStructureProjects(AgentEditorModel editor) {
        return editor.ProjectStructureAccess.AllowedProjectIds
            .Where(projectId => projectId != Guid.Empty)
            .Distinct()
            .Count();
    }

    public string DescribeProjectStructureScope(AgentEditorModel editor) {
        return editor.ProjectStructureAccess.AllowAllProjects
            ? "All current and future projects"
            : $"{CountAllowedProjectStructureProjects(editor)} selected";
    }

    public int CountAllowedProcesses(AgentEditorModel editor) {
        return editor.ProcessAccess.AllowedDefinitionIds
            .Where(definitionId => definitionId != Guid.Empty)
            .Distinct()
            .Count();
    }

    public string DescribeProcessScope(AgentEditorModel editor) {
        return editor.ProcessAccess.AllowAllDefinitions
            ? "All current and future processes"
            : $"{CountAllowedProcesses(editor)} selected";
    }

    public string DescribeWorkspaceFileScope(AgentEditorModel editor) {
        if (!editor.WorkspaceToolAccess.CanReadFiles &&
            !editor.WorkspaceToolAccess.CanWriteFiles) {
            return "File tools disabled";
        }

        var accessMode = editor.WorkspaceToolAccess.CanWriteFiles
            ? "Read/write"
            : "Read-only";
        var externalRootCount = editor.WorkspaceToolAccess.AllowedExternalTargetAliases.Count;
        return externalRootCount == 0
            ? $"{accessMode}; managed workspace only"
            : $"{accessMode}; {externalRootCount} external root(s)";
    }

    public string DescribeWorkspaceExecutionScope(AgentEditorModel editor) {
        var access = editor.WorkspaceToolAccess;
        var enabled = new List<string>();
        if (access.CanRunValidationCommands) {
            enabled.Add("build/test/run");
        }

        if (access.CanRunLocalScripts) {
            enabled.Add(access.CanScriptsReadEnvironment ? "local scripts (with environment access)" : "local scripts");
        }

        if (access.CanScaffoldProjects) {
            enabled.Add("project scaffolding");
        }

        if (access.CanManageWorkspacePaths) {
            enabled.Add("path management");
        }

        if (access.CanTransformArtifacts) {
            enabled.Add("artifact transforms");
        }

        return enabled.Count == 0
            ? "Execution tools disabled"
            : $"Enabled: {string.Join(", ", enabled)}";
    }

    public string DescribeStorageScope(AgentEditorModel editor) {
        if (!editor.WorkspaceToolAccess.CanReadStorage &&
            !editor.WorkspaceToolAccess.CanWriteStorage) {
            return "Storage tools disabled";
        }

        var accessMode = editor.WorkspaceToolAccess.CanWriteStorage
            ? "Read/write"
            : "Read-only";

        return editor.WorkspaceToolAccess.AllowAllStorageCatalogs
            ? $"{accessMode}; all storage catalogs"
            : $"{accessMode}; {editor.WorkspaceToolAccess.AllowedStorageCatalogIds.Count} storage catalog(s)";
    }

    public string DescribeSecretScope(AgentEditorModel editor) {
        var count = editor.AllowedSecretReferences
            .Where(item => item.SecretId != Guid.Empty)
            .Select(item => item.SecretId)
            .Distinct()
            .Count();
        return count == 0
            ? "No stored secrets"
            : $"{count} stored secret(s)";
    }

    public bool HasAllowedSecret(Guid secretId)
        => editorModel.AllowedSecretReferences.Any(item => item.SecretId == secretId);

    private void ToggleAllowedSecret(AgentEditorSecret secret, object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        if (!isEnabled) {
            editorModel.AllowedSecretReferences.RemoveAll(item => item.SecretId == secret.Id);
            return;
        }

        if (HasAllowedSecret(secret.Id)) {
            return;
        }
        editorModel.AllowedSecretReferences.Add(new AgentAllowedSecretReference(
            secret.Id,
            secret.Name,
            AgentSecretPurposes.GeneralAgentRequest));
    }

    private void ToggleProjectStructureRead(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanRead = isEnabled;
        if (!isEnabled) {
            editorModel.ProjectStructureAccess.CanWrite = false;
            editorModel.ProjectStructureAccess.CanWriteNonTaskStructure = false;
            editorModel.ProjectStructureAccess.CanWriteTasks = false;
            editorModel.ProjectStructureAccess.CanCreateProjects = false;
            editorModel.ProjectStructureAccess.CanCreateSubprojects = false;
            editorModel.ProjectStructureAccess.AllowAllProjects = false;
            editorModel.ProjectStructureAccess.AllowedProjectIds = [];
            editorModel.ProjectStructureAccess.AllowedProjectLifetimes = [];
        }
    }

    private void ToggleProjectCreation(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanCreateProjects = isEnabled;
        EnsureProjectAccessLoadedWhenEnabled(isEnabled);
    }

    private void ToggleSubprojectCreation(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanCreateSubprojects = isEnabled;
        EnsureProjectAccessLoadedWhenEnabled(isEnabled);
    }

    private void ToggleProjectStructureNonTaskWrite(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanWriteNonTaskStructure = isEnabled;
        if (isEnabled) {
            editorModel.ProjectStructureAccess.CanRead = true;
        }
    }

    private void ToggleProjectStructureTaskWrite(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanWriteTasks = isEnabled;
        if (isEnabled) {
            editorModel.ProjectStructureAccess.CanRead = true;
        }
    }

    private void ToggleProjectStructureWrite(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.CanWrite = isEnabled;
        if (isEnabled) {
            editorModel.ProjectStructureAccess.CanRead = true;
        }
    }

    private void ToggleProjectStructureAllowAll(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProjectStructureAccess.AllowAllProjects = isEnabled;
        if (isEnabled) {
            editorModel.ProjectStructureAccess.CanRead = true;
            editorModel.ProjectStructureAccess.AllowedProjectIds = [];
        }
    }

    private void ToggleProcessRead(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProcessAccess.CanRead = isEnabled;
        if (!isEnabled) {
            editorModel.ProcessAccess.CanWrite = false;
            editorModel.ProcessAccess.AllowAllDefinitions = false;
        }
    }

    private void ToggleProcessWrite(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProcessAccess.CanWrite = isEnabled;
        if (isEnabled) {
            editorModel.ProcessAccess.CanRead = true;
        }
    }

    private void ToggleProcessAllowAll(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.ProcessAccess.AllowAllDefinitions = isEnabled;
        if (isEnabled) {
            editorModel.ProcessAccess.CanRead = true;
        }
    }

    private void ToggleWorkspaceFileRead(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanReadFiles = isEnabled;
        if (!isEnabled) {
            editorModel.WorkspaceToolAccess.CanWriteFiles = false;
        }

        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceFileWrite(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanWriteFiles = isEnabled;
        if (isEnabled) {
            editorModel.WorkspaceToolAccess.CanReadFiles = true;
        }

        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceValidationCommands(object? rawValue) {
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanRunValidationCommands = rawValue is bool value && value;
        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceScaffoldProjects(object? rawValue) {
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanScaffoldProjects = rawValue is bool value && value;
        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceManagePaths(object? rawValue) {
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanManageWorkspacePaths = rawValue is bool value && value;
        NormalizeWorkspaceToolAccess();
    }

    private void ToggleWorkspaceTransformArtifacts(object? rawValue) {
        MarkWorkspaceToolProfileCustom();
        editorModel.WorkspaceToolAccess.CanTransformArtifacts = rawValue is bool value && value;
        NormalizeWorkspaceToolAccess();
    }

    private void ToggleStorageRead(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.WorkspaceToolAccess.CanReadStorage = isEnabled;
        if (!isEnabled) {
            editorModel.WorkspaceToolAccess.CanWriteStorage = false;
            editorModel.WorkspaceToolAccess.AllowAllStorageCatalogs = false;
        }
    }

    private void ToggleStorageWrite(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.WorkspaceToolAccess.CanWriteStorage = isEnabled;
        if (isEnabled) {
            editorModel.WorkspaceToolAccess.CanReadStorage = true;
        }
    }

    private void ToggleStorageAllowAll(object? rawValue) {
        var isEnabled = rawValue is bool value && value;
        editorModel.WorkspaceToolAccess.AllowAllStorageCatalogs = isEnabled;
        if (isEnabled) {
            editorModel.WorkspaceToolAccess.CanReadStorage = true;
        }
    }

    private void EnsureProjectAccessLoadedWhenEnabled(bool isEnabled) {
        if (!isEnabled) {
            return;
        }

        editorModel.ProjectStructureAccess.CanRead = true;
    }

    private void ChangeWorkspaceToolProfile(AgentWorkspaceToolProfileKind profile) {
        if (!Enum.IsDefined(profile)) {
            throw new ArgumentOutOfRangeException(nameof(profile));
        }
        var current = editorModel.WorkspaceToolAccess;
        var next = profile == AgentWorkspaceToolProfileKind.Custom
            ? AgentEditorDraftSnapshot.CopyWorkspaceAccess(current)
            : AgentWorkspaceToolAccessProfiles.CreateSettings(profile);
        next.Profile = profile;
        next.AllowedExternalTargetAliases = current.AllowedExternalTargetAliases.ToList();
        next.ExternalTargetRootBindings = current.ExternalTargetRootBindings.ToList();
        next.CanReadStorage = current.CanReadStorage;
        next.CanWriteStorage = current.CanWriteStorage;
        next.AllowAllStorageCatalogs = current.AllowAllStorageCatalogs;
        next.AllowedStorageCatalogIds = current.AllowedStorageCatalogIds.ToList();
        editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(next);
    }

    private void MarkWorkspaceToolProfileCustom() {
        editorModel.WorkspaceToolAccess.Profile = AgentWorkspaceToolProfileKind.Custom;
    }

    private void NormalizeWorkspaceToolAccess() {
        editorModel.WorkspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(editorModel.WorkspaceToolAccess);
    }

    public bool HasProjectStructureProjectAccess(Guid projectId) {
        return editorModel.ProjectStructureAccess.AllowedProjectIds.Contains(projectId);
    }

    private void ToggleProjectStructureProject(Guid projectId, object? rawValue) {
        var selectedProjects = editorModel.ProjectStructureAccess.AllowedProjectIds.ToList();
        var isEnabled = rawValue is bool value && value;
        if (isEnabled) {
            editorModel.ProjectStructureAccess.AllowAllProjects = false;
            if (!selectedProjects.Contains(projectId)) {
                selectedProjects.Add(projectId);
            }
        }
        else {
            selectedProjects.RemoveAll(item => item == projectId);
            editorModel.ProjectStructureAccess.AllowedProjectLifetimes.RemoveAll(item => item.ProjectId == projectId);
        }

        editorModel.ProjectStructureAccess.AllowedProjectIds = selectedProjects
            .Distinct()
            .OrderBy(item => item)
            .ToList();
    }

    private void SelectAllProjectStructureProjects() {
        editorModel.ProjectStructureAccess.AllowAllProjects = false;
        editorModel.ProjectStructureAccess.AllowedProjectIds = Projects
            .Select(item => item.Id)
            .Concat(editorModel.ProjectStructureAccess.AllowedProjectIds)
            .Distinct()
            .OrderBy(item => item)
            .ToList();
    }

    private void ClearProjectStructureProjects() {
        editorModel.ProjectStructureAccess.AllowedProjectIds = [];
        editorModel.ProjectStructureAccess.AllowedProjectLifetimes = [];
    }

    private bool MatchesCapabilitySearch(CapabilityCatalogItem capability) {
        if (string.IsNullOrWhiteSpace(CapabilitySearch)) {
            return true;
        }

        var search = CapabilitySearch.Trim();
        return capability.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               capability.Key.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               capability.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               capability.EndpointOrPath.Contains(search, StringComparison.OrdinalIgnoreCase) ||
               capability.Tags.Any(tag => tag.Contains(search, StringComparison.OrdinalIgnoreCase));
    }

    private bool MatchesCapabilityAssignmentFilter(CapabilityCatalogItem capability) {
        var isAttached = editorModel.SelectedCapabilityIds.Contains(capability.Id);
        return CapabilityAssignmentFilter switch {
            CapabilityDialogAssignmentFilter.Attached => isAttached,
            CapabilityDialogAssignmentFilter.Available => !isAttached,
            _ => true
        };
    }

    private bool MatchesCapabilityKindFilter(CapabilityCatalogItem capability) {
        return CapabilityKindFilter switch {
            CapabilityDialogKindFilter.Tool => capability.Kind == CapabilityKind.Tool,
            CapabilityDialogKindFilter.Skill => capability.Kind == CapabilityKind.Skill,
            CapabilityDialogKindFilter.Mcp => capability.Kind == CapabilityKind.McpServer,
            _ => true
        };
    }

    public void ResetCapabilityFilters() {
        CapabilitySearch = string.Empty;
        CapabilityAssignmentFilter = CapabilityDialogAssignmentFilter.All;
        CapabilityKindFilter = CapabilityDialogKindFilter.All;
    }

    public string FormatWorkspaceToolProfile(AgentWorkspaceToolProfileKind profile) {
        return profile switch {
            AgentWorkspaceToolProfileKind.ReadOnly => "Read only",
            AgentWorkspaceToolProfileKind.SoftwareDevelopment => "Software development",
            AgentWorkspaceToolProfileKind.QualityValidation => "Quality validation",
            AgentWorkspaceToolProfileKind.ArchitectureReview => "Architecture review",
            AgentWorkspaceToolProfileKind.SecurityReview => "Security review",
            AgentWorkspaceToolProfileKind.BusinessAnalysis => "Business analysis",
            _ => "Custom"
        };
    }

    public IReadOnlyList<CapabilityCatalogItem> AssignableCapabilities => Capabilities
        .Where(item => item.Kind is CapabilityKind.Tool or CapabilityKind.Skill or CapabilityKind.McpServer ||
                       editorModel.SelectedCapabilityIds.Contains(item.Id))
        .OrderByDescending(item => editorModel.SelectedCapabilityIds.Contains(item.Id))
        .ThenBy(item => item.Kind)
        .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
        .ToList();

    public IReadOnlyList<CapabilityCatalogItem> FilteredAssignableCapabilities => AssignableCapabilities
        .Where(MatchesCapabilitySearch)
        .Where(MatchesCapabilityAssignmentFilter)
        .Where(MatchesCapabilityKindFilter)
        .ToList();

    public IReadOnlyList<AgentWorkspaceToolProfileKind> WorkspaceToolProfileOptions { get; } =
    [
        AgentWorkspaceToolProfileKind.Custom,
        AgentWorkspaceToolProfileKind.ReadOnly,
        AgentWorkspaceToolProfileKind.SoftwareDevelopment,
        AgentWorkspaceToolProfileKind.QualityValidation,
        AgentWorkspaceToolProfileKind.ArchitectureReview,
        AgentWorkspaceToolProfileKind.SecurityReview,
        AgentWorkspaceToolProfileKind.BusinessAnalysis
    ];

}
