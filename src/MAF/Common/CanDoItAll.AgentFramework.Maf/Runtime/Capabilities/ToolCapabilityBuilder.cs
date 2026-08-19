using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Text.Json;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class ToolCapabilityBuilder(
    IRegisteredCapabilityServiceSource registeredServices,
    string workspaceRoot,
    WorkspaceScopeDescriptor workspaceScope,
    IMafProviderCredentialService providerCredentialService,
    WorkspaceFilesystemRuntimePlugin filesystemPlugin,
    WorkspaceRuntimePlugin workspacePlugin,
    WorkspaceSpreadsheetRuntimePlugin spreadsheetPlugin,
    StorageRuntimePlugin? storagePlugin,
    IWorkspaceCommandExecutionService workspaceCommandExecutionService,
    AgentWorkspaceToolAccessSettings workspaceToolAccess,
    IReadOnlyList<FileSkillExecutionPolicy> fileSkillExecutionPolicies,
    IPhysicalFileSystemPathPolicyFactory physicalPathPolicyFactory,
    RuntimeCapabilityAccessPlan capabilityAccessPlan)
{
    private static readonly ProviderProfileService ProviderFeatureService = new();
    private readonly AgentWorkspaceToolAccessSettings workspaceToolAccess = AgentWorkspaceToolAccessMetadata.Normalize(workspaceToolAccess);
    private readonly WorkspaceToolSet workspaceToolSet = new(
        workspaceToolAccess,
        filesystemPlugin,
        workspacePlugin,
        spreadsheetPlugin,
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
            if (workspaceToolSet.TryCreateCatalogCapabilityTools(
                    capability,
                    suppressApprovalRequirements,
                    out var workspaceTools))
            {
                return workspaceTools;
            }

            var configuration = MafRuntimeJson.DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson) ?? new BuiltInToolConfiguration();
            var toolKey = configuration.Tool ?? capability.Key;
            if (!IsBuiltInToolEnabled(toolKey, configuration))
            {
                return [];
            }

            IReadOnlyList<AITool> tools = toolKey switch
            {
                "provider-native-code-interpreter" or ProviderNativeToolKeys.CodeInterpreter => [CreateHostedCodeInterpreterTool(capability, provider, configuration)],
                "provider-native-file-search" or ProviderNativeToolKeys.FileSearch => [CreateHostedFileSearchTool(capability, provider, configuration)],
                "provider-native-web-search" or ProviderNativeToolKeys.WebSearch => [CreateHostedWebSearchTool(capability, provider, configuration)],
                "provider-health" or "provider_health" => [AIFunctionFactory.Create(() => DescribeProviderHealth(provider), ToolContractCatalog.ProviderHealth, capability.Description)],
                "agent-package-export" or "agent_package_export" => [AIFunctionFactory.Create(ListExportPackages, ToolContractCatalog.AgentPackageExport, capability.Description)],
                _ => []
            };

            return ApplyConfiguredApprovalRequirement(capability, toolKey, tools, configuration, suppressApprovalRequirements);
        }

        public bool CapabilityHasApprovalTools(
            CapabilityCatalogItem capability,
            bool suppressApprovalRequirements = false)
        {
            if (workspaceToolSet.TryCapabilityHasApprovalTools(
                    capability,
                    suppressApprovalRequirements,
                    out var workspaceCapabilityHasApprovalTools))
            {
                return workspaceCapabilityHasApprovalTools;
            }

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

            return SupportsFrameworkApprovalWrapper(toolKey) && configuration.ApprovalRequired == true;
        }

        public IReadOnlyList<AITool> CreateWorkspaceTools(
            IReadOnlyList<CapabilityCatalogItem> catalogCapabilities,
            bool suppressApprovalRequirements = false)
            => workspaceToolSet.CreateTools(
                catalogCapabilities,
                suppressApprovalRequirements);

        public bool IsWorkspaceToolCapability(CapabilityCatalogItem capability)
            => workspaceToolSet.IsWorkspaceToolCapability(capability);

        public bool CanWorkspaceToolCapabilityParticipate(
            CapabilityCatalogItem capability,
            IReadOnlyList<CapabilityCatalogItem> catalogCapabilities)
            => workspaceToolSet.CanWorkspaceToolCapabilityParticipate(
                capability,
                catalogCapabilities);

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

        /// <remarks>
        /// The <paramref name="serviceProvider"/> parameter exists only to satisfy the SDK-owned
        /// <see cref="AgentFileSkillScriptRunner"/> delegate contract consumed by
        /// <c>AgentSkillsProviderBuilder.UseFileScriptRunner</c>. It is intentionally unused: skill script
        /// execution resolves its collaborators through constructor-injected typed dependencies.
        /// </remarks>
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
            var policy = ResolveSkillExecutionPolicy(script.FullPath);
            return await SkillScriptExecutionBoundary.ExecuteAsync(
                skill,
                script,
                scriptArguments,
                policy,
                workspaceCommandExecutionService).ConfigureAwait(false);
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

            var service = registeredServices.Resolve(serviceType);
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
                if (physicalPathPolicyFactory.Create(policy.RootPath).IsWithinRoot(fullPath))
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
                .ThenBy(Path.GetFileName, StringComparer.Ordinal)
                .ThenBy(path => path, StringComparer.Ordinal)
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
