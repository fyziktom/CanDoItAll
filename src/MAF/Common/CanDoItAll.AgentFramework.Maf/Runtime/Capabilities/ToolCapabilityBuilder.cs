using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ToolCapabilityBuilder(
    IServiceProvider services,
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope,
    IMafProviderCredentialService providerCredentialService,
    WorkspaceRuntimePlugin workspacePlugin,
    StorageRuntimePlugin? storagePlugin,
    IWorkspaceCommandExecutionService workspaceCommandExecutionService,
    AgentWorkspaceToolAccessSettings workspaceToolAccess,
    IReadOnlyList<FileSkillExecutionPolicy> fileSkillExecutionPolicies,
    RuntimeCapabilityAccessPlan capabilityAccessPlan)
{
    private static readonly ProviderProfileService ProviderFeatureService = new();
    private readonly AgentWorkspaceToolAccessSettings workspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);
    private readonly ConfiguredWorkspaceToolSet configuredWorkspaceToolSet = new(
        workspaceToolAccess,
        workspacePlugin,
        storagePlugin,
        capabilityAccessPlan);

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
            var configuration = MafRuntimeJson.DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson) ?? new BuiltInToolConfiguration();
            var toolKey = configuration.Tool ?? capability.Key;
            if (!IsBuiltInToolEnabled(toolKey, configuration))
            {
                return [];
            }

            var tools = toolKey switch
            {
                "workspace-plugin" => configuredWorkspaceToolSet.CreateWorkspacePluginTools(suppressApprovalRequirements),
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
                "workspace-git-log" or "workspace_git_log" => [AIFunctionFactory.Create(workspacePlugin.GitWorkspaceLog, "workspace_git_log", capability.Description)],
                "workspace-git-show" or "workspace_git_show" => [AIFunctionFactory.Create(workspacePlugin.GitWorkspaceShow, "workspace_git_show", capability.Description)],
                "workspace-git-add" or "workspace_git_add" => [AIFunctionFactory.Create(workspacePlugin.GitWorkspaceAdd, "workspace_git_add", capability.Description)],
                "workspace-git-unstage" or "workspace_git_unstage" => [AIFunctionFactory.Create(workspacePlugin.GitWorkspaceUnstage, "workspace_git_unstage", capability.Description)],
                "workspace-git-commit" or "workspace_git_commit" => [AIFunctionFactory.Create(workspacePlugin.GitWorkspaceCommit, "workspace_git_commit", capability.Description)],
                "workspace-git-branch-create" or "workspace_git_branch_create" => [AIFunctionFactory.Create(workspacePlugin.GitWorkspaceBranchCreate, "workspace_git_branch_create", capability.Description)],
                "workspace-git-switch" or "workspace_git_switch" => [AIFunctionFactory.Create(workspacePlugin.GitWorkspaceSwitch, "workspace_git_switch", capability.Description)],
                "workspace-dotnet-restore" or "workspace_dotnet_restore" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRestore, "workspace_dotnet_restore", capability.Description)],
                "workspace-dotnet-build" or "workspace_dotnet_build" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceBuild, "workspace_dotnet_build", capability.Description)],
                "workspace-dotnet-test" or "workspace_dotnet_test" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceTest, "workspace_dotnet_test", capability.Description)],
                "workspace-dotnet-run" or "workspace_dotnet_run" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceRun, "workspace_dotnet_run", capability.Description)],
                "workspace-dotnet-stop" or "workspace_dotnet_stop" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceStop, "workspace_dotnet_stop", capability.Description)],
                "workspace-dotnet-new" or "workspace_dotnet_new" => [AIFunctionFactory.Create(workspacePlugin.DotnetWorkspaceNew, "workspace_dotnet_new", capability.Description)],
                "workspace-python-run-file" or "workspace_python_run_file" => [AIFunctionFactory.Create(workspacePlugin.RunWorkspacePythonFile, "workspace_python_run_file", capability.Description)],
                "workspace-pwsh-run-script" or "workspace_pwsh_run_script" => [AIFunctionFactory.Create(workspacePlugin.RunWorkspacePowerShellScript, "workspace_pwsh_run_script", capability.Description)],
                "workspace-convert-document" or "workspace_convert_document" => [AIFunctionFactory.Create(workspacePlugin.ConvertDocumentToMarkdown, "workspace_convert_document", capability.Description)],
                "workspace-inspect-spreadsheet" or "workspace_inspect_spreadsheet" => [AIFunctionFactory.Create(workspacePlugin.InspectSpreadsheetFile, "workspace_inspect_spreadsheet", capability.Description)],
                "workspace-inspect-image" or "workspace_inspect_image" => [AIFunctionFactory.Create(workspacePlugin.InspectImageFile, "workspace_inspect_image", capability.Description)],
                "workspace-analyze-image" or "workspace_analyze_image" => [AIFunctionFactory.Create(workspacePlugin.AnalyzeImageFile, "workspace_analyze_image", capability.Description)],
                "workspace-analyze-images" or "workspace_analyze_images" => [AIFunctionFactory.Create(workspacePlugin.AnalyzeImageFiles, "workspace_analyze_images", capability.Description)],
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

            var configuration = MafRuntimeJson.DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson) ?? new BuiltInToolConfiguration();
            var toolKey = configuration.Tool ?? capability.Key;
            if (!IsBuiltInToolEnabled(toolKey, configuration))
            {
                return false;
            }

            return SupportsFrameworkApprovalWrapper(toolKey) && configuration.ApprovalRequired == true
                || string.Equals(toolKey, "workspace-plugin", StringComparison.OrdinalIgnoreCase);
        }

        public IReadOnlyList<AITool> CreateConfiguredWorkspaceTools(
            AgentDefinition agent,
            bool suppressApprovalRequirements = false)
            => configuredWorkspaceToolSet.CreateTools(agent, suppressApprovalRequirements);

        public IReadOnlyList<AITool> CreatePluginTools(
            CapabilityCatalogItem capability,
            ProviderProfile provider,
            AgentDefinition? agent,
            bool suppressApprovalRequirements = false)
        {
            var configuration = MafRuntimeJson.DeserializeConfiguration<PluginCapabilityConfiguration>(capability.ConfigurationJson) ?? new PluginCapabilityConfiguration();
            var tools = ResolveRegisteredPluginTools(capability, configuration);
            if (tools.Count == 0)
            {
                tools = CreateTools(capability, provider, agent, suppressApprovalRequirements);
            }

            return MafRuntimeToolApproval.ApplyApprovalRequirement(tools, configuration.ApprovalRequired == true, suppressApprovalRequirements).ToList();
        }

        public async Task<object?> RunSkillScriptAsync(
            AgentFileSkill skill,
            AgentFileSkillScript script,
            AIFunctionArguments arguments,
            CancellationToken cancellationToken)
            => await RunSkillScriptCoreAsync(
                skill,
                script,
                ResolveSkillScriptArguments(arguments),
                cancellationToken);

        public async Task<object?> RunSkillScriptAsync(
            AgentFileSkill skill,
            AgentFileSkillScript script,
            JsonElement? arguments,
            IServiceProvider? serviceProvider,
            CancellationToken cancellationToken)
            => await RunSkillScriptCoreAsync(
                skill,
                script,
                ResolveSkillScriptArguments(arguments),
                cancellationToken);

        private async Task<object?> RunSkillScriptCoreAsync(
            AgentFileSkill skill,
            AgentFileSkillScript script,
            IReadOnlyList<string> scriptArguments,
            CancellationToken cancellationToken)
        {
            if (!File.Exists(script.FullPath))
            {
                return $"Error: Script file not found: {script.FullPath}";
            }

            var policy = ResolveSkillExecutionPolicy(script.FullPath);

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

        private static IReadOnlyList<string> ResolveSkillScriptArguments(AIFunctionArguments arguments)
        {
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

            return scriptArguments;
        }

        private static IReadOnlyList<string> ResolveSkillScriptArguments(JsonElement? arguments)
        {
            if (arguments is null)
            {
                return [];
            }

            return arguments.Value.ValueKind switch
            {
                JsonValueKind.Array => ResolveSkillScriptArrayArguments(arguments.Value),
                JsonValueKind.Object => ResolveSkillScriptObjectArguments(arguments.Value),
                JsonValueKind.String => [arguments.Value.GetString() ?? string.Empty],
                JsonValueKind.Number => [arguments.Value.GetRawText()],
                JsonValueKind.True => ["true"],
                JsonValueKind.False => ["false"],
                _ => []
            };
        }

        private static IReadOnlyList<string> ResolveSkillScriptArrayArguments(JsonElement arguments)
        {
            var scriptArguments = new List<string>();
            foreach (var item in arguments.EnumerateArray())
            {
                var value = item.ValueKind == JsonValueKind.String
                    ? item.GetString()
                    : item.GetRawText();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    scriptArguments.Add(value);
                }
            }

            return scriptArguments;
        }

        private static IReadOnlyList<string> ResolveSkillScriptObjectArguments(JsonElement arguments)
        {
            var scriptArguments = new List<string>();
            foreach (var property in arguments.EnumerateObject())
            {
                if (property.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                {
                    continue;
                }

                if (property.Value.ValueKind is JsonValueKind.False)
                {
                    continue;
                }

                scriptArguments.Add(NormalizeCliKey(property.Name));
                if (property.Value.ValueKind is JsonValueKind.True)
                {
                    continue;
                }

                scriptArguments.Add(property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? string.Empty
                    : property.Value.GetRawText());
            }

            return scriptArguments;
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

            var service = services.GetService(serviceType);
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

            return MafRuntimeToolApproval.ApplyApprovalRequirement(tools, configuration.ApprovalRequired == true, suppressApprovalRequirements).ToList();
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
                pair => MafToolInvocationArgumentFormatter.ConvertJsonValue(pair.Value),
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
                if (MafRuntimePathResolver.IsPathWithinRoot(fullPath, policy.RootPath))
                {
                    return policy;
                }
            }

            return new FileSkillExecutionPolicy(
                RootPath: Path.GetDirectoryName(fullPath) ?? workspaceRoot,
                ApprovalRequired: true,
                TrustLevel: "UncataloguedFileSkill");
        }

        private string DescribeProviderHealth(ProviderProfile provider)
        {
            var credential = providerCredentialService.Resolve(provider);
            var keyPresent = string.IsNullOrWhiteSpace(provider.ApiKeyEnvironmentVariable)
                ? "not configured"
                : credential.IsResolved ? "present" : "missing";
            var featureMatrix = ProviderFeatureService.ResolveFeatureMatrix(provider);
            var nativeToolSummary = ProviderFeatureService.DescribeSupportedNativeToolFamilies(provider);

            return $"Provider '{provider.Name}' uses transport '{provider.Transport}', endpoint '{provider.BaseUrl}', default model '{provider.DefaultModel}', and API key state '{keyPresent}'. Provider-native tool families: {nativeToolSummary}. GitHub Copilot recommendation: {featureMatrix.GitHubCopilotRecommendation} Last recorded health summary: {provider.HealthStatus}.";
        }

        private string ListExportPackages()
        {
            var exportRoot = Path.Combine(workspaceScope.ResolveDataRoot(workspaceRoot), "exports");
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
