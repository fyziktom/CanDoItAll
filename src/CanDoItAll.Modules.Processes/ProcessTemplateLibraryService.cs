using CanDoItAll.Components.BaseLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessTemplateLibraryService
{
    private readonly ProcessTemplatePackLoader packLoader;
    private readonly ProcessTemplateProjectionService projectionService;

    public ProcessTemplateLibraryService(
        ProcessTemplatePackLoader packLoader,
        ProcessTemplateProjectionService projectionService)
    {
        this.packLoader = packLoader;
        this.projectionService = projectionService;
    }

    public IReadOnlyList<ProcessTemplateLibraryListItem> ListItems(ProcessTemplateLibraryCategory category)
    {
        var pack = packLoader.Load();

        return category switch
        {
            ProcessTemplateLibraryCategory.Processes => pack.Processes.Values
                .OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(BuildProcessListItem)
                .ToList(),
            ProcessTemplateLibraryCategory.Roles => EnumerateRoleDescriptors(pack)
                .OrderBy(item => item.Resource.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(BuildRoleListItem)
                .ToList(),
            ProcessTemplateLibraryCategory.Artifacts => EnumerateArtifactDescriptors(pack)
                .OrderBy(item => item.Resource.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(BuildArtifactListItem)
                .ToList(),
            _ => []
        };
    }

    public ProcessTemplateLibraryPreview GetPreview(ProcessTemplateLibraryCategory category, string itemId)
    {
        var pack = packLoader.Load();

        return category switch
        {
            ProcessTemplateLibraryCategory.Processes => BuildProcessPreview(pack, ResolveProcess(pack, itemId)),
            ProcessTemplateLibraryCategory.Roles => BuildRolePreview(ResolveRole(pack, itemId)),
            ProcessTemplateLibraryCategory.Artifacts => BuildArtifactPreview(ResolveArtifact(pack, itemId)),
            _ => throw new InvalidOperationException($"Unsupported template category '{category}'.")
        };
    }

    public ProcessImportExportEnvelope CreateProcessImportEnvelope(
        string processKey,
        Guid? projectId,
        string? definitionName = null)
    {
        return projectionService.GetProjectedEnvelope(processKey, projectId, definitionName);
    }

    public ProcessRoleEditorModel CreateRoleDraft(string itemId, int ordinal)
    {
        var descriptor = ResolveRole(packLoader.Load(), itemId);
        var keySuffix = ordinal > 1
            ? "-" + ordinal.ToString()
            : string.Empty;

        return ProcessTemplateEditorModelFactory.CreateRoleFromResource(
            descriptor.Resource,
            Guid.NewGuid(),
            descriptor.Resource.Key + keySuffix,
            descriptor.Resource.DisplayName,
            descriptor.Resource.PreferredExecutorKind,
            100);
    }

    public ProcessArtifactExpectationEditorModel CreateArtifactExpectation(string itemId, bool isRequired = true)
    {
        return ProcessTemplateEditorModelFactory.CreateArtifactExpectationFromResource(
            ResolveArtifact(packLoader.Load(), itemId).Resource,
            Guid.NewGuid(),
            isRequired);
    }

    private static ProcessTemplateLibraryListItem BuildProcessListItem(ProcessTemplateDefinition process)
    {
        return new ProcessTemplateLibraryListItem(
            process.Key,
            ProcessTemplateLibraryCategory.Processes,
            process.Key,
            process.DisplayName,
            process.Summary,
            "Process template",
            "Process library",
            string.Empty,
            string.Empty,
            [
                new ProcessTemplateLibraryFact("Criticality", NormalizeValue(process.Criticality, "Standard")),
                new ProcessTemplateLibraryFact("Autonomy", NormalizeValue(process.AutonomyLevel, "Assisted")),
                new ProcessTemplateLibraryFact("Steps", process.Steps.Count.ToString()),
                new ProcessTemplateLibraryFact("Roles", (process.SharedRoleRefs.Count + process.LocalRoleRefs.Count).ToString()),
                new ProcessTemplateLibraryFact("Artifacts", (process.SharedArtifactRefs.Count + process.LocalArtifactRefs.Count).ToString())
            ]);
    }

    private static ProcessTemplateLibraryListItem BuildRoleListItem(RoleDescriptor descriptor)
    {
        return new ProcessTemplateLibraryListItem(
            descriptor.ItemId,
            ProcessTemplateLibraryCategory.Roles,
            descriptor.Resource.Key,
            descriptor.Resource.DisplayName,
            descriptor.Resource.Summary,
            descriptor.IsShared ? "Shared role template" : "Process role template",
            descriptor.IsShared ? "Shared role library" : descriptor.ProcessDisplayName,
            descriptor.ProcessKey,
            descriptor.ProcessDisplayName,
            [
                new ProcessTemplateLibraryFact("Executor", NormalizeValue(descriptor.Resource.PreferredExecutorKind, "Not set")),
                new ProcessTemplateLibraryFact("Allocation", descriptor.Resource.DefaultAllocationPercent > 0 ? $"{descriptor.Resource.DefaultAllocationPercent}%" : "Not set"),
                new ProcessTemplateLibraryFact("Approval", descriptor.Resource.RequiresExplicitApproval ? "Explicit" : "Embedded"),
                new ProcessTemplateLibraryFact("Scope", descriptor.IsShared ? "Shared" : "Process-local")
            ]);
    }

    private static ProcessTemplateLibraryListItem BuildArtifactListItem(ArtifactDescriptor descriptor)
    {
        return new ProcessTemplateLibraryListItem(
            descriptor.ItemId,
            ProcessTemplateLibraryCategory.Artifacts,
            descriptor.Resource.Key,
            descriptor.Resource.DisplayName,
            descriptor.Resource.Summary,
            descriptor.IsShared ? "Shared artifact template" : "Process artifact template",
            descriptor.IsShared ? "Shared artifact library" : descriptor.ProcessDisplayName,
            descriptor.ProcessKey,
            descriptor.ProcessDisplayName,
            [
                new ProcessTemplateLibraryFact("Kind", NormalizeValue(descriptor.Resource.ArtifactKind, "Evidence")),
                new ProcessTemplateLibraryFact("Owner", NormalizeValue(descriptor.Resource.OwnerRoleKey, "Not set")),
                new ProcessTemplateLibraryFact("Trust", NormalizeValue(descriptor.Resource.DefaultTrustRequirement, "Review required")),
                new ProcessTemplateLibraryFact("Retention", descriptor.Resource.DefaultRetentionDays > 0 ? $"{descriptor.Resource.DefaultRetentionDays} days" : "Not set")
            ]);
    }

    private ProcessTemplateLibraryPreview BuildProcessPreview(ProcessTemplatePack pack, ProcessTemplateDefinition process)
    {
        var roleLinks = BuildProcessRoleLinks(pack, process);
        var artifactLinks = BuildProcessArtifactLinks(pack, process);

        return new ProcessTemplateLibraryPreview(
            process.Key,
            ProcessTemplateLibraryCategory.Processes,
            process.Key,
            process.DisplayName,
            process.Summary,
            "Process template",
            "Process library",
            [
                new ProcessTemplateLibraryFact("Criticality", NormalizeValue(process.Criticality, "Standard")),
                new ProcessTemplateLibraryFact("Autonomy", NormalizeValue(process.AutonomyLevel, "Assisted")),
                new ProcessTemplateLibraryFact("Operating mode", NormalizeValue(process.OperatingMode, "Not set")),
                new ProcessTemplateLibraryFact("Customer", NormalizeValue(process.CustomerName, "Not set")),
                new ProcessTemplateLibraryFact("Owner", NormalizeValue(process.OwnerName, "Not set"))
            ],
            BuildProcessTree(process, roleLinks, artifactLinks),
            BuildDocuments(
                ("definition", "Definition", process.DefinitionMarkdownPath),
                ("compatibility", "Compatibility", process.CurrentModuleCompatibilityReportMarkdownPath)),
            BuildDocuments(
                ("definition-json", "Definition JSON", process.DefinitionJsonPath),
                ("import-envelope", "Import envelope", process.CurrentModuleImportEnvelopePath),
                ("compatibility-json", "Compatibility JSON", process.CurrentModuleCompatibilityReportPath)),
            BuildMermaidDiagrams(
                ("flowchart", "Flowchart", process.FlowchartPath),
                ("sequence", "Sequence", process.SequencePath)),
            roleLinks,
            artifactLinks);
    }

    private static ProcessTemplateLibraryPreview BuildRolePreview(RoleDescriptor descriptor)
    {
        return new ProcessTemplateLibraryPreview(
            descriptor.ItemId,
            ProcessTemplateLibraryCategory.Roles,
            descriptor.Resource.Key,
            descriptor.Resource.DisplayName,
            descriptor.Resource.Summary,
            descriptor.IsShared ? "Shared role template" : "Process role template",
            descriptor.IsShared ? "Shared role library" : descriptor.ProcessDisplayName,
            [
                new ProcessTemplateLibraryFact("Executor", NormalizeValue(descriptor.Resource.PreferredExecutorKind, "Not set")),
                new ProcessTemplateLibraryFact("Allocation", descriptor.Resource.DefaultAllocationPercent > 0 ? $"{descriptor.Resource.DefaultAllocationPercent}%" : "Not set"),
                new ProcessTemplateLibraryFact("Scope", descriptor.IsShared ? "Shared" : "Process-local"),
                new ProcessTemplateLibraryFact("Source process", descriptor.IsShared ? "Shared library" : descriptor.ProcessDisplayName),
                new ProcessTemplateLibraryFact("Snapshot", NormalizeValue(descriptor.Resource.RoleTemplateSnapshotName, "Not set"))
            ],
            BuildRoleTree(descriptor),
            BuildDocuments(("role-doc", "Role definition", descriptor.Resource.DocPath)),
            BuildDocuments(("role-json", "Role JSON", ResolveSiblingJsonPath(descriptor.Resource.DocPath))),
            [],
            [],
            []);
    }

    private static ProcessTemplateLibraryPreview BuildArtifactPreview(ArtifactDescriptor descriptor)
    {
        return new ProcessTemplateLibraryPreview(
            descriptor.ItemId,
            ProcessTemplateLibraryCategory.Artifacts,
            descriptor.Resource.Key,
            descriptor.Resource.DisplayName,
            descriptor.Resource.Summary,
            descriptor.IsShared ? "Shared artifact template" : "Process artifact template",
            descriptor.IsShared ? "Shared artifact library" : descriptor.ProcessDisplayName,
            [
                new ProcessTemplateLibraryFact("Kind", NormalizeValue(descriptor.Resource.ArtifactKind, "Evidence")),
                new ProcessTemplateLibraryFact("Owner role", NormalizeValue(descriptor.Resource.OwnerRoleKey, "Not set")),
                new ProcessTemplateLibraryFact("Trust", NormalizeValue(descriptor.Resource.DefaultTrustRequirement, "Review required")),
                new ProcessTemplateLibraryFact("Sensitivity", NormalizeValue(descriptor.Resource.DefaultSensitivityLevel, "Internal")),
                new ProcessTemplateLibraryFact("Retention", descriptor.Resource.DefaultRetentionDays > 0 ? $"{descriptor.Resource.DefaultRetentionDays} days" : "Not set")
            ],
            BuildArtifactTree(descriptor),
            BuildDocuments(("artifact-doc", "Artifact definition", descriptor.Resource.DocPath)),
            BuildDocuments(("artifact-json", "Artifact JSON", ResolveSiblingJsonPath(descriptor.Resource.DocPath))),
            [],
            [],
            []);
    }
}
