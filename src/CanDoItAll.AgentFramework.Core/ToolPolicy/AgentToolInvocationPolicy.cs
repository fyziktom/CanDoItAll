using System.Text.Json;

namespace CanDoItAll.AgentFramework.Core;

public enum ToolInvocationDecisionKind
{
    Allow,
    Deny,
    RequireApproval,
    SanitizeResult,
    SkipExecution
}

public enum ToolInvocationClassification
{
    Unknown,
    Read,
    Mutation,
    Validation,
    HostedProviderNative,
    LocalMcp,
    HostedMcp
}

public sealed class AgentToolPolicyBlockedException : Exception
{
    public AgentToolPolicyBlockedException(
        string toolName,
        ToolInvocationDecisionKind decisionKind,
        string reason)
        : base($"Tool '{toolName}' was blocked by policy. {reason}")
    {
        ToolName = toolName;
        DecisionKind = decisionKind;
        Reason = reason;
    }

    public string ToolName { get; }

    public ToolInvocationDecisionKind DecisionKind { get; }

    public string Reason { get; }
}

public sealed record AgentToolInvocationTrace(
    string ToolName,
    ToolInvocationClassification Classification,
    int Sequence,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    bool Succeeded,
    string FailureMessage);

public sealed record AgentToolPolicyMetadata(
    string Name,
    ToolInvocationClassification Classification,
    bool RequiresApprovalByDefault,
    bool IsStateChanging);

public sealed record ToolInvocationPolicyContext(
    Guid AgentId,
    string AgentName,
    string ToolName,
    IReadOnlyDictionary<string, string> RedactedArguments,
    ToolInvocationClassification Classification,
    bool IsKnownTool,
    bool AutoApprovalAllowed,
    bool ApprovalWrapperAvailable,
    string ExecutionRunId,
    string SourceKind,
    string ProcessRunId,
    string ProcessStepId,
    bool ApprovalWrapperEffectiveForProvider = false,
    bool ApplicationApprovalAvailable = false)
{
    public bool HasEffectiveApprovalPath =>
        (ApprovalWrapperAvailable && ApprovalWrapperEffectiveForProvider) ||
        ApplicationApprovalAvailable;
}

public sealed record ToolInvocationPolicyDecision(
    ToolInvocationDecisionKind Kind,
    string Reason,
    string Signature)
{
    public static ToolInvocationPolicyDecision Allow(string signature)
        => new(ToolInvocationDecisionKind.Allow, "Tool invocation is allowed.", signature);

    public static ToolInvocationPolicyDecision RequireApproval(string signature, string reason)
        => new(ToolInvocationDecisionKind.RequireApproval, reason, signature);

    public static ToolInvocationPolicyDecision Deny(string signature, string reason)
        => new(ToolInvocationDecisionKind.Deny, reason, signature);
}

public interface IAgentToolInvocationPolicy
{
    ValueTask<ToolInvocationPolicyDecision> EvaluateAsync(
        ToolInvocationPolicyContext context,
        CancellationToken cancellationToken);
}

public static class AgentToolPolicyBlockGuard
{
    public static void ThrowIfBlocked(
        string toolName,
        ToolInvocationPolicyDecision decision,
        bool hasEffectiveApprovalPath)
    {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.Kind is ToolInvocationDecisionKind.Deny or ToolInvocationDecisionKind.SkipExecution)
        {
            throw new AgentToolPolicyBlockedException(toolName, decision.Kind, decision.Reason);
        }

        if (decision.Kind == ToolInvocationDecisionKind.RequireApproval && !hasEffectiveApprovalPath)
        {
            throw new AgentToolPolicyBlockedException(toolName, decision.Kind, decision.Reason);
        }
    }
}

public sealed class DefaultAgentToolInvocationPolicy : IAgentToolInvocationPolicy
{
    public const int MaxRepeatedMutationOrValidationInvocations = 3;

    private readonly Dictionary<string, int> invocationCounts = new(StringComparer.OrdinalIgnoreCase);
    private int mutationGeneration;

    public ValueTask<ToolInvocationPolicyDecision> EvaluateAsync(
        ToolInvocationPolicyContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var signature = AgentToolInvocationPolicyMetadata.BuildSignature(
            context.ToolName,
            context.RedactedArguments);

        if (!context.IsKnownTool)
        {
            return ValueTask.FromResult(ToolInvocationPolicyDecision.Deny(
                signature,
                $"Tool '{context.ToolName}' is not part of the composed capability set for agent '{context.AgentId:N}'."));
        }

        if (context.Classification is ToolInvocationClassification.Mutation or ToolInvocationClassification.Validation)
        {
            var countedSignature = context.Classification == ToolInvocationClassification.Validation
                ? $"{signature}|mutationGeneration={mutationGeneration}"
                : signature;
            var invocationCount = invocationCounts.TryGetValue(countedSignature, out var currentCount)
                ? currentCount + 1
                : 1;
            invocationCounts[countedSignature] = invocationCount;

            if (invocationCount > MaxRepeatedMutationOrValidationInvocations)
            {
                return ValueTask.FromResult(ToolInvocationPolicyDecision.Deny(
                    countedSignature,
                    $"Tool '{context.ToolName}' repeated the same mutation or validation signature {invocationCount} times in one run."));
            }
        }

        if (context.Classification == ToolInvocationClassification.Mutation)
        {
            mutationGeneration++;
            if (context.AutoApprovalAllowed)
            {
                return ValueTask.FromResult(ToolInvocationPolicyDecision.Allow(signature));
            }

            var approvalReason = context.HasEffectiveApprovalPath
                ? $"Tool '{context.ToolName}' is a mutation tool and must pass through the configured approval path."
                : $"Tool '{context.ToolName}' is a mutation tool, but no effective approval path is available for this provider and run.";
            return ValueTask.FromResult(ToolInvocationPolicyDecision.RequireApproval(
                signature,
                approvalReason));
        }

        return ValueTask.FromResult(ToolInvocationPolicyDecision.Allow(signature));
    }
}

public static class AgentToolInvocationPolicyMetadata
{
    public const string ProcessesDefinitionSave = "processes_definition_save";
    public const string ProcessesDefinitionPublish = "processes_definition_publish";
    public const string ProcessesDefinitionDelete = "processes_definition_delete";
    public const string ProcessesDefinitionImport = "processes_definition_import";
    public const string ProcessesRunStart = "processes_run_start";
    public const string ProcessesStepTransition = "processes_step_transition";
    public const string ProcessesAssignmentResolve = "processes_assignment_resolve";
    public const string ProcessesArtifactRecord = "processes_artifact_record";
    public const string ProcessesDefinitionsList = "processes_definitions_list";
    public const string ProcessesDefinitionEditorGet = "processes_definition_editor_get";
    public const string ProcessesDefinitionExport = "processes_definition_export";
    public const string ProcessesRunsList = "processes_runs_list";
    public const string ProcessesRunDetailGet = "processes_run_detail_get";
    public const string ProcessesAnalyticsGet = "processes_analytics_get";
    public const string ProcessesPartyOptionsList = "processes_party_options_list";
    public const string ProcessesExecutorOptionsList = "processes_executor_options_list";
    public const string ProcessesTemplatesList = "processes_templates_list";
    public const string ProcessesTemplateGet = "processes_template_get";
    public const string ProcessesTemplateMermaidGet = "processes_template_mermaid_get";
    public const string ProcessesTemplateImport = "processes_template_import";
    public const string ProcessesTemplateBaselineScenariosList = "processes_template_baseline_scenarios_list";

    private static readonly string[] SensitiveArgumentNameFragments =
    [
        "api_key",
        "apikey",
        "authorization",
        "credential",
        "header",
        "password",
        "secret",
        "token"
    ];

    private static readonly IReadOnlyDictionary<string, AgentToolPolicyMetadata> RegisteredTools =
        new[]
        {
            Mutation("workspace_dotnet_new"),
            Mutation("workspace_pwsh_run_script"),
            Mutation("workspace_python_run_file"),
            Mutation("workspace_create_directory"),
            Mutation("workspace_write_file"),
            Mutation("workspace_append_file"),
            Mutation("workspace_copy_path"),
            Mutation("workspace_move_path"),
            Mutation("workspace_delete_path"),
            Validation("workspace_dotnet_restore"),
            Validation("workspace_dotnet_build"),
            Validation("workspace_dotnet_test"),
            Validation("workspace_dotnet_run"),
            Mutation(ProcessesDefinitionSave),
            Mutation(ProcessesDefinitionPublish),
            Mutation(ProcessesDefinitionDelete),
            Mutation(ProcessesDefinitionImport),
            Mutation(ProcessesRunStart),
            Mutation(ProcessesStepTransition),
            Mutation(ProcessesAssignmentResolve),
            Mutation(ProcessesArtifactRecord),
            Mutation(ProcessesTemplateImport),
            Read(ProcessesDefinitionsList),
            Read(ProcessesDefinitionEditorGet),
            Read(ProcessesDefinitionExport),
            Read(ProcessesRunsList),
            Read(ProcessesRunDetailGet),
            Read(ProcessesAnalyticsGet),
            Read(ProcessesPartyOptionsList),
            Read(ProcessesExecutorOptionsList),
            Read(ProcessesTemplatesList),
            Read(ProcessesTemplateGet),
            Read(ProcessesTemplateMermaidGet),
            Read(ProcessesTemplateBaselineScenariosList)
        }.ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<AgentToolPolicyMetadata> Tools => RegisteredTools.Values.ToList();

    public static ToolInvocationClassification Classify(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return ToolInvocationClassification.Unknown;
        }

        var normalized = toolName.Trim();
        if (RegisteredTools.TryGetValue(normalized, out var metadata))
        {
            return metadata.Classification;
        }

        if (normalized.StartsWith("provider_native_", StringComparison.OrdinalIgnoreCase) ||
            normalized.StartsWith("provider-native-", StringComparison.OrdinalIgnoreCase))
        {
            return ToolInvocationClassification.HostedProviderNative;
        }

        if (normalized.StartsWith("mcp_", StringComparison.OrdinalIgnoreCase))
        {
            return ToolInvocationClassification.LocalMcp;
        }

        return ToolInvocationClassification.Read;
    }

    public static bool IsMutationTool(string toolName)
    {
        return RegisteredTools.TryGetValue(toolName, out var metadata) &&
               metadata.Classification == ToolInvocationClassification.Mutation;
    }

    public static bool IsValidationTool(string toolName)
    {
        return RegisteredTools.TryGetValue(toolName, out var metadata) &&
               metadata.Classification == ToolInvocationClassification.Validation;
    }

    public static bool RequiresApprovalByDefault(string toolName)
    {
        return RegisteredTools.TryGetValue(toolName, out var metadata) &&
               metadata.RequiresApprovalByDefault;
    }

    public static IReadOnlyDictionary<string, string> RedactArguments(
        IEnumerable<KeyValuePair<string, object?>> arguments)
    {
        return arguments
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => item.Key,
                item => ShouldRedact(item.Key) ? "<redacted>" : FormatArgumentValue(item.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    public static string BuildSignature(
        string toolName,
        IReadOnlyDictionary<string, string> redactedArguments)
    {
        var normalizedToolName = string.IsNullOrWhiteSpace(toolName)
            ? "unknown"
            : toolName.Trim();
        if (redactedArguments.Count == 0)
        {
            return normalizedToolName;
        }

        var argumentSignature = string.Join(
            ",",
            redactedArguments
                .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
                .Select(item => $"{item.Key}={item.Value}"));
        return $"{normalizedToolName}|{argumentSignature}";
    }

    private static bool ShouldRedact(string key)
    {
        return SensitiveArgumentNameFragments.Any(fragment =>
            key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string FormatArgumentValue(object? value)
    {
        if (value is null)
        {
            return "<null>";
        }

        var text = value switch
        {
            string stringValue => stringValue,
            JsonElement jsonValue => jsonValue.ToString(),
            _ => JsonSerializer.Serialize(value, AgentOutputJson.SerializerOptions)
        };

        text = text.ReplaceLineEndings(" ").Trim();
        return text.Length <= 160 ? text : text[..160] + "...";
    }

    private static AgentToolPolicyMetadata Mutation(string name)
    {
        return new AgentToolPolicyMetadata(
            name,
            ToolInvocationClassification.Mutation,
            RequiresApprovalByDefault: true,
            IsStateChanging: true);
    }

    private static AgentToolPolicyMetadata Validation(string name)
    {
        return new AgentToolPolicyMetadata(
            name,
            ToolInvocationClassification.Validation,
            RequiresApprovalByDefault: false,
            IsStateChanging: false);
    }

    private static AgentToolPolicyMetadata Read(string name)
    {
        return new AgentToolPolicyMetadata(
            name,
            ToolInvocationClassification.Read,
            RequiresApprovalByDefault: false,
            IsStateChanging: false);
    }
}
