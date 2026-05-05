using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

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
    IReadOnlyList<string>? AllowedExternalTargetAliases = null,
    IReadOnlyList<string>? ReadOnlyExternalTargetAliases = null,
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
    private static readonly Regex ExternalTargetAliasRegex = new(
        @"\bexternal-target/[A-Za-z](?:/[^\s,;`""'\]\)}]+)?",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
    private static readonly Regex ConsecutiveSlashRegex = new(
        "/{2,}",
        RegexOptions.CultureInvariant);
    private static readonly HashSet<string> ExternalTargetManagedWorkspaceIsolationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "workspace_list_files",
        "workspace_search",
        "workspace_read_file",
        "workspace_stat_path",
        "workspace_create_directory",
        "workspace_write_file",
        "workspace_append_file",
        "workspace_copy_path",
        "workspace_move_path",
        "workspace_delete_path",
        "workspace_dotnet_new",
        "workspace_dotnet_restore",
        "workspace_dotnet_build",
        "workspace_dotnet_test",
        "workspace_dotnet_run",
        "workspace_pwsh_run_script",
        "workspace_python_run_file"
    };
    private static readonly HashSet<string> BroadManagedWorkspaceDiscoveryTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "workspace_list_files",
        "workspace_search"
    };
    private static readonly HashSet<string> ProductFileMutationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "workspace_create_directory",
        "workspace_write_file",
        "workspace_append_file",
        "workspace_copy_path",
        "workspace_move_path",
        "workspace_delete_path",
        "workspace_dotnet_new"
    };
    private static readonly string[] ManagedWorkspacePathArgumentFragments =
    [
        "directory",
        "file",
        "folder",
        "path",
        "root",
        "script",
        "source",
        "target",
        "working"
    ];
    private static readonly string[] AllowedExternalRunManagedRoots =
    [
        ".playwright-mcp",
        "artifacts",
        "data",
        "integration-map",
        "output",
        "process-runs"
    ];
    private static readonly string[] DeniedExternalRunManagedRoots =
    [
        "bin",
        "obj",
        "scripts",
        "src",
        "tests",
        "tools"
    ];
    private static readonly string[] ManagedEvidenceRoots =
    [
        "artifacts",
        "data",
        "integration-map",
        "output"
    ];

    private readonly Dictionary<string, int> invocationCounts = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> dotnetNewTemplatesByScaffoldRoot = new(StringComparer.OrdinalIgnoreCase);
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

        if (context.Classification == ToolInvocationClassification.Unknown)
        {
            return ValueTask.FromResult(ToolInvocationPolicyDecision.Deny(
                signature,
                $"Tool '{context.ToolName}' has no registered invocation policy classification."));
        }

        var governedBrowserDecision = EvaluateGovernedBrowserToolBounds(context, signature);
        if (governedBrowserDecision is not null)
        {
            return ValueTask.FromResult(governedBrowserDecision);
        }

        var externalTargetDecision = EvaluateGovernedExternalTargetIsolation(context, signature);
        if (externalTargetDecision is not null)
        {
            return ValueTask.FromResult(externalTargetDecision);
        }

        var managedWorkspaceIsolationDecision = EvaluateExternalTargetManagedWorkspaceIsolation(context, signature);
        if (managedWorkspaceIsolationDecision is not null)
        {
            return ValueTask.FromResult(managedWorkspaceIsolationDecision);
        }

        var readOnlyExternalTargetDecision = EvaluateReadOnlyExternalTargetMutation(context, signature);
        if (readOnlyExternalTargetDecision is not null)
        {
            return ValueTask.FromResult(readOnlyExternalTargetDecision);
        }

        var dotnetNewTemplateConsistencyDecision = EvaluateDotnetNewTemplateConsistency(context, signature);
        if (dotnetNewTemplateConsistencyDecision is not null)
        {
            return ValueTask.FromResult(dotnetNewTemplateConsistencyDecision);
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

    private static ToolInvocationPolicyDecision? EvaluateGovernedBrowserToolBounds(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsGovernedProcessRun(context))
        {
            return null;
        }

        if (string.Equals(context.ToolName, "browser_snapshot", StringComparison.OrdinalIgnoreCase))
        {
            var depthAllowed = context.RedactedArguments.TryGetValue("depth", out var depthValue) &&
                               int.TryParse(depthValue, out var depth) &&
                               depth <= 4;
            if (!depthAllowed)
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "Governed process browser snapshots must set depth to 4 or less. Retry with a bounded snapshot such as depth=2.");
            }

            if (context.RedactedArguments.TryGetValue("boxes", out var boxesValue) &&
                bool.TryParse(boxesValue, out var boxes) &&
                boxes)
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "Governed process browser snapshots must not request element boxes because they can produce oversized tool output. Retry with boxes=false.");
            }

            return null;
        }

        if (string.Equals(context.ToolName, "browser_take_screenshot", StringComparison.OrdinalIgnoreCase) &&
            context.RedactedArguments.TryGetValue("fullPage", out var fullPageValue) &&
            bool.TryParse(fullPageValue, out var fullPage) &&
            fullPage)
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                "Governed process browser screenshots must be viewport-bounded. Retry with fullPage=false or omit fullPage.");
        }

        return null;
    }

    public void RecordSuccessfulInvocation(ToolInvocationPolicyContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        RecordSuccessfulDotnetNewTemplate(context);
    }

    private ToolInvocationPolicyDecision? EvaluateDotnetNewTemplateConsistency(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!string.Equals(context.ToolName, "workspace_dotnet_new", StringComparison.OrdinalIgnoreCase) ||
            !context.RedactedArguments.TryGetValue("template", out var template))
        {
            return null;
        }

        var scaffoldRoot = ResolveDotnetNewScaffoldRoot(context.RedactedArguments);
        var normalizedTemplate = NormalizeToolArgument(template);
        if (IsSolutionDotnetNewTemplate(normalizedTemplate) ||
            string.IsNullOrWhiteSpace(scaffoldRoot) ||
            string.IsNullOrWhiteSpace(normalizedTemplate))
        {
            return null;
        }

        if (dotnetNewTemplatesByScaffoldRoot.TryGetValue(scaffoldRoot, out var previousTemplate) &&
            !string.Equals(previousTemplate, normalizedTemplate, StringComparison.OrdinalIgnoreCase))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"workspace_dotnet_new already scaffolded target '{scaffoldRoot}' with template '{previousTemplate}' in this run. Do not layer a second template such as '{normalizedTemplate}' into the same project root; inspect and repair the existing scaffold explicitly.");
        }

        return null;
    }

    private void RecordSuccessfulDotnetNewTemplate(ToolInvocationPolicyContext context)
    {
        if (!string.Equals(context.ToolName, "workspace_dotnet_new", StringComparison.OrdinalIgnoreCase) ||
            !context.RedactedArguments.TryGetValue("template", out var template))
        {
            return;
        }

        var scaffoldRoot = ResolveDotnetNewScaffoldRoot(context.RedactedArguments);
        var normalizedTemplate = NormalizeToolArgument(template);
        if (IsSolutionDotnetNewTemplate(normalizedTemplate) ||
            string.IsNullOrWhiteSpace(scaffoldRoot) ||
            string.IsNullOrWhiteSpace(normalizedTemplate))
        {
            return;
        }

        dotnetNewTemplatesByScaffoldRoot[scaffoldRoot] = normalizedTemplate;
    }

    private static bool IsSolutionDotnetNewTemplate(string template)
        => string.Equals(template, "sln", StringComparison.OrdinalIgnoreCase);

    private static ToolInvocationPolicyDecision? EvaluateGovernedExternalTargetIsolation(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsGovernedProcessRun(context))
        {
            return null;
        }

        var referencedAliases = ResolveReferencedExternalTargetAliases(context.RedactedArguments);
        if (referencedAliases.Count == 0)
        {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        foreach (var referencedAlias in referencedAliases)
        {
            if (IsAllowedExternalTargetAlias(referencedAlias, allowedAliases) ||
                IsAllowedScaffoldParentAlias(context, referencedAlias, allowedAliases))
            {
                continue;
            }

            var allowedSummary = allowedAliases.Count == 0
                ? "no external-target roots are grounded for this run"
                : $"allowed roots: {string.Join(", ", allowedAliases)}";
            var currentRunGuidance = BuildCurrentRunExternalTargetGuidance(allowedAliases);
            var scaffoldGuidance = BuildScaffoldParentGuidance(context, allowedAliases);
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Governed process runs may only access external-target paths grounded by the current run. Denied '{referencedAlias}' because {allowedSummary}.{currentRunGuidance}{scaffoldGuidance}");
        }

        return null;
    }

    private static string BuildCurrentRunExternalTargetGuidance(IReadOnlyList<string> allowedAliases)
    {
        if (allowedAliases.Count == 0)
        {
            return " No external product root is grounded for this run, so abandon the denied external-target path instead of retrying it.";
        }

        return $" Current-run product root is '{allowedAliases[0]}'. Abandon the denied external-target path and inspect or modify only that root, its children, or current-run managed artifact folders.";
    }

    private static string BuildScaffoldParentGuidance(
        ToolInvocationPolicyContext context,
        IReadOnlyList<string> allowedAliases)
    {
        if (!string.Equals(context.ToolName, "workspace_dotnet_new", StringComparison.OrdinalIgnoreCase) ||
            allowedAliases.Count == 0 ||
            !context.RedactedArguments.TryGetValue("name", out var name))
        {
            return string.Empty;
        }

        var normalizedName = NormalizeExternalTargetChildName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return string.Empty;
        }

        var groundedRoot = allowedAliases[0];
        if (normalizedName.EndsWith(".Tests", StringComparison.OrdinalIgnoreCase) ||
            normalizedName.EndsWith(".Test", StringComparison.OrdinalIgnoreCase))
        {
            return $" For test scaffolds, use a parentDirectory under the grounded product root, for example '{groundedRoot}/tests' with name '{normalizedName}', not the product parent.";
        }

        return $" For additional scaffolds, use a parentDirectory under the grounded product root, for example '{groundedRoot}/src' or '{groundedRoot}/tests', not the product parent.";
    }

    private static ToolInvocationPolicyDecision? EvaluateExternalTargetManagedWorkspaceIsolation(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsGovernedProcessRun(context) ||
            !ExternalTargetManagedWorkspaceIsolationTools.Contains(context.ToolName))
        {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        if (allowedAliases.Count == 0)
        {
            return null;
        }

        var pathArguments = ResolveManagedWorkspacePathArguments(context.RedactedArguments);
        if (pathArguments.Count == 0)
        {
            return BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName)
                ? ToolInvocationPolicyDecision.Deny(
                    signature,
                    "This governed run has a grounded external product target. Broad managed-workspace root discovery is denied because it can pull stale source or helper files from unrelated runs; list or search the grounded external-target alias or current-run artifact folders instead.")
                : null;
        }

        foreach (var pathArgument in pathArguments)
        {
            var normalizedPath = NormalizeManagedWorkspacePath(pathArgument.Value);
            if (string.IsNullOrWhiteSpace(normalizedPath) ||
                IsExternalTargetAliasPath(normalizedPath))
            {
                continue;
            }

            if (IsShallowSharedManagedEvidencePath(normalizedPath))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed run has a grounded external product target. Managed workspace path '{normalizedPath}' is a shallow shared scope artifact path and may be overwritten by unrelated concurrent runs; use the current-run artifact root '{BuildCurrentRunManagedArtifactRoot(context)}' unless a required artifact input or output names a deeper managed path.");
            }

            if (BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName) &&
                IsBroadManagedEvidenceDiscoveryPath(normalizedPath))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed run has a grounded external product target. Broad managed evidence discovery at '{normalizedPath}' is denied because it can pull stale artifacts from unrelated runs; list or search the grounded external-target alias or current-run artifact root '{BuildCurrentRunManagedArtifactRoot(context)}' instead.");
            }

            if (IsAllowedExternalRunManagedPath(normalizedPath))
            {
                continue;
            }

            if (IsBroadManagedWorkspacePath(normalizedPath) &&
                BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "This governed run has a grounded external product target. Broad managed-workspace root discovery is denied because it can pull stale source or helper files from unrelated runs; list or search the grounded external-target alias or current-run artifact folders instead.");
            }

            if (IsDeniedExternalRunManagedPath(normalizedPath))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed run has a grounded external product target. Managed workspace path '{normalizedPath}' is outside current-run evidence folders and may contain stale source or helper files from unrelated runs; use the grounded external-target alias or current-run artifacts instead.");
            }
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateReadOnlyExternalTargetMutation(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsGovernedProcessRun(context) ||
            !ProductFileMutationTools.Contains(context.ToolName))
        {
            return null;
        }

        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        if (readOnlyAliases.Count == 0)
        {
            return null;
        }

        var referencedAliases = ResolveReferencedExternalTargetAliases(context.RedactedArguments);
        var matchedAlias = referencedAliases.FirstOrDefault(referencedAlias =>
            readOnlyAliases.Any(readOnlyAlias =>
                string.Equals(referencedAlias, readOnlyAlias, StringComparison.OrdinalIgnoreCase) ||
                referencedAlias.StartsWith(readOnlyAlias + "/", StringComparison.OrdinalIgnoreCase)));
        if (string.IsNullOrWhiteSpace(matchedAlias))
        {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"This governed step has read-only access to product target '{matchedAlias}'. Use read, build, test, run, browser, and durable evidence-artifact tools for validation; route defects to a repair implementation step instead of mutating product files from a review or QA step.");
    }

    private static bool IsGovernedProcessRun(ToolInvocationPolicyContext context)
    {
        return string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(context.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(context.ProcessStepId);
    }

    private static IReadOnlyList<string> ResolveReferencedExternalTargetAliases(
        IReadOnlyDictionary<string, string> arguments)
    {
        return arguments
            .Where(argument => IsPathLikeArgumentName(argument.Key))
            .SelectMany(argument => ExternalTargetAliasRegex
                .Matches(argument.Value ?? string.Empty)
                .Select(match => NormalizeExternalTargetAlias(match.Value)))
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> NormalizeAllowedExternalTargetAliases(
        IReadOnlyList<string>? aliases)
    {
        return aliases?
            .Select(NormalizeExternalTargetAlias)
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(alias => alias.Length)
            .ToArray() ?? [];
    }

    private static bool IsAllowedExternalTargetAlias(
        string referencedAlias,
        IReadOnlyList<string> allowedAliases)
    {
        return allowedAliases.Any(allowedAlias =>
            string.Equals(referencedAlias, allowedAlias, StringComparison.OrdinalIgnoreCase) ||
            referencedAlias.StartsWith(allowedAlias + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsAllowedScaffoldParentAlias(
        ToolInvocationPolicyContext context,
        string referencedAlias,
        IReadOnlyList<string> allowedAliases)
    {
        if (!string.Equals(context.ToolName, "workspace_dotnet_new", StringComparison.OrdinalIgnoreCase) ||
            !context.RedactedArguments.TryGetValue("parentDirectory", out var parentDirectory) ||
            !context.RedactedArguments.TryGetValue("name", out var name))
        {
            return false;
        }

        var normalizedParentDirectory = NormalizeExternalTargetAlias(parentDirectory);
        if (!string.Equals(referencedAlias, normalizedParentDirectory, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var normalizedName = NormalizeExternalTargetChildName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return false;
        }

        var requestedScaffoldRoot = NormalizeExternalTargetAlias($"{normalizedParentDirectory}/{normalizedName}");
        return allowedAliases.Any(allowedAlias =>
            string.Equals(requestedScaffoldRoot, allowedAlias, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeExternalTargetAlias(string? alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
        {
            return string.Empty;
        }

        var normalizedAlias = alias
            .Replace('\\', '/')
            .Trim()
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');

        return ConsecutiveSlashRegex.Replace(normalizedAlias, "/");
    }

    private static string NormalizeExternalTargetChildName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var normalizedName = name
            .Replace('\\', '/')
            .Trim()
            .Trim('`', '"', '\'')
            .Trim('/');

        return normalizedName.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(segment => string.Equals(segment, ".", StringComparison.Ordinal) || string.Equals(segment, "..", StringComparison.Ordinal))
                ? string.Empty
                : normalizedName;
    }

    private static string ResolveDotnetNewScaffoldRoot(IReadOnlyDictionary<string, string> arguments)
    {
        arguments.TryGetValue("parentDirectory", out var parentDirectory);
        arguments.TryGetValue("name", out var name);

        var normalizedName = NormalizeExternalTargetChildName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
        {
            return string.Empty;
        }

        var combinedPath = string.IsNullOrWhiteSpace(parentDirectory)
            ? normalizedName
            : $"{parentDirectory}/{normalizedName}";

        return NormalizeToolPath(combinedPath);
    }

    private static string NormalizeToolArgument(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Trim('`', '"', '\'');
    }

    private static string NormalizeToolPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalizedPath = path
            .Replace('\\', '/')
            .Trim()
            .Trim('`', '"', '\'')
            .TrimEnd('/', '.', ',', ';', ':', ')', ']', '}');
        normalizedPath = ConsecutiveSlashRegex.Replace(normalizedPath, "/");

        while (normalizedPath.StartsWith("./", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath[2..];
        }

        return normalizedPath.TrimStart('/');
    }

    private static IReadOnlyList<KeyValuePair<string, string>> ResolveManagedWorkspacePathArguments(
        IReadOnlyDictionary<string, string> arguments)
    {
        return arguments
            .Where(argument => IsPathLikeArgumentName(argument.Key))
            .Where(argument => !string.IsNullOrWhiteSpace(argument.Value))
            .ToArray();
    }

    private static bool IsPathLikeArgumentName(string argumentName)
    {
        return !string.IsNullOrWhiteSpace(argumentName) &&
               ManagedWorkspacePathArgumentFragments.Any(fragment =>
                   argumentName.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsExternalTargetAliasPath(string normalizedPath)
    {
        return normalizedPath.StartsWith("external-target/", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBroadManagedWorkspacePath(string normalizedPath)
    {
        return string.IsNullOrWhiteSpace(normalizedPath) ||
               string.Equals(normalizedPath, ".", StringComparison.Ordinal) ||
               string.Equals(normalizedPath, "./", StringComparison.Ordinal) ||
               string.Equals(normalizedPath, "*", StringComparison.Ordinal);
    }

    private static bool IsAllowedExternalRunManagedPath(string normalizedPath)
    {
        return AllowedExternalRunManagedRoots.Any(root =>
            string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsDeniedExternalRunManagedPath(string normalizedPath)
    {
        return DeniedExternalRunManagedRoots.Any(root =>
            string.Equals(normalizedPath, root, StringComparison.OrdinalIgnoreCase) ||
            normalizedPath.StartsWith(root + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsBroadManagedEvidenceDiscoveryPath(string normalizedPath)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        if (ManagedEvidenceRoots.Any(root => string.Equals(segments[0], root, StringComparison.OrdinalIgnoreCase)))
        {
            return segments.Length == 1 ||
                   (segments.Length is >= 2 and <= 4 &&
                    string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase));
        }

        return string.Equals(segments[0], "process-runs", StringComparison.OrdinalIgnoreCase) &&
               segments.Length == 1;
    }

    private static bool IsShallowSharedManagedEvidencePath(string normalizedPath)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 2 &&
            IsManagedEvidenceRoot(segments[0]) &&
            HasFileExtension(segments[1]))
        {
            return true;
        }

        return segments.Length is 4 or 5 &&
               IsManagedEvidenceRoot(segments[0]) &&
               string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedEvidenceRoot(string segment)
    {
        return ManagedEvidenceRoots.Any(root => string.Equals(segment, root, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasFileExtension(string segment)
    {
        var dotIndex = segment.LastIndexOf('.');
        return dotIndex > 0 && dotIndex < segment.Length - 1;
    }

    private static string BuildCurrentRunManagedArtifactRoot(ToolInvocationPolicyContext context)
    {
        return string.IsNullOrWhiteSpace(context.ProcessRunId)
            ? "artifacts/process-runs/<current-run-id>"
            : $"artifacts/process-runs/{context.ProcessRunId.Trim()}";
    }

    private static string NormalizeManagedWorkspacePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        var normalizedPath = path
            .Replace('\\', '/')
            .Trim()
            .Trim('`', '"', '\'')
            .TrimEnd('.', ',', ';', ':', ')', ']', '}');
        normalizedPath = ConsecutiveSlashRegex.Replace(normalizedPath, "/");

        while (normalizedPath.StartsWith("./", StringComparison.Ordinal))
        {
            normalizedPath = normalizedPath[2..];
        }

        return normalizedPath.TrimStart('/');
    }
}

public static class AgentToolInvocationPolicyMetadata
{
    public const string LoadSkill = "load_skill";
    public const string ReadSkillResource = "read_skill_resource";
    public const string RunSkillScript = "run_skill_script";
    public const string ProcessesDefinitionSave = "processes_definition_save";
    public const string ProcessesDefinitionRoleAdd = "processes_definition_role_add";
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
            Read(LoadSkill),
            Read(ReadSkillResource),
            Mutation(RunSkillScript),
            Mutation(ProcessesDefinitionSave),
            Mutation(ProcessesDefinitionRoleAdd),
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

        if (normalized.StartsWith("processes_", StringComparison.OrdinalIgnoreCase))
        {
            return ToolInvocationClassification.Unknown;
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
        return text.Length <= 160 ? text : text[..160] + $"...#{ComputeStableHash(text)}";
    }

    private static string ComputeStableHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
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
