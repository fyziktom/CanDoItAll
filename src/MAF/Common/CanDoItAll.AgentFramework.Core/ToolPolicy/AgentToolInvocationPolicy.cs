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
    string FailureMessage)
{
    public string RuntimeToolProviderKey { get; init; } = string.Empty;

    public string RuntimeToolProviderName { get; init; } = string.Empty;

    public string Signature { get; init; } = string.Empty;
}

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
    bool ProcessRequiresProductMutationBeforeManagedOutput = false,
    IReadOnlyList<string>? ProcessProductMutationToolNames = null,
    IReadOnlyList<string>? ProcessStepAllowedOperations = null,
    string ProcessStepTargetScope = "",
    string ContextWorkspaceScopeKind = "",
    string ContextWorkspaceScopeKey = "",
    string InspectedScriptContent = "",
    string ScriptInspectionFailure = "",
    string ScriptSideEffectManifestJson = "",
    IReadOnlyList<AgentToolInvocationTrace>? ToolInvocationTraces = null)
{
    public string SourceId { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedManagedArtifactReadRefs { get; init; } = [];

    public IReadOnlyList<AgentToolInvocationTrace> RecentToolInvocationTraces { get; } = ToolInvocationTraces ?? [];

    public IReadOnlyList<string> ProductMutationToolNames { get; } = ProcessProductMutationToolNames ?? [];

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
    private const string OwnPrimaryManagedOutputReadDenialMarker =
        "cannot read, stat, list, or search its own primary managed output";
    private const string OwnPrimaryManagedOutputPreCreationMarker = "before creating it";
    private const string OwnPrimaryManagedOutputInProgressWriteDenialMarker =
        "cannot write primary managed output";
    private const string OwnPrimaryManagedOutputBlockedPlaceholderWriteDenialMarker =
        "cannot write a status-only Blocked placeholder";
    private const string GovernedDotnetNewForceDeniedMarker =
        "cannot run workspace_dotnet_new with force=true";

    private static readonly HashSet<string> RecoverableGovernedReadDiscoveryTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch,
        ToolContractCatalog.WorkspaceReadFile,
        ToolContractCatalog.WorkspaceStatPath
    };
    private static readonly HashSet<string> RecoverableGovernedBrowserProofTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.BrowserSnapshot,
        ToolContractCatalog.BrowserTakeScreenshot
    };
    private static readonly HashSet<string> RecoverableGovernedWorkspaceBoundaryTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceCreateDirectory,
        ToolContractCatalog.WorkspaceWriteFile,
        ToolContractCatalog.WorkspaceAppendFile,
        ToolContractCatalog.WorkspaceCopyPath,
        ToolContractCatalog.WorkspaceMovePath,
        ToolContractCatalog.WorkspaceDeletePath,
        ToolContractCatalog.WorkspaceDotNetNew,
        ToolContractCatalog.WorkspaceDotNetRestore,
        ToolContractCatalog.WorkspaceDotNetBuild,
        ToolContractCatalog.WorkspaceDotNetTest,
        ToolContractCatalog.WorkspaceDotNetRun,
        ToolContractCatalog.WorkspacePowerShellRunScript,
        ToolContractCatalog.WorkspacePythonRunFile,
        ToolContractCatalog.WorkspaceCommandRun
    };

    public static bool TryCreateRecoverableDeniedResult(
        string toolName,
        ToolInvocationPolicyDecision decision,
        ToolInvocationPolicyContext context,
        out string result)
    {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(context);

        result = string.Empty;
        if (decision.Kind is not ToolInvocationDecisionKind.Deny and not ToolInvocationDecisionKind.SkipExecution)
        {
            return false;
        }

        if (!IsRecoverableGovernedProcessStep(context))
        {
            return false;
        }

        if (context.Classification == ToolInvocationClassification.Read &&
            RecoverableGovernedReadDiscoveryTools.Contains(toolName) &&
            IsRecoverableCurrentStepOwnOutputPreCreationReadDenial(decision))
        {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} This is not a missing tool permission and not a blocker. Do not retry the read, stat, list, or search. Do not write a status-only InProgress or Blocked placeholder and stop. Continue the step's required product, validation, or external work from launch variables, upstream artifacts, project-structure context, or product readback. When recording the step outcome, create or overwrite the named primary managed artifact with workspace_write_file or workspace_append_file, then return submit_process_step_outcome with evidenceRefs containing that managed ref. Submit Blocked only if the artifact write is denied, or if a required tool is denied or fails on a concrete environment boundary.";
            return true;
        }

        if (context.Classification == ToolInvocationClassification.Mutation &&
            IsManagedOutputWriteTool(toolName) &&
            IsRecoverableCurrentStepOwnOutputPlaceholderWriteDenial(decision))
        {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} This is not a missing tool permission and not a blocker. Do not retry the placeholder write. Continue the step's required product, validation, or external work from launch variables, upstream artifacts, project-structure context, or product readback. When the work is complete, create or overwrite the primary managed artifact with final evidence and Status: Completed, Failed, Blocked, WaitingApproval, or Refused, then return submit_process_step_outcome with matching evidenceRefs.";
            return true;
        }

        if (context.Classification == ToolInvocationClassification.Read &&
            RecoverableGovernedReadDiscoveryTools.Contains(toolName))
        {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} Use the grounded external-target alias or current-run artifact folder named in the tool boundary, then retry with narrower arguments. When the denial gives a replacement external-target alias, retry the same structured workspace tool with that alias before finalizing Blocked. If this was only an optional context probe for an evidence-producing step, continue from launch variables or project-structure context and create the managed artifact instead of blocking.";
            return true;
        }

        if (IsRecoverableScriptInspectionDenial(toolName, decision))
        {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} Treat this as helper-script ordering, not as missing permission. Create or overwrite the current-run helper script with workspace_write_file, verify that exact helper path with workspace_stat_path or workspace_read_file, then retry {toolName} with the same helper path. Do not submit Blocked only because a pre-creation script invocation was denied; submit Blocked only if the verified retry is denied or fails on a concrete policy, permission, or environment boundary.";
            return true;
        }

        if (IsRecoverableDotnetNewForceDenial(toolName, decision))
        {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} Treat this as an unsafe scaffold overwrite request, not as missing permission. Retry without force only when the target scaffold is absent; when files already exist, inspect them and repair precise drift with governed product-mutation tools or a reviewed ProductMutation helper script.";
            return true;
        }

        if (RecoverableGovernedWorkspaceBoundaryTools.Contains(toolName) &&
            IsRecoverableWorkspaceBoundaryDenial(decision))
        {
            result = $"PolicyDenied: Tool '{toolName}' was denied for this governed process step. {decision.Reason} Treat this as a wrong tool argument, not as missing permission. Retry with the grounded current-run external-target alias or current-run artifact path named in the denial before finalizing Blocked.";
            return true;
        }

        if (IsRecoverableGovernedBrowserProofBoundsDenial(toolName, decision, context))
        {
            result = $"PolicyDenied: Tool '{toolName}' was denied by governed browser proof bounds. {decision.Reason} Retry once with the bounded browser-proof arguments named in this denial. Do not report the process blocked until that bounded retry fails.";
            return true;
        }

        return false;
    }

    private static bool IsRecoverableCurrentStepOwnOutputPreCreationReadDenial(
        ToolInvocationPolicyDecision decision)
    {
        return decision.Reason.Contains(OwnPrimaryManagedOutputReadDenialMarker, StringComparison.OrdinalIgnoreCase) &&
               decision.Reason.Contains(OwnPrimaryManagedOutputPreCreationMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableCurrentStepOwnOutputPlaceholderWriteDenial(
        ToolInvocationPolicyDecision decision)
    {
        return decision.Reason.Contains(OwnPrimaryManagedOutputInProgressWriteDenialMarker, StringComparison.OrdinalIgnoreCase) ||
               decision.Reason.Contains(OwnPrimaryManagedOutputBlockedPlaceholderWriteDenialMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedOutputWriteTool(string toolName)
    {
        return string.Equals(toolName, ToolContractCatalog.WorkspaceWriteFile, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(toolName, ToolContractCatalog.WorkspaceAppendFile, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableScriptInspectionDenial(
        string toolName,
        ToolInvocationPolicyDecision decision)
    {
        if (!string.Equals(toolName, ToolContractCatalog.WorkspacePowerShellRunScript, StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(toolName, ToolContractCatalog.WorkspacePythonRunFile, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return decision.Reason.Contains("could not be inspected", StringComparison.OrdinalIgnoreCase) ||
               decision.Reason.Contains("must be inspected", StringComparison.OrdinalIgnoreCase) ||
               (decision.Reason.Contains("script path", StringComparison.OrdinalIgnoreCase) &&
                decision.Reason.Contains("does not exist", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsRecoverableDotnetNewForceDenial(
        string toolName,
        ToolInvocationPolicyDecision decision)
    {
        return string.Equals(toolName, ToolContractCatalog.WorkspaceDotNetNew, StringComparison.OrdinalIgnoreCase) &&
               decision.Reason.Contains(GovernedDotnetNewForceDeniedMarker, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableGovernedProcessStep(ToolInvocationPolicyContext context)
    {
        return string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(context.ProcessRunId) &&
               !string.IsNullOrWhiteSpace(context.ProcessStepId);
    }

    private static bool IsRecoverableWorkspaceBoundaryDenial(ToolInvocationPolicyDecision decision)
    {
        return decision.Reason.Contains("current-run", StringComparison.OrdinalIgnoreCase) ||
               decision.Reason.Contains("workspace boundary", StringComparison.OrdinalIgnoreCase) ||
               decision.Reason.Contains("outside the current run boundary", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRecoverableGovernedBrowserProofBoundsDenial(
        string toolName,
        ToolInvocationPolicyDecision decision,
        ToolInvocationPolicyContext context)
    {
        if (!RecoverableGovernedBrowserProofTools.Contains(toolName) ||
            context.Classification is not (ToolInvocationClassification.Read or ToolInvocationClassification.Validation) ||
            !ProcessStepAllowsOperation(context, ProcessOperationContractNames.CaptureRuntimeProof))
        {
            return false;
        }

        return decision.Reason.StartsWith("Governed process browser snapshots", StringComparison.Ordinal) ||
               decision.Reason.StartsWith("Governed process browser screenshots", StringComparison.Ordinal);
    }

    private static bool ProcessStepAllowsOperation(ToolInvocationPolicyContext context, string operationName)
    {
        return context.ProcessStepAllowedOperations?.Contains(operationName, StringComparer.OrdinalIgnoreCase) == true;
    }

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
    private static readonly Regex WindowsNativeAbsolutePathRegex = new(
        "^[A-Za-z]:[\\\\/]",
        RegexOptions.CultureInvariant);
    private static readonly Regex ManagedArtifactStatusLineRegex = new(
        @"^\s{0,3}#*\s*Status\s*:\s*(?<status>Waiting[\s_-]*Approval|In[\s_-]*Progress|Completed|Blocked|Failed|Refused)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);
    private static readonly HashSet<string> ExternalTargetManagedWorkspaceIsolationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch,
        ToolContractCatalog.WorkspaceReadFile,
        ToolContractCatalog.WorkspaceStatPath,
        ToolContractCatalog.WorkspaceCreateDirectory,
        ToolContractCatalog.WorkspaceWriteFile,
        ToolContractCatalog.WorkspaceAppendFile,
        ToolContractCatalog.WorkspaceCopyPath,
        ToolContractCatalog.WorkspaceMovePath,
        ToolContractCatalog.WorkspaceDeletePath,
        ToolContractCatalog.WorkspaceDotNetNew,
        ToolContractCatalog.WorkspaceDotNetRestore,
        ToolContractCatalog.WorkspaceDotNetBuild,
        ToolContractCatalog.WorkspaceDotNetTest,
        ToolContractCatalog.WorkspaceDotNetRun,
        ToolContractCatalog.WorkspaceDotNetStop,
        ToolContractCatalog.WorkspacePowerShellRunScript,
        ToolContractCatalog.WorkspacePythonRunFile,
        ToolContractCatalog.WorkspaceInspectImage,
        ToolContractCatalog.WorkspaceAnalyzeImage,
        ToolContractCatalog.WorkspaceAnalyzeImages
    };
    private static readonly HashSet<string> BroadManagedWorkspaceDiscoveryTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch
    };
    private static readonly HashSet<string> CurrentStepOwnManagedOutputReadTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceReadFile,
        ToolContractCatalog.WorkspaceStatPath,
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch
    };
    private static readonly HashSet<string> CurrentStepOwnManagedOutputWriteTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceWriteFile,
        ToolContractCatalog.WorkspaceAppendFile
    };
    private static readonly string[] ProductTargetPathArgumentNames =
    [
        "path",
        "relativePath",
        "targetPath",
        "sourcePath",
        "destinationPath",
        "workingDirectory",
        "projectPath",
        "filePath"
    ];
    private static readonly HashSet<string> ProductFileMutationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceCreateDirectory,
        ToolContractCatalog.WorkspaceWriteFile,
        ToolContractCatalog.WorkspaceAppendFile,
        ToolContractCatalog.WorkspaceCopyPath,
        ToolContractCatalog.WorkspaceMovePath,
        ToolContractCatalog.WorkspaceDeletePath,
        ToolContractCatalog.WorkspaceDotNetNew
    };
    private static readonly HashSet<string> WorkspaceScriptExecutionTools = new(StringComparer.OrdinalIgnoreCase)
    {
        AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
        AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile
    };
    private static readonly HashSet<string> ExternalTargetAliasLiteralUnsafeScriptTools = new(StringComparer.OrdinalIgnoreCase)
    {
        AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
        AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile,
        AgentToolInvocationPolicyMetadata.RunSkillScript
    };
    private static readonly HashSet<string> DirectProductFileMutationTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ToolContractCatalog.WorkspaceWriteFile,
        ToolContractCatalog.WorkspaceAppendFile,
        ToolContractCatalog.WorkspaceCopyPath,
        ToolContractCatalog.WorkspaceMovePath,
        ToolContractCatalog.WorkspaceDeletePath
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
        "process-artifacts",
        "process-runs"
    ];
    private static readonly string[] DeniedExternalRunManagedRoots =
    [
        "bin",
        "managed-files",
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
    private static readonly ToolOperationRequirementResolver operationRequirementResolver = new();
    private static readonly BrowserProofPolicy browserProofPolicy = new();
    private static readonly ExternalTargetBoundaryPolicy externalTargetBoundaryPolicy = new();
    private static readonly ScriptSideEffectPolicy scriptSideEffectPolicy = new();
    private static readonly StaleProofPolicy staleProofPolicy = new();

    private const string OperationWriteManagedProcessArtifacts = ProcessOperationContractNames.WriteManagedProcessArtifacts;
    private const string OperationWriteExternalArtifactDestination = ProcessOperationContractNames.WriteExternalArtifactDestination;
    private const string OperationMutateProductTarget = ProcessOperationContractNames.MutateProductTarget;
    private const string OperationRunValidation = ProcessOperationContractNames.RunValidation;
    private const string OperationLaunchRuntime = ProcessOperationContractNames.LaunchRuntime;
    private const string OperationCaptureRuntimeProof = ProcessOperationContractNames.CaptureRuntimeProof;
    private const string OperationReadProjectStructure = ProcessOperationContractNames.ReadProjectStructure;
    private const string OperationExecuteExternalAction = ProcessOperationContractNames.ExecuteExternalAction;
    private const string OperationRecoverArtifactsOnly = ProcessOperationContractNames.RecoverArtifactsOnly;
    private const string OperationEscalateOrDecide = ProcessOperationContractNames.EscalateOrDecide;

    private readonly RepeatInvocationGuard repeatInvocationGuard = new();
    private readonly DotnetNewTemplateConsistencyPolicy dotnetNewTemplateConsistencyPolicy = new();

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

        var currentStepOwnOutputReadDecision = EvaluateGovernedCurrentStepOwnOutputRead(context, signature);
        if (currentStepOwnOutputReadDecision is not null)
        {
            return ValueTask.FromResult(currentStepOwnOutputReadDecision);
        }

        var currentStepOwnOutputPlaceholderWriteDecision = EvaluateGovernedCurrentStepOwnOutputPlaceholderWrite(context, signature);
        if (currentStepOwnOutputPlaceholderWriteDecision is not null)
        {
            return ValueTask.FromResult(currentStepOwnOutputPlaceholderWriteDecision);
        }

        var productMutationBeforeManagedOutputDecision = EvaluateRequiredProductMutationBeforeManagedOutput(context, signature);
        if (productMutationBeforeManagedOutputDecision is not null)
        {
            return ValueTask.FromResult(productMutationBeforeManagedOutputDecision);
        }

        var governedBrowserDecision = browserProofPolicy.EvaluateGovernedToolBounds(context, signature);
        if (governedBrowserDecision is not null)
        {
            return ValueTask.FromResult(governedBrowserDecision);
        }

        var externalTargetDecision = externalTargetBoundaryPolicy.EvaluateGovernedExternalTargetIsolation(context, signature);
        if (externalTargetDecision is not null)
        {
            return ValueTask.FromResult(externalTargetDecision);
        }

        var scriptExternalTargetAliasDecision = EvaluateGovernedScriptExternalTargetAliasLiteral(context, signature);
        if (scriptExternalTargetAliasDecision is not null)
        {
            return ValueTask.FromResult(scriptExternalTargetAliasDecision);
        }

        var staleProductCopyDecision = staleProofPolicy.EvaluateGovernedStaleExternalProductCopySource(context, signature);
        if (staleProductCopyDecision is not null)
        {
            return ValueTask.FromResult(staleProductCopyDecision);
        }

        var archivedExternalProductPathDecision = staleProofPolicy.EvaluateGovernedArchivedExternalProductPathAccess(context, signature);
        if (archivedExternalProductPathDecision is not null)
        {
            return ValueTask.FromResult(archivedExternalProductPathDecision);
        }

        var processBoundaryDecision = externalTargetBoundaryPolicy.EvaluateGovernedProcessProductMutationBoundary(context, signature);
        if (processBoundaryDecision is not null)
        {
            return ValueTask.FromResult(processBoundaryDecision);
        }

        var scriptSideEffectDecision = scriptSideEffectPolicy.EvaluateGovernedScriptSideEffectBoundary(context, signature);
        if (scriptSideEffectDecision is not null)
        {
            return ValueTask.FromResult(scriptSideEffectDecision);
        }

        var managedWorkspaceIsolationDecision = externalTargetBoundaryPolicy.EvaluateExternalTargetManagedWorkspaceIsolation(context, signature);
        if (managedWorkspaceIsolationDecision is not null)
        {
            return ValueTask.FromResult(managedWorkspaceIsolationDecision);
        }

        var readOnlyExternalTargetDecision = externalTargetBoundaryPolicy.EvaluateReadOnlyExternalTargetMutation(context, signature);
        if (readOnlyExternalTargetDecision is not null)
        {
            return ValueTask.FromResult(readOnlyExternalTargetDecision);
        }

        var scaffoldToolOnlyDecision = EvaluateScaffoldToolOnlyDirectProductMutation(context, signature);
        if (scaffoldToolOnlyDecision is not null)
        {
            return ValueTask.FromResult(scaffoldToolOnlyDecision);
        }

        var dotnetNewForceDecision = EvaluateGovernedDotnetNewForce(context, signature);
        if (dotnetNewForceDecision is not null)
        {
            return ValueTask.FromResult(dotnetNewForceDecision);
        }

        var dotnetNewTemplateConsistencyDecision = EvaluateDotnetNewTemplateConsistency(context, signature);
        if (dotnetNewTemplateConsistencyDecision is not null)
        {
            return ValueTask.FromResult(dotnetNewTemplateConsistencyDecision);
        }

        var repeatInvocationDecision = repeatInvocationGuard.Evaluate(context, signature);
        if (repeatInvocationDecision is not null)
        {
            return ValueTask.FromResult(repeatInvocationDecision);
        }

        if (context.Classification == ToolInvocationClassification.Mutation)
        {
            repeatInvocationGuard.RecordMutationDecision();
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
        => operationRequirementResolver.Resolve(context);

    private static ToolInvocationPolicyDecision? EvaluateGovernedCurrentStepOwnOutputRead(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            context.Classification != ToolInvocationClassification.Read ||
            !CurrentStepOwnManagedOutputReadTools.Contains(context.ToolName) ||
            string.IsNullOrWhiteSpace(context.ProcessRunId) ||
            string.IsNullOrWhiteSpace(context.SourceId))
        {
            return null;
        }

        var matchedPath = ResolveManagedWorkspacePathArguments(context.RedactedArguments)
            .Select(argument => NormalizeManagedWorkspacePath(argument.Value))
            .FirstOrDefault(path => IsCurrentStepPrimaryManagedArtifactPath(context, path));
        if (string.IsNullOrWhiteSpace(matchedPath))
        {
            return null;
        }

        var primaryRef = BuildCurrentStepPrimaryManagedArtifactPath(context);
        if (HasSuccessfulCurrentStepPrimaryManagedArtifactWrite(context, primaryRef) ||
            context.AllowedManagedArtifactReadRefs.Any(allowedRef =>
                string.Equals(
                    NormalizeManagedWorkspacePath(allowedRef),
                    NormalizeManagedWorkspacePath(primaryRef),
                    StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Governed process step '{context.SourceId}' cannot read, stat, list, or search its own primary managed output '{primaryRef}' before creating it. Do not retry that read. Continue from launch variables, upstream artifacts, project-structure context, or product readback. When the step has evidence for its outcome, create or overwrite that managed artifact with workspace_write_file or workspace_append_file, then return submit_process_step_outcome with evidenceRefs containing the same managed ref.");
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedCurrentStepOwnOutputPlaceholderWrite(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            context.Classification != ToolInvocationClassification.Mutation ||
            !CurrentStepOwnManagedOutputWriteTools.Contains(context.ToolName) ||
            string.IsNullOrWhiteSpace(context.ProcessRunId) ||
            string.IsNullOrWhiteSpace(context.SourceId))
        {
            return null;
        }

        var matchedPath = ResolveManagedWorkspacePathArguments(context.RedactedArguments)
            .Select(argument => NormalizeManagedWorkspacePath(argument.Value))
            .FirstOrDefault(path => IsCurrentStepPrimaryManagedArtifactPath(context, path));
        if (string.IsNullOrWhiteSpace(matchedPath) ||
            !TryResolveManagedArtifactWriteContent(context.RedactedArguments, out var content) ||
            !TryResolveManagedArtifactStatus(content, out var status))
        {
            return null;
        }

        var primaryRef = BuildCurrentStepPrimaryManagedArtifactPath(context);
        if (string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Governed process step '{context.SourceId}' cannot write primary managed output '{primaryRef}' with status InProgress. Primary managed step artifacts are final evidence, not progress notes. Complete the required work and write Status: Completed, or submit a concrete Blocked/Failed/WaitingApproval outcome with actionable evidence.");
        }

        if (string.Equals(status, "Blocked", StringComparison.OrdinalIgnoreCase) &&
            IsStatusOnlyManagedArtifactPlaceholder(content))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Governed process step '{context.SourceId}' cannot write a status-only Blocked placeholder to primary managed output '{primaryRef}'. Submit Blocked only with concrete denied tool, failed command, unavailable dependency, or other actionable boundary evidence.");
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateRequiredProductMutationBeforeManagedOutput(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!context.ProcessRequiresProductMutationBeforeManagedOutput ||
            !string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            context.Classification != ToolInvocationClassification.Mutation ||
            !CurrentStepOwnManagedOutputWriteTools.Contains(context.ToolName) ||
            string.IsNullOrWhiteSpace(context.ProcessRunId) ||
            string.IsNullOrWhiteSpace(context.SourceId))
        {
            return null;
        }

        var writesPrimaryManagedOutput = ResolveManagedWorkspacePathArguments(context.RedactedArguments)
            .Select(argument => NormalizeManagedWorkspacePath(argument.Value))
            .Any(path => IsCurrentStepPrimaryManagedArtifactPath(context, path));
        if (!writesPrimaryManagedOutput ||
            !TryResolveManagedArtifactWriteContent(context.RedactedArguments, out var content) ||
            !TryResolveManagedArtifactStatus(content, out var status) ||
            !string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) ||
            HasSuccessfulProductTargetMutation(context))
        {
            return null;
        }

        var primaryRef = BuildCurrentStepPrimaryManagedArtifactPath(context);
        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Governed process step '{context.SourceId}' cannot write primary managed output '{primaryRef}' with status Completed before a successful current-execution product-target mutation. Perform the required product mutation under a grounded external-target alias first, verify the changed product file, then write the final managed artifact. A planned changed-file list or an unchanged successful build is not product mutation evidence.");
    }

    private static bool HasSuccessfulProductTargetMutation(ToolInvocationPolicyContext context)
    {
        if (context.RecentToolInvocationTraces.Count == 0 ||
            context.AllowedExternalTargetAliases is null ||
            context.AllowedExternalTargetAliases.Count == 0)
        {
            return false;
        }

        var normalizedAliases = context.AllowedExternalTargetAliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(NormalizeManagedWorkspacePath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return context.RecentToolInvocationTraces.Any(trace =>
            trace.Succeeded &&
            trace.CompletedAtUtc is not null &&
            context.ProductMutationToolNames.Contains(trace.ToolName, StringComparer.OrdinalIgnoreCase) &&
            normalizedAliases.Any(alias => ToolSignatureTargetsExternalAlias(trace.Signature, alias)));
    }

    private static bool ToolSignatureTargetsExternalAlias(string signature, string normalizedAlias)
    {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return false;
        }

        var normalizedSignature = NormalizeManagedWorkspacePath(signature);
        return ProductTargetPathArgumentNames.Any(argumentName =>
            normalizedSignature.Contains($"|{argumentName}={normalizedAlias}/", StringComparison.OrdinalIgnoreCase) ||
            normalizedSignature.Contains($",{argumentName}={normalizedAlias}/", StringComparison.OrdinalIgnoreCase) ||
            normalizedSignature.EndsWith($"|{argumentName}={normalizedAlias}", StringComparison.OrdinalIgnoreCase) ||
            normalizedSignature.EndsWith($",{argumentName}={normalizedAlias}", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryResolveManagedArtifactWriteContent(
        IReadOnlyDictionary<string, string> arguments,
        out string content)
    {
        foreach (var argument in arguments)
        {
            if (!argument.Key.Contains("content", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var values = EnumerateArgumentTextValues(argument.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (values.Length == 0)
            {
                continue;
            }

            content = string.Join(Environment.NewLine, values);
            return true;
        }

        content = string.Empty;
        return false;
    }

    private static bool TryResolveManagedArtifactStatus(string content, out string status)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            status = string.Empty;
            return false;
        }

        var match = ManagedArtifactStatusLineRegex.Match(content);
        if (!match.Success)
        {
            status = string.Empty;
            return false;
        }

        status = NormalizeManagedArtifactStatus(match.Groups["status"].Value);
        return !string.IsNullOrWhiteSpace(status);
    }

    private static string NormalizeManagedArtifactStatus(string status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return string.Empty;
        }

        return string.Concat(status.Where(character => !char.IsWhiteSpace(character) && character != '-' && character != '_'));
    }

    private static bool IsStatusOnlyManagedArtifactPlaceholder(string content)
    {
        var normalized = content.Trim();
        if (normalized.Length > 700)
        {
            return false;
        }

        return !ContainsConcreteBlockedEvidenceSignal(normalized);
    }

    private static bool ContainsConcreteBlockedEvidenceSignal(string content)
    {
        return content.Contains("PolicyDenied", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("denied", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("failed", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("failure", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("exception", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("error", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("required tool", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("unavailable", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("evidence", StringComparison.OrdinalIgnoreCase) ||
               content.Contains("receipt", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasSuccessfulCurrentStepPrimaryManagedArtifactWrite(
        ToolInvocationPolicyContext context,
        string primaryRef)
    {
        if (context.RecentToolInvocationTraces.Count == 0 ||
            string.IsNullOrWhiteSpace(primaryRef))
        {
            return false;
        }

        return context.RecentToolInvocationTraces.Any(trace =>
            trace.Succeeded &&
            trace.CompletedAtUtc is not null &&
            CurrentStepOwnManagedOutputWriteTools.Contains(trace.ToolName) &&
            ToolSignatureTargetsManagedPath(trace.Signature, primaryRef));
    }

    private static bool ToolSignatureTargetsManagedPath(
        string signature,
        string managedPath)
    {
        if (string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(managedPath))
        {
            return false;
        }

        var normalizedSignature = NormalizeManagedWorkspacePath(signature);
        var normalizedManagedPath = NormalizeManagedWorkspacePath(managedPath);
        return normalizedSignature.Contains("path=" + normalizedManagedPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedSignature.Contains("relativepath=" + normalizedManagedPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedSignature.Contains("filepath=" + normalizedManagedPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedSignature.Contains("targetpath=" + normalizedManagedPath, StringComparison.OrdinalIgnoreCase);
    }

    private sealed class ToolOperationRequirementResolver
    {
        public IReadOnlyList<OperationRequirement> Resolve(ToolInvocationPolicyContext context)
        {
            if (!ToolCapabilityRegistry.TryResolve(context.ToolName, out var capability))
            {
                return [];
            }

            return capability.OperationRequirementKind switch
            {
                ToolCapabilityOperationRequirementKind.Static => capability.OperationRequirements
                    .Select(requirement => new OperationRequirement(requirement.AnyOf))
                    .ToArray(),
                ToolCapabilityOperationRequirementKind.WorkspaceFileMutation => [ResolveWorkspaceFileMutationRequirement(context)],
                ToolCapabilityOperationRequirementKind.WorkspaceScript => [ResolveWorkspaceScriptRequirement(context)],
                ToolCapabilityOperationRequirementKind.DotNetRun => [ResolveDotnetRunOperationRequirement(context)],
                ToolCapabilityOperationRequirementKind.ProcessArtifactWrite => [ResolveProcessArtifactWriteRequirement(context)],
                _ => []
            };
        }
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

    private static OperationRequirement ResolveDotnetRunOperationRequirement(ToolInvocationPolicyContext context)
    {
        var keepAlive = context.RedactedArguments.TryGetValue("keepAlive", out var keepAliveValue) &&
                        IsTruthyToolArgument(keepAliveValue);
        var processRunLifetime = context.RedactedArguments.TryGetValue("lifetimeScope", out var lifetimeScope) &&
                                 string.Equals(lifetimeScope, "ProcessRun", StringComparison.OrdinalIgnoreCase);

        return keepAlive || processRunLifetime
            ? OperationRequirement.Any(OperationLaunchRuntime)
            : OperationRequirement.Any(OperationRunValidation, OperationLaunchRuntime);
    }

    private static bool IsTruthyToolArgument(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim().Trim('`', '"', '\'') switch
        {
            "1" => true,
            var text when string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) => true,
            var text when string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
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
        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        if (IsProductMutationStep(context) &&
            referencedAliases.Any(alias => IsAllowedExternalTargetAlias(alias, allowedAliases)))
        {
            return OperationRequirement.Any(OperationMutateProductTarget);
        }

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

    private static OperationRequirement ResolveProcessArtifactWriteRequirement(ToolInvocationPolicyContext context)
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
            if (!context.RedactedArguments.TryGetValue("depth", out var depthValue) ||
                !int.TryParse(depthValue, out var depth) ||
                depth > 4)
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "Governed process browser snapshots must set depth to 4 or less. Retry once with depth=2 and do not repeat this blocked call.");
            }

            if (context.RedactedArguments.TryGetValue("boxes", out var boxesValue) &&
                bool.TryParse(boxesValue, out var boxes) &&
                boxes &&
                depth > 2)
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "Governed process browser snapshots with element boxes must set depth to 2 or less because deeper boxed snapshots can produce oversized tool output. Retry once with depth=2 or boxes=false and do not repeat this blocked call.");
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

    private static ToolInvocationPolicyDecision? EvaluateGovernedScriptExternalTargetAliasLiteral(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsGovernedProcessRun(context) ||
            !ExternalTargetAliasLiteralUnsafeScriptTools.Contains(context.ToolName) ||
            string.IsNullOrWhiteSpace(context.InspectedScriptContent))
        {
            return null;
        }

        var referencedAliases = ResolveExternalTargetAliasesFromText(context.InspectedScriptContent);
        if (referencedAliases.Count == 0)
        {
            return null;
        }

        var readableAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases)
            .Concat(NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var matchedAlias = referencedAliases.FirstOrDefault(alias => IsAllowedExternalTargetAlias(alias, readableAliases))
            ?? referencedAliases[0];

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Governed scripts must not use external-target aliases as literal OS paths. Alias '{matchedAlias}' is only valid in structured workspace tool path arguments; PowerShell and Python treat it as a relative path and can create a wrong nested external-target folder. Use structured workspace tools such as {ToolContractCatalog.WorkspaceDotNetNew}, {ToolContractCatalog.WorkspaceReadFile}, or {ToolContractCatalog.WorkspaceCopyPath} with the alias, or use the native absolute ProductRoot/DotNet* launch variable inside a ProductMutation script.");
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
        dotnetNewTemplateConsistencyPolicy.RecordSuccessfulInvocation(context);
    }

    private ToolInvocationPolicyDecision? EvaluateDotnetNewTemplateConsistency(
        ToolInvocationPolicyContext context,
        string signature)
        => dotnetNewTemplateConsistencyPolicy.Evaluate(context, signature);

    private sealed class DotnetNewTemplateConsistencyPolicy
    {
        private readonly Dictionary<string, string> dotnetNewTemplatesByScaffoldRoot = new(StringComparer.OrdinalIgnoreCase);

        public ToolInvocationPolicyDecision? Evaluate(
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

        public void RecordSuccessfulInvocation(ToolInvocationPolicyContext context)
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
    }

    private sealed class BrowserProofPolicy
    {
        public ToolInvocationPolicyDecision? EvaluateGovernedToolBounds(
            ToolInvocationPolicyContext context,
            string signature)
        {
            return DefaultAgentToolInvocationPolicy.EvaluateGovernedBrowserToolBounds(context, signature);
        }
    }

    private sealed class ExternalTargetBoundaryPolicy
    {
        public ToolInvocationPolicyDecision? EvaluateGovernedExternalTargetIsolation(
            ToolInvocationPolicyContext context,
            string signature)
        {
            return DefaultAgentToolInvocationPolicy.EvaluateGovernedExternalTargetIsolation(context, signature);
        }

        public ToolInvocationPolicyDecision? EvaluateGovernedProcessProductMutationBoundary(
            ToolInvocationPolicyContext context,
            string signature)
        {
            return DefaultAgentToolInvocationPolicy.EvaluateGovernedProcessProductMutationBoundary(context, signature);
        }

        public ToolInvocationPolicyDecision? EvaluateExternalTargetManagedWorkspaceIsolation(
            ToolInvocationPolicyContext context,
            string signature)
        {
            return DefaultAgentToolInvocationPolicy.EvaluateExternalTargetManagedWorkspaceIsolation(context, signature);
        }

        public ToolInvocationPolicyDecision? EvaluateReadOnlyExternalTargetMutation(
            ToolInvocationPolicyContext context,
            string signature)
        {
            return DefaultAgentToolInvocationPolicy.EvaluateReadOnlyExternalTargetMutation(context, signature);
        }
    }

    private sealed class ScriptSideEffectPolicy
    {
        public ToolInvocationPolicyDecision? EvaluateGovernedScriptSideEffectBoundary(
            ToolInvocationPolicyContext context,
            string signature)
        {
            return DefaultAgentToolInvocationPolicy.EvaluateGovernedScriptSideEffectBoundary(context, signature);
        }
    }

    private sealed class StaleProofPolicy
    {
        public ToolInvocationPolicyDecision? EvaluateGovernedStaleExternalProductCopySource(
            ToolInvocationPolicyContext context,
            string signature)
        {
            return DefaultAgentToolInvocationPolicy.EvaluateGovernedStaleExternalProductCopySource(context, signature);
        }

        public ToolInvocationPolicyDecision? EvaluateGovernedArchivedExternalProductPathAccess(
            ToolInvocationPolicyContext context,
            string signature)
        {
            return DefaultAgentToolInvocationPolicy.EvaluateGovernedArchivedExternalProductPathAccess(context, signature);
        }
    }

    private sealed class RepeatInvocationGuard
    {
        private readonly Dictionary<string, int> invocationCounts = new(StringComparer.OrdinalIgnoreCase);
        private int mutationGeneration;

        public ToolInvocationPolicyDecision? Evaluate(
            ToolInvocationPolicyContext context,
            string signature)
        {
            if (context.Classification is not (ToolInvocationClassification.Mutation or ToolInvocationClassification.Validation))
            {
                return null;
            }

            var countedSignature = context.Classification == ToolInvocationClassification.Validation
                ? $"{signature}|mutationGeneration={mutationGeneration}"
                : signature;
            var invocationCount = invocationCounts.TryGetValue(countedSignature, out var currentCount)
                ? currentCount + 1
                : 1;
            invocationCounts[countedSignature] = invocationCount;

            if (invocationCount <= MaxRepeatedMutationOrValidationInvocations)
            {
                return null;
            }

            return ToolInvocationPolicyDecision.Deny(
                countedSignature,
                $"Tool '{context.ToolName}' repeated the same mutation or validation signature {invocationCount} times in one run.");
        }

        public void RecordMutationDecision()
        {
            mutationGeneration++;
        }
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
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
        if (readableAliases.Length == 0)
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
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
        if (readableAliases.Length == 0)
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
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
        if (readableAliases.Length == 0)
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
            var rawPath = NormalizeToolArgument(pathArgument.Value);
            if (BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName) &&
                IsBroadManagedWorkspacePath(rawPath))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "This governed run has a grounded external product target. Broad managed-workspace root discovery is denied because it can pull stale source or helper files from unrelated runs; list or search the grounded external-target alias or current-run artifact folders instead.");
            }

            var nativeAbsolutePathDecision = EvaluateNativeAbsoluteWorkspaceToolPath(
                context,
                signature,
                rawPath,
                readableAliases);
            if (nativeAbsolutePathDecision is not null)
            {
                return nativeAbsolutePathDecision;
            }

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

            if (BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName) &&
                IsBroadManagedProcessRunDiscoveryPath(normalizedPath))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed run has a grounded external product target. Broad process-run artifact discovery at '{normalizedPath}' is denied because it can pull stale artifacts from unrelated runs; list or search a specific current-run or child-run artifact folder instead.");
            }

            var processRunArtifactBoundaryDecision = EvaluateManagedProcessRunArtifactBoundary(
                context,
                signature,
                normalizedPath);
            if (processRunArtifactBoundaryDecision is not null)
            {
                return processRunArtifactBoundaryDecision;
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

            if (IsReadOnlyProjectMediaImageTool(context) &&
                IsManagedProjectMediaImagePath(normalizedPath) &&
                IsManagedProjectMediaPathForCurrentProject(normalizedPath, context))
            {
                continue;
            }

            if (IsReadOnlyProjectMediaFileTool(context) &&
                IsManagedProjectMediaFilePath(normalizedPath) &&
                IsManagedProjectMediaPathForCurrentProject(normalizedPath, context))
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

    private static ToolInvocationPolicyDecision? EvaluateManagedProcessRunArtifactBoundary(
        ToolInvocationPolicyContext context,
        string signature,
        string normalizedPath)
    {
        if (!WorkspaceProcessRunArtifactPath.TryResolveRunId(normalizedPath, out var referencedRunId, out _))
        {
            return null;
        }

        var currentRunId = NormalizeToolArgument(context.ProcessRunId);
        if (string.Equals(referencedRunId, currentRunId, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (WorkspaceProcessRunArtifactPath.IsMalformedRunId(referencedRunId))
        {
            if (context.Classification == ToolInvocationClassification.Read &&
                WorkspaceProcessRunArtifactPath.IsRecoverableMalformedCurrentRunPath(normalizedPath, currentRunId))
            {
                return null;
            }

            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed run cannot use malformed managed process-run artifact ref '{normalizedPath}'. Copy the exact current-run artifact ref from the step brief or a successful subprocess launch result; do not abbreviate, ellipsize, or guess process run ids.");
        }

        if (!Guid.TryParse(currentRunId, out _))
        {
            return null;
        }

        if (ProcessStepAllows(context, OperationExecuteExternalAction))
        {
            return null;
        }

        if (context.Classification == ToolInvocationClassification.Read &&
            context.AllowedManagedArtifactReadRefs.Any(allowedRef =>
                string.Equals(
                    NormalizeManagedWorkspacePath(allowedRef),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase)))
        {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"This governed step cannot read managed artifacts for process run '{referencedRunId}' from '{normalizedPath}'. Use the current-run artifact root '{BuildCurrentRunManagedArtifactRoot(context)}' or an exact runtime-authorized upstream artifact ref listed in the step brief. Other cross-run artifacts require an external-action subprocess coordinator step.");
    }

    private static ToolInvocationPolicyDecision? EvaluateNativeAbsoluteWorkspaceToolPath(
        ToolInvocationPolicyContext context,
        string signature,
        string rawPath,
        IReadOnlyList<string> readableAliases)
    {
        if (string.IsNullOrWhiteSpace(rawPath) ||
            IsExternalTargetAliasPath(rawPath) ||
            !IsNativeAbsolutePath(rawPath))
        {
            return null;
        }

        var normalizedAlias = NormalizeExternalTargetAlias(rawPath);
        if (string.IsNullOrWhiteSpace(normalizedAlias))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed run has a grounded external product target. Native absolute path '{rawPath}' is outside the workspace-tool boundary; use a grounded external-target alias or a relative current-run artifact path.");
        }

        if (IsExplicitManagedWorkspaceFileRead(context, rawPath))
        {
            return null;
        }

        if (IsAllowedExternalTargetAlias(normalizedAlias, readableAliases))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed run has a grounded external product target. Workspace tools must use grounded external-target aliases, not native absolute paths. Retry this structured workspace tool with '{normalizedAlias}' instead of '{rawPath}' before treating the access problem as a blocker.");
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"This governed run has a grounded external product target. Native absolute path '{rawPath}' resolves to '{normalizedAlias}', which is outside the current-run external-target roots; use the grounded external-target alias or current-run artifact folders instead.");
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
            $"This governed .NET scaffold step is tool-only for product files. Use {ToolContractCatalog.WorkspaceDotNetNew} for new scaffolds or {ToolContractCatalog.WorkspacePowerShellRunScript} with a ProductMutation sideEffectManifest for surgical dotnet CLI operations or project-file repair. Do not retry {ToolContractCatalog.WorkspaceWriteFile} against external-target product paths; use it only for current-run artifacts.");
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedDotnetNewForce(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsGovernedProcessRun(context) ||
            !string.Equals(context.ToolName, ToolContractCatalog.WorkspaceDotNetNew, StringComparison.OrdinalIgnoreCase) ||
            !TryResolveTruthyToolArgument(context.RedactedArguments, "force"))
        {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Governed process steps cannot run {ToolContractCatalog.WorkspaceDotNetNew} with force=true because it can overwrite existing product scaffold files during retries. Use force=false for missing scaffolds, inspect existing files first, and repair drift with focused product-mutation tools or a reviewed ProductMutation helper script.");
    }

    private static bool TryResolveTruthyToolArgument(
        IReadOnlyDictionary<string, string> arguments,
        string key)
    {
        if (!arguments.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return value.Trim() switch
        {
            "1" => true,
            var text when string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) => true,
            var text when string.Equals(text, "y", StringComparison.OrdinalIgnoreCase) => true,
            var text when bool.TryParse(text, out _) => bool.Parse(text),
            _ => false
        };
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

    private static bool ProcessStepAllows(ToolInvocationPolicyContext context, string operation)
    {
        return !string.IsNullOrWhiteSpace(operation) &&
               (context.ProcessStepAllowedOperations?.Any(candidate =>
                   string.Equals(candidate, operation, StringComparison.OrdinalIgnoreCase)) ?? false);
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

    private static bool IsBroadManagedProcessRunDiscoveryPath(string normalizedPath)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 2 &&
            string.Equals(segments[0], "artifacts", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "process-runs", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return segments.Length == 5 &&
               string.Equals(segments[0], "artifacts", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[4], "process-runs", StringComparison.OrdinalIgnoreCase);
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

    private static bool IsNativeAbsolutePath(string path)
    {
        return WindowsNativeAbsolutePathRegex.IsMatch(path) ||
               path.StartsWith(@"\\", StringComparison.Ordinal) ||
               path.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool IsExplicitManagedWorkspaceFileRead(
        ToolInvocationPolicyContext context,
        string path)
    {
        if (!string.Equals(context.ToolName, ToolContractCatalog.WorkspaceReadFile, StringComparison.OrdinalIgnoreCase) ||
            context.Classification != ToolInvocationClassification.Read)
        {
            return false;
        }

        var normalizedPath = NormalizeManagedWorkspacePath(path);
        var payload = ExtractAfterMarker(normalizedPath, "/workspace/");
        if (string.IsNullOrWhiteSpace(payload))
        {
            return false;
        }

        payload = NormalizeManagedWorkspacePath(payload);
        if (IsBroadManagedWorkspacePath(payload) ||
            IsDeniedExternalRunManagedPath(payload))
        {
            return false;
        }

        var segments = payload.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 ||
            !HasFileExtension(segments[^1]))
        {
            return false;
        }

        return IsAllowedExternalRunManagedPath(payload) ||
               (segments.Length == 1 && IsManagedEvidenceFileName(segments[0]));
    }

    private static bool IsManagedEvidenceFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".log", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadOnlyProjectMediaImageTool(ToolInvocationPolicyContext context)
    {
        if (context.Classification != ToolInvocationClassification.Read ||
            !HasScopedProjectMediaReadGrant(context))
        {
            return false;
        }

        return string.Equals(context.ToolName, ToolContractCatalog.WorkspaceReadFile, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceStatPath, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceInspectImage, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceAnalyzeImage, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceAnalyzeImages, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadOnlyProjectMediaFileTool(ToolInvocationPolicyContext context)
    {
        if (context.Classification != ToolInvocationClassification.Read ||
            !HasScopedProjectMediaReadGrant(context))
        {
            return false;
        }

        return string.Equals(context.ToolName, ToolContractCatalog.WorkspaceReadFile, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceStatPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasScopedProjectMediaReadGrant(ToolInvocationPolicyContext context)
    {
        return string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) &&
               ProcessOperationContractNames.IsTargetScopeName(context.ProcessStepTargetScope) &&
               (ProcessStepAllows(context, OperationReadProjectStructure) ||
                ProcessStepAllows(context, OperationCaptureRuntimeProof));
    }

    private static bool IsManagedProjectMediaImagePath(string normalizedPath)
    {
        if (!normalizedPath.StartsWith("managed-files/project-media/images/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName);
        return extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".webp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".gif", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".svg", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".avif", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedProjectMediaFilePath(string normalizedPath)
    {
        if (!normalizedPath.StartsWith("managed-files/project-media/files/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = Path.GetFileName(normalizedPath);
        return !string.IsNullOrWhiteSpace(fileName) && HasFileExtension(fileName);
    }

    private static bool IsManagedProjectMediaPathForCurrentProject(
        string normalizedPath,
        ToolInvocationPolicyContext context)
    {
        if (!string.Equals(context.ContextWorkspaceScopeKind, WorkspaceScopeKind.Project.ToString(), StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(context.ContextWorkspaceScopeKey))
        {
            return false;
        }

        var projectSegment = ResolveManagedProjectMediaProjectSegment(normalizedPath);
        if (string.IsNullOrWhiteSpace(projectSegment))
        {
            return false;
        }

        return ResolveCurrentProjectMediaSegments(context.ContextWorkspaceScopeKey)
            .Contains(NormalizeProjectMediaSegment(projectSegment), StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveManagedProjectMediaProjectSegment(string normalizedPath)
    {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return segments.Length >= 4 &&
               string.Equals(segments[0], "managed-files", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "project-media", StringComparison.OrdinalIgnoreCase) &&
               (string.Equals(segments[2], "images", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(segments[2], "files", StringComparison.OrdinalIgnoreCase))
            ? segments[3]
            : string.Empty;
    }

    private static IReadOnlyList<string> ResolveCurrentProjectMediaSegments(string contextWorkspaceScopeKey)
    {
        var segments = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var normalizedKey = NormalizeProjectMediaSegment(contextWorkspaceScopeKey);
        if (!string.IsNullOrWhiteSpace(normalizedKey))
        {
            segments.Add(normalizedKey);
        }

        if (Guid.TryParse(contextWorkspaceScopeKey, out var projectId))
        {
            segments.Add(projectId.ToString("N"));
            segments.Add(projectId.ToString("D"));
        }

        return segments.ToArray();
    }

    private static string NormalizeProjectMediaSegment(string value)
        => string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Trim('/').Replace("-", string.Empty, StringComparison.Ordinal);

    private static string ExtractAfterMarker(string value, string marker)
    {
        var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? string.Empty : value[(index + marker.Length)..];
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

    private static bool IsCurrentStepPrimaryManagedArtifactPath(
        ToolInvocationPolicyContext context,
        string normalizedPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedPath))
        {
            return false;
        }

        var unscopedPath = NormalizeScopedProcessRunArtifactPath(normalizedPath);
        return string.Equals(
            unscopedPath,
            BuildCurrentStepPrimaryManagedArtifactPath(context),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildCurrentStepPrimaryManagedArtifactPath(ToolInvocationPolicyContext context)
        => $"{BuildCurrentRunManagedArtifactRoot(context)}/steps/{SanitizeProcessStepPathSegment(context.SourceId)}.md";

    private static string NormalizeScopedProcessRunArtifactPath(string normalizedPath)
    {
        if (!normalizedPath.StartsWith("artifacts/scopes/", StringComparison.OrdinalIgnoreCase))
        {
            return normalizedPath;
        }

        var processRunsIndex = normalizedPath.IndexOf("/process-runs/", StringComparison.OrdinalIgnoreCase);
        return processRunsIndex < 0
            ? normalizedPath
            : "artifacts" + normalizedPath[processRunsIndex..];
    }

    private static string SanitizeProcessStepPathSegment(string value)
    {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "step"
            : value.Trim();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.Length == 0 ? "step" : builder.ToString();
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
    public const string ProcessesTemplateLiveRunProfilesList = "processes_template_live_run_profiles_list";
    public const string ImageGenerationCreate = "image_generation_create";
    public const string WorkspaceInspectImage = "workspace_inspect_image";
    public const string WorkspaceAnalyzeImage = "workspace_analyze_image";
    public const string WorkspaceAnalyzeImages = "workspace_analyze_images";
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
    public const string ProjectStructureNodeTypeUpdate = "project_structure_node_type_update";
    public const string ProjectStructureNodeMetadataUpdate = "project_structure_node_metadata_update";
    public const string ProjectStructureNodesStatusUpdate = "project_structure_nodes_status_update";
    public const string ProjectStructureNodeStatusUpdate = "project_structure_node_status_update";
    public const string ProjectStructureNodesProgressUpdate = "project_structure_nodes_progress_update";
    public const string ProjectStructureNodeProgressUpdate = "project_structure_node_progress_update";
    public const string ProjectStructureNodesMarkerUpdate = "project_structure_nodes_marker_update";
    public const string ProjectStructureNodeMarkerUpdate = "project_structure_node_marker_update";
    public const string ProjectStructureNodesPriorityUpdate = "project_structure_nodes_priority_update";
    public const string ProjectStructureNodePriorityUpdate = "project_structure_node_priority_update";
    public const string ProjectStructureNodeMove = "project_structure_node_move";
    public const string ProjectStructureNodeRecompose = "project_structure_node_recompose";
    public const string ProjectStructureNodeReparent = "project_structure_node_reparent";
    public const string ProjectStructureNodeDescendantsToProjectMove = "project_structure_node_descendants_to_project_move";
    public const string ProjectStructureNodeCommandExecute = "project_structure_node_command_execute";
    public const string ProjectStructureNodeProcessDefinitionLink = "project_structure_node_process_definition_link";
    public const string ProjectStructureNodeProcessStart = "project_structure_node_process_start";
    public const string ProjectStructureProcessSubprocessLaunch = "project_structure_process_subprocess_launch";
    public const string ProjectStructureNodeWorkflowAddOptions = "project_structure_node_workflow_add_options";
    public const string ProjectStructureNodeWorkflowDefinitionCreate = "project_structure_node_workflow_definition_create";
    public const string ProjectStructureNodeWorkflowStart = "project_structure_node_workflow_start";
    public const string ProjectStructureNodeWorkflowStatusGet = "project_structure_node_workflow_status_get";
    public const string ProjectStructureNodeDelete = "project_structure_node_delete";
    public const string ProjectStructureNodesDelete = "project_structure_nodes_delete";
    public const string ProjectStructureApprovalRequest = "project_structure_approval_request";
    public const string ProjectStructureAssetCreate = "project_structure_asset_create";
    public const string ProjectStructureAssetGet = "project_structure_asset_get";
    public const string ProjectStructureAssetContentGet = "project_structure_asset_content_get";
    public const string ProjectStructureAssetCreateRevision = "project_structure_asset_create_revision";
    public const string ProjectStructureLinkCreate = "project_structure_link_create";
    public const string ProjectStructureLinkUnlink = "project_structure_link_unlink";
    public const string ProjectStructureImport = "project_structure_import";
    public const string ProjectStructureKnowledgeQuery = "project_structure_knowledge_query";
    public const string ProjectStructureAnalyticsQuery = "project_structure_analytics_query";
    public const string ProjectStructureProjectLeaseAcquire = "project_structure_project_lease_acquire";
    public const string ProjectStructureRepoBranchLeaseAcquire = "project_structure_repo_branch_lease_acquire";
    public const string ProjectStructureLeaseGet = "project_structure_lease_get";
    public const string ProjectStructureLeaseRenew = "project_structure_lease_renew";
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
        ProjectStructureAssetContentGet,
        ProjectStructureNodeWorkflowAddOptions,
        ProjectStructureNodeWorkflowStatusGet,
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
        ProjectStructureNodeTypeUpdate,
        ProjectStructureNodeMetadataUpdate,
        ProjectStructureNodesStatusUpdate,
        ProjectStructureNodeStatusUpdate,
        ProjectStructureNodesProgressUpdate,
        ProjectStructureNodeProgressUpdate,
        ProjectStructureNodesMarkerUpdate,
        ProjectStructureNodeMarkerUpdate,
        ProjectStructureNodesPriorityUpdate,
        ProjectStructureNodePriorityUpdate,
        ProjectStructureNodeMove,
        ProjectStructureNodeRecompose,
        ProjectStructureNodeReparent,
        ProjectStructureNodeDescendantsToProjectMove,
        ProjectStructureNodeCommandExecute,
        ProjectStructureNodeProcessDefinitionLink,
        ProjectStructureNodeProcessStart,
        ProjectStructureProcessSubprocessLaunch,
        ProjectStructureNodeWorkflowDefinitionCreate,
        ProjectStructureNodeWorkflowStart,
        ProjectStructureNodeDelete,
        ProjectStructureNodesDelete,
        ProjectStructureApprovalRequest,
        ProjectStructureAssetCreate,
        ProjectStructureAssetCreateRevision,
        ProjectStructureLinkCreate,
        ProjectStructureLinkUnlink,
        ProjectStructureImport,
        ProjectStructureProjectLeaseAcquire,
        ProjectStructureRepoBranchLeaseAcquire,
        ProjectStructureLeaseRenew,
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

    public static IReadOnlyCollection<AgentToolPolicyMetadata> Tools => ToolCapabilityRegistry.PolicyMetadata;

    public static IReadOnlyList<string> ProjectStructureReadTools => ProjectStructureReadToolNames.ToArray();

    public static IReadOnlyList<string> ProjectStructureMutationTools => ProjectStructureMutationToolNames.ToArray();

    public static ToolInvocationClassification Classify(string? toolName)
        => ToolCapabilityRegistry.Classify(toolName);

    public static bool IsMutationTool(string toolName)
        => ToolCapabilityRegistry.IsMutationTool(toolName);

    public static bool IsValidationTool(string toolName)
        => ToolCapabilityRegistry.IsValidationTool(toolName);

    public static bool IsProjectStructureMutationTool(string toolName)
    {
        return ProjectStructureMutationToolNames.Contains(toolName, StringComparer.OrdinalIgnoreCase);
    }

    public static bool RequiresApprovalByDefault(string toolName)
        => ToolCapabilityRegistry.RequiresApprovalByDefault(toolName);

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

}
