using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace CanDoItAll.AgentFramework.Maf;

internal sealed class AgentRuntimeConfiguration
{
    public bool? EnableCompaction { get; set; }

    public int? SlidingWindowTurns { get; set; }

    public int? TruncationTokenLimit { get; set; }

    public int? ToolCompactionMessageThreshold { get; set; }

    public int? MaxInjectedMemoryItems { get; set; }

    public int? MaxLocalRagResults { get; set; }

    public List<string>? PreferredSkillRoots { get; set; }
}

internal sealed record FileSkillExecutionPolicy(
    string RootPath,
    bool ApprovalRequired,
    string TrustLevel);

internal enum AgentRuntimeContextPolicyKind
{
    InteractiveChat = 0,
    GovernedProcessAutomation = 1,
    AutoApprovedNonInteractive = 2,
    A2AEndpoint = 3
}

internal sealed record RuntimeCompactionDecision(
    AgentRuntimeContextPolicyKind PolicyKind,
    bool ShouldAttachCompaction,
    string Message)
{
    public static RuntimeCompactionDecision Attach(
        AgentRuntimeContextPolicyKind policyKind,
        string message)
    {
        return new RuntimeCompactionDecision(policyKind, true, message);
    }

    public static RuntimeCompactionDecision Skip(
        AgentRuntimeContextPolicyKind policyKind,
        string message)
    {
        return new RuntimeCompactionDecision(policyKind, false, message);
    }
}

internal sealed record RuntimeCapabilityComposition(
    RuntimeCapabilityState State,
    AgentRuntimeConfiguration AgentConfiguration,
    SkillCapabilityBuilder SkillBuilder,
    ContextCapabilityBuilder ContextBuilder,
    IReadOnlyList<IAgentContextContributor> ContextContributors,
    IReadOnlyList<RuntimeToolProviderRegistration> RuntimeToolProviders,
    McpCapabilityBuilder McpBuilder,
    ToolCapabilityBuilder ToolBuilder,
    RuntimeCapabilityAccessPlan CapabilityAccessPlan);

internal sealed class SkillCapabilityConfiguration
{
    public string? SkillSource { get; set; }

    public string? SkillRoot { get; set; }

    public List<string>? AllowedExternalRoots { get; set; }

    public string? RegisteredSkillServiceType { get; set; }

    public InlineSkillDefinition? InlineSkill { get; set; }

    public bool? ScriptApproval { get; set; }

    public FileSkillScriptExecutionConfiguration? ScriptExecution { get; set; }
}

internal sealed class FileSkillScriptExecutionConfiguration
{
    public bool? ApprovalRequired { get; set; }

    public string? TrustLevel { get; set; }
}

internal sealed class InlineSkillDefinition
{
    public string? Name { get; set; }

    public string? Description { get; set; }

    public string? Instructions { get; set; }

    public List<InlineSkillResourceDefinition>? Resources { get; set; }
}

internal sealed class InlineSkillResourceDefinition
{
    public string? Name { get; set; }

    public string? Content { get; set; }

    public string? Description { get; set; }
}

internal sealed class McpCapabilityConfiguration
{
    public string? Transport { get; set; }

    public bool? Hosted { get; set; }

    public string? ServerName { get; set; }

    public string? Endpoint { get; set; }

    public string? Command { get; set; }

    public List<string>? Arguments { get; set; }

    public string? WorkingDirectory { get; set; }

    public string? MessageFraming { get; set; }

    public List<string>? AllowedWorkingDirectories { get; set; }

    public Dictionary<string, string>? EnvironmentVariables { get; set; }

    public Dictionary<string, string>? EnvironmentVariableBindings { get; set; }

    public Dictionary<string, string>? Headers { get; set; }

    public Dictionary<string, string>? HeaderBindings { get; set; }

    public List<string>? AllowedTools { get; set; }

    public string? ApprovalMode { get; set; }

    public int? TimeoutSeconds { get; set; }
}

internal sealed class RagCapabilityConfiguration
{
    public string? RagRoot { get; set; }

    public List<string>? Extensions { get; set; }

    public List<string>? ExcludePaths { get; set; }

    public string? SearchTime { get; set; }

    public int? RecentMessageMemoryLimit { get; set; }

    public int? MaxResults { get; set; }

    public int? MaxFilesToScan { get; set; }

    public int? MinQueryTerms { get; set; }

    public int? MinMatchedTerms { get; set; }

    public int? MinScore { get; set; }
}

internal sealed class AiContextCapabilityConfiguration
{
    public string? Message { get; set; }

    public string? Role { get; set; }
}

internal sealed class MemoryCapabilityConfiguration
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

internal sealed class PluginCapabilityConfiguration
{
    public string? RegisteredPluginServiceType { get; set; }

    public bool? ApprovalRequired { get; set; }
}

internal sealed class BuiltInToolConfiguration
{
    public string? Tool { get; set; }

    public bool? ApprovalRequired { get; set; }

    public bool? Enabled { get; set; }

    public int? MaximumResultCount { get; set; }

    public Dictionary<string, JsonElement>? AdditionalProperties { get; set; }
}

internal sealed record PreparedInputAttachments(
    string Prompt,
    AgentRuntimeExecutionOptions RuntimeOptions,
    IReadOnlyList<ProviderUsageObservation>? UsageObservations = null);

internal sealed record InputAttachmentAnalysis(
    string Name,
    string SourcePath,
    string Model,
    string Analysis,
    int InputTokens,
    int OutputTokens);

internal static class MafRuntimeToolApproval
{
    public static IEnumerable<AITool> ApplyApprovalRequirement(
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
}

internal static class MafRuntimeChatRoles
{
    public static ChatRole Parse(string? role)
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
}
