using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

public sealed partial class MafAgentRuntime
{
    private async Task<RuntimeCapabilityState> CreateCapabilityStateAsync(
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        IReadOnlyList<AgentMemoryRecord> memory,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements = false)
    {
        var composition = CreateCapabilityComposition(agent, capabilities);

        await AttachWorkspaceMemoryAsync(composition, memory, progressCallback);
        await AttachSkillsAsync(composition, capabilities, progressCallback, suppressApprovalRequirements);
        await AttachCatalogCapabilitiesAsync(
            composition,
            agent,
            provider,
            capabilities,
            progressCallback,
            cancellationToken,
            suppressApprovalRequirements);
        await AttachCompactionAsync(composition, agent, progressCallback);

        DeduplicateTools(composition.State.Tools);
        return composition.State;
    }

    private RuntimeCapabilityComposition CreateCapabilityComposition(
        AgentDefinition agent,
        IReadOnlyList<CapabilityCatalogItem> capabilities)
    {
        var agentConfiguration = DeserializeConfiguration<AgentRuntimeConfiguration>(agent.ConfigurationJson) ?? new AgentRuntimeConfiguration();
        var workspaceFileService = services.GetService(typeof(IWorkspaceFileService)) as IWorkspaceFileService
            ?? new WorkspaceFileService(workspaceRoot, workspaceScope);
        var workspaceCommandExecutionService = services.GetService(typeof(IWorkspaceCommandExecutionService)) as IWorkspaceCommandExecutionService
            ?? new WorkspaceCommandExecutionService(workspaceRoot, new LocalWorkspaceProcessHost(), workspaceScope);
        var workspaceArtifactToolService = services.GetService(typeof(IWorkspaceArtifactToolService)) as IWorkspaceArtifactToolService
            ?? new WorkspaceArtifactToolService(workspaceRoot, workspaceCommandExecutionService, workspaceScope);
        var workspacePlugin = new WorkspaceRuntimePlugin(workspaceFileService, workspaceCommandExecutionService, workspaceArtifactToolService);
        var skillBuilder = new SkillCapabilityBuilder(this);
        var contextBuilder = new ContextCapabilityBuilder(this);
        var mcpBuilder = new McpCapabilityBuilder(this);
        var fileSkillExecutionPolicies = skillBuilder.ResolveScriptExecutionPolicies(capabilities);
        var toolBuilder = new ToolCapabilityBuilder(this, workspacePlugin, workspaceCommandExecutionService, fileSkillExecutionPolicies);

        return new RuntimeCapabilityComposition(
            new RuntimeCapabilityState(),
            agentConfiguration,
            skillBuilder,
            contextBuilder,
            mcpBuilder,
            toolBuilder);
    }

    private async Task AttachWorkspaceMemoryAsync(
        RuntimeCapabilityComposition composition,
        IReadOnlyList<AgentMemoryRecord> memory,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        if (memory.Count == 0)
        {
            return;
        }

        var maxInjectedMemoryItems = composition.AgentConfiguration.MaxInjectedMemoryItems ?? 6;
        composition.State.ContextProviders.Add(new WorkspaceMemoryContextProvider(memory, maxInjectedMemoryItems));
        await progressCallback(
            ExecutionState.Preparing,
            "Memory",
            $"Attached {Math.Min(memory.Count, maxInjectedMemoryItems)} workspace memory item(s) as AI context.");
    }

    private async Task AttachSkillsAsync(
        RuntimeCapabilityComposition composition,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        Func<ExecutionState, string, string, Task> progressCallback,
        bool suppressApprovalRequirements)
    {
        var skillRoots = composition.SkillBuilder.ResolveSkillRoots(capabilities, composition.AgentConfiguration);
        var inlineSkills = composition.SkillBuilder.ResolveInlineSkills(capabilities);
        var serviceSkills = composition.SkillBuilder.ResolveRegisteredSkills(capabilities);

        if (skillRoots.Count == 0 && inlineSkills.Count == 0 && serviceSkills.Count == 0)
        {
            return;
        }

        var skillsBuilder = new AgentSkillsProviderBuilder()
            .UseFileScriptRunner(composition.ToolBuilder.RunSkillScriptAsync);

        foreach (var skillRoot in skillRoots)
        {
            skillsBuilder.UseFileSkill(skillRoot);
        }

        if (inlineSkills.Count > 0)
        {
            skillsBuilder.UseSkills(inlineSkills);
        }

        if (serviceSkills.Count > 0)
        {
            skillsBuilder.UseSkills(serviceSkills);
        }

        if (!suppressApprovalRequirements && capabilities.Any(composition.SkillBuilder.RequiresSkillScriptApproval))
        {
            skillsBuilder.UseScriptApproval();
            composition.State.HasApprovalTools = true;
        }

        composition.State.ContextProviders.Add(skillsBuilder.Build());
        await progressCallback(
            ExecutionState.Preparing,
            "Skills",
            $"Loaded {skillRoots.Count} file skill root(s), {inlineSkills.Count} inline skill(s), and {serviceSkills.Count} DI-provided skill(s) through AgentSkillsProvider.");
    }

    private async Task AttachCatalogCapabilitiesAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        ProviderProfile provider,
        IReadOnlyList<CapabilityCatalogItem> capabilities,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements)
    {
        foreach (var capability in capabilities)
        {
            await AttachCapabilityAsync(
                composition,
                capability,
                agent,
                provider,
                progressCallback,
                cancellationToken,
                suppressApprovalRequirements);
        }
    }

    private async Task AttachCapabilityAsync(
        RuntimeCapabilityComposition composition,
        CapabilityCatalogItem capability,
        AgentDefinition agent,
        ProviderProfile provider,
        Func<ExecutionState, string, string, Task> progressCallback,
        CancellationToken cancellationToken,
        bool suppressApprovalRequirements)
    {
        if (await TrySkipUnsupportedProviderNativeCapabilityAsync(capability, provider, progressCallback))
        {
            return;
        }

        switch (capability.Kind)
        {
            case CapabilityKind.Tool:
                foreach (var tool in composition.ToolBuilder.CreateTools(capability, provider, suppressApprovalRequirements))
                {
                    composition.State.Tools.Add(tool);
                }

                composition.State.HasApprovalTools |= composition.ToolBuilder.CapabilityHasApprovalTools(capability, suppressApprovalRequirements);
                break;
            case CapabilityKind.Plugin:
                foreach (var tool in composition.ToolBuilder.CreatePluginTools(capability, provider, suppressApprovalRequirements))
                {
                    composition.State.Tools.Add(tool);
                }

                composition.State.HasApprovalTools |= !suppressApprovalRequirements
                    && DeserializeConfiguration<PluginCapabilityConfiguration>(capability.ConfigurationJson)?.ApprovalRequired == true;
                break;
            case CapabilityKind.McpServer:
                await composition.McpBuilder.AddMcpToolsAsync(
                    composition.State,
                    capability,
                    agent,
                    provider,
                    progressCallback,
                    cancellationToken,
                    suppressApprovalRequirements);
                break;
            case CapabilityKind.Rag:
                composition.ContextBuilder.AddRagProvider(composition.State, capability, composition.AgentConfiguration);
                break;
            case CapabilityKind.AiContext:
                composition.ContextBuilder.AddConfiguredAiContextProvider(composition.State, capability);
                break;
            case CapabilityKind.Memory:
                await composition.ContextBuilder.AddMemoryProviderAsync(composition.State, capability, agent, progressCallback, cancellationToken);
                break;
        }
    }

    private static async Task<bool> TrySkipUnsupportedProviderNativeCapabilityAsync(
        CapabilityCatalogItem capability,
        ProviderProfile provider,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        if (capability.Kind == CapabilityKind.Tool)
        {
            var configuration = DeserializeConfiguration<BuiltInToolConfiguration>(capability.ConfigurationJson) ?? new BuiltInToolConfiguration();
            var toolKey = configuration.Tool ?? capability.Key;
            if (ProviderNativeToolKeys.TryResolveFamily(toolKey, out var family))
            {
                var support = ProviderFeatureService.GetNativeToolSupport(provider, family);
                if (!support.IsSupported)
                {
                    await progressCallback(
                        ExecutionState.Preparing,
                        "Capability compatibility",
                        $"Skipping capability '{capability.Name}' for provider '{provider.Name}'. {support.Summary} {support.Remediation}");
                    return true;
                }
            }
        }
        else if (capability.Kind == CapabilityKind.McpServer)
        {
            var configuration = DeserializeConfiguration<McpCapabilityConfiguration>(capability.ConfigurationJson);
            if (configuration?.Hosted == true)
            {
                var support = ProviderFeatureService.GetNativeToolSupport(provider, ProviderNativeToolFamily.HostedMcpServer);
                if (!support.IsSupported)
                {
                    await progressCallback(
                        ExecutionState.Preparing,
                        "Capability compatibility",
                        $"Skipping capability '{capability.Name}' for provider '{provider.Name}'. {support.Summary} {support.Remediation}");
                    return true;
                }
            }
        }

        return false;
    }

    private async Task AttachCompactionAsync(
        RuntimeCapabilityComposition composition,
        AgentDefinition agent,
        Func<ExecutionState, string, string, Task> progressCallback)
    {
        if (!ShouldEnableCompaction(agent, composition.AgentConfiguration))
        {
            return;
        }

        composition.State.ContextProviders.Add(CreateCompactionProvider(composition.AgentConfiguration));
        await progressCallback(
            ExecutionState.Preparing,
            "Compaction",
            "Attached Microsoft Agent Framework compaction to manage long-running local history.");
    }

    private static bool ShouldEnableCompaction(AgentDefinition agent, AgentRuntimeConfiguration configuration)
    {
        if (configuration.EnableCompaction.HasValue)
        {
            return configuration.EnableCompaction.Value;
        }

        return agent.ChatHistoryMode == AgentChatHistoryMode.FrameworkManaged
            || agent.Workload is AgentWorkloadKind.Programming or AgentWorkloadKind.Research;
    }

    private static CompactionProvider CreateCompactionProvider(AgentRuntimeConfiguration configuration)
    {
        var slidingWindowTurns = configuration.SlidingWindowTurns ?? 8;
        var truncationTokenLimit = configuration.TruncationTokenLimit ?? 12000;
        var toolMessageThreshold = configuration.ToolCompactionMessageThreshold ?? 10;

        var pipeline = new PipelineCompactionStrategy(
            new ToolResultCompactionStrategy(CompactionTriggers.MessagesExceed(toolMessageThreshold)),
            new SlidingWindowCompactionStrategy(CompactionTriggers.TurnsExceed(slidingWindowTurns)),
            new TruncationCompactionStrategy(CompactionTriggers.TokensExceed(truncationTokenLimit)));

        return new CompactionProvider(pipeline);
    }

    private static void DeduplicateTools(List<AITool> tools)
    {
        var deduplicated = tools
            .GroupBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.First())
            .ToList();

        tools.Clear();
        tools.AddRange(deduplicated);
    }

    private static IEnumerable<AITool> ApplyApprovalRequirement(
        IEnumerable<AITool> tools,
        bool approvalRequired,
        bool suppressApprovalRequirements = false)
    {
        if (!approvalRequired || suppressApprovalRequirements)
        {
            return tools;
        }

        return tools.Select(tool => tool is AIFunction function
            ? new ApprovalRequiredAIFunction(function)
            : tool);
    }

    private static ChatRole ParseChatRole(string? role)
    {
        if (string.Equals(role, nameof(ChatRole.User), StringComparison.OrdinalIgnoreCase))
        {
            return ChatRole.User;
        }

        if (string.Equals(role, nameof(ChatRole.Assistant), StringComparison.OrdinalIgnoreCase))
        {
            return ChatRole.Assistant;
        }

        return ChatRole.System;
    }

    private string ResolvePathFromWorkspace(string path, bool allowExternal, IReadOnlyList<string>? allowedExternalRoots = null)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return workspaceRoot;
        }

        var expandedPath = ExpandPortablePath(path);
        var fullPath = Path.GetFullPath(Path.IsPathRooted(expandedPath) ? expandedPath : Path.Combine(workspaceRoot, expandedPath));
        if (IsPathWithinRoot(fullPath, workspaceRoot))
        {
            return fullPath;
        }

        if (!allowExternal)
        {
            throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root. Use a workspace-relative path or import the external file into chat attachments first.");
        }

        var allowedRoots = allowedExternalRoots?
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(ExpandPortablePath)
            .Select(item => Path.GetFullPath(Path.IsPathRooted(item) ? item : Path.Combine(workspaceRoot, item)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList()
            ?? [];

        if (allowedRoots.Any(allowedRoot => IsPathWithinRoot(fullPath, allowedRoot)))
        {
            return fullPath;
        }

        throw new InvalidOperationException($"Path '{path}' resolves outside the workspace root and is not covered by an explicit external-root allowlist.");
    }

    private static string ExpandPortablePath(string path)
    {
        var expanded = Environment.ExpandEnvironmentVariables(path.Trim());
        if (string.Equals(expanded, "~", StringComparison.Ordinal))
        {
            var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return string.IsNullOrWhiteSpace(homeDirectory)
                ? expanded
                : homeDirectory;
        }

        if (!expanded.StartsWith("~" + Path.DirectorySeparatorChar, StringComparison.Ordinal) &&
            !expanded.StartsWith("~" + Path.AltDirectorySeparatorChar, StringComparison.Ordinal))
        {
            return expanded;
        }

        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return expanded;
        }

        var relativePath = expanded[2..]
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar);
        return Path.Combine(home, relativePath);
    }

    private static bool IsPathWithinRoot(string fullPath, string rootPath)
    {
        var normalizedRoot = Path.GetFullPath(rootPath);
        if (string.Equals(fullPath, normalizedRoot, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rootWithSeparator = normalizedRoot.EndsWith(Path.DirectorySeparatorChar) || normalizedRoot.EndsWith(Path.AltDirectorySeparatorChar)
            ? normalizedRoot
            : normalizedRoot + Path.DirectorySeparatorChar;
        return fullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    private static TConfiguration? DeserializeConfiguration<TConfiguration>(string? json)
        where TConfiguration : class
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<TConfiguration>(json, SerializerOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class AgentRuntimeConfiguration
    {
        public bool? EnableCompaction { get; set; }

        public int? SlidingWindowTurns { get; set; }

        public int? TruncationTokenLimit { get; set; }

        public int? ToolCompactionMessageThreshold { get; set; }

        public int? MaxInjectedMemoryItems { get; set; }

        public int? MaxLocalRagResults { get; set; }

        public List<string>? PreferredSkillRoots { get; set; }
    }

    private sealed record FileSkillExecutionPolicy(
        string RootPath,
        bool ApprovalRequired,
        string TrustLevel);

    private sealed record RuntimeCapabilityComposition(
        RuntimeCapabilityState State,
        AgentRuntimeConfiguration AgentConfiguration,
        SkillCapabilityBuilder SkillBuilder,
        ContextCapabilityBuilder ContextBuilder,
        McpCapabilityBuilder McpBuilder,
        ToolCapabilityBuilder ToolBuilder);

    private sealed class SkillCapabilityConfiguration
    {
        public string? SkillSource { get; set; }

        public string? SkillRoot { get; set; }

        public List<string>? AllowedExternalRoots { get; set; }

        public string? RegisteredSkillServiceType { get; set; }

        public InlineSkillDefinition? InlineSkill { get; set; }

        public bool? ScriptApproval { get; set; }

        public FileSkillScriptExecutionConfiguration? ScriptExecution { get; set; }
    }

    private sealed class FileSkillScriptExecutionConfiguration
    {
        public bool? ApprovalRequired { get; set; }

        public string? TrustLevel { get; set; }
    }

    private sealed class InlineSkillDefinition
    {
        public string? Name { get; set; }

        public string? Description { get; set; }

        public string? Instructions { get; set; }

        public List<InlineSkillResourceDefinition>? Resources { get; set; }
    }

    private sealed class InlineSkillResourceDefinition
    {
        public string? Name { get; set; }

        public string? Content { get; set; }

        public string? Description { get; set; }
    }

    private sealed class McpCapabilityConfiguration
    {
        public string? Transport { get; set; }

        public bool? Hosted { get; set; }

        public string? ServerName { get; set; }

        public string? Endpoint { get; set; }

        public string? Command { get; set; }

        public List<string>? Arguments { get; set; }

        public string? WorkingDirectory { get; set; }

        public List<string>? AllowedWorkingDirectories { get; set; }

        public Dictionary<string, string>? EnvironmentVariables { get; set; }

        public Dictionary<string, string>? EnvironmentVariableBindings { get; set; }

        public Dictionary<string, string>? Headers { get; set; }

        public Dictionary<string, string>? HeaderBindings { get; set; }

        public List<string>? AllowedTools { get; set; }

        public string? ApprovalMode { get; set; }
    }

    private sealed class RagCapabilityConfiguration
    {
        public string? RagRoot { get; set; }

        public List<string>? Extensions { get; set; }

        public List<string>? ExcludePaths { get; set; }

        public string? SearchTime { get; set; }

        public int? RecentMessageMemoryLimit { get; set; }

        public int? MaxResults { get; set; }
    }

    private sealed class AiContextCapabilityConfiguration
    {
        public string? Message { get; set; }

        public string? Role { get; set; }
    }

    private sealed class MemoryCapabilityConfiguration
    {
        public string? Provider { get; set; }

        public string? Endpoint { get; set; }

        public string? ApiKeyEnvironmentVariable { get; set; }

        public string? ApplicationId { get; set; }

        public string? AgentId { get; set; }

        public string? ThreadId { get; set; }

        public string? UserId { get; set; }

        public string? ContextPrompt { get; set; }

        public string? StateKey { get; set; }

        public bool? EnableSensitiveTelemetryData { get; set; }
    }

    private sealed class PluginCapabilityConfiguration
    {
        public string? RegisteredPluginServiceType { get; set; }

        public bool? ApprovalRequired { get; set; }
    }

    private sealed class BuiltInToolConfiguration
    {
        public string? Tool { get; set; }

        public bool? ApprovalRequired { get; set; }

        public bool? Enabled { get; set; }

        public int? MaximumResultCount { get; set; }

        public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
    }
}
