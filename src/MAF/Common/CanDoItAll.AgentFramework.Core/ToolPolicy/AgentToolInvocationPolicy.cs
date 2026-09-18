using static CanDoItAll.AgentFramework.Core.ToolInvocationWorkspacePathFacts;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Core.Execution;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.AgentFramework.Core;

public enum ToolInvocationDecisionKind
{
    Allow,
    Deny,
    RequireApproval,
    SanitizeResult,
    SkipExecution
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
    IReadOnlyList<AgentToolInvocationTrace>? ToolInvocationTraces = null,
    IReadOnlyList<string>? ProcessProductMutationRequiredBranchOutcomeKeys = null)
{
    public ToolCapabilityMetadata? DeclaredCapability { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public IToolInvocationScopePolicy? ScopePolicy { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public WorkspacePathScopeContribution? WorkspacePaths { get; init; }

    [System.Text.Json.Serialization.JsonIgnore]
    public string? ExternalWorkspaceReadRecoveryContinuation { get; init; }

    public string SourceId { get; init; } = string.Empty;

    public IReadOnlyList<string> AllowedManagedArtifactReadRefs { get; init; } = [];

    /// <summary>
    /// The admitted execution governance snapshot for this run, when present.
    /// The invocation policy enforces it independently of capability
    /// composition: a mutation-classified tool is denied when the snapshot
    /// forbids mutation, and workspace tools are denied when it forbids read.
    /// Absent for runs admitted without application context.
    /// </summary>
    public AgentExecutionGovernanceSnapshot? ExecutionGovernance { get; init; }

    public required ToolInvocationPathArgumentSet PathArguments { get; init; }

    // Expression-bodied so `with` mutations of the positional parameters stay
    // visible through these projections (a stored initializer would keep the
    // pre-mutation value on the cloned record).
    public IReadOnlyList<AgentToolInvocationTrace> RecentToolInvocationTraces => ToolInvocationTraces ?? [];

    public IReadOnlyList<string> ProductMutationToolNames => ProcessProductMutationToolNames ?? [];

    public IReadOnlyList<string> ProductMutationRequiredBranchOutcomeKeys =>
        ProcessProductMutationRequiredBranchOutcomeKeys ?? [];

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

public static class AgentToolPolicyBlockGuard {
    private static readonly HashSet<string> RecoverableWorkspaceReadDiscoveryTools = new(StringComparer.OrdinalIgnoreCase) {
        ToolContractCatalog.WorkspaceListDirectory,
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch,
        ToolContractCatalog.WorkspaceReadFile,
        ToolContractCatalog.WorkspaceStatPath,
        ToolContractCatalog.WorkspaceHashPath,
        ToolContractCatalog.WorkspaceDiffText
    };

    public static bool TryCreateRecoverableDeniedResult(
        string toolName, ToolInvocationPolicyDecision decision, ToolInvocationPolicyContext context, out string result) {
        ArgumentNullException.ThrowIfNull(decision);
        ArgumentNullException.ThrowIfNull(context);
        result = string.Empty;
        if (decision.Kind is not ToolInvocationDecisionKind.Deny and not ToolInvocationDecisionKind.SkipExecution) {
            return false;
        }
        if (IsRecoverableInteractiveExternalWorkspaceReadDenial(toolName, decision, context)) {
            var continuation = context.ExternalWorkspaceReadRecoveryContinuation ?? "Continue within the managed workspace";
            result = $"PolicyDenied: Tool '{toolName}' was denied by the external workspace boundary. {decision.Reason} This is a recoverable path error, not a failed agent run. Do not broaden or guess another external-target root. {continuation}, or use an exact external root explicitly grounded for this run.";
            return true;
        }
        return context.ScopePolicy?.TryCreateRecoverableDeniedResult(toolName, decision, context, out result) == true;
    }

    private static bool IsRecoverableInteractiveExternalWorkspaceReadDenial(
        string toolName,
        ToolInvocationPolicyDecision decision,
        ToolInvocationPolicyContext context) {
        if (context.ScopePolicy?.GetWorkspaceFacts(context).UsesScopedRecovery == true ||
            context.Classification != ToolInvocationClassification.Read ||
            !RecoverableWorkspaceReadDiscoveryTools.Contains(toolName)) {
            return false;
        }

        return decision.Reason.Contains("external-target", StringComparison.OrdinalIgnoreCase) &&
               (decision.Reason.Contains("outside the current run boundary", StringComparison.OrdinalIgnoreCase) ||
                decision.Reason.Contains("external drive root", StringComparison.OrdinalIgnoreCase));
    }

    public static void ThrowIfBlocked(
        string toolName,
        ToolInvocationPolicyDecision decision,
        bool hasEffectiveApprovalPath) {
        ArgumentNullException.ThrowIfNull(decision);

        if (decision.Kind is ToolInvocationDecisionKind.Deny or ToolInvocationDecisionKind.SkipExecution) {
            throw new AgentToolPolicyBlockedException(toolName, decision.Kind, decision.Reason);
        }

        if (decision.Kind == ToolInvocationDecisionKind.RequireApproval && !hasEffectiveApprovalPath) {
            throw new AgentToolPolicyBlockedException(toolName, decision.Kind, decision.Reason);
        }
    }
}

public sealed class DefaultAgentToolInvocationPolicy : IAgentToolInvocationPolicy
{
    public const int MaxRepeatedMutationOrValidationInvocations = 3;

    private readonly RepeatInvocationGuard repeatInvocationGuard = new();

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

        var governanceDecision = EvaluateExecutionGovernanceBoundary(context, signature);
        if (governanceDecision is not null)
        {
            return ValueTask.FromResult(governanceDecision);
        }

        var pathArgumentDecision = EvaluateScopeRestrictions(context, signature, ToolInvocationScopePolicyPhase.PathArguments);
        if (pathArgumentDecision is not null)
        {
            return ValueTask.FromResult(pathArgumentDecision);
        }

        var workspacePathDecision = WorkspacePathScopeContribution.Evaluate(
            context,
            signature);
        if (workspacePathDecision is not null) {
            return ValueTask.FromResult(workspacePathDecision);
        }

        var scopePolicy = context.ScopePolicy;
        if (IsGovernedProcessRun(context) && scopePolicy is null) {
            throw new InvalidOperationException("This governed run has no owner invocation-scope policy.");
        }
        var scopeDecision = EvaluateScopeRestrictions(context, signature, ToolInvocationScopePolicyPhase.Contract);
        if (scopeDecision is not null) {
            return ValueTask.FromResult(scopeDecision);
        }

        var governedBrowserDecision = EvaluateScopeRestrictions(context, signature, ToolInvocationScopePolicyPhase.BeforeExternalTargetBoundary);
        if (governedBrowserDecision is not null)
        {
            return ValueTask.FromResult(governedBrowserDecision);
        }

        var externalTargetDecision = EvaluateExternalTargetIsolation(context, signature);
        if (externalTargetDecision is not null)
        {
            return ValueTask.FromResult(externalTargetDecision);
        }

        var workspaceScopeDecision = EvaluateScopeRestrictions(context, signature, ToolInvocationScopePolicyPhase.AfterExternalTargetBoundary);
        if (workspaceScopeDecision is not null) {
            return ValueTask.FromResult(workspaceScopeDecision);
        }

        var readOnlyExternalTargetDecision = EvaluateReadOnlyExternalTargetMutation(context, signature);
        if (readOnlyExternalTargetDecision is not null)
        {
            return ValueTask.FromResult(readOnlyExternalTargetDecision);
        }

        var dotnetNewForceDecision = EvaluateScopeRestrictions(context, signature, ToolInvocationScopePolicyPhase.AfterReadOnlyTargetBoundary);
        if (dotnetNewForceDecision is not null)
        {
            return ValueTask.FromResult(dotnetNewForceDecision);
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

    /// <summary>
    /// Independent enforcement of the admitted execution governance snapshot.
    /// Composition already filters tools outside the snapshot, but the
    /// invocation policy re-checks so a tool that slipped into the graph (or a
    /// provider-side alias) still cannot exceed the admitted authority. The
    /// snapshot can only deny here — allowance still flows through every other
    /// policy below.
    /// </summary>
    private static ToolInvocationPolicyDecision? EvaluateExecutionGovernanceBoundary(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (context.ExecutionGovernance is not { } governance)
        {
            return null;
        }

        if (!governance.ReadAllowed &&
            context.Classification is ToolInvocationClassification.Read or ToolInvocationClassification.Mutation &&
            IsWorkspaceBoundaryTool(context.ToolName))
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Tool '{context.ToolName}' is denied because the admitted execution authority for this turn does not grant workspace read access.");
        }

        if (!governance.MutationAllowed &&
            context.Classification == ToolInvocationClassification.Mutation)
        {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Tool '{context.ToolName}' is denied because the admitted execution authority for this turn is read-only. Approval cannot widen an admitted read-only authority; start a new turn from a surface with mutation access instead.");
        }

        return null;
    }

    public static string BuildInspectedChildScriptMarker(string childScript)
    {
        return WorkspaceScriptSideEffectAnalyzer.BuildInspectedChildScriptMarker(childScript);
    }

    private sealed class RepeatInvocationGuard
    {
        private readonly Dictionary<string, int> invocationCounts = new(StringComparer.OrdinalIgnoreCase);
        private int validationEpoch;

        public ToolInvocationPolicyDecision? Evaluate(
            ToolInvocationPolicyContext context,
            string signature)
        {
            if (StartsBrowserInteractionEpoch(context)) {
                validationEpoch++;
                return null;
            }

            if (context.Classification is not (ToolInvocationClassification.Mutation or ToolInvocationClassification.Validation))
            {
                return null;
            }

            var countedSignature = context.Classification == ToolInvocationClassification.Validation
                ? $"{signature}|validationEpoch={validationEpoch}"
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
            validationEpoch++;
        }

        private static bool StartsBrowserInteractionEpoch(ToolInvocationPolicyContext context) {
            var capability = context.DeclaredCapability ??
                (ToolCapabilityRegistry.TryResolve(context.ToolName, out var registered) ? registered : null);
            return capability?.BrowserProofRole == ToolCapabilityBrowserProofRole.Interaction;
        }
    }

    private static ToolInvocationPolicyDecision? EvaluateExternalTargetIsolation(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsWorkspaceBoundaryTool(context.ToolName))
        {
            return null;
        }

        var pathArguments = ResolveManagedWorkspacePathArguments(context);
        if (pathArguments.Count == 0)
        {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
        foreach (var pathArgument in pathArguments)
        {
            var normalizedPath = NormalizeManagedWorkspacePath(pathArgument.Value);
            if (!IsExternalTargetAliasPath(normalizedPath))
            {
                continue;
            }

            var referencedAlias = NormalizeExternalTargetAlias(normalizedPath);
            if (string.IsNullOrWhiteSpace(referencedAlias))
            {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "The requested external-target path resolves to an external drive root. External drive-root discovery is denied; use a specific grounded path like external-target/C/path/to/project.");
            }

            if (IsAllowedExternalTargetAlias(referencedAlias, readableAliases))
            {
                continue;
            }

            var allowedSummary = readableAliases.Length == 0
                ? "no external-target roots are grounded for this run"
                : $"current-run roots: {string.Join(", ", readableAliases)}";
            var currentRunGuidance = BuildCurrentRunExternalTargetGuidance(allowedAliases, readOnlyAliases);
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Workspace tools may only access external-target paths grounded by the current run. The requested external-target path is outside the current run boundary; {allowedSummary}.{currentRunGuidance}");
        }

        return null;
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

    private static ToolInvocationPolicyDecision? EvaluateReadOnlyExternalTargetMutation(
        ToolInvocationPolicyContext context,
        string signature)
    {
        if (!IsWorkspaceFileMutationTool(context.ToolName))
        {
            return null;
        }

        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        if (readOnlyAliases.Count == 0)
        {
            return null;
        }

        var referencedAliases = ResolveReferencedExternalTargetAliases(context);
        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var accessScope = new EffectiveExternalTargetAccessScope(allowedAliases, readOnlyAliases);
        var matchedAlias = referencedAliases.FirstOrDefault(referencedAlias =>
            accessScope.CanRead(referencedAlias) &&
            !accessScope.CanWrite(referencedAlias));
        if (string.IsNullOrWhiteSpace(matchedAlias))
        {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"This governed step has read-only access to product target '{matchedAlias}'. Use read, build, test, run, browser, and durable evidence-artifact tools for validation; route defects to a repair implementation step instead of mutating product files from a review or QA step.");
    }

    private static ToolInvocationPolicyDecision? EvaluateScopeRestrictions(ToolInvocationPolicyContext context,
        string signature, ToolInvocationScopePolicyPhase phase) {
        var decision = context.ScopePolicy?.EvaluateRestrictions(context, signature, phase);
        if (decision is not null && decision.Kind is not (ToolInvocationDecisionKind.Deny or ToolInvocationDecisionKind.SkipExecution)) {
            throw new InvalidOperationException("An invocation-scope policy may only deny or defer to the remaining invocation policy.");
        }
        return decision;
    }

    private static bool IsGovernedProcessRun(ToolInvocationPolicyContext context)
    {
        return string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(context.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(context.ProcessStepId);
    }

}

public static class AgentToolInvocationPolicyMetadata
{
    private const string RedactedArgumentValue = "<redacted>";
    private const string InvalidApprovalArgumentsRetentionScheme = "approval-invalid-json-redacted-v1";

    private static readonly IReadOnlySet<string> SensitiveManagedArgumentPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "avatarImageUrl",
        "modelParameterConfigurationJson",
        "responseFormat",
        "revisionReason",
        "schemaDescription",
        "schemaJson",
        "schemaName",
        "settings",
        "systemPrompt",
        "affiliationId",
        "allowedExternalRoots",
        "allowedWorkingDirectories",
        "arguments",
        "command",
        "configurationJson",
        "displayName",
        "content",
        "countryCode",
        "creationReason",
        "description",
        "environmentVariableBindings",
        "endpoint",
        "endpointOrPath",
        "externalCode",
        "executorSettingsJson",
        "expectedValueJson",
        "headerBindings",
        "inputJson",
        "inlineInstructions",
        "instructions",
        "jobTitle",
        "jsonInput",
        "legalName",
        "managerPartyId",
        "name",
        "notes",
        "organizationPartyId",
        "organizationUnitPartyId",
        "otherConfiguration",
        "outputFormat",
        "phase",
        "personPartyId",
        "prompt",
        "preferredName",
        "query",
        "question",
        "region",
        "responseJson",
        "roleTitle",
        "searchText",
        "setupAttestationToken",
        "skillRoot",
        "summary",
        "tags",
        "text",
        "timeZone",
        "title",
        "visualBrief",
        "workingDirectory"
    };

    private static readonly IReadOnlySet<string> SafeDisplayArgumentPropertyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        "action",
        "contentType",
        "count",
        "format",
        "includeArchived",
        "includeChildren",
        "includeDeleted",
        "kind",
        "limit",
        "marker",
        "maxColumns",
        "maxRows",
        "mode",
        "model",
        "objectSubtype",
        "objectType",
        "offset",
        "operation",
        "path",
        "paths",
        "priority",
        "progressMode",
        "progressPercent",
        "revision",
        "scope",
        "scopeKind",
        "source",
        "status",
        "transport",
        "type"
    };

    public const string LoadSkill = "load_skill";
    public const string ReadSkillResource = "read_skill_resource";
    public const string RunSkillScript = "run_skill_script";
    public const string WorkspacePowerShellRunScript = "workspace_pwsh_run_script";
    public const string WorkspacePythonRunFile = "workspace_python_run_file";
    public const string WorkspaceInspectImage = "workspace_inspect_image";
    public const string WorkspaceAnalyzeImage = "workspace_analyze_image";
    public const string WorkspaceAnalyzeImages = "workspace_analyze_images";

    private enum ToolArgumentSanitizationMode
    {
        SecretKeys,
        SecretKeysAndManagedBusinessContent,
        DisplayKnownSafeFields,
        DisplayUnknownIdentityOnly
    }

    private readonly record struct SanitizedArgumentNode(
        JsonNode? Value,
        bool WasRedacted);

    public static IReadOnlyCollection<AgentToolPolicyMetadata> Tools => ToolCapabilityRegistry.PolicyMetadata;

    public static ToolInvocationClassification Classify(string? toolName, AgentToolPolicyCatalog? catalog = null)
        => (catalog ?? AgentToolPolicyCatalog.BuiltIn).Classify(toolName);

    public static bool IsMutationTool(string toolName, AgentToolPolicyCatalog? catalog = null)
        => Classify(toolName, catalog) == ToolInvocationClassification.Mutation;

    public static bool IsValidationTool(string toolName, AgentToolPolicyCatalog? catalog = null)
        => Classify(toolName, catalog) == ToolInvocationClassification.Validation;

    public static bool RequiresApprovalByDefault(string toolName, AgentToolPolicyCatalog? catalog = null)
        => (catalog ?? AgentToolPolicyCatalog.BuiltIn).RequiresApprovalByDefault(toolName);

    public static IReadOnlyDictionary<string, string> RedactArguments(
        IEnumerable<KeyValuePair<string, object?>> arguments)
        => RedactArguments(string.Empty, arguments);

    public static IReadOnlyDictionary<string, string> RedactArguments(
        string? toolName,
        IEnumerable<KeyValuePair<string, object?>> arguments,
        AgentToolPolicyCatalog? catalog = null) {
        return SanitizeArguments(toolName, arguments, catalog)
            .OrderBy(item => item.Key, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                item => item.Key,
                item => FormatArgumentValue(item.Value),
                StringComparer.OrdinalIgnoreCase);
    }

    public static IReadOnlyDictionary<string, object?> SanitizeArguments(
        string? toolName,
        IEnumerable<KeyValuePair<string, object?>> arguments,
        AgentToolPolicyCatalog? catalog = null) {
        ArgumentNullException.ThrowIfNull(arguments);

        var mode = HasSensitiveBusinessArguments(toolName, catalog)
            ? ToolArgumentSanitizationMode.SecretKeysAndManagedBusinessContent
            : ToolArgumentSanitizationMode.SecretKeys;
        return SanitizeArguments(arguments, mode);
    }

    public static IReadOnlyDictionary<string, object?> SanitizeArgumentsForDisplay(
        string? toolName,
        IEnumerable<KeyValuePair<string, object?>> arguments,
        AgentToolPolicyCatalog? catalog = null) {
        ArgumentNullException.ThrowIfNull(arguments);

        return SanitizeArguments(
            arguments,
            (catalog ?? AgentToolPolicyCatalog.BuiltIn).TryResolve(toolName, out _)
                ? ToolArgumentSanitizationMode.DisplayKnownSafeFields
                : ToolArgumentSanitizationMode.DisplayUnknownIdentityOnly);
    }

    private static IReadOnlyDictionary<string, object?> SanitizeArguments(
        IEnumerable<KeyValuePair<string, object?>> arguments,
        ToolArgumentSanitizationMode mode)
    {
        return arguments
            .Where(item => !string.IsNullOrWhiteSpace(item.Key))
            .ToDictionary(
                item => item.Key,
                item => SanitizeArgumentValue(item.Key, item.Value, mode),
                StringComparer.OrdinalIgnoreCase);
    }

    public static bool HasSensitiveBusinessArguments(string? toolName, AgentToolPolicyCatalog? catalog = null) {
        return (catalog ?? AgentToolPolicyCatalog.BuiltIn).TryResolve(toolName, out var metadata) &&
            metadata.BusinessArgumentRetentionScheme is not null;
    }

    internal static string ProtectApprovalArgumentsForAudit(
        string? toolName,
        string? argumentsJson,
        AgentToolPolicyCatalog? catalog = null) {
        var retentionScheme = ResolveApprovalAuditRetentionScheme(toolName, catalog);
        var normalizedArgumentsJson = string.IsNullOrWhiteSpace(argumentsJson)
            ? "{}"
            : argumentsJson.Trim();

        try
        {
            using var document = JsonDocument.Parse(normalizedArgumentsJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("Approval arguments must be a JSON object.");
            }

            var canonicalArgumentsJson = CanonicalizeJson(document.RootElement)?.ToJsonString() ?? "null";
            var mode = retentionScheme is null
                ? ToolArgumentSanitizationMode.SecretKeys
                : ToolArgumentSanitizationMode.SecretKeysAndManagedBusinessContent;
            var protectedArguments = SanitizeArgumentElement(
                document.RootElement,
                propertyName: null,
                mode: mode);
            if (retentionScheme is null)
            {
                return protectedArguments.WasRedacted
                    ? protectedArguments.Value?.ToJsonString() ?? "null"
                    : argumentsJson ?? "{}";
            }

            var audit = new JsonObject
            {
                ["retentionScheme"] = retentionScheme,
                ["argumentsSha256"] = ComputeSha256Hash(canonicalArgumentsJson),
                ["arguments"] = protectedArguments.Value
            };
            return audit.ToJsonString();
        }
        catch (Exception exception) when (exception is JsonException or ArgumentException or InvalidOperationException)
        {
            if (retentionScheme is null)
            {
                return new JsonObject
                {
                    ["retentionScheme"] = InvalidApprovalArgumentsRetentionScheme,
                    ["argumentsSha256"] = ComputeSha256Hash(normalizedArgumentsJson),
                    ["arguments"] = RedactedArgumentValue
                }.ToJsonString();
            }

            var audit = new JsonObject
            {
                ["retentionScheme"] = retentionScheme,
                ["argumentsSha256"] = ComputeSha256Hash(normalizedArgumentsJson),
                ["arguments"] = JsonValue.Create("<redacted-invalid-json>")
            };
            return audit.ToJsonString();
        }
    }

    internal static string ProtectPreviouslyProtectedApprovalArgumentsForExport(
        string? toolName,
        string? argumentsJson,
        AgentToolPolicyCatalog? catalog = null) {
        var retentionScheme = ResolveApprovalAuditRetentionScheme(toolName, catalog);
        if (retentionScheme is null || string.IsNullOrWhiteSpace(argumentsJson))
        {
            return ProtectApprovalArgumentsForAudit(toolName, argumentsJson, catalog);
        }

        try
        {
            using var document = JsonDocument.Parse(argumentsJson);
            return IsProtectedApprovalAudit(document.RootElement, retentionScheme)
                ? document.RootElement.GetRawText()
                : ProtectApprovalArgumentsForAudit(toolName, argumentsJson, catalog);
        }
        catch (JsonException)
        {
            return ProtectApprovalArgumentsForAudit(toolName, argumentsJson, catalog);
        }
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
        => WorkflowExecutorRedaction.IsSensitivePropertyName(key);

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

    private static object? SanitizeArgumentValue(
        string propertyName,
        object? value,
        ToolArgumentSanitizationMode mode)
    {
        if (ShouldRedact(propertyName))
        {
            return RedactedArgumentValue;
        }

        if (value is null)
        {
            return null;
        }

        try
        {
            var element = value is JsonElement jsonElement
                ? jsonElement
                : JsonSerializer.SerializeToElement(value, AgentOutputJson.SerializerOptions);
            var sanitized = SanitizeArgumentElement(element, propertyName, mode);
            return sanitized.WasRedacted ||
                   mode == ToolArgumentSanitizationMode.SecretKeysAndManagedBusinessContent
                ? sanitized.Value
                : value;
        }
        catch (Exception exception) when (exception is JsonException or NotSupportedException)
        {
            var valueTypeName = value.GetType().FullName ?? "unknown";
            return $"{RedactedArgumentValue}#{ComputeStableHash(valueTypeName)}";
        }
    }

    private static SanitizedArgumentNode SanitizeArgumentElement(
        JsonElement element,
        string? propertyName,
        ToolArgumentSanitizationMode mode)
    {
        if (!string.IsNullOrWhiteSpace(propertyName) && ShouldRedact(propertyName))
        {
            return new SanitizedArgumentNode(
                JsonValue.Create(RedactedArgumentValue),
                WasRedacted: true);
        }

        if (mode == ToolArgumentSanitizationMode.SecretKeysAndManagedBusinessContent &&
            !string.IsNullOrWhiteSpace(propertyName) &&
            SensitiveManagedArgumentPropertyNames.Contains(propertyName))
        {
            return new SanitizedArgumentNode(
                JsonValue.Create($"{RedactedArgumentValue}#{ComputeStableHash(element.GetRawText())}"),
                WasRedacted: true);
        }

        if ((mode == ToolArgumentSanitizationMode.DisplayKnownSafeFields ||
             mode == ToolArgumentSanitizationMode.DisplayUnknownIdentityOnly) &&
            element.ValueKind is not (JsonValueKind.Object or JsonValueKind.Array) &&
            !IsSafeDisplayValue(propertyName, element, mode))
        {
            return new SanitizedArgumentNode(
                JsonValue.Create($"{RedactedArgumentValue}#{ComputeStableHash(element.GetRawText())}"),
                WasRedacted: true);
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => SanitizeArgumentObject(element, mode),
            JsonValueKind.Array => SanitizeArgumentArray(element, propertyName, mode),
            JsonValueKind.String => SanitizeArgumentString(element),
            JsonValueKind.Null or JsonValueKind.Undefined => new SanitizedArgumentNode(null, WasRedacted: false),
            _ => new SanitizedArgumentNode(JsonNode.Parse(element.GetRawText()), WasRedacted: false)
        };
    }

    private static SanitizedArgumentNode SanitizeArgumentString(JsonElement element)
    {
        var value = element.GetString() ?? string.Empty;
        var sanitizedValue = WorkflowExecutorRedaction.RedactText(value);
        return new SanitizedArgumentNode(
            JsonValue.Create(sanitizedValue),
            WasRedacted: !string.Equals(value, sanitizedValue, StringComparison.Ordinal));
    }

    private static SanitizedArgumentNode SanitizeArgumentObject(
        JsonElement element,
        ToolArgumentSanitizationMode mode)
    {
        var result = new JsonObject();
        var wasRedacted = false;
        IEnumerable<JsonProperty> properties = element.EnumerateObject().ToArray();
        if (mode is ToolArgumentSanitizationMode.DisplayKnownSafeFields or
            ToolArgumentSanitizationMode.DisplayUnknownIdentityOnly)
        {
            properties = properties
                .OrderBy(property =>
                    !ShouldRedact(property.Name) && IsIdentityPropertyName(property.Name)
                        ? 0
                        : 1)
                .ThenBy(property => property.Name, StringComparer.Ordinal);
        }
        else if (mode is not ToolArgumentSanitizationMode.SecretKeys)
        {
            properties = properties.OrderBy(property => property.Name, StringComparer.Ordinal);
        }

        foreach (var property in properties)
        {
            var sanitized = SanitizeArgumentElement(property.Value, property.Name, mode);
            result[property.Name] = sanitized.Value;
            wasRedacted |= sanitized.WasRedacted;
        }

        return new SanitizedArgumentNode(result, wasRedacted);
    }

    private static SanitizedArgumentNode SanitizeArgumentArray(
        JsonElement element,
        string? propertyName,
        ToolArgumentSanitizationMode mode)
    {
        var result = new JsonArray();
        var wasRedacted = false;
        foreach (var item in element.EnumerateArray())
        {
            var sanitized = SanitizeArgumentElement(item, propertyName, mode);
            result.Add(sanitized.Value);
            wasRedacted |= sanitized.WasRedacted;
        }

        return new SanitizedArgumentNode(result, wasRedacted);
    }

    private static bool IsSafeDisplayValue(
        string? propertyName,
        JsonElement element,
        ToolArgumentSanitizationMode mode)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        var normalized = string.Concat(propertyName.Where(char.IsLetterOrDigit));
        var isIdentity = IsIdentityPropertyName(propertyName);
        if (!isIdentity &&
            (mode == ToolArgumentSanitizationMode.DisplayUnknownIdentityOnly ||
             !SafeDisplayArgumentPropertyNames.Contains(normalized)))
        {
            return false;
        }

        return element.ValueKind switch
        {
            JsonValueKind.String => IsBoundedSingleLineDisplayValue(
                element.GetString(),
                isIdentity ? 160 : 260),
            JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => !isIdentity,
            JsonValueKind.Null or JsonValueKind.Undefined => true,
            _ => false
        };
    }

    private static bool IsIdentityPropertyName(string propertyName)
    {
        var trimmed = propertyName.Trim();
        return trimmed.Equals("id", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("ids", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("key", StringComparison.OrdinalIgnoreCase) ||
               trimmed.Equals("keys", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith("Id", StringComparison.Ordinal) ||
               trimmed.EndsWith("Ids", StringComparison.Ordinal) ||
               trimmed.EndsWith("Key", StringComparison.Ordinal) ||
               trimmed.EndsWith("Keys", StringComparison.Ordinal) ||
               trimmed.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith("_ids", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith("_key", StringComparison.OrdinalIgnoreCase) ||
               trimmed.EndsWith("_keys", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBoundedSingleLineDisplayValue(string? value, int maxLength)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.Length <= maxLength &&
               !value.Any(char.IsControl);
    }

    private static string? ResolveApprovalAuditRetentionScheme(string? toolName, AgentToolPolicyCatalog? catalog) {
        return (catalog ?? AgentToolPolicyCatalog.BuiltIn).TryResolve(toolName, out var metadata)
            ? metadata.BusinessArgumentRetentionScheme
            : null;
    }

    private static bool IsProtectedApprovalAudit(JsonElement element, string retentionScheme)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        var properties = element.EnumerateObject().ToArray();
        if (properties.Length != 3 ||
            !element.TryGetProperty("retentionScheme", out var retentionSchemeElement) ||
            retentionSchemeElement.ValueKind != JsonValueKind.String ||
            !string.Equals(retentionSchemeElement.GetString(), retentionScheme, StringComparison.Ordinal) ||
            !element.TryGetProperty("argumentsSha256", out var argumentsHashElement) ||
            argumentsHashElement.ValueKind != JsonValueKind.String ||
            !IsLowercaseHex(argumentsHashElement.GetString(), 64) ||
            !element.TryGetProperty("arguments", out var argumentsElement))
        {
            return false;
        }

        return argumentsElement.ValueKind switch
        {
            JsonValueKind.Object => IsSanitizedApprovalArgumentElement(
                argumentsElement,
                propertyName: null),
            JsonValueKind.String => string.Equals(
                argumentsElement.GetString(),
                "<redacted-invalid-json>",
                StringComparison.Ordinal),
            _ => false
        };
    }

    private static bool IsSanitizedApprovalArgumentElement(
        JsonElement element,
        string? propertyName)
    {
        if (!string.IsNullOrWhiteSpace(propertyName) && ShouldRedact(propertyName))
        {
            return element.ValueKind == JsonValueKind.String &&
                   string.Equals(element.GetString(), RedactedArgumentValue, StringComparison.Ordinal);
        }

        if (!string.IsNullOrWhiteSpace(propertyName) &&
            SensitiveManagedArgumentPropertyNames.Contains(propertyName))
        {
            const string hashPrefix = RedactedArgumentValue + "#";
            return element.ValueKind == JsonValueKind.String &&
                   element.GetString() is { } value &&
                   value.StartsWith(hashPrefix, StringComparison.Ordinal) &&
                   IsLowercaseHex(value[hashPrefix.Length..], 12);
        }

        return element.ValueKind switch
        {
            JsonValueKind.Object => element.EnumerateObject().All(property =>
                IsSanitizedApprovalArgumentElement(property.Value, property.Name)),
            JsonValueKind.Array => element.EnumerateArray().All(item =>
                IsSanitizedApprovalArgumentElement(item, propertyName: null)),
            JsonValueKind.String => element.GetString() is { } value &&
                                    string.Equals(
                                        WorkflowExecutorRedaction.RedactText(value),
                                        value,
                                        StringComparison.Ordinal),
            _ => true
        };
    }

    private static bool IsLowercaseHex(string? value, int expectedLength)
    {
        return value is { Length: var length } &&
               length == expectedLength &&
               value.All(character =>
                   character is >= '0' and <= '9' or >= 'a' and <= 'f');
    }

    private static JsonNode? CanonicalizeJson(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.Object => new JsonObject(element.EnumerateObject()
                .OrderBy(property => property.Name, StringComparer.Ordinal)
                .Select(property => new KeyValuePair<string, JsonNode?>(
                    property.Name,
                    CanonicalizeJson(property.Value)))),
            JsonValueKind.Array => new JsonArray(element.EnumerateArray()
                .Select(CanonicalizeJson)
                .ToArray()),
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => JsonNode.Parse(element.GetRawText())
        };
    }

    private static string ComputeStableHash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes, 0, 6).ToLowerInvariant();
    }

    private static string ComputeSha256Hash(string text)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(text));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

}
