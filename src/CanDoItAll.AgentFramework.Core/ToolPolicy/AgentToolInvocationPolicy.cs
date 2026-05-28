using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Models;

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
    bool ApplicationApprovalAvailable = false,
    bool ProcessScaffoldToolOnly = false,
    bool ProcessAllowsProductMutation = true,
    IReadOnlyList<string>? ProcessStepAllowedOperations = null,
    string ProcessStepTargetScope = "",
    string InspectedScriptContent = "",
    string ScriptInspectionFailure = "",
    string ScriptSideEffectManifestJson = "")
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

internal sealed record OperationRequirement(IReadOnlyList<string> AnyOf)
{
    public static OperationRequirement Any(params string[] operations)
    {
        return new OperationRequirement(operations
            .Where(operation => !string.IsNullOrWhiteSpace(operation))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray());
    }
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
        "workspace_python_run_file",
        "workspace_inspect_image"
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
    private static readonly HashSet<string> WorkspaceScriptExecutionTools = new(StringComparer.OrdinalIgnoreCase)
    {
        AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
        AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile
    };
    private static readonly HashSet<string> DirectProductFileMutationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "workspace_write_file",
        "workspace_append_file",
        "workspace_copy_path",
        "workspace_move_path",
        "workspace_delete_path"
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
    private static readonly HashSet<string> ExternalProductArchiveSourceSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        ".oldruns",
        "oldruns",
        "old-runs",
        "previous-runs",
        "backup",
        "backups",
        "archive",
        "archives",
        "agent-evidence",
        "observation",
        "process-definition",
        "process-definitions",
        "launch-plan",
        "launch-plans",
        "evidence-only"
    };
    private static readonly string[] ManagedEvidenceRoots =
    [
        "artifacts",
        "data",
        "integration-map",
        "output"
    ];
    private static readonly HashSet<string> BrowserProofTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "browser_console_messages",
        "browser_evaluate",
        "browser_network_requests",
        "browser_snapshot",
        "browser_take_screenshot"
    };
    private static readonly HashSet<string> DotNetValidationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        "workspace_dotnet_restore",
        "workspace_dotnet_build",
        "workspace_dotnet_test"
    };
    private static readonly HashSet<string> ProcessDefinitionMutationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionSave,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionRoleAdd,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionPublish,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionDelete,
        AgentToolInvocationPolicyMetadata.ProcessesDefinitionImport,
        AgentToolInvocationPolicyMetadata.ProcessesRunStart,
        AgentToolInvocationPolicyMetadata.ProcessesAssignmentResolve,
        AgentToolInvocationPolicyMetadata.ProcessesTemplateImport
    };
    private const string OperationWriteManagedProcessArtifacts = "WriteManagedProcessArtifacts";
    private const string OperationWriteExternalArtifactDestination = "WriteExternalArtifactDestination";
    private const string OperationMutateProductTarget = "MutateProductTarget";
    private const string OperationRunValidation = "RunValidation";
    private const string OperationLaunchRuntime = "LaunchRuntime";
    private const string OperationCaptureRuntimeProof = "CaptureRuntimeProof";
    private const string OperationExecuteExternalAction = "ExecuteExternalAction";
    private const string OperationRecoverArtifactsOnly = "RecoverArtifactsOnly";
    private const string OperationEscalateOrDecide = "EscalateOrDecide";

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

        var operationDecision = EvaluateGovernedProcessOperationAuthorization(context, signature);
        if (operationDecision is not null)
        {
            return ValueTask.FromResult(operationDecision);
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

        var staleProductCopyDecision = EvaluateGovernedStaleExternalProductCopySource(context, signature);
        if (staleProductCopyDecision is not null)
        {
            return ValueTask.FromResult(staleProductCopyDecision);
        }

        var archivedExternalProductPathDecision = EvaluateGovernedArchivedExternalProductPathAccess(context, signature);
        if (archivedExternalProductPathDecision is not null)
        {
            return ValueTask.FromResult(archivedExternalProductPathDecision);
        }

        var processBoundaryDecision = EvaluateGovernedProcessProductMutationBoundary(context, signature);
        if (processBoundaryDecision is not null)
        {
            return ValueTask.FromResult(processBoundaryDecision);
        }

        var scriptSideEffectDecision = EvaluateGovernedScriptSideEffectBoundary(context, signature);
        if (scriptSideEffectDecision is not null)
        {
            return ValueTask.FromResult(scriptSideEffectDecision);
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

        var scaffoldToolOnlyDecision = EvaluateScaffoldToolOnlyDirectProductMutation(context, signature);
        if (scaffoldToolOnlyDecision is not null)
        {
            return ValueTask.FromResult(scaffoldToolOnlyDecision);
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

    private static ToolInvocationPolicyDecision? EvaluateGovernedProcessOperationAuthorization(
        ToolInvocationPolicyContext context,
        string signature)
    {
        return ProcessToolOperationAuthorizer.Evaluate(context, signature, ResolveOperationRequirements(context));
    }

    private static IReadOnlyList<OperationRequirement> ResolveOperationRequirements(ToolInvocationPolicyContext context)
    {
        if (DotNetValidationTools.Contains(context.ToolName))
        {
            return [OperationRequirement.Any(OperationRunValidation)];
        }

        if (string.Equals(context.ToolName, "workspace_dotnet_run", StringComparison.OrdinalIgnoreCase))
        {
            return [OperationRequirement.Any(OperationLaunchRuntime)];
        }

        if (BrowserProofTools.Contains(context.ToolName) ||
            string.Equals(context.ToolName, AgentToolInvocationPolicyMetadata.WorkspaceInspectImage, StringComparison.OrdinalIgnoreCase))
        {
            return [OperationRequirement.Any(OperationCaptureRuntimeProof)];
        }

        if (ProductFileMutationTools.Contains(context.ToolName))
        {
            return [ResolveWorkspaceFileMutationRequirement(context)];
        }

        if (WorkspaceScriptExecutionTools.Contains(context.ToolName))
        {
            return [ResolveWorkspaceScriptRequirement(context)];
        }

        if (string.Equals(context.ToolName, AgentToolInvocationPolicyMetadata.ProcessesArtifactRecord, StringComparison.OrdinalIgnoreCase))
        {
            return [ResolveProcessArtifactRecordRequirement(context)];
        }

        if (string.Equals(context.ToolName, AgentToolInvocationPolicyMetadata.ProcessesStepTransition, StringComparison.OrdinalIgnoreCase))
        {
            return [OperationRequirement.Any(OperationEscalateOrDecide, OperationRecoverArtifactsOnly, OperationExecuteExternalAction)];
        }

        if (AgentToolInvocationPolicyMetadata.IsProjectStructureMutationTool(context.ToolName))
        {
            return [OperationRequirement.Any(OperationExecuteExternalAction)];
        }

        if (ProcessDefinitionMutationTools.Contains(context.ToolName) ||
            string.Equals(context.ToolName, AgentToolInvocationPolicyMetadata.ImageGenerationCreate, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(context.ToolName, AgentToolInvocationPolicyMetadata.RunSkillScript, StringComparison.OrdinalIgnoreCase))
        {
            return [OperationRequirement.Any(OperationExecuteExternalAction)];
        }

        return [];
    }

    private static OperationRequirement ResolveWorkspaceFileMutationRequirement(ToolInvocationPolicyContext context)
    {
        var referencedAliases = ResolveReferencedExternalTargetAliases(context.RedactedArguments);
        if (referencedAliases.Count > 0)
        {
            var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
            if (IsProductMutationStep(context) &&
                referencedAliases.Any(alias => IsAllowedExternalTargetAlias(alias, allowedAliases)))
            {
                return OperationRequirement.Any(OperationMutateProductTarget);
            }

            if (referencedAliases.Any(IsExternalArtifactDestinationPath))
            {
                return OperationRequirement.Any(OperationWriteExternalArtifactDestination);
            }

            return OperationRequirement.Any(OperationMutateProductTarget);
        }

        var normalizedPaths = ResolveManagedWorkspacePathArguments(context.RedactedArguments)
            .Select(argument => NormalizeManagedWorkspacePath(argument.Value))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        if (normalizedPaths.Length == 0)
        {
            return OperationRequirement.Any(OperationMutateProductTarget);
        }

        return normalizedPaths.Any(path => IsAllowedExternalRunManagedPath(path) && !IsManagedOutputPath(path))
            ? OperationRequirement.Any(OperationWriteManagedProcessArtifacts)
            : OperationRequirement.Any(OperationMutateProductTarget);
    }

    private static OperationRequirement ResolveWorkspaceScriptRequirement(ToolInvocationPolicyContext context)
    {
        var manifest = TryParseScriptSideEffectManifestForRequirement(context);
        if (manifest?.Mode == GovernedScriptSideEffectMode.ProductMutation)
        {
            return OperationRequirement.Any(OperationMutateProductTarget);
        }

        if (manifest?.Mode == GovernedScriptSideEffectMode.ExternalArtifactDestination)
        {
            return OperationRequirement.Any(OperationWriteExternalArtifactDestination);
        }

        if (manifest?.Mode == GovernedScriptSideEffectMode.ManagedProcessArtifacts)
        {
            return OperationRequirement.Any(OperationWriteManagedProcessArtifacts);
        }

        var referencedAliases = ResolveReferencedExternalTargetAliases(context.RedactedArguments)
            .Concat(ResolveExternalTargetAliasesFromText(context.InspectedScriptContent))
            .Concat(ResolveExternalTargetAliasesFromManifest(manifest))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (referencedAliases.Any(alias => !IsExternalArtifactDestinationPath(alias)))
        {
            return OperationRequirement.Any(OperationMutateProductTarget);
        }

        if (referencedAliases.Any(IsExternalArtifactDestinationPath) ||
            ResolveScriptDeclaredOutputPaths(context.RedactedArguments)
                .Select(NormalizeManagedWorkspacePath)
                .Any(path => IsExternalTargetAliasPath(path) && IsExternalArtifactDestinationPath(NormalizeExternalTargetAlias(path))))
        {
            return OperationRequirement.Any(OperationWriteExternalArtifactDestination);
        }

        var analysis = ProcessScriptSideEffectAnalyzer.Analyze(context.ToolName, context.InspectedScriptContent);
        if (analysis.HasWriteSignal ||
            ResolveScriptDeclaredOutputPaths(context.RedactedArguments)
                .Select(NormalizeManagedWorkspacePath)
                .Any(path => IsAllowedExternalRunManagedPath(path) && !IsManagedOutputPath(path)))
        {
            return OperationRequirement.Any(OperationWriteManagedProcessArtifacts);
        }

        return OperationRequirement.Any(
            OperationRunValidation,
            OperationLaunchRuntime,
            OperationCaptureRuntimeProof,
            OperationExecuteExternalAction,
            OperationRecoverArtifactsOnly);
    }

    private static GovernedScriptSideEffectManifest? TryParseScriptSideEffectManifestForRequirement(
        ToolInvocationPolicyContext context)
    {
        if (string.IsNullOrWhiteSpace(context.ScriptSideEffectManifestJson))
        {
            return null;
        }

        return GovernedScriptSideEffectManifest.TryParse(
            context.ScriptSideEffectManifestJson,
            out var manifest,
            out _)
                ? manifest
                : null;
    }

    private static OperationRequirement ResolveProcessArtifactRecordRequirement(ToolInvocationPolicyContext context)
    {
        var referencedAliases = ResolveReferencedExternalTargetAliases(context.RedactedArguments);
        if (referencedAliases.Any(IsExternalArtifactDestinationPath))
        {
            return OperationRequirement.Any(OperationWriteExternalArtifactDestination);
        }

        return OperationRequirement.Any(OperationWriteManagedProcessArtifacts, OperationRecoverArtifactsOnly);
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
                    "Governed process browser snapshots must set depth to 4 or less. Retry once with depth=2 and do not repeat this blocked call.");
            }

            if (context.RedactedArguments.TryGetValue("boxes", out var boxesValue) &&
                bool.TryParse(boxesValue, out var boxes) &&
                boxes)
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "Governed process browser snapshots must not request element boxes because they can produce oversized tool output. Retry once with boxes=false and do not repeat this blocked call.");
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
                "Governed process browser screenshots must be viewport-bounded. Retry once with fullPage=false or omit fullPage, and do not repeat this blocked call.");
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedProcessProductMutationBoundary(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsGovernedProcessRun(context) ||
            context.ProcessAllowsProductMutation ||
            !ProductFileMutationTools.Contains(context.ToolName))
        {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        foreach (var pathArgument in ResolveManagedWorkspacePathArguments(context.RedactedArguments))
        {
            var normalizedPath = NormalizeManagedWorkspacePath(pathArgument.Value);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            if (IsExternalTargetAliasPath(normalizedPath))
            {
                var normalizedAlias = NormalizeExternalTargetAlias(normalizedPath);
                if (IsAllowedExternalTargetAlias(normalizedAlias, allowedAliases) &&
                    IsExternalArtifactDestinationPath(normalizedAlias))
                {
                    continue;
                }

                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to mutate product targets. External product path '{normalizedPath}' is read-only for this step; write managed process artifacts under the current-run artifact root instead.");
            }

            if (IsManagedOutputPath(normalizedPath))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to mutate managed output product files. Managed output path '{normalizedPath}' is outside the current-run process artifact boundary for this step.");
            }
        }

        return null;
    }

    private static bool IsExternalArtifactDestinationPath(string normalizedAlias)
    {
        var segments = normalizedAlias
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(2)
            .ToArray();
        if (segments.Length == 0)
        {
            return false;
        }

        if (segments.Any(segment =>
                string.Equals(segment, "product", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "source", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "src", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segment, "app", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        return segments.Any(segment =>
            string.Equals(segment, "artifact", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "artifacts", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "evidence", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "report", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "reports", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "decision", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "decisions", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(segment, "output", StringComparison.OrdinalIgnoreCase));
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedScriptSideEffectBoundary(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsGovernedProcessRun(context) ||
            context.ProcessAllowsProductMutation ||
            !WorkspaceScriptExecutionTools.Contains(context.ToolName))
        {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(context.ScriptInspectionFailure))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to mutate product targets. Script '{ResolveScriptPathDisplay(context)}' could not be inspected before execution: {context.ScriptInspectionFailure}");
        }

        if (string.IsNullOrWhiteSpace(context.InspectedScriptContent))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to mutate product targets. Script '{ResolveScriptPathDisplay(context)}' must be inspected before execution; use current-run artifact writes or a read-only validation command instead.");
        }

        if (!GovernedScriptSideEffectManifest.TryParse(
                context.ScriptSideEffectManifestJson,
                out var manifest,
                out var manifestFailureMessage))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to run scripts without declared side effects. Script '{ResolveScriptPathDisplay(context)}' was denied because {manifestFailureMessage}");
        }

        if (manifest.Mode == GovernedScriptSideEffectMode.ProductMutation)
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to mutate product targets. Script '{ResolveScriptPathDisplay(context)}' declared product mutation in its side-effect manifest.");
        }

        var analysis = ProcessScriptSideEffectAnalyzer.Analyze(context.ToolName, context.InspectedScriptContent);
        var manifestDecision = EvaluateScriptSideEffectManifestBoundary(context, signature, manifest, analysis);
        if (manifestDecision is not null)
        {
            return manifestDecision;
        }

        var declaredOutputDecision = EvaluateScriptDeclaredOutputBoundary(context, signature, manifest);
        if (declaredOutputDecision is not null)
        {
            return declaredOutputDecision;
        }

        var referencedAliases = ResolveReferencedExternalTargetAliases(context.RedactedArguments)
            .Concat(ResolveExternalTargetAliasesFromText(context.InspectedScriptContent))
            .Concat(ResolveExternalTargetAliasesFromManifest(manifest))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var referencedAlias in referencedAliases)
        {
            if (!IsAllowedExternalTargetAlias(referencedAlias, readableAliases))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                $"Governed scripts may only reference external-target paths grounded by the current run. Script '{ResolveScriptPathDisplay(context)}' references '{referencedAlias}', which is outside the current run boundary.");
            }
        }

        if (!analysis.HasWriteSignal)
        {
            return null;
        }

        var productAlias = referencedAliases.FirstOrDefault(alias => !IsExternalArtifactDestinationPath(alias));
        if (!string.IsNullOrWhiteSpace(productAlias))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to mutate product targets. Script '{ResolveScriptPathDisplay(context)}' contains write operations against product target '{productAlias}'.");
        }

        var productWorkingContext = ResolveScriptProductWorkingContext(context);
        if (!string.IsNullOrWhiteSpace(productWorkingContext))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to mutate product targets. Script '{ResolveScriptPathDisplay(context)}' contains write operations while executing from product target '{productWorkingContext}'.");
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateScriptSideEffectManifestBoundary(
        ToolInvocationPolicyContext context,
        string signature,
        GovernedScriptSideEffectManifest manifest,
        ProcessScriptSideEffectAnalysis analysis)
    {
        var declaredWritePaths = ResolveScriptDeclaredOutputPaths(context.RedactedArguments)
            .Concat(manifest.DeclaredWritePaths)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (analysis.EncodedCommandSignals.Count > 0)
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to run encoded script content. Script '{ResolveScriptPathDisplay(context)}' contains encoded command usage that cannot be inspected: {string.Join(", ", analysis.EncodedCommandSignals)}.");
        }

        if (analysis.ShellDelegationSignals.Count > 0 && !manifest.AllowShellDelegation)
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to run undeclared shell delegation. Script '{ResolveScriptPathDisplay(context)}' contains: {string.Join(", ", analysis.ShellDelegationSignals)}.");
        }

        var undeclaredChildScripts = analysis.ChildScriptSignals
            .Where(childScript => !ProcessScriptSideEffectAnalyzer.IsDeclaredChildScript(childScript, manifest.DeclaredChildScripts))
            .ToArray();
        if (undeclaredChildScripts.Length > 0)
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to run undeclared child scripts. Script '{ResolveScriptPathDisplay(context)}' invokes: {string.Join(", ", undeclaredChildScripts)}.");
        }

        if (analysis.ChildScriptSignals.Count > 0)
        {
            var uninspectedChildScripts = manifest.DeclaredChildScripts
                .Where(childScript => !ProcessScriptSideEffectAnalyzer.HasInspectedChildScriptMarker(context.InspectedScriptContent, childScript))
                .ToArray();
            if (uninspectedChildScripts.Length > 0)
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to run child scripts that were not inspected. Script '{ResolveScriptPathDisplay(context)}' declared but did not inspect: {string.Join(", ", uninspectedChildScripts)}.");
            }
        }

        if (manifest.Mode == GovernedScriptSideEffectMode.NoMutation)
        {
            if (declaredWritePaths.Length > 0)
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Script '{ResolveScriptPathDisplay(context)}' declared no mutation but also declared write paths: {string.Join(", ", declaredWritePaths)}.");
            }

            if (analysis.HasWriteSignal)
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Script '{ResolveScriptPathDisplay(context)}' declared no mutation but contains write-capable operations.");
            }
        }

        if (analysis.HasWriteSignal &&
            manifest.Mode is not GovernedScriptSideEffectMode.ManagedProcessArtifacts and not GovernedScriptSideEffectMode.ExternalArtifactDestination)
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Script '{ResolveScriptPathDisplay(context)}' contains write-capable operations but did not declare an allowed non-product write mode.");
        }

        if (analysis.HasWriteSignal && declaredWritePaths.Length == 0)
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Script '{ResolveScriptPathDisplay(context)}' contains write-capable operations but did not declare the write target paths in `{GovernedScriptSideEffectManifest.ArgumentName}`.");
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateScriptDeclaredOutputBoundary(
        ToolInvocationPolicyContext context,
        string signature,
        GovernedScriptSideEffectManifest manifest)
    {
        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        foreach (var outputPath in ResolveScriptDeclaredOutputPaths(context.RedactedArguments).Concat(manifest.DeclaredWritePaths))
        {
            var normalizedPath = NormalizeManagedWorkspacePath(outputPath);
            if (string.IsNullOrWhiteSpace(normalizedPath))
            {
                continue;
            }

            if (IsExternalTargetAliasPath(normalizedPath))
            {
                var normalizedAlias = NormalizeExternalTargetAlias(normalizedPath);
                if (IsAllowedExternalTargetAlias(normalizedAlias, allowedAliases) &&
                    IsExternalArtifactDestinationPath(normalizedAlias))
                {
                    continue;
                }

                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to mutate product targets. Script output path '{normalizedPath}' is outside an allowed external artifact destination.");
            }

            if (IsManagedOutputPath(normalizedPath) ||
                !IsCurrentRunManagedArtifactPath(normalizedPath, context))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to mutate managed output product files. Script output path '{normalizedPath}' is outside the current-run process artifact boundary for this step.");
            }
        }

        return null;
    }

    private static IReadOnlyList<string> ResolveScriptDeclaredOutputPaths(
        IReadOnlyDictionary<string, string> arguments)
    {
        return arguments
            .Where(argument => argument.Key.Contains("output", StringComparison.OrdinalIgnoreCase) &&
                               IsPathLikeArgumentName(argument.Key))
            .SelectMany(argument => EnumerateArgumentTextValues(argument.Value))
            .ToArray();
    }

    private static string ResolveScriptProductWorkingContext(ToolInvocationPolicyContext context)
    {
        foreach (var key in new[] { "workingDirectory", "path", "scriptPath" })
        {
            if (!context.RedactedArguments.TryGetValue(key, out var value))
            {
                continue;
            }

            foreach (var argumentValue in EnumerateArgumentTextValues(value))
            {
                var productAlias = ResolveExternalTargetAliasesFromText(argumentValue)
                    .FirstOrDefault(alias => !IsExternalArtifactDestinationPath(alias));
                if (!string.IsNullOrWhiteSpace(productAlias))
                {
                    return productAlias;
                }
            }
        }

        return string.Empty;
    }

    private static IReadOnlyList<string> ResolveExternalTargetAliasesFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        return ExternalTargetAliasRegex
            .Matches(text)
            .Select(match => NormalizeExternalTargetAlias(match.Value))
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> ResolveExternalTargetAliasesFromManifest(
        GovernedScriptSideEffectManifest? manifest)
    {
        if (manifest is null)
        {
            return [];
        }

        return manifest.DeclaredReadPaths
            .Concat(manifest.DeclaredWritePaths)
            .Concat(manifest.DeclaredChildScripts)
            .SelectMany(ResolveExternalTargetAliasesFromText)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool HasScriptProductWriteSignal(string toolName, string scriptContent)
        => ProcessScriptSideEffectAnalyzer.Analyze(toolName, scriptContent).HasWriteSignal;

    public static string BuildInspectedChildScriptMarker(string childScript)
    {
        return ProcessScriptSideEffectAnalyzer.BuildInspectedChildScriptMarker(childScript);
    }

    private static IEnumerable<string> EnumerateArgumentTextValues(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            yield break;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) ||
            trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            var document = TryParseJsonDocument(trimmed);
            if (document is not null)
            {
                using (document)
                {
                    foreach (var text in EnumerateJsonStringValues(document.RootElement))
                    {
                        yield return text;
                    }
                }

                yield break;
            }
        }

        yield return trimmed;
    }

    private static JsonDocument? TryParseJsonDocument(string value)
    {
        try
        {
            return JsonDocument.Parse(value);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateJsonStringValues(JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    yield return value;
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                {
                    foreach (var itemValue in EnumerateJsonStringValues(item))
                    {
                        yield return itemValue;
                    }
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject())
                {
                    foreach (var propertyValue in EnumerateJsonStringValues(property.Value))
                    {
                        yield return propertyValue;
                    }
                }

                break;
        }
    }

    private static string ResolveScriptPathDisplay(ToolInvocationPolicyContext context)
    {
        return context.RedactedArguments.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path)
            ? path
            : context.ToolName;
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
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
        foreach (var referencedAlias in referencedAliases)
        {
            if (IsAllowedExternalTargetAlias(referencedAlias, readableAliases) ||
                IsAllowedScaffoldParentAlias(context, referencedAlias, allowedAliases))
            {
                continue;
            }

            var allowedSummary = readableAliases.Length == 0
                ? "no external-target roots are grounded for this run"
                : $"current-run roots: {string.Join(", ", readableAliases)}";
            var currentRunGuidance = BuildCurrentRunExternalTargetGuidance(allowedAliases, readOnlyAliases);
            var scaffoldGuidance = BuildScaffoldParentGuidance(context, allowedAliases);
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Governed process runs may only access external-target paths grounded by the current run. The requested external-target path is outside the current run boundary; {allowedSummary}.{currentRunGuidance}{scaffoldGuidance}");
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedArchivedExternalProductPathAccess(
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

        var archivedAlias = ResolveReferencedExternalTargetAliases(context.RedactedArguments)
            .FirstOrDefault(alias =>
                IsAllowedExternalTargetAlias(alias, allowedAliases) &&
                IsExternalProductArchiveSourceAlias(alias));
        if (string.IsNullOrWhiteSpace(archivedAlias))
        {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"This governed process run cannot use archived, backup, or previous-run product material at '{archivedAlias}' as current product input. Use the grounded product root excluding archive folders and current-run managed artifacts only.");
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedStaleExternalProductCopySource(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsGovernedProcessRun(context) ||
            !string.Equals(context.ToolName, "workspace_copy_path", StringComparison.OrdinalIgnoreCase) ||
            !IsProductMutationStep(context))
        {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        if (allowedAliases.Count == 0)
        {
            return null;
        }

        var sourceAliases = ResolveExternalTargetAliasesFromArguments(
            context.RedactedArguments,
            IsCopySourceArgumentName);
        if (sourceAliases.Count == 0 ||
            !sourceAliases.Any(IsExternalProductArchiveSourceAlias))
        {
            return null;
        }

        var destinationAliases = ResolveExternalTargetAliasesFromArguments(
            context.RedactedArguments,
            IsCopyDestinationArgumentName);
        if (!destinationAliases.Any(alias => IsAllowedExternalTargetAlias(alias, allowedAliases)))
        {
            return null;
        }

        var sourceAlias = sourceAliases.First(IsExternalProductArchiveSourceAlias);
        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"This governed product mutation step cannot copy archived, backup, or previous-run product material from '{sourceAlias}' into the current product target. Use the current-run project structure and mutate the grounded product root directly.");
    }

    private static string BuildCurrentRunExternalTargetGuidance(
        IReadOnlyList<string> writableAliases,
        IReadOnlyList<string> readOnlyAliases)
    {
        if (writableAliases.Count > 0)
        {
            return $" Current-run writable product root is '{writableAliases[0]}'. Abandon the denied external-target path and inspect or modify only that root, its children, or current-run managed artifact folders.";
        }

        if (readOnlyAliases.Count > 0)
        {
            return $" Current-run external-target roots are read-only; the first read-only root is '{readOnlyAliases[0]}'. Abandon the denied external-target path and use only grounded read-only roots or current-run managed artifact folders.";
        }

        return " No external product root is grounded for this run, so abandon the denied external-target path instead of retrying it.";
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

            if (IsManagedOutputPath(normalizedPath) &&
                context.Classification is ToolInvocationClassification.Mutation or ToolInvocationClassification.Validation)
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed run has a grounded external product target. Managed output path '{normalizedPath}' is not a fallback product root; use the grounded external-target alias or return Blocked with the exact access problem.");
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
        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var matchedAlias = referencedAliases.FirstOrDefault(referencedAlias =>
            !IsAllowedExternalTargetAlias(referencedAlias, allowedAliases) &&
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

    private static ToolInvocationPolicyDecision? EvaluateScaffoldToolOnlyDirectProductMutation(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!context.ProcessScaffoldToolOnly ||
            !DirectProductFileMutationTools.Contains(context.ToolName))
        {
            return null;
        }

        var referencedAliases = ResolveReferencedExternalTargetAliases(context.RedactedArguments);
        if (referencedAliases.Count == 0)
        {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var matchedAlias = referencedAliases.FirstOrDefault(referencedAlias =>
            allowedAliases.Any(allowedAlias =>
                string.Equals(referencedAlias, allowedAlias, StringComparison.OrdinalIgnoreCase) ||
                referencedAlias.StartsWith(allowedAlias + "/", StringComparison.OrdinalIgnoreCase)));
        if (string.IsNullOrWhiteSpace(matchedAlias))
        {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            "This governed .NET scaffold step is tool-only for product files. Create or modify product solution, project, and source files with scaffold/build tools such as workspace_dotnet_new or a reviewed script, and use workspace_write_file only for current-run artifacts.");
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
        return ResolveExternalTargetAliasesFromArguments(arguments, IsPathLikeArgumentName);
    }

    private static IReadOnlyList<string> ResolveExternalTargetAliasesFromArguments(
        IReadOnlyDictionary<string, string> arguments,
        Func<string, bool> argumentNamePredicate)
    {
        return arguments
            .Where(argument => argumentNamePredicate(argument.Key))
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
        return AgentWorkspaceToolAccessMetadata.NormalizeExternalTargetAlias(alias) ?? string.Empty;
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

    private static bool IsCopySourceArgumentName(string argumentName)
    {
        return !string.IsNullOrWhiteSpace(argumentName) &&
               (argumentName.Contains("source", StringComparison.OrdinalIgnoreCase) ||
                argumentName.StartsWith("from", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCopyDestinationArgumentName(string argumentName)
    {
        return !string.IsNullOrWhiteSpace(argumentName) &&
               (argumentName.Contains("destination", StringComparison.OrdinalIgnoreCase) ||
                argumentName.Contains("target", StringComparison.OrdinalIgnoreCase) ||
                argumentName.StartsWith("to", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsProductMutationStep(ToolInvocationPolicyContext context)
    {
        return string.Equals(context.ProcessStepTargetScope, "ExternalProductTargetMutable", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ProcessStepTargetScope, "ManagedOutputProduct", StringComparison.OrdinalIgnoreCase) ||
               (context.ProcessStepAllowedOperations?.Any(operation =>
                   string.Equals(operation, OperationMutateProductTarget, StringComparison.OrdinalIgnoreCase)) ?? false);
    }

    private static bool IsExternalProductArchiveSourceAlias(string normalizedAlias)
    {
        return normalizedAlias
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(2)
            .Any(segment => ExternalProductArchiveSourceSegments.Contains(segment));
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

    private static bool IsManagedOutputPath(string normalizedPath)
    {
        return string.Equals(normalizedPath, "output", StringComparison.OrdinalIgnoreCase) ||
               normalizedPath.StartsWith("output/", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsCurrentRunManagedArtifactPath(string normalizedPath, ToolInvocationPolicyContext context)
    {
        var currentRunRoot = BuildCurrentRunManagedArtifactRoot(context);
        return !string.IsNullOrWhiteSpace(normalizedPath) &&
               (string.Equals(normalizedPath, currentRunRoot, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(currentRunRoot + "/", StringComparison.OrdinalIgnoreCase));
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
    public const string WorkspacePowerShellRunScript = "workspace_pwsh_run_script";
    public const string WorkspacePythonRunFile = "workspace_python_run_file";
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
    public const string ImageGenerationCreate = "image_generation_create";
    public const string WorkspaceInspectImage = "workspace_inspect_image";
    public const string ProjectStructureProjectsList = "project_structure_projects_list";
    public const string ProjectStructureProjectCreate = "project_structure_project_create";
    public const string ProjectStructureProjectUpdate = "project_structure_project_update";
    public const string ProjectStructureHierarchyGet = "project_structure_hierarchy_get";
    public const string ProjectStructureSubprojectLink = "project_structure_subproject_link";
    public const string ProjectStructureNodesToNewSubproject = "project_structure_nodes_to_new_subproject";
    public const string ProjectStructureRead = "project_structure_read";
    public const string ProjectStructureNodeCatalog = "project_structure_node_catalog";
    public const string ProjectStructureChecklist = "project_structure_checklist";
    public const string ProjectStructureDependenciesQuery = "project_structure_dependencies_query";
    public const string ProjectStructureDependencyLink = "project_structure_dependency_link";
    public const string ProjectStructureDependencyUnlink = "project_structure_dependency_unlink";
    public const string ProjectStructureNodeCreate = "project_structure_node_create";
    public const string ProjectStructureNodeUpdate = "project_structure_node_update";
    public const string ProjectStructureNodeMove = "project_structure_node_move";
    public const string ProjectStructureNodeRecompose = "project_structure_node_recompose";
    public const string ProjectStructureNodeReparent = "project_structure_node_reparent";
    public const string ProjectStructureApprovalRequest = "project_structure_approval_request";
    public const string ProjectStructureAssetCreate = "project_structure_asset_create";
    public const string ProjectStructureAssetGet = "project_structure_asset_get";
    public const string ProjectStructureAssetCreateRevision = "project_structure_asset_create_revision";
    public const string ProjectStructureImport = "project_structure_import";
    public const string ProjectStructureKnowledgeQuery = "project_structure_knowledge_query";
    public const string ProjectStructureAnalyticsQuery = "project_structure_analytics_query";
    public const string ProjectStructureProjectLeaseAcquire = "project_structure_project_lease_acquire";
    public const string ProjectStructureRepoBranchLeaseAcquire = "project_structure_repo_branch_lease_acquire";
    public const string ProjectStructureLeaseGet = "project_structure_lease_get";
    public const string ProjectStructureLeaseRelease = "project_structure_lease_release";

    private static readonly string[] ProjectStructureReadToolNames =
    [
        ProjectStructureProjectsList,
        ProjectStructureHierarchyGet,
        ProjectStructureRead,
        ProjectStructureNodeCatalog,
        ProjectStructureChecklist,
        ProjectStructureDependenciesQuery,
        ProjectStructureAssetGet,
        ProjectStructureKnowledgeQuery,
        ProjectStructureAnalyticsQuery,
        ProjectStructureLeaseGet
    ];

    private static readonly string[] ProjectStructureMutationToolNames =
    [
        ProjectStructureProjectCreate,
        ProjectStructureProjectUpdate,
        ProjectStructureSubprojectLink,
        ProjectStructureNodesToNewSubproject,
        ProjectStructureDependencyLink,
        ProjectStructureDependencyUnlink,
        ProjectStructureNodeCreate,
        ProjectStructureNodeUpdate,
        ProjectStructureNodeMove,
        ProjectStructureNodeRecompose,
        ProjectStructureNodeReparent,
        ProjectStructureApprovalRequest,
        ProjectStructureAssetCreate,
        ProjectStructureAssetCreateRevision,
        ProjectStructureImport,
        ProjectStructureProjectLeaseAcquire,
        ProjectStructureRepoBranchLeaseAcquire,
        ProjectStructureLeaseRelease
    ];

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
            Mutation(WorkspacePowerShellRunScript),
            Mutation(WorkspacePythonRunFile),
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
            Read(WorkspaceInspectImage),
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
            Mutation(ImageGenerationCreate),
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
        }
        .Concat(ProjectStructureReadToolNames.Select(Read))
        .Concat(ProjectStructureMutationToolNames.Select(Mutation))
        .ToDictionary(item => item.Name, StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyCollection<AgentToolPolicyMetadata> Tools => RegisteredTools.Values.ToList();

    public static IReadOnlyList<string> ProjectStructureReadTools => ProjectStructureReadToolNames.ToArray();

    public static IReadOnlyList<string> ProjectStructureMutationTools => ProjectStructureMutationToolNames.ToArray();

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

        if (normalized.StartsWith("project_structure_", StringComparison.OrdinalIgnoreCase))
        {
            return ToolInvocationClassification.Unknown;
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

    public static bool IsProjectStructureMutationTool(string toolName)
    {
        return ProjectStructureMutationToolNames.Contains(toolName, StringComparer.OrdinalIgnoreCase);
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
