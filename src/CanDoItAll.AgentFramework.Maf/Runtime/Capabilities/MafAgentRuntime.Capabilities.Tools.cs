using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private sealed class ToolCapabilityBuilder(
        MafAgentRuntime owner,
        WorkspaceRuntimePlugin workspacePlugin,
        StorageRuntimePlugin? storagePlugin,
        IWorkspaceCommandExecutionService workspaceCommandExecutionService,
        AgentWorkspaceToolAccessSettings workspaceToolAccess,
        IReadOnlyList<FileSkillExecutionPolicy> fileSkillExecutionPolicies)
    {
        private readonly AgentWorkspaceToolAccessSettings workspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);

        public IReadOnlyList<AITool> CreateTools(
            CapabilityCatalogItem capability,
            ProviderProfile provider,
            bool suppressApprovalRequirements = false)
            => CreateTools(capability, provider, agent: null, suppressApprovalRequirements);

        public IReadOnlyList<AITool> CreateTools(
            CapabilityCatalogItem capability,
            ProviderProfile provider,
            AgentDefinition? agent,
            bool suppressApprovalRequirements = false)
        {
            var configuration = DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson) ?? new BuiltInToolConfiguration();
            var toolKey = configuration.Tool ?? capability.Key;
            if (!IsBuiltInToolEnabled(toolKey, configuration))
            {
                return [];
            }

            if (agent is not null && !IsWorkspaceToolAllowed(toolKey))
            {
                return [];
            }

            var tools = toolKey switch
            {
                "workspace-plugin" => CreateWorkspacePluginTools(suppressApprovalRequirements),
                "provider-native-code-interpreter" or ProviderNativeToolKeys.CodeInterpreter => [CreateHostedCodeInterpreterTool(capability, provider, configuration)],
                "provider-native-file-search" or ProviderNativeToolKeys.FileSearch => [CreateHostedFileSearchTool(capability, provider, configuration)],
                "provider-native-web-search" or ProviderNativeToolKeys.WebSearch => [CreateHostedWebSearchTool(capability, provider, configuration)],
                "workspace-execution-boundary" or "workspace_execution_boundary" => [AIFunctionFactory.Create(workspacePlugin.GetWorkspaceExecutionBoundary, "workspace_execution_boundary", capability.Description)],
                "workspace-search" or "workspace_search" => [AIFunctionFactory.Create(workspacePlugin.SearchWorkspace, "workspace_search", capability.Description)],
                "workspace-read-file" or "workspace_read_file" => [AIFunctionFactory.Create(workspacePlugin.ReadWorkspaceTextFile, "workspace_read_file", capability.Description)],
                "workspace-list-files" or "workspace_list_files" => [AIFunctionFactory.Create(workspacePlugin.ListWorkspaceFiles, "workspace_list_files", capability.Description)],
                "workspace-stat-path" or "workspace_stat_path" => [AIFunctionFactory.Create(workspacePlugin.StatWorkspacePath, "workspace_stat_path", capability.Description)],
                "workspace-create-directory" or "workspace_create_directory" => [AIFunctionFactory.Create(workspacePlugin.CreateWorkspaceDirectory, "workspace_create_directory", capability.Description)],
                "workspace-write-file" or "workspace_write_file" => [AIFunctionFactory.Create(workspacePlugin.WriteWorkspaceTextFile, "workspace_write_file", capability.Description)],
                "workspace-append-file" or "workspace_append_file" => [AIFunctionFactory.Create(workspacePlugin.AppendWorkspaceTextFile, "workspace_append_file", capability.Description)],
                "workspace-copy-path" or "workspace_copy_path" => [AIFunctionFactory.Create(workspacePlugin.CopyWorkspacePath, "workspace_copy_path", capability.Description)],
                "workspace-move-path" or "workspace_move_path" => [AIFunctionFactory.Create(workspacePlugin.MoveWorkspacePath, "workspace_move_path", capability.Description)],
                "workspace-delete-path" or "workspace_delete_path" => [AIFunctionFactory.Create(workspacePlugin.DeleteWorkspacePath, "workspace_delete_path", capability.Description)],
                "workspace-diff-text" or "workspace_diff_text" => [AIFunctionFactory.Create(workspacePlugin.DiffWorkspaceTextFiles, "workspace_diff_text", capability.Description)],
                "workspace-git-status" or "workspace_git_status" => [AIFunctionFactory.Create(workspacePlugin.GitWorkspaceStatus, "workspace_git_status", capability.Description)],
                "workspace-git-diff" or "workspace_git_diff" => [AIFunctionFactory.Create(workspacePlugin.GitWorkspaceDiff, "workspace_git_diff", capability.Description)],
                "workspace-dotnet-restore" or "workspace_dotnet_restore" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRestore, "workspace_dotnet_restore", capability.Description)],
                "workspace-dotnet-build" or "workspace_dotnet_build" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceBuild, "workspace_dotnet_build", capability.Description)],
                "workspace-dotnet-test" or "workspace_dotnet_test" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceTest, "workspace_dotnet_test", capability.Description)],
                "workspace-dotnet-run" or "workspace_dotnet_run" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRun, "workspace_dotnet_run", capability.Description)],
                "workspace-dotnet-new" or "workspace_dotnet_new" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceNew, "workspace_dotnet_new", capability.Description)],
                "workspace-python-run-file" or "workspace_python_run_file" => [AIFunctionFactory.Create(workspacePlugin.RunWorkspacePythonFile, "workspace_python_run_file", capability.Description)],
                "workspace-pwsh-run-script" or "workspace_pwsh_run_script" => [AIFunctionFactory.Create(workspacePlugin.RunWorkspacePowerShellScript, "workspace_pwsh_run_script", capability.Description)],
                "workspace-convert-document" or "workspace_convert_document" => [AIFunctionFactory.Create(workspacePlugin.ConvertDocumentToMarkdown, "workspace_convert_document", capability.Description)],
                "workspace-inspect-spreadsheet" or "workspace_inspect_spreadsheet" => [AIFunctionFactory.Create(workspacePlugin.InspectSpreadsheetFile, "workspace_inspect_spreadsheet", capability.Description)],
                "workspace-inspect-image" or "workspace_inspect_image" => [AIFunctionFactory.Create(workspacePlugin.InspectImageFile, "workspace_inspect_image", capability.Description)],
                "provider-health" or "provider_health" => [AIFunctionFactory.Create(() => DescribeProviderHealth(provider), "provider_health", capability.Description)],
                "agent-package-export" or "agent_package_export" => [AIFunctionFactory.Create(ListExportPackages, "agent_package_export", capability.Description)],
                _ => []
            };

            return ApplyConfiguredApprovalRequirement(capability, toolKey, tools, configuration, suppressApprovalRequirements);
        }

        public bool CapabilityHasApprovalTools(
            CapabilityCatalogItem capability,
            bool suppressApprovalRequirements = false)
        {
            if (suppressApprovalRequirements)
            {
                return false;
            }

            var configuration = DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson) ?? new BuiltInToolConfiguration();
            var toolKey = configuration.Tool ?? capability.Key;
            if (!IsBuiltInToolEnabled(toolKey, configuration))
            {
                return false;
            }

            return SupportsFrameworkApprovalWrapper(toolKey) && configuration.ApprovalRequired == true
                || string.Equals(toolKey, "workspace-plugin", StringComparison.OrdinalIgnoreCase);
        }

        public IReadOnlyList<AITool> CreatePluginTools(
            CapabilityCatalogItem capability,
            ProviderProfile provider,
            AgentDefinition? agent,
            bool suppressApprovalRequirements = false)
        {
            var configuration = DeserializeConfiguration<PluginCapabilityConfiguration>(capability.ConfigurationJson) ?? new PluginCapabilityConfiguration();
            var tools = ResolveRegisteredPluginTools(capability, configuration);
            if (tools.Count == 0)
            {
                tools = CreateTools(capability, provider, agent, suppressApprovalRequirements);
            }

            return MafAgentRuntime.ApplyApprovalRequirement(tools, configuration.ApprovalRequired == true, suppressApprovalRequirements).ToList();
        }

        public IReadOnlyList<AITool> CreateConfiguredWorkspaceTools(
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
                tools.Add(AIFunctionFactory.Create(workspacePlugin.GetWorkspaceExecutionBoundary, "workspace_execution_boundary", "Describes the effective tool-execution boundary and whether the host provides real sandboxing."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.ListWorkspaceFiles, "workspace_list_files", "Lists files and directories from the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.SearchWorkspace, "workspace_search", "Searches text across the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.ReadWorkspaceTextFile, "workspace_read_file", "Reads text files from the managed workspace, a configured external workspace root, or an absolute external path grounded by the current prompt."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.StatWorkspacePath, "workspace_stat_path", "Returns file or directory metadata for a managed workspace path, configured external workspace root, or prompt-grounded absolute external path."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.DiffWorkspaceTextFiles, "workspace_diff_text", "Computes a bounded line-level diff between two allowed workspace text files."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceStatus, "workspace_git_status", "Runs a bounded git status recipe in the managed workspace or configured external workspace root."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.GitWorkspaceDiff, "workspace_git_diff", "Runs a bounded git diff recipe in the managed workspace or configured external workspace root."));
            }

            if (attachFileTools && access.CanWriteFiles)
            {
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.CreateWorkspaceDirectory, "workspace_create_directory", "Creates a directory in the managed workspace or configured external workspace root."), suppressApprovalRequirements));
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.WriteWorkspaceTextFile, "workspace_write_file", "Creates or overwrites a text file in the managed workspace or configured external workspace root."), suppressApprovalRequirements));
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.AppendWorkspaceTextFile, "workspace_append_file", "Appends text to a workspace file in the managed workspace or configured external workspace root."), suppressApprovalRequirements));
            }

            if (attachFileTools && access.CanManageWorkspacePaths)
            {
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.CopyWorkspacePath, "workspace_copy_path", "Copies a file or directory inside allowed workspace roots."), suppressApprovalRequirements));
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.MoveWorkspacePath, "workspace_move_path", "Moves or renames a file or directory inside allowed workspace roots."), suppressApprovalRequirements));
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.DeleteWorkspacePath, "workspace_delete_path", "Deletes a file or directory inside allowed workspace roots."), suppressApprovalRequirements));
            }

            if (attachFileTools && access.CanRunValidationCommands)
            {
                tools.Add(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRestore, "workspace_dotnet_restore", "Runs a bounded dotnet restore recipe in the managed workspace or configured external workspace root."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceBuild, "workspace_dotnet_build", "Runs a bounded dotnet build recipe in the managed workspace or configured external workspace root."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceTest, "workspace_dotnet_test", "Runs a bounded dotnet test recipe in the managed workspace or configured external workspace root."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRun, "workspace_dotnet_run", "Runs a bounded dotnet run recipe or loopback HTTP startup smoke in the managed workspace or configured external workspace root. HTTP smoke stops the launched process tree by default. Use keepAlive true with lifetimeScope ExecutionRun for same-step browser proof, or lifetimeScope ProcessRun only when a later process step owns capture and cleanup."));
            }

            if (attachFileTools && access.CanScaffoldProjects)
            {
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceNew, "workspace_dotnet_new", "Creates a bounded dotnet project or solution scaffold in the managed workspace or configured external workspace root. For a grounded product root, create the solution at the product root and create app/test projects under child folders such as src or tests."), suppressApprovalRequirements));
            }

            if (attachFileTools && access.CanRunLocalScripts)
            {
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.RunWorkspacePythonFile, "workspace_python_run_file", "Runs a workspace Python file with structured arguments through the controlled execution plane."), suppressApprovalRequirements));
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.RunWorkspacePowerShellScript, "workspace_pwsh_run_script", "Runs a workspace PowerShell script in non-interactive mode through the controlled execution plane."), suppressApprovalRequirements));
            }

            if (attachFileTools && access.CanTransformArtifacts)
            {
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.ConvertDocumentToMarkdown, "workspace_convert_document", "Converts a workspace document such as a PDF to markdown using markitdown."), suppressApprovalRequirements));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.InspectSpreadsheetFile, "workspace_inspect_spreadsheet", "Inspects a workspace .xls, .xlsx, .csv, or .tsv file and returns a compact preview."));
                tools.Add(AIFunctionFactory.Create(workspacePlugin.InspectImageFile, "workspace_inspect_image", "Inspects a workspace PNG, JPEG, or GIF image and returns format, dimensions, and byte size before asset storage."));
            }

            if (storagePlugin is not null && (access.CanReadStorage || access.CanWriteStorage))
            {
                tools.Add(AIFunctionFactory.Create(storagePlugin.ListStorageCatalogs, "storage_catalog_list", "Lists storage catalogs this agent is allowed to use."));
                tools.Add(AIFunctionFactory.Create(storagePlugin.ReadStorageTextFile, "storage_read_text_file", "Reads a text object from an allowed storage catalog through the configured storage driver."));
            }

            if (storagePlugin is not null && access.CanWriteStorage)
            {
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(storagePlugin.WriteStorageTextFile, "storage_write_text_file", "Writes a text object to an allowed storage catalog through the configured storage driver."), suppressApprovalRequirements));
                tools.Add(WrapWithApproval(AIFunctionFactory.Create(storagePlugin.DeleteStorageObject, "storage_delete_object", "Deletes an object from an allowed storage catalog through the configured storage driver."), suppressApprovalRequirements));
            }

            return tools;
        }

        public async Task<object?> RunSkillScriptAsync(
            AgentFileSkill skill,
            AgentFileSkillScript script,
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(script.FullPath))
            {
                return $"Error: Script file not found: {script.FullPath}";
            }

            var policy = ResolveSkillExecutionPolicy(script.FullPath);
            var scriptArguments = new List<string>();
            foreach (var argument in arguments)
            {
                if (argument.Value is bool boolValue)
                {
                    if (boolValue)
                    {
                        scriptArguments.Add(NormalizeCliKey(argument.Key));
                    }
                }
                else if (argument.Value is not null)
                {
                    scriptArguments.Add(NormalizeCliKey(argument.Key));
                    scriptArguments.Add(argument.Value.ToString()!);
                }
            }

            try
            {
                return await workspaceCommandExecutionService.RunSkillScript(
                    Path.GetFileName(skill.Path),
                    script.FullPath,
                    scriptArguments.ToArray(),
                    Path.GetDirectoryName(script.FullPath),
                    policy.ApprovalRequired,
                    policy.TrustLevel,
                    [policy.RootPath]).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                return $"Error: Failed to execute script '{script.Name}': {exception.Message}";
            }
        }

        private IReadOnlyList<AITool> ResolveRegisteredPluginTools(
            CapabilityCatalogItem capability,
            PluginCapabilityConfiguration configuration)
        {
            if (string.IsNullOrWhiteSpace(configuration.RegisteredPluginServiceType))
            {
                return [];
            }

            var serviceType = Type.GetType(configuration.RegisteredPluginServiceType, throwOnError: false);
            if (serviceType is null)
            {
                throw new InvalidOperationException($"Registered plugin type '{configuration.RegisteredPluginServiceType}' for capability '{capability.Name}' could not be resolved.");
            }

            var service = owner.services.GetService(serviceType);
            if (service is null)
            {
                throw new InvalidOperationException($"Registered plugin type '{configuration.RegisteredPluginServiceType}' for capability '{capability.Name}' is not available in DI.");
            }

            if (service is AITool singleTool)
            {
                return [singleTool];
            }

            if (service is IEnumerable<AITool> toolCollection)
            {
                return toolCollection.ToList();
            }

            var asAiToolsMethod = serviceType.GetMethod("AsAITools", Type.EmptyTypes);
            if (asAiToolsMethod?.Invoke(service, null) is IEnumerable<AITool> reflectedTools)
            {
                return reflectedTools.ToList();
            }

            throw new InvalidOperationException($"Registered plugin service '{configuration.RegisteredPluginServiceType}' for capability '{capability.Name}' does not expose AITool instances.");
        }

        private bool IsWorkspaceToolAllowed(string toolKey)
        {
            if (!AgentWorkspaceToolAccessMetadata.TryResolveWorkspaceToolPermission(toolKey, out _))
            {
                return true;
            }

            return AgentWorkspaceToolAccessMetadata.IsWorkspaceToolAllowed(
                workspaceToolAccess,
                toolKey);
        }

        private IReadOnlyList<AITool> CreateWorkspacePluginTools(bool suppressApprovalRequirements)
        {
            var tools = new List<AITool>();
            AddWorkspacePluginTool(tools, "workspace_execution_boundary", () => AIFunctionFactory.Create(workspacePlugin.GetWorkspaceExecutionBoundary, "workspace_execution_boundary", "Describes the effective tool-execution boundary and whether the host provides real sandboxing."));
            AddWorkspacePluginTool(tools, "workspace_list_files", () => AIFunctionFactory.Create(workspacePlugin.ListWorkspaceFiles, "workspace_list_files", "Lists files and directories from the managed workspace or a grounded external-target alias. Supports simple patterns and recursive globstar patterns such as **/* and **/*.cs. In external-target process runs, broad managed-root browsing is denied; list current-run artifacts or the grounded product alias instead."));
            AddWorkspacePluginTool(tools, "workspace_search", () => AIFunctionFactory.Create(workspacePlugin.SearchWorkspace, "workspace_search", "Searches text across the current workspace; external-target paths require explicit current-run grounding. In external-target process runs, broad managed-root search is denied; search current-run artifacts or the grounded product alias instead."));
            AddWorkspacePluginTool(tools, "workspace_read_file", () => AIFunctionFactory.Create(workspacePlugin.ReadWorkspaceTextFile, "workspace_read_file", "Reads text files from the managed workspace or a grounded external-target alias. In external-target process runs, do not read unmanaged source or helper roots unless they are current-run artifacts."));
            AddWorkspacePluginTool(tools, "workspace_stat_path", () => AIFunctionFactory.Create(workspacePlugin.StatWorkspacePath, "workspace_stat_path", "Returns file or directory metadata for a managed workspace path or grounded external-target alias. In external-target process runs, prefer current-run artifacts and the grounded product alias."));
            AddWorkspacePluginTool(tools, "workspace_diff_text", () => AIFunctionFactory.Create(workspacePlugin.DiffWorkspaceTextFiles, "workspace_diff_text", "Computes a bounded line-level diff between two workspace text files."));
            AddWorkspacePluginTool(tools, "workspace_git_status", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceStatus, "workspace_git_status", "Runs a bounded git status recipe in the current workspace."));
            AddWorkspacePluginTool(tools, "workspace_git_diff", () => AIFunctionFactory.Create(workspacePlugin.GitWorkspaceDiff, "workspace_git_diff", "Runs a bounded git diff recipe in the current workspace."));
            AddWorkspacePluginTool(tools, "workspace_dotnet_build", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceBuild, "workspace_dotnet_build", "Runs a bounded dotnet build recipe in the managed workspace or a grounded external-target alias. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying."));
            AddWorkspacePluginTool(tools, "workspace_dotnet_test", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceTest, "workspace_dotnet_test", "Runs a bounded dotnet test recipe in the managed workspace or a grounded external-target alias. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying."));
            AddWorkspacePluginTool(tools, "workspace_dotnet_run", () => AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRun, "workspace_dotnet_run", "Runs a bounded dotnet run recipe or loopback HTTP startup smoke in the managed workspace or a grounded external-target alias. Use lifetimeScope ProcessRun only when the process graph has a later cleanup step. On failure, read the returned stdout/stderr diagnostics or artifact paths before editing or retrying."));
            AddWorkspacePluginTool(tools, "workspace_create_directory", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.CreateWorkspaceDirectory, "workspace_create_directory", "Creates a directory in the managed workspace or a grounded external-target alias. In external-target process runs, product source, tests, scripts, and assets must stay under the grounded product alias or current-run artifact folders."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_write_file", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.WriteWorkspaceTextFile, "workspace_write_file", "Creates or overwrites a text file in the managed workspace or a grounded external-target alias. In external-target process runs, product source and tests must be written under the grounded product alias, not managed src/tests/tools roots."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_append_file", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.AppendWorkspaceTextFile, "workspace_append_file", "Appends text to a workspace file. In external-target process runs, product source and tests must be written under the grounded product alias, not managed src/tests/tools roots."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_copy_path", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.CopyWorkspacePath, "workspace_copy_path", "Copies a file or directory inside the current workspace."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_move_path", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.MoveWorkspacePath, "workspace_move_path", "Moves or renames a file or directory inside the current workspace."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_delete_path", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.DeleteWorkspacePath, "workspace_delete_path", "Deletes a file or directory inside the current workspace."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_dotnet_restore", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRestore, "workspace_dotnet_restore", "Runs a bounded dotnet restore recipe in the managed workspace or a grounded external-target alias."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_dotnet_new", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceNew, "workspace_dotnet_new", "Creates a bounded dotnet project or solution scaffold in the managed workspace or a grounded external-target alias. Approved templates include current SDK project templates and sln for empty solution files. Inspect an unsuccessful result before retrying. For an exact output root, pass its parent as parentDirectory and the root leaf as name. Timeout arguments are seconds, not milliseconds; prefer the default or use normal second values such as 120 or 300. For test projects, pass a parentDirectory under the grounded product root, such as <product-root>/tests, with name <AppName>.Tests; never reuse the product parent to create <AppName>.Tests as a sibling. Keep tests and support projects under child folders of the grounded product root unless another root is explicitly grounded. Do not scaffold product or test projects into managed src/tests/tools roots or sibling external-target roots during an external-target run."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_python_run_file", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.RunWorkspacePythonFile, "workspace_python_run_file", "Runs a workspace Python file with structured arguments through the controlled execution plane."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_pwsh_run_script", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.RunWorkspacePowerShellScript, "workspace_pwsh_run_script", "Runs a workspace PowerShell script in non-interactive mode through the controlled execution plane."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_convert_document", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.ConvertDocumentToMarkdown, "workspace_convert_document", "Converts a workspace document such as a PDF to markdown using markitdown."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_inspect_spreadsheet", () => WrapWithApproval(AIFunctionFactory.Create(workspacePlugin.InspectSpreadsheetFile, "workspace_inspect_spreadsheet", "Inspects a workspace .xls, .xlsx, .csv, or .tsv file and returns a compact preview."), suppressApprovalRequirements));
            AddWorkspacePluginTool(tools, "workspace_inspect_image", () => AIFunctionFactory.Create(workspacePlugin.InspectImageFile, "workspace_inspect_image", "Inspects a workspace PNG, JPEG, or GIF image and returns format, dimensions, and byte size before asset storage."));
            return tools;
        }

        private void AddWorkspacePluginTool(
            List<AITool> tools,
            string toolName,
            Func<AITool> createTool)
        {
            if (IsWorkspaceToolAllowed(toolName))
            {
                tools.Add(createTool());
            }
        }

        private static bool IsBuiltInToolEnabled(string toolKey, BuiltInToolConfiguration configuration)
            => configuration.Enabled != false;

        private IReadOnlyList<AITool> ApplyConfiguredApprovalRequirement(
            CapabilityCatalogItem capability,
            string toolKey,
            IReadOnlyList<AITool> tools,
            BuiltInToolConfiguration configuration,
            bool suppressApprovalRequirements)
        {
            if (configuration.ApprovalRequired == true && !SupportsFrameworkApprovalWrapper(toolKey))
            {
                throw new InvalidOperationException(
                    $"Capability '{capability.Name}' requests approvalRequired for '{toolKey}', but provider-native hosted tools do not project approval wrappers through the current MAF bridge. Keep approvalRequired disabled for this capability until the approval-alignment phase is complete.");
            }

            return MafAgentRuntime.ApplyApprovalRequirement(tools, configuration.ApprovalRequired == true, suppressApprovalRequirements).ToList();
        }

        private static AITool WrapWithApproval(AITool tool, bool suppressApprovalRequirements = false)
        {
            return !suppressApprovalRequirements && tool is AIFunction function
                ? new ApprovalRequiredAIFunction(function)
                : tool;
        }

        private static bool SupportsFrameworkApprovalWrapper(string toolKey)
        {
            return !ProviderNativeToolKeys.TryResolveFamily(toolKey, out _);
        }

        private HostedCodeInterpreterTool CreateHostedCodeInterpreterTool(
            CapabilityCatalogItem capability,
            ProviderProfile provider,
            BuiltInToolConfiguration configuration)
        {
            EnsureProviderNativeToolSupported(capability, provider, ProviderNativeToolFamily.CodeInterpreter);
            var additionalProperties = ResolveAdditionalProperties(configuration);
            return additionalProperties.Count == 0
                ? new HostedCodeInterpreterTool()
                : new HostedCodeInterpreterTool(additionalProperties);
        }

        private HostedFileSearchTool CreateHostedFileSearchTool(
            CapabilityCatalogItem capability,
            ProviderProfile provider,
            BuiltInToolConfiguration configuration)
        {
            EnsureProviderNativeToolSupported(capability, provider, ProviderNativeToolFamily.FileSearch);
            var additionalProperties = ResolveAdditionalProperties(configuration);
            var tool = additionalProperties.Count == 0
                ? new HostedFileSearchTool()
                : new HostedFileSearchTool(additionalProperties);

            if (configuration.MaximumResultCount.HasValue)
            {
                tool.MaximumResultCount = configuration.MaximumResultCount.Value;
            }

            return tool;
        }

        private HostedWebSearchTool CreateHostedWebSearchTool(
            CapabilityCatalogItem capability,
            ProviderProfile provider,
            BuiltInToolConfiguration configuration)
        {
            EnsureProviderNativeToolSupported(capability, provider, ProviderNativeToolFamily.WebSearch);
            var additionalProperties = ResolveAdditionalProperties(configuration);
            return additionalProperties.Count == 0
                ? new HostedWebSearchTool()
                : new HostedWebSearchTool(additionalProperties);
        }

        private static IReadOnlyDictionary<string, object?> ResolveAdditionalProperties(BuiltInToolConfiguration configuration)
        {
            if (configuration.AdditionalProperties is null || configuration.AdditionalProperties.Count == 0)
            {
                return new Dictionary<string, object?>();
            }

            return configuration.AdditionalProperties.ToDictionary(
                pair => pair.Key,
                pair => ConvertJsonValue(pair.Value),
                StringComparer.OrdinalIgnoreCase);
        }

        private static void EnsureProviderNativeToolSupported(
            CapabilityCatalogItem capability,
            ProviderProfile provider,
            ProviderNativeToolFamily family)
        {
            var support = ProviderFeatureService.GetNativeToolSupport(provider, family);
            if (support.IsSupported)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Capability '{capability.Name}' cannot attach {ProviderNativeToolKeys.GetDisplayName(family)} to provider '{provider.Name}'. {support.Summary} {support.Remediation}");
        }

        private FileSkillExecutionPolicy ResolveSkillExecutionPolicy(string scriptPath)
        {
            var fullPath = Path.GetFullPath(scriptPath);
            foreach (var policy in fileSkillExecutionPolicies.OrderByDescending(item => item.RootPath.Length))
            {
                if (IsPathWithinRoot(fullPath, policy.RootPath))
                {
                    return policy;
                }
            }

            return new FileSkillExecutionPolicy(
                RootPath: Path.GetDirectoryName(fullPath) ?? owner.workspaceRoot,
                ApprovalRequired: true,
                TrustLevel: "UncataloguedFileSkill");
        }

        private string DescribeProviderHealth(ProviderProfile provider)
        {
            var credential = owner.ResolveProviderCredential(provider);
            var keyPresent = string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable)
                ? "not configured"
                : credential.IsResolved ? "present" : "missing";
            var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
            var nativeToolSummary = ProviderFeatureService.DescribeSupportedNativeToolFamilies(provider);

            return $"Provider '{provider.Name}' uses transport '{provider.Transport}', endpoint '{provider.BaseUrl}', default model '{provider.DefaultModel}', and API key state '{keyPresent}'. Provider-native tool families: {nativeToolSummary}. GitHub Copilot recommendation: {featureMatrix.GitHubCopilotRecommendation} Last recorded health summary: {provider.HealthStatus}.";
        }

        private string ListExportPackages()
        {
            var exportRoot = Path.Combine(owner.workspaceScope.ResolveDataRoot(owner.workspaceRoot), "exports");
            if (!Directory.Exists(exportRoot))
            {
                return "The workspace export folder does not exist yet.";
            }

            var exports = Directory.EnumerateFiles(exportRoot, "*.zip", SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .Take(10)
                .Select(path => $"{Path.GetFileName(path)} ({File.GetLastWriteTime(path):g})")
                .ToList();

            return exports.Count == 0
                ? "No exported agent packages are currently available."
                : "Available exported agent packages:" + Environment.NewLine + string.Join(Environment.NewLine, exports.Select(item => $"- {item}"));
        }

        private static string NormalizeCliKey(string key)
        {
            return "--" + key.TrimStart('-');
        }
    }
}
