using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using AccessCapabilityTag = CanDoItAll.AgentFramework.Capabilities.Abstractions.CapabilityTag;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ConfiguredWorkspaceToolSet(
    AgentWorkspaceToolAccessSettings workspaceToolAccess,
    WorkspaceFilesystemRuntimePlugin filesystemPlugin,
    WorkspaceRuntimePlugin workspacePlugin,
    WorkspaceSpreadsheetRuntimePlugin spreadsheetPlugin,
    StorageRuntimePlugin? storagePlugin,
    RuntimeCapabilityAccessPlan capabilityAccessPlan)
{
        private readonly AgentWorkspaceToolAccessSettings workspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);

        public IReadOnlyList<AITool> CreateTools(
            AgentDefinition agent,
            bool suppressApprovalRequirements = false)
        {
            var access = workspaceToolAccess;
            var auditScope = WorkspaceExecutionAuditContext.Current;
            var hasGroundedExternalPath = auditScope is not null &&
                                          (auditScope.AllowedExternalTargetAliases.Count > 0 ||
                                           auditScope.ReadOnlyExternalTargetAliases.Count > 0);
            var tools = new List<AITool>();
            var attachFileTools = access.AllowedExternalTargetAliases.Count > 0 ||
                                  access.CanWriteFiles ||
                                  access.CanRunValidationCommands ||
                                  access.CanRunLocalScripts ||
                                  access.CanScaffoldProjects ||
                                  access.CanManageWorkspacePaths ||
                                  access.CanTransformArtifacts ||
                                  hasGroundedExternalPath;
            if (attachFileTools && (access.CanReadFiles || access.CanWriteFiles))
            {
                AddConfiguredToolIfAllowed(tools, "workspace_execution_boundary", () => AIFunctionFactory.Create(workspacePlugin.GetWorkspaceExecutionBoundary, "workspace_execution_boundary", "Describes the effective tool-execution boundary and whether the host provides real sandboxing."));
                AddConfiguredToolIfAllowed(tools, "workspace_list_directory", () => AIFunctionFactory.Create(filesystemPlugin.ListWorkspaceDirectory, "workspace_list_directory", "Lists direct child files and directories from the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt. Use this before recursive browsing when the folder shape is unknown."));
                AddConfiguredToolIfAllowed(tools, "workspace_list_files", () => AIFunctionFactory.Create(filesystemPlugin.ListWorkspaceFiles, "workspace_list_files", "Lists files and directories from the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt. searchPattern uses glob syntax, not regex; examples: *quote*.pdf and **/*.pdf. For project-structure assets, prefer project_structure_read and project_structure_asset_content_get before browsing."));
                AddConfiguredToolIfAllowed(tools, "workspace_search", () => AIFunctionFactory.Create(filesystemPlugin.SearchWorkspace, "workspace_search", "Searches text across the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt. Use project-structure tools for project asset discovery; this is text search, not binary media discovery."));
                AddConfiguredToolIfAllowed(tools, "workspace_read_file", () => AIFunctionFactory.Create(filesystemPlugin.ReadWorkspaceTextFile, "workspace_read_file", "Reads text files from the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt."));
                AddConfiguredToolIfAllowed(tools, "workspace_stat_path", () => AIFunctionFactory.Create(filesystemPlugin.StatWorkspacePath, "workspace_stat_path", "Returns file or directory metadata for a managed workspace path, configured external workspace root, or prompt-grounded absolute external path."));
                AddConfiguredToolIfAllowed(tools, "workspace_hash_path", () => AIFunctionFactory.Create(filesystemPlugin.HashWorkspacePath, "workspace_hash_path", "Computes a bounded SHA-256 hash for an allowed file or directory manifest."));
                AddConfiguredToolIfAllowed(tools, "workspace_diff_text", () => AIFunctionFactory.Create(filesystemPlugin.DiffWorkspaceTextFiles, "workspace_diff_text", "Computes a bounded line-level diff between two allowed workspace text files."));
                AddConfiguredToolIfAllowed(tools, "workspace_git_status", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceStatus, "workspace_git_status", "Runs a bounded git status recipe in the managed workspace or configured external workspace root."));
                AddConfiguredToolIfAllowed(tools, "workspace_git_diff", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceDiff, "workspace_git_diff", "Runs a bounded git diff recipe in the managed workspace or configured external workspace root."));
                AddConfiguredToolIfAllowed(tools, "workspace_git_log", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceLog, "workspace_git_log", "Runs a bounded git log recipe in the managed workspace or configured external workspace root."));
                AddConfiguredToolIfAllowed(tools, "workspace_git_show", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceShow, "workspace_git_show", "Runs a bounded git show recipe for a validated revision in the managed workspace or configured external workspace root."));
            }

            if (attachFileTools && access.CanWriteFiles)
            {
                AddConfiguredToolIfAllowed(tools, "workspace_create_directory", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.CreateWorkspaceDirectory, "workspace_create_directory", "Creates a directory in the managed workspace or configured external workspace root."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_write_file", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.WriteWorkspaceTextFile, "workspace_write_file", "Creates or overwrites a text file in the managed workspace or configured external workspace root."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_append_file", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.AppendWorkspaceTextFile, "workspace_append_file", "Appends text to a workspace file in the managed workspace or configured external workspace root."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_zip_path", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.ZipWorkspacePath, "workspace_zip_path", "Creates a bounded zip archive from an allowed workspace file or directory."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_unzip_archive", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.UnzipWorkspaceArchive, "workspace_unzip_archive", "Extracts a bounded workspace zip archive into an allowed destination directory."), suppressApprovalRequirements));
            }

            if (attachFileTools && access.CanManageWorkspacePaths)
            {
                AddConfiguredToolIfAllowed(tools, "workspace_copy_path", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.CopyWorkspacePath, "workspace_copy_path", "Copies a file or directory inside allowed workspace roots."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_move_path", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.MoveWorkspacePath, "workspace_move_path", "Moves or renames a file or directory inside allowed workspace roots."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_delete_path", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.DeleteWorkspacePath, "workspace_delete_path", "Deletes a file or directory inside allowed workspace roots."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_git_add", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceAdd, "workspace_git_add", "Stages allowed workspace paths through a bounded git add recipe."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_git_unstage", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceUnstage, "workspace_git_unstage", "Unstages allowed workspace paths through a bounded git restore --staged recipe."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_git_commit", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceCommit, "workspace_git_commit", "Creates a local git commit with a masked commit message argument."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_git_branch_create", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceBranchCreate, "workspace_git_branch_create", "Creates a local git branch using a validated branch name."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_git_switch", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceSwitch, "workspace_git_switch", "Switches to a local git branch using a validated branch name."), suppressApprovalRequirements));
            }

            if (attachFileTools && access.CanRunValidationCommands)
            {
                AddConfiguredToolIfAllowed(tools, "workspace_dotnet_restore", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRestore, "workspace_dotnet_restore", "Runs a bounded dotnet restore recipe in the managed workspace or configured external workspace root."));
                AddConfiguredToolIfAllowed(tools, "workspace_dotnet_build", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceBuild, "workspace_dotnet_build", "Runs a bounded dotnet build recipe in the managed workspace or configured external workspace root."));
                AddConfiguredToolIfAllowed(tools, "workspace_dotnet_test", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceTest, "workspace_dotnet_test", "Runs a bounded dotnet test recipe in the managed workspace or configured external workspace root."));
                AddConfiguredToolIfAllowed(tools, "workspace_dotnet_run", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRun, "workspace_dotnet_run", "Runs a bounded dotnet run recipe or loopback HTTP startup smoke in the managed workspace or configured external workspace root. HTTP smoke stops the launched process tree by default. Use keepAlive true with lifetimeScope ExecutionRun for same-step browser proof, or lifetimeScope ProcessRun only when a later process step owns capture and cleanup."));
                AddConfiguredToolIfAllowed(tools, "workspace_dotnet_stop", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceStop, "workspace_dotnet_stop", "Stops a kept-alive workspace_dotnet_run process tree using its startup.json receipt and records cleanup evidence next to that receipt."));
            }

            if (attachFileTools && access.CanScaffoldProjects)
            {
                AddConfiguredToolIfAllowed(tools, "workspace_dotnet_new", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceNew, "workspace_dotnet_new", "Creates a bounded dotnet project or solution scaffold in the managed workspace or configured external workspace root. For a grounded product root, create the solution at the product root and create app/test projects under child folders such as src or tests."), suppressApprovalRequirements));
            }

            if (attachFileTools && access.CanRunLocalScripts)
            {
                AddConfiguredToolIfAllowed(tools, "workspace_python_run_file", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.RunWorkspacePythonFile, "workspace_python_run_file", "Runs a workspace Python file with structured arguments through the controlled execution plane."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "workspace_pwsh_run_script", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.RunWorkspacePowerShellScript, "workspace_pwsh_run_script", "Runs a workspace PowerShell script in non-interactive mode through the controlled execution plane."), suppressApprovalRequirements));
            }

            if (attachFileTools && access.CanTransformArtifacts)
            {
                AddConfiguredToolIfAllowed(tools, "workspace_convert_document", () => AIFunctionFactory.Create(workspacePlugin.ConvertDocumentToMarkdown, "workspace_convert_document", "Converts a workspace PDF or document to markdown using ManagedCode.MarkItDown, writes a managed converted-documents artifact by default, and returns a markdown preview/output path. For project-structure assets, use the exact mediaRelativePath returned by project_structure_asset_content_get. Do not use for image assets; use workspace_inspect_image or workspace_analyze_image."));
                AddConfiguredToolIfAllowed(tools, "workspace_inspect_spreadsheet", () => AIFunctionFactory.Create(workspacePlugin.InspectSpreadsheetFile, "workspace_inspect_spreadsheet", "Inspects a workspace .xls, .xlsx, .csv, or .tsv file and returns a compact preview."));
                AddConfiguredToolIfAllowed(tools, "workspace_spreadsheet_summary", () => AIFunctionFactory.Create(spreadsheetPlugin.InspectWorkbook, "workspace_spreadsheet_summary", "Returns worksheet names, positions, used ranges, and used dimensions for a workspace .xlsx workbook."));
                AddConfiguredToolIfAllowed(tools, "workspace_read_spreadsheet_cell", () => AIFunctionFactory.Create(spreadsheetPlugin.ReadSpreadsheetCell, "workspace_read_spreadsheet_cell", "Reads one cell from a workspace .xlsx workbook. Formula cells return the A1 formula string with a leading equals sign."));
                AddConfiguredToolIfAllowed(tools, "workspace_read_spreadsheet_range", () => AIFunctionFactory.Create(spreadsheetPlugin.ReadSpreadsheetRange, "workspace_read_spreadsheet_range", "Reads a bounded cell range from a workspace .xlsx workbook and returns values plus a markdown table. Formula cells return A1 formula strings with leading equals signs."));
                AddConfiguredToolIfAllowed(tools, "workspace_spreadsheet_function_catalog", () => AIFunctionFactory.Create(spreadsheetPlugin.ListSpreadsheetFunctions, "workspace_spreadsheet_function_catalog", "Lists common Excel-compatible formula functions with syntax and examples for building spreadsheet cells."));
                AddConfiguredToolIfAllowed(tools, "workspace_inspect_image", () => AIFunctionFactory.Create(workspacePlugin.InspectImageFile, "workspace_inspect_image", "Inspects a workspace PNG, JPEG, or GIF image and returns format, dimensions, and byte size before asset storage."));
                AddConfiguredToolIfAllowed(tools, "workspace_analyze_image", () => AIFunctionFactory.Create(workspacePlugin.AnalyzeImageFile, "workspace_analyze_image", "Analyzes a workspace PNG, JPEG, or GIF image through the provider's vision-capable analysis model and returns visible evidence plus provider token counts."));
                AddConfiguredToolIfAllowed(tools, "workspace_analyze_images", () => AIFunctionFactory.Create(workspacePlugin.AnalyzeImageFiles, "workspace_analyze_images", "Analyzes two or more workspace screenshot images together through the provider's vision-capable analysis model and returns visible comparison evidence plus provider token counts."));
            }

            if (attachFileTools && access.CanWriteFiles)
            {
                AddConfiguredToolIfAllowed(tools, "workspace_write_spreadsheet", () => WrapWithApproval(AIFunctionFactory.Create(spreadsheetPlugin.WriteSpreadsheetWorkbook, "workspace_write_spreadsheet", "Creates or updates a workspace .xlsx workbook and worksheet using typed cell and range writes. Values beginning with = are stored as formulas. Creates missing workbooks and worksheets when requested."), suppressApprovalRequirements));
            }

            if (storagePlugin is not null && (access.CanReadStorage || access.CanWriteStorage))
            {
                AddConfiguredToolIfAllowed(tools, "storage_catalog_list", () => AIFunctionFactory.Create(storagePlugin.ListStorageCatalogs, "storage_catalog_list", "Lists storage catalogs this agent is allowed to use."));
                AddConfiguredToolIfAllowed(tools, "storage_read_text_file", () => AIFunctionFactory.Create(storagePlugin.ReadStorageTextFile, "storage_read_text_file", "Reads a text object from an allowed storage catalog through the configured storage driver."));
            }

            if (storagePlugin is not null && access.CanWriteStorage)
            {
                AddConfiguredToolIfAllowed(tools, "storage_write_text_file", () => WrapWithApproval(AIFunctionFactory.Create(storagePlugin.WriteStorageTextFile, "storage_write_text_file", "Writes a text object to an allowed storage catalog through the configured storage driver."), suppressApprovalRequirements));
                AddConfiguredToolIfAllowed(tools, "storage_delete_object", () => WrapWithApproval(AIFunctionFactory.Create(storagePlugin.DeleteStorageObject, "storage_delete_object", "Deletes an object from an allowed storage catalog through the configured storage driver."), suppressApprovalRequirements));
            }

            return tools;
        }

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
            if (IsConfiguredRuntimeToolAllowed(runtimeToolName))
            {
                tools.Add(createTool());
            }
        }

        public IReadOnlyList<AITool> CreateWorkspacePluginTools(bool suppressApprovalRequirements)
        {
            var tools = new List<AITool>();
            AddWorkspacePluginTool(tools, "workspace_execution_boundary", () => AIFunctionFactory.Create(workspacePlugin.GetWorkspaceExecutionBoundary, "workspace_execution_boundary", "Describes the effective tool-execution boundary and whether the host provides real sandboxing."));
            AddWorkspacePluginTool(tools, "workspace_list_directory", () => AIFunctionFactory.Create(filesystemPlugin.ListWorkspaceDirectory, "workspace_list_directory", "Lists direct child files and directories from the managed workspace or a grounded external-target alias. Use this before recursive browsing when the folder shape is unknown."));
            AddWorkspacePluginTool(tools, "workspace_list_files", () => AIFunctionFactory.Create(filesystemPlugin.ListWorkspaceFiles, "workspace_list_files", "Lists files and directories from the managed workspace or a grounded external-target alias. searchPattern uses glob syntax, not regex; examples: *quote*.pdf, **/*, and **/*.cs. In external-target process runs, broad managed-root browsing is denied; list current-run artifacts or the grounded product alias instead. For project-structure assets, prefer project_structure_read and project_structure_asset_content_get before browsing."));
            AddWorkspacePluginTool(tools, "workspace_search", () => AIFunctionFactory.Create(filesystemPlugin.SearchWorkspace, "workspace_search", "Searches text across the current workspace; external-target paths require explicit current-run grounding. Use project-structure tools for project asset discovery; this is text search, not binary media discovery. In external-target process runs, broad managed-root search is denied; search current-run artifacts or the grounded product alias instead."));
            AddWorkspacePluginTool(tools, "workspace_read_file", () => AIFunctionFactory.Create(filesystemPlugin.ReadWorkspaceTextFile, "workspace_read_file", "Reads text files from the managed workspace or a grounded external-target alias. In external-target process runs, do not read unmanaged source or helper roots unless they are current-run artifacts."));
            AddWorkspacePluginTool(tools, "workspace_stat_path", () => AIFunctionFactory.Create(filesystemPlugin.StatWorkspacePath, "workspace_stat_path", "Returns file or directory metadata for a managed workspace path or grounded external-target alias. In external-target process runs, prefer current-run artifacts and the grounded product alias."));
            AddWorkspacePluginTool(tools, "workspace_hash_path", () => AIFunctionFactory.Create(filesystemPlugin.HashWorkspacePath, "workspace_hash_path", "Computes a bounded SHA-256 hash for a workspace file or directory manifest."));
            AddWorkspacePluginTool(tools, "workspace_diff_text", () => AIFunctionFactory.Create(filesystemPlugin.DiffWorkspaceTextFiles, "workspace_diff_text", "Computes a bounded line-level diff between two workspace text files."));
            AddWorkspacePluginTool(tools, "workspace_git_status", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceStatus, "workspace_git_status", "Runs a bounded git status recipe in the current workspace."));
            AddWorkspacePluginTool(tools, "workspace_git_diff", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceDiff, "workspace_git_diff", "Runs a bounded git diff recipe in the current workspace."));
            AddWorkspacePluginTool(tools, "workspace_git_log", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceLog, "workspace_git_log", "Runs a bounded git log recipe in the current workspace."));
            AddWorkspacePluginTool(tools, "workspace_git_show", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceShow, "workspace_git_show", "Runs a bounded git show recipe for a validated revision in the current workspace."));
            AddWorkspacePluginTool(tools, "workspace_dotnet_build", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceBuild, "workspace_dotnet_build", "Runs a bounded dotnet build recipe in the managed workspace or a grounded external-target alias. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying."));
            AddWorkspacePluginTool(tools, "workspace_dotnet_test", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceTest, "workspace_dotnet_test", "Runs a bounded dotnet test recipe in the managed workspace or a grounded external-target alias. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying."));
            AddWorkspacePluginTool(tools, "workspace_dotnet_run", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRun, "workspace_dotnet_run", "Runs a bounded dotnet run recipe or loopback HTTP startup smoke in the managed workspace or a grounded external-target alias. Use lifetimeScope ProcessRun only when the process graph has a later cleanup step. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying."));
            AddWorkspacePluginTool(tools, "workspace_dotnet_stop", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceStop, "workspace_dotnet_stop", "Stops a kept-alive workspace_dotnet_run process tree from its startup.json receipt and writes cleanup.json proof next to the receipt. Use this instead of workspace_pwsh_run_script for runtime cleanup."));
            AddWorkspacePluginTool(tools, "workspace_create_directory", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.CreateWorkspaceDirectory, "workspace_create_directory", "Creates a directory in the managed workspace or a grounded external-target alias. In external-target process runs, product source, tests, scripts, and assets must stay under the grounded product alias or current-run artifact folders."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_write_file", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.WriteWorkspaceTextFile, "workspace_write_file", "Creates or overwrites a text file in the managed workspace or a grounded external-target alias. In external-target process runs, product source and tests must be written under the grounded product alias, not managed src/tests/tools roots."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_append_file", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.AppendWorkspaceTextFile, "workspace_append_file", "Appends text to a workspace file. In external-target process runs, product source and tests must be written under the grounded product alias, not managed src/tests/tools roots."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_copy_path", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.CopyWorkspacePath, "workspace_copy_path", "Copies a file or directory inside the current workspace."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_move_path", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.MoveWorkspacePath, "workspace_move_path", "Moves or renames a file or directory inside the current workspace."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_delete_path", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.DeleteWorkspacePath, "workspace_delete_path", "Deletes a file or directory inside the current workspace."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_zip_path", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.ZipWorkspacePath, "workspace_zip_path", "Creates a bounded zip archive from a workspace file or directory."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_unzip_archive", () => WrapWithApproval(AIFunctionFactory.Create(filesystemPlugin.UnzipWorkspaceArchive, "workspace_unzip_archive", "Extracts a bounded workspace zip archive into an allowed destination directory."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_git_add", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceAdd, "workspace_git_add", "Stages allowed workspace paths through a bounded git add recipe."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_git_unstage", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceUnstage, "workspace_git_unstage", "Unstages allowed workspace paths through a bounded git restore --staged recipe."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_git_commit", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceCommit, "workspace_git_commit", "Creates a local git commit with a masked commit message argument."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_git_branch_create", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceBranchCreate, "workspace_git_branch_create", "Creates a local git branch using a validated branch name."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_git_switch", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceSwitch, "workspace_git_switch", "Switches to a local git branch using a validated branch name."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_dotnet_restore", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRestore, "workspace_dotnet_restore", "Runs a bounded dotnet restore recipe in the managed workspace or a grounded external-target alias."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_dotnet_new", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceNew, "workspace_dotnet_new", "Creates a bounded dotnet project or solution scaffold in the managed workspace or a grounded external-target alias. Approved templates include current SDK project templates and sln for empty solution files. Inspect an unsuccessful result before retrying. For an exact output root, pass its parent as parentDirectory and the root leaf as name. Timeout arguments are seconds, not milliseconds; prefer the default or use normal second values such as 120 or 300. For test projects, pass a parentDirectory under the grounded product root, such as <product-root>/tests, with name <AppName>.Tests; never reuse the product parent to create <AppName>.Tests as a sibling. Keep tests and support projects under child folders of the grounded product root unless another root is explicitly grounded. Do not scaffold product or test projects into managed src/tests/tools roots or sibling external-target roots during an external-target run."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_python_run_file", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.RunWorkspacePythonFile, "workspace_python_run_file", "Runs a workspace Python file with structured arguments through the controlled execution plane."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_pwsh_run_script", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.RunWorkspacePowerShellScript, "workspace_pwsh_run_script", "Runs a workspace PowerShell script in non-interactive mode through the controlled execution plane."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_convert_document", () => AIFunctionFactory.Create(workspacePlugin.ConvertDocumentToMarkdown, "workspace_convert_document", "Converts a workspace PDF or document to markdown using ManagedCode.MarkItDown, writes a managed converted-documents artifact by default, and returns a markdown preview/output path. For project-structure assets, use the exact mediaRelativePath returned by project_structure_asset_content_get. Do not use for image assets; use workspace_inspect_image or workspace_analyze_image."));
            AddWorkspacePluginTool(tools, "workspace_inspect_spreadsheet", () => AIFunctionFactory.Create(workspacePlugin.InspectSpreadsheetFile, "workspace_inspect_spreadsheet", "Inspects a workspace .xls, .xlsx, .csv, or .tsv file and returns a compact preview."));
            AddWorkspacePluginTool(tools, "workspace_spreadsheet_summary", () => AIFunctionFactory.Create(spreadsheetPlugin.InspectWorkbook, "workspace_spreadsheet_summary", "Returns worksheet names, positions, used ranges, and used dimensions for a workspace .xlsx workbook."));
            AddWorkspacePluginTool(tools, "workspace_read_spreadsheet_cell", () => AIFunctionFactory.Create(spreadsheetPlugin.ReadSpreadsheetCell, "workspace_read_spreadsheet_cell", "Reads one cell from a workspace .xlsx workbook. Formula cells return the A1 formula string with a leading equals sign."));
            AddWorkspacePluginTool(tools, "workspace_read_spreadsheet_range", () => AIFunctionFactory.Create(spreadsheetPlugin.ReadSpreadsheetRange, "workspace_read_spreadsheet_range", "Reads a bounded cell range from a workspace .xlsx workbook and returns values plus a markdown table. Formula cells return A1 formula strings with leading equals signs."));
            AddWorkspacePluginTool(tools, "workspace_write_spreadsheet", () => WrapWithApproval(AIFunctionFactory.Create(spreadsheetPlugin.WriteSpreadsheetWorkbook, "workspace_write_spreadsheet", "Creates or updates a workspace .xlsx workbook and worksheet using typed cell and range writes. Values beginning with = are stored as formulas. Creates missing workbooks and worksheets when requested."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_spreadsheet_function_catalog", () => AIFunctionFactory.Create(spreadsheetPlugin.ListSpreadsheetFunctions, "workspace_spreadsheet_function_catalog", "Lists common Excel-compatible formula functions with syntax and examples for building spreadsheet cells."));
            AddWorkspacePluginTool(tools, "workspace_inspect_image", () => AIFunctionFactory.Create(workspacePlugin.InspectImageFile, "workspace_inspect_image", "Inspects a workspace PNG, JPEG, or GIF image and returns format, dimensions, and byte size before asset storage."));
            AddWorkspacePluginTool(tools, "workspace_analyze_image", () => AIFunctionFactory.Create(workspacePlugin.AnalyzeImageFile, "workspace_analyze_image", "Analyzes a workspace PNG, JPEG, or GIF image through the provider's vision-capable analysis model and returns visible evidence plus provider token counts."));
            AddWorkspacePluginTool(tools, "workspace_analyze_images", () => AIFunctionFactory.Create(workspacePlugin.AnalyzeImageFiles, "workspace_analyze_images", "Analyzes two or more workspace screenshot images together through the provider's vision-capable analysis model and returns visible comparison evidence plus provider token counts."));
            return tools;
        }

        private void AddWorkspacePluginTool(
            List<AITool> tools,
            string toolName,
            Func<AITool> createTool)
        {
            if (IsConfiguredRuntimeToolAllowed(toolName))
            {
                tools.Add(createTool());
            }
        }

        private static AITool WrapWithApproval(AITool tool, bool suppressApprovalRequirements = false)
        {
            return !suppressApprovalRequirements && tool is AIFunction function
                ? new ApprovalRequiredAIFunction(function)
                : tool;
        }
}
