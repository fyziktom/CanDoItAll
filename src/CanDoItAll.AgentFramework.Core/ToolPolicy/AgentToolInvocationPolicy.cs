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
    public AgentToolPolicyBlockedException(string toolName, string reason)
        : base($"Tool '{toolName}' was blocked by policy. {reason}")
    {
        ToolName = toolName;
        Reason = reason;
    }

    public string ToolName { get; }

    public string Reason { get; }
}

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

    public static ToolInvocationClassification Classify(string? toolName)
    {
        if (string.IsNullOrWhiteSpace(toolName))
        {
            return ToolInvocationClassification.Unknown;
        }

        var normalized = toolName.Trim();
        if (IsMutationTool(normalized))
        {
            return ToolInvocationClassification.Mutation;
        }

        if (IsValidationTool(normalized))
        {
            return ToolInvocationClassification.Validation;
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
        return string.Equals(toolName, "workspace_dotnet_new", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_pwsh_run_script", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_python_run_file", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_create_directory", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_write_file", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_append_file", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_copy_path", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_move_path", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_delete_path", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsValidationTool(string toolName)
    {
        return string.Equals(toolName, "workspace_dotnet_restore", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_dotnet_build", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_dotnet_test", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, "workspace_dotnet_run", StringComparison.OrdinalIgnoreCase);
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
}
