using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AccessCapabilityTag = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityTag;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class WorkspaceToolSet(
    AgentWorkspaceToolAccessSettings workspaceToolAccess,
    WorkspaceFilesystemRuntimePlugin filesystemPlugin,
    WorkspaceRuntimePlugin workspacePlugin,
    WorkspaceSpreadsheetRuntimePlugin spreadsheetPlugin,
    StorageRuntimePlugin? storagePlugin,
    RuntimeCapabilityAccessPlan capabilityAccessPlan)
{
    private const string SpreadsheetWriteDescription = "Creates or updates a workspace .xlsx workbook and worksheet using typed cell and range writes. Each rangeWrites values row must fit within the columns of its rangeAddress, and the number of values rows must fit within that range. Values beginning with = are stored as formulas. Creates missing workbooks and worksheets when requested.";

    private readonly AgentWorkspaceToolAccessSettings workspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);

    public IReadOnlyList<AITool> CreateTools(
        IReadOnlyList<CapabilityCatalogItem> catalogCapabilities,
        bool suppressApprovalRequirements = false)
    {
        ArgumentNullException.ThrowIfNull(catalogCapabilities);

        var access = workspaceToolAccess;
        var tools = CreateWorkspaceTools(
            catalogCapabilities,
            includeConfiguredTools: true,
            suppressApprovalRequirements);

        if (storagePlugin is not null && (access.CanReadStorage || access.CanWriteStorage))
        {
            AddConfiguredToolIfAllowed(tools, ToolContractCatalog.StorageCatalogList, () => AIFunctionFactory.Create(storagePlugin.ListStorageCatalogs, ToolContractCatalog.StorageCatalogList, "Lists storage catalogs this agent is allowed to use."));
            AddConfiguredToolIfAllowed(tools, ToolContractCatalog.StorageBrowse, () => AIFunctionFactory.Create(storagePlugin.BrowseStorage, ToolContractCatalog.StorageBrowse, "Lists one bounded page of direct child folders and objects in an allowed storage catalog. Use entryId as the read locator and a container entry id as containerKey to descend. When nextCursor is returned, pass it in the next call while repeating the same storageId, containerKey, pageSize, and includeMetadata values."));
            AddConfiguredToolIfAllowed(tools, ToolContractCatalog.StorageReadTextFile, () => AIFunctionFactory.Create(storagePlugin.ReadStorageTextFile, ToolContractCatalog.StorageReadTextFile, "Reads a text object from an allowed storage catalog through the configured storage driver."));
        }

        if (storagePlugin is not null && access.CanWriteStorage)
        {
            AddConfiguredToolIfAllowed(tools, ToolContractCatalog.StorageWriteTextFile, () => WrapWithApproval(AIFunctionFactory.Create(storagePlugin.WriteStorageTextFile, ToolContractCatalog.StorageWriteTextFile, "Writes a text object to an allowed storage catalog through the configured storage driver."), suppressApprovalRequirements));
            AddConfiguredToolIfAllowed(tools, ToolContractCatalog.StorageDeleteObject, () => WrapWithApproval(AIFunctionFactory.Create(storagePlugin.DeleteStorageObject, ToolContractCatalog.StorageDeleteObject, "Deletes an object from an allowed storage catalog through the configured storage driver."), suppressApprovalRequirements));
        }

        return tools;
    }

    public bool TryCreateCatalogCapabilityTools(
        CapabilityCatalogItem capability,
        bool suppressApprovalRequirements,
        out IReadOnlyList<AITool> tools)
    {
        ArgumentNullException.ThrowIfNull(capability);

        var candidates = CreateWorkspaceToolCandidates();
        var declaration = TryCreateDeclaration(capability, candidates);
        if (declaration is null)
        {
            tools = [];
            return false;
        }

        tools = declaration.Enabled && IsCatalogCapabilityAllowed(capability)
            ? CreateWorkspaceTools(
                [capability],
                includeConfiguredTools: false,
                suppressApprovalRequirements)
            : [];
        return true;
    }

    public bool IsWorkspaceToolCapability(CapabilityCatalogItem capability)
    {
        ArgumentNullException.ThrowIfNull(capability);
        return TryCreateDeclaration(capability, CreateWorkspaceToolCandidates()) is not null;
    }

    public bool CanWorkspaceToolCapabilityParticipate(
        CapabilityCatalogItem capability,
        IReadOnlyList<CapabilityCatalogItem> catalogCapabilities)
    {
        ArgumentNullException.ThrowIfNull(capability);
        ArgumentNullException.ThrowIfNull(catalogCapabilities);

        var candidates = CreateWorkspaceToolCandidates();
        var declaration = TryCreateDeclaration(capability, candidates);
        if (declaration is null ||
            !declaration.Enabled ||
            !IsCatalogCapabilityAllowed(capability))
        {
            return false;
        }

        return declaration.IsWorkspacePlugin
            ? CanWorkspacePluginParticipate(candidates, catalogCapabilities)
            : AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(
                workspaceToolAccess,
                declaration.RuntimeToolName!);
    }

    private bool CanWorkspacePluginParticipate(
        IReadOnlyList<WorkspaceToolCandidate> candidates,
        IReadOnlyList<CapabilityCatalogItem> catalogCapabilities)
    {
        var explicitRuntimeToolNames = catalogCapabilities
            .Where(IsCatalogCapabilityAllowed)
            .Select(capability => TryCreateDeclaration(capability, candidates))
            .OfType<WorkspaceToolCapabilityDeclaration>()
            .Where(declaration => declaration.Enabled && !declaration.IsWorkspacePlugin)
            .Select(declaration => declaration.RuntimeToolName)
            .OfType<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        return candidates.Any(candidate =>
            AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(
                workspaceToolAccess,
                candidate.RuntimeToolName) &&
            (IsConfiguredRuntimeToolAllowed(candidate.RuntimeToolName) ||
             explicitRuntimeToolNames.Contains(candidate.RuntimeToolName)));
    }

    public bool TryCapabilityHasApprovalTools(
        CapabilityCatalogItem capability,
        bool suppressApprovalRequirements,
        out bool hasApprovalTools)
    {
        ArgumentNullException.ThrowIfNull(capability);

        var candidates = CreateWorkspaceToolCandidates();
        var declaration = TryCreateDeclaration(capability, candidates);
        if (declaration is null)
        {
            hasApprovalTools = false;
            return false;
        }

        if (suppressApprovalRequirements ||
            !declaration.Enabled ||
            !IsCatalogCapabilityAllowed(capability))
        {
            hasApprovalTools = false;
            return true;
        }

        hasApprovalTools = declaration.IsWorkspacePlugin
            ? candidates
                .Where(candidate => IsConfiguredWorkspaceToolAllowed(candidate.RuntimeToolName))
                .Any(candidate =>
                    candidate.RequiresApproval ||
                    IsWorkspacePluginApprovalRequired(candidate.RuntimeToolName) ||
                    declaration.ApprovalRequired)
            : candidates.Any(candidate =>
                string.Equals(
                    candidate.RuntimeToolName,
                    declaration.RuntimeToolName,
                    StringComparison.OrdinalIgnoreCase) &&
                AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(
                    workspaceToolAccess,
                    candidate.RuntimeToolName) &&
                (candidate.RequiresApproval || declaration.ApprovalRequired));
        return true;
    }

    private IReadOnlyList<WorkspaceToolCandidate> CreateWorkspaceToolCandidates()
        =>
        [
            new(ToolContractCatalog.WorkspaceExecutionBoundary, description => AIFunctionFactory.Create(workspacePlugin.GetWorkspaceExecutionBoundary, ToolContractCatalog.WorkspaceExecutionBoundary, description), "Describes the effective tool-execution boundary and whether the host provides real sandboxing."),
                new(ToolContractCatalog.WorkspaceListDirectory, description => AIFunctionFactory.Create(filesystemPlugin.ListWorkspaceDirectory, ToolContractCatalog.WorkspaceListDirectory, description), "Lists direct child files and directories from the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt. Use this before recursive browsing when the folder shape is unknown."),
                new(ToolContractCatalog.WorkspaceListFiles, description => AIFunctionFactory.Create(filesystemPlugin.ListWorkspaceFiles, ToolContractCatalog.WorkspaceListFiles, description), "Lists files and directories from the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt. searchPattern uses glob syntax, not regex; examples: *quote*.pdf and **/*.pdf. For project-structure assets, prefer project_structure_read and project_structure_asset_content_get before browsing."),
                new(ToolContractCatalog.WorkspaceSearch, description => AIFunctionFactory.Create(filesystemPlugin.SearchWorkspace, ToolContractCatalog.WorkspaceSearch, description), "Searches text across the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt. Use project-structure tools for project asset discovery; this is text search, not binary media discovery."),
                new(ToolContractCatalog.WorkspaceReadFile, description => AIFunctionFactory.Create(filesystemPlugin.ReadWorkspaceTextFile, ToolContractCatalog.WorkspaceReadFile, description), "Reads text files from the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt."),
                new(ToolContractCatalog.WorkspaceStatPath, description => AIFunctionFactory.Create(filesystemPlugin.StatWorkspacePath, ToolContractCatalog.WorkspaceStatPath, description), "Returns file or directory metadata for a managed workspace path, configured external workspace root, or prompt-grounded absolute external path."),
                new(ToolContractCatalog.WorkspaceHashPath, description => AIFunctionFactory.Create(filesystemPlugin.HashWorkspacePath, ToolContractCatalog.WorkspaceHashPath, description), "Computes a bounded SHA-256 hash for an allowed file or directory manifest."),
                new(ToolContractCatalog.WorkspaceDiffText, description => AIFunctionFactory.Create(filesystemPlugin.DiffWorkspaceTextFiles, ToolContractCatalog.WorkspaceDiffText, description), "Computes a bounded line-level diff between two allowed workspace text files."),
                new(ToolContractCatalog.WorkspaceGitStatus, description => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceStatus, ToolContractCatalog.WorkspaceGitStatus, description), "Runs a bounded git status recipe in the managed workspace or configured external workspace root."),
                new(ToolContractCatalog.WorkspaceGitDiff, description => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceDiff, ToolContractCatalog.WorkspaceGitDiff, description), "Runs a bounded git diff recipe in the managed workspace or configured external workspace root."),
                new(ToolContractCatalog.WorkspaceGitLog, description => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceLog, ToolContractCatalog.WorkspaceGitLog, description), "Runs a bounded git log recipe in the managed workspace or configured external workspace root."),
                new(ToolContractCatalog.WorkspaceGitShow, description => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceShow, ToolContractCatalog.WorkspaceGitShow, description), "Runs a bounded git show recipe for a validated revision in the managed workspace or configured external workspace root."),
                new(ToolContractCatalog.WorkspaceInspectSpreadsheet, description => AIFunctionFactory.Create(workspacePlugin.InspectSpreadsheetFile, ToolContractCatalog.WorkspaceInspectSpreadsheet, description), "Inspects a workspace .xls, .xlsx, .csv, or .tsv file and returns a compact preview."),
                new(ToolContractCatalog.WorkspaceSpreadsheetSummary, description => AIFunctionFactory.Create(spreadsheetPlugin.InspectWorkbook, ToolContractCatalog.WorkspaceSpreadsheetSummary, description), "Returns worksheet names, positions, used ranges, and used dimensions for a workspace .xlsx workbook."),
                new(ToolContractCatalog.WorkspaceReadSpreadsheetCell, description => AIFunctionFactory.Create(spreadsheetPlugin.ReadSpreadsheetCell, ToolContractCatalog.WorkspaceReadSpreadsheetCell, description), "Reads one cell from a workspace .xlsx workbook. Formula cells return the A1 formula string with a leading equals sign."),
                new(ToolContractCatalog.WorkspaceReadSpreadsheetRange, description => AIFunctionFactory.Create(spreadsheetPlugin.ReadSpreadsheetRange, ToolContractCatalog.WorkspaceReadSpreadsheetRange, description), "Reads a bounded cell range from a workspace .xlsx workbook and returns values plus a markdown table. Formula cells return A1 formula strings with leading equals signs."),
                new(ToolContractCatalog.WorkspaceSpreadsheetFunctionCatalog, description => AIFunctionFactory.Create(spreadsheetPlugin.ListSpreadsheetFunctions, ToolContractCatalog.WorkspaceSpreadsheetFunctionCatalog, description), "Lists common Excel-compatible formula functions with syntax and examples for building spreadsheet cells."),
                new(ToolContractCatalog.WorkspaceInspectImage, description => AIFunctionFactory.Create(workspacePlugin.InspectImageFile, ToolContractCatalog.WorkspaceInspectImage, description), "Inspects a workspace PNG, JPEG, or GIF image and returns format, dimensions, and byte size before asset storage."),
                new(ToolContractCatalog.WorkspaceCreateDirectory, description => AIFunctionFactory.Create(filesystemPlugin.CreateWorkspaceDirectory, ToolContractCatalog.WorkspaceCreateDirectory, description), "Creates a directory in the managed workspace or configured external workspace root.", true),
                new(ToolContractCatalog.WorkspaceWriteFile, description => AIFunctionFactory.Create(filesystemPlugin.WriteWorkspaceTextFile, ToolContractCatalog.WorkspaceWriteFile, description), "Creates or overwrites a text file in the managed workspace or configured external workspace root.", true),
                new(ToolContractCatalog.WorkspaceAppendFile, description => AIFunctionFactory.Create(filesystemPlugin.AppendWorkspaceTextFile, ToolContractCatalog.WorkspaceAppendFile, description), "Appends text to a workspace file in the managed workspace or configured external workspace root.", true),
                new(ToolContractCatalog.WorkspaceZipPath, description => AIFunctionFactory.Create(filesystemPlugin.ZipWorkspacePath, ToolContractCatalog.WorkspaceZipPath, description), "Creates a bounded zip archive from an allowed workspace file or directory.", true),
                new(ToolContractCatalog.WorkspaceUnzipArchive, description => AIFunctionFactory.Create(filesystemPlugin.UnzipWorkspaceArchive, ToolContractCatalog.WorkspaceUnzipArchive, description), "Extracts a bounded workspace zip archive into an allowed destination directory.", true),
                new(ToolContractCatalog.WorkspaceWriteSpreadsheet, description => AIFunctionFactory.Create(spreadsheetPlugin.WriteSpreadsheetWorkbook, ToolContractCatalog.WorkspaceWriteSpreadsheet, description), SpreadsheetWriteDescription, true),
                new(ToolContractCatalog.WorkspaceCopyPath, description => AIFunctionFactory.Create(filesystemPlugin.CopyWorkspacePath, ToolContractCatalog.WorkspaceCopyPath, description), "Copies a file or directory inside allowed workspace roots.", true),
                new(ToolContractCatalog.WorkspaceMovePath, description => AIFunctionFactory.Create(filesystemPlugin.MoveWorkspacePath, ToolContractCatalog.WorkspaceMovePath, description), "Moves or renames a file or directory inside allowed workspace roots.", true),
                new(ToolContractCatalog.WorkspaceDeletePath, description => AIFunctionFactory.Create(filesystemPlugin.DeleteWorkspacePath, ToolContractCatalog.WorkspaceDeletePath, description), "Deletes a file or directory inside allowed workspace roots.", true),
                new(ToolContractCatalog.WorkspaceGitAdd, description => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceAdd, ToolContractCatalog.WorkspaceGitAdd, description), "Stages allowed workspace paths through a bounded git add recipe.", true),
                new(ToolContractCatalog.WorkspaceGitUnstage, description => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceUnstage, ToolContractCatalog.WorkspaceGitUnstage, description), "Unstages allowed workspace paths through a bounded git restore --staged recipe.", true),
                new(ToolContractCatalog.WorkspaceGitCommit, description => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceCommit, ToolContractCatalog.WorkspaceGitCommit, description), "Creates a local git commit with a masked commit message argument.", true),
                new(ToolContractCatalog.WorkspaceGitBranchCreate, description => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceBranchCreate, ToolContractCatalog.WorkspaceGitBranchCreate, description), "Creates a local git branch using a validated branch name.", true),
                new(ToolContractCatalog.WorkspaceGitSwitch, description => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceSwitch, ToolContractCatalog.WorkspaceGitSwitch, description), "Switches to a local git branch using a validated branch name.", true),
                new(ToolContractCatalog.WorkspaceDotNetRestore, description => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRestore, ToolContractCatalog.WorkspaceDotNetRestore, description), "Runs a bounded dotnet restore recipe in the managed workspace or configured external workspace root."),
                new(ToolContractCatalog.WorkspaceDotNetBuild, description => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceBuild, ToolContractCatalog.WorkspaceDotNetBuild, description), "Runs a bounded dotnet build recipe in the managed workspace or configured external workspace root."),
                new(ToolContractCatalog.WorkspaceDotNetTest, description => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceTest, ToolContractCatalog.WorkspaceDotNetTest, description), "Runs a bounded dotnet test recipe in the managed workspace or configured external workspace root."),
                new(ToolContractCatalog.WorkspaceDotNetRun, description => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRun, ToolContractCatalog.WorkspaceDotNetRun, description), "Runs a bounded dotnet run recipe or loopback HTTP startup smoke in the managed workspace or configured external workspace root. HTTP smoke stops the launched process tree by default. Use keepAlive true with lifetimeScope ExecutionRun for same-step browser proof, or lifetimeScope ProcessRun only when a later process step owns capture and cleanup."),
                new(ToolContractCatalog.WorkspaceDotNetStop, description => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceStop, ToolContractCatalog.WorkspaceDotNetStop, description), "Stops a kept-alive workspace_dotnet_run process tree using its startup.json receipt and records cleanup evidence next to that receipt."),
                new(ToolContractCatalog.WorkspaceDotNetNew, description => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceNew, ToolContractCatalog.WorkspaceDotNetNew, description), "Creates a bounded dotnet project or solution in the managed workspace or configured external workspace root.", true),
                new(ToolContractCatalog.WorkspacePythonRunFile, description => AIFunctionFactory.Create(workspacePlugin.RunWorkspacePythonFile, ToolContractCatalog.WorkspacePythonRunFile, description), "Runs a workspace Python file with structured arguments through the controlled execution plane.", true),
                new(ToolContractCatalog.WorkspacePowerShellRunScript, description => AIFunctionFactory.Create(workspacePlugin.RunWorkspacePowerShellScript, ToolContractCatalog.WorkspacePowerShellRunScript, description), "Runs a workspace PowerShell script in non-interactive mode through the controlled execution plane.", true),
                new(ToolContractCatalog.WorkspaceConvertDocument, description => AIFunctionFactory.Create(workspacePlugin.ConvertDocumentToMarkdown, ToolContractCatalog.WorkspaceConvertDocument, description), "Converts a workspace PDF or document to markdown using ManagedCode.MarkItDown, writes a managed converted-documents artifact by default, and returns a markdown preview/output path. For project-structure document assets, follow the exact next action returned by project_structure_asset_content_get. Do not use this tool for images; use project_structure_asset_image_analyze by node id for project images and workspace image tools only for authorized workspace paths."),
                new(ToolContractCatalog.WorkspaceAnalyzeImage, description => AIFunctionFactory.Create(workspacePlugin.AnalyzeImageFile, ToolContractCatalog.WorkspaceAnalyzeImage, description), "Analyzes a workspace PNG, JPEG, or GIF image through the provider's vision-capable analysis model and returns visible evidence plus provider token counts."),
                new(ToolContractCatalog.WorkspaceAnalyzeImages, description => AIFunctionFactory.Create(workspacePlugin.AnalyzeImageFiles, ToolContractCatalog.WorkspaceAnalyzeImages, description), "Analyzes two or more workspace images together through the provider's vision-capable analysis model and returns visible comparison evidence plus provider token counts.")
        ];

    private List<AITool> CreateWorkspaceTools(
        IReadOnlyList<CapabilityCatalogItem> catalogCapabilities,
        bool includeConfiguredTools,
        bool suppressApprovalRequirements)
    {
        var candidates = CreateWorkspaceToolCandidates();
        var declarations = catalogCapabilities
            .Where(IsCatalogCapabilityAllowed)
            .Select(capability => TryCreateDeclaration(capability, candidates))
            .OfType<WorkspaceToolCapabilityDeclaration>()
            .Where(declaration => declaration.Enabled)
            .ToList();
        var workspacePluginDeclarations = declarations
            .Where(declaration => declaration.IsWorkspacePlugin)
            .ToList();
        var hasWorkspacePlugin = workspacePluginDeclarations.Count > 0;
        var workspacePluginRequiresApproval = workspacePluginDeclarations.Any(declaration => declaration.ApprovalRequired);
        var tools = new List<AITool>();

        foreach (var candidate in candidates)
        {
            if (!AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(
                    workspaceToolAccess,
                    candidate.RuntimeToolName))
            {
                continue;
            }

            var explicitDeclarations = declarations
                .Where(declaration =>
                    !declaration.IsWorkspacePlugin &&
                    string.Equals(
                        declaration.RuntimeToolName,
                        candidate.RuntimeToolName,
                        StringComparison.OrdinalIgnoreCase))
                .ToList();
            var configuredOrPluginIncludesCandidate =
                (includeConfiguredTools || hasWorkspacePlugin) &&
                IsConfiguredWorkspaceToolAllowed(candidate.RuntimeToolName);
            if (!configuredOrPluginIncludesCandidate && explicitDeclarations.Count == 0)
            {
                continue;
            }

            var description = ResolveWorkspaceToolDescription(
                candidate,
                explicitDeclarations,
                hasWorkspacePlugin);
            var requiresApproval = candidate.RequiresApproval ||
                                   explicitDeclarations.Any(declaration => declaration.ApprovalRequired) ||
                                   hasWorkspacePlugin &&
                                   (workspacePluginRequiresApproval ||
                                    IsWorkspacePluginApprovalRequired(candidate.RuntimeToolName));
            tools.Add(CreateWorkspaceTool(
                candidate,
                description,
                requiresApproval,
                suppressApprovalRequirements));
        }

        return tools;
    }

    private static string ResolveWorkspaceToolDescription(
        WorkspaceToolCandidate candidate,
        IReadOnlyList<WorkspaceToolCapabilityDeclaration> explicitDeclarations,
        bool hasWorkspacePlugin)
    {
        var explicitDescription = explicitDeclarations
            .Where(declaration => !string.IsNullOrWhiteSpace(declaration.Capability.Description))
            .OrderBy(declaration => declaration.Capability.Key, StringComparer.OrdinalIgnoreCase)
            .ThenBy(declaration => declaration.Capability.Id)
            .Select(declaration => declaration.Capability.Description.Trim())
            .FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(explicitDescription))
        {
            return explicitDescription;
        }

        return hasWorkspacePlugin
            ? ResolveWorkspacePluginDescription(candidate)
            : candidate.ConfiguredDescription;
    }

    private static WorkspaceToolCapabilityDeclaration? TryCreateDeclaration(
        CapabilityCatalogItem capability,
        IReadOnlyList<WorkspaceToolCandidate> candidates)
    {
        if (capability.Kind != CapabilityKind.Tool)
        {
            return null;
        }

        var configuration = MafRuntimeJson.DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson) ??
                            new BuiltInToolConfiguration();
        var toolKey = configuration.Tool ?? capability.Key;
        var normalizedToolKey = NormalizeWorkspaceToolKey(toolKey);
        if (string.Equals(normalizedToolKey, "workspace_plugin", StringComparison.OrdinalIgnoreCase))
        {
            return new WorkspaceToolCapabilityDeclaration(
                capability,
                RuntimeToolName: null,
                IsWorkspacePlugin: true,
                ApprovalRequired: configuration.ApprovalRequired == true,
                Enabled: configuration.Enabled != false);
        }

        var candidate = candidates.FirstOrDefault(candidate =>
            string.Equals(
                candidate.RuntimeToolName,
                normalizedToolKey,
                StringComparison.OrdinalIgnoreCase));
        return string.IsNullOrWhiteSpace(candidate.RuntimeToolName)
            ? null
            : new WorkspaceToolCapabilityDeclaration(
                capability,
                candidate.RuntimeToolName,
                IsWorkspacePlugin: false,
                ApprovalRequired: configuration.ApprovalRequired == true,
                Enabled: configuration.Enabled != false);
    }

    private static string NormalizeWorkspaceToolKey(string? toolKey)
        => string.IsNullOrWhiteSpace(toolKey)
            ? string.Empty
            : toolKey.Trim().Replace('-', '_').ToLowerInvariant();

    private bool IsConfiguredWorkspaceToolAllowed(string runtimeToolName)
        => AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(workspaceToolAccess, runtimeToolName) &&
               IsConfiguredRuntimeToolAllowed(runtimeToolName);

    private bool IsCatalogCapabilityAllowed(CapabilityCatalogItem capability)
        => capabilityAccessPlan.AllowedCatalogCapabilities.Any(allowed => allowed.Id == capability.Id);

    private bool IsConfiguredRuntimeToolAllowed(string toolKey)
    {
        if (!RuntimeToolCapabilityDescriptorFactory.TryCreateRuntimeToolName(toolKey, out var runtimeToolName))
        {
            return false;
        }

        var configuredTag = AccessCapabilityTag.Create("configured");
        return capabilityAccessPlan.InitialAllowedCapabilities.Any(capability =>
            capability.RuntimeToolName == runtimeToolName &&
            capability.Tags.Contains(configuredTag));
    }

    private void AddConfiguredToolIfAllowed(
        List<AITool> tools,
        string runtimeToolName,
        Func<AITool> createTool)
    {
        if (IsConfiguredWorkspaceToolAllowed(runtimeToolName))
        {
            tools.Add(createTool());
        }
    }

    private static string ResolveWorkspacePluginDescription(WorkspaceToolCandidate candidate)
        => candidate.RuntimeToolName switch
        {
            ToolContractCatalog.WorkspaceListDirectory => "Lists direct child files and directories from the managed workspace or a grounded external-target alias. Use this before recursive browsing when the folder shape is unknown.",
            ToolContractCatalog.WorkspaceListFiles => "Lists files and directories from the managed workspace or a grounded external-target alias. searchPattern uses glob syntax, not regex; examples: *quote*.pdf, **/*, and **/*.cs. In external-target process runs, broad managed-root browsing is denied; list current-run artifacts or the grounded product alias instead. For project-structure assets, prefer project_structure_read and project_structure_asset_content_get before browsing.",
            ToolContractCatalog.WorkspaceSearch => "Searches text across the current workspace; external-target paths require explicit current-run grounding. Use project-structure tools for project asset discovery; this is text search, not binary media discovery. In external-target process runs, broad managed-root search is denied; search current-run artifacts or the grounded product alias instead.",
            ToolContractCatalog.WorkspaceReadFile => "Reads text files from the managed workspace or a grounded external-target alias. In external-target process runs, do not read unmanaged source or helper roots unless they are current-run artifacts.",
            ToolContractCatalog.WorkspaceStatPath => "Returns file or directory metadata for a managed workspace path or grounded external-target alias. In external-target process runs, prefer current-run artifacts and the grounded product alias.",
            ToolContractCatalog.WorkspaceHashPath => "Computes a bounded SHA-256 hash for a workspace file or directory manifest.",
            ToolContractCatalog.WorkspaceDiffText => "Computes a bounded line-level diff between two workspace text files.",
            ToolContractCatalog.WorkspaceGitStatus => "Runs a bounded git status recipe in the current workspace.",
            ToolContractCatalog.WorkspaceGitDiff => "Runs a bounded git diff recipe in the current workspace.",
            ToolContractCatalog.WorkspaceGitLog => "Runs a bounded git log recipe in the current workspace.",
            ToolContractCatalog.WorkspaceGitShow => "Runs a bounded git show recipe for a validated revision in the current workspace.",
            ToolContractCatalog.WorkspaceCreateDirectory => "Creates a directory in the managed workspace or a grounded external-target alias. In external-target process runs, keep product material under a grounded external-target alias and evidence under current-run artifact folders.",
            ToolContractCatalog.WorkspaceWriteFile => "Creates or overwrites a text file in the managed workspace or a grounded external-target alias. In external-target process runs, write product material only under a grounded external-target alias and evidence only under current-run artifact folders.",
            ToolContractCatalog.WorkspaceAppendFile => "Appends text to a workspace file. In external-target process runs, write product material only under a grounded external-target alias and evidence only under current-run artifact folders.",
            ToolContractCatalog.WorkspaceZipPath => "Creates a bounded zip archive from a workspace file or directory.",
            ToolContractCatalog.WorkspaceCopyPath => "Copies a file or directory inside the current workspace.",
            ToolContractCatalog.WorkspaceMovePath => "Moves or renames a file or directory inside the current workspace.",
            ToolContractCatalog.WorkspaceDeletePath => "Deletes a file or directory inside the current workspace.",
            ToolContractCatalog.WorkspaceDotNetRestore => "Runs a bounded dotnet restore recipe in the managed workspace or a grounded external-target alias.",
            ToolContractCatalog.WorkspaceDotNetBuild => "Runs a bounded dotnet build recipe in the managed workspace or a grounded external-target alias. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying.",
            ToolContractCatalog.WorkspaceDotNetTest => "Runs a bounded dotnet test recipe in the managed workspace or a grounded external-target alias. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying.",
            ToolContractCatalog.WorkspaceDotNetRun => "Runs a bounded dotnet run recipe or loopback HTTP startup smoke in the managed workspace or a grounded external-target alias. Use lifetimeScope ProcessRun only when the process graph has a later cleanup step. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying.",
            ToolContractCatalog.WorkspaceDotNetStop => "Stops a kept-alive workspace_dotnet_run process tree from its startup.json receipt and writes cleanup.json proof next to the receipt. Use this instead of workspace_pwsh_run_script for runtime cleanup.",
            ToolContractCatalog.WorkspaceDotNetNew => "Creates a bounded dotnet project or solution in the managed workspace or a grounded external-target alias. Use an approved SDK template, inspect an unsuccessful result before retrying, and pass an allowed workspace parentDirectory. Timeout arguments are seconds, not milliseconds; prefer the default or use normal second values such as 120 or 300. In an external-target process run, do not substitute an ungrounded external path or unrelated managed path for the product target.",
            _ => candidate.ConfiguredDescription
        };

    private static bool IsWorkspacePluginApprovalRequired(string runtimeToolName)
        => string.Equals(
            runtimeToolName,
            ToolContractCatalog.WorkspaceDotNetRestore,
            StringComparison.OrdinalIgnoreCase);

    private static AITool CreateWorkspaceTool(
        WorkspaceToolCandidate candidate,
        string description,
        bool requiresApproval,
        bool suppressApprovalRequirements)
    {
        var tool = candidate.CreateTool(description);
        return requiresApproval
            ? WrapWithApproval(tool, suppressApprovalRequirements)
            : tool;
    }

    private static AITool WrapWithApproval(AITool tool, bool suppressApprovalRequirements = false)
    {
        return !suppressApprovalRequirements && tool is AIFunction function
            ? new ApprovalRequiredAIFunction(function)
            : tool;
    }

    private readonly record struct WorkspaceToolCandidate(
        string RuntimeToolName,
        Func<string, AITool> CreateTool,
        string ConfiguredDescription,
        bool RequiresApproval = false);

    private sealed record WorkspaceToolCapabilityDeclaration(
        CapabilityCatalogItem Capability,
        string? RuntimeToolName,
        bool IsWorkspacePlugin,
        bool ApprovalRequired,
        bool Enabled);
}
