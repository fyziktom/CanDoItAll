using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using static CanDoItAll.AgentFramework.Core.ToolInvocationWorkspacePathFacts;
using static CanDoItAll.Modules.Processes.ProcessWorkspacePathPolicy;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessToolInvocationScopePolicy : IToolInvocationScopePolicy {
    public static ProcessToolInvocationScopePolicy Instance { get; } = new();

    public ToolInvocationPolicyDecision? EvaluateRestrictions(ToolInvocationPolicyContext context, string signature) {
        ArgumentNullException.ThrowIfNull(context);
        return EvaluateGovernedProcessOperationAuthorization(context, signature)
            ?? EvaluateGovernedCurrentStepOwnOutputRead(context, signature)
            ?? EvaluateGovernedCurrentStepOwnOutputPlaceholderWrite(context, signature)
            ?? EvaluateRequiredProductMutationBeforeManagedOutput(context, signature);
    }

    public ToolInvocationPolicyDecision? EvaluateRestrictions(ToolInvocationPolicyContext context, string signature,
        ToolInvocationScopePolicyPhase phase) {
        ArgumentNullException.ThrowIfNull(context);
        return phase == ToolInvocationScopePolicyPhase.Contract
            ? EvaluateRestrictions(context, signature)
            : ProcessWorkspaceInvocationPolicy.Evaluate(context, signature, phase);
    }

    public ToolInvocationWorkspaceScopeFacts GetWorkspaceFacts(ToolInvocationPolicyContext context) {
        ArgumentNullException.ThrowIfNull(context);
        return new(IsProductMutationStep(context), BuildCurrentRunManagedArtifactRoot(context),
            ProcessToolInvocationRecoveryPolicy.IsRecoverableGovernedProcessStep(context));
    }

    public bool TryCreateRecoverableDeniedResult(string toolName, ToolInvocationPolicyDecision decision,
        ToolInvocationPolicyContext context, out string result)
        => ProcessToolInvocationRecoveryPolicy.TryCreateRecoverableDeniedResult(toolName, decision, context, out result);

    private static readonly Regex ManagedArtifactStatusLineRegex = new(
        @"^\s{0,3}#*\s*Status\s*:\s*(?<status>Waiting[\s_-]*Approval|In[\s_-]*Progress|Completed|Blocked|Failed|Refused)\b",
        RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.Multiline);

    private static readonly HashSet<string> CurrentStepOwnManagedOutputReadTools = new(StringComparer.OrdinalIgnoreCase) {
        ToolContractCatalog.WorkspaceReadFile,
        ToolContractCatalog.WorkspaceStatPath,
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch
    };

    private static readonly HashSet<string> CurrentStepOwnManagedOutputWriteTools = new(StringComparer.OrdinalIgnoreCase) {
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

    private const string OperationWriteManagedProcessArtifacts = ProcessOperationContractNames.WriteManagedProcessArtifacts;
    private const string OperationWriteExternalArtifactDestination = ProcessOperationContractNames.WriteExternalArtifactDestination;
    private const string OperationMutateProductTarget = ProcessOperationContractNames.MutateProductTarget;
    private const string OperationRunValidation = ProcessOperationContractNames.RunValidation;
    private const string OperationLaunchRuntime = ProcessOperationContractNames.LaunchRuntime;
    private const string OperationCaptureRuntimeProof = ProcessOperationContractNames.CaptureRuntimeProof;
    private const string OperationExecuteExternalAction = ProcessOperationContractNames.ExecuteExternalAction;
    private const string OperationRecoverArtifactsOnly = ProcessOperationContractNames.RecoverArtifactsOnly;

    private static readonly ToolOperationRequirementResolver operationRequirementResolver = new();

    private sealed class ToolOperationRequirementResolver {
        public IReadOnlyList<ToolCapabilityProcessOperationRequirement> Resolve(ToolInvocationPolicyContext context) {
            var capability = context.DeclaredCapability ??
                (ToolCapabilityRegistry.TryResolve(context.ToolName, out var registered) ? registered : null);
            if (capability is null) {
                return [];
            }

            return capability.OperationRequirementKind switch {
                ToolCapabilityOperationRequirementKind.Static => capability.OperationRequirements
                    .Select(requirement => new ToolCapabilityProcessOperationRequirement(requirement.AnyOf))
                    .ToArray(),
                ToolCapabilityOperationRequirementKind.WorkspaceFileMutation => [ResolveWorkspaceFileMutationRequirement(context)],
                ToolCapabilityOperationRequirementKind.WorkspaceScript => [ResolveWorkspaceScriptRequirement(context)],
                ToolCapabilityOperationRequirementKind.DotNetRun => [ResolveDotnetRunOperationRequirement(context)],
                ToolCapabilityOperationRequirementKind.ProcessArtifactWrite => [ResolveProcessArtifactWriteRequirement(context)],
                _ => []
            };
        }
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedProcessOperationAuthorization(
        ToolInvocationPolicyContext context,
        string signature) {
        return ProcessToolOperationAuthorizer.Evaluate(context, signature, ResolveOperationRequirements(context));
    }

    private static IReadOnlyList<ToolCapabilityProcessOperationRequirement> ResolveOperationRequirements(ToolInvocationPolicyContext context)
        => operationRequirementResolver.Resolve(context);

    private static ToolInvocationPolicyDecision? EvaluateGovernedCurrentStepOwnOutputRead(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            context.Classification != ToolInvocationClassification.Read ||
            !CurrentStepOwnManagedOutputReadTools.Contains(context.ToolName) ||
            string.IsNullOrWhiteSpace(context.ProcessRunId) ||
            string.IsNullOrWhiteSpace(context.SourceId)) {
            return null;
        }

        var matchedPath = ResolveManagedWorkspacePathArguments(context)
            .Select(argument => NormalizeManagedWorkspacePath(argument.Value))
            .FirstOrDefault(path => IsCurrentStepPrimaryManagedArtifactPath(context, path));
        if (string.IsNullOrWhiteSpace(matchedPath)) {
            return null;
        }

        var primaryRef = BuildCurrentStepPrimaryManagedArtifactPath(context);
        if (HasSuccessfulCurrentStepPrimaryManagedArtifactWrite(context, primaryRef) ||
            context.AllowedManagedArtifactReadRefs.Any(allowedRef =>
                string.Equals(
                    NormalizeManagedWorkspacePath(allowedRef),
                    NormalizeManagedWorkspacePath(primaryRef),
                    StringComparison.OrdinalIgnoreCase))) {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Governed process step '{context.SourceId}' cannot read, stat, list, or search its own primary managed output '{primaryRef}' before creating it. Do not retry that read. Continue from launch variables, upstream artifacts, project-structure context, or product readback. When the step has evidence for its outcome, create or overwrite that managed artifact with workspace_write_file or workspace_append_file, then return submit_process_step_outcome with evidenceRefs containing the same managed ref.");
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedCurrentStepOwnOutputPlaceholderWrite(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            context.Classification != ToolInvocationClassification.Mutation ||
            !CurrentStepOwnManagedOutputWriteTools.Contains(context.ToolName) ||
            string.IsNullOrWhiteSpace(context.ProcessRunId) ||
            string.IsNullOrWhiteSpace(context.SourceId)) {
            return null;
        }

        var matchedPath = ResolveManagedWorkspacePathArguments(context)
            .Select(argument => NormalizeManagedWorkspacePath(argument.Value))
            .FirstOrDefault(path => IsCurrentStepPrimaryManagedArtifactPath(context, path));
        if (string.IsNullOrWhiteSpace(matchedPath) ||
            !TryResolveManagedArtifactWriteContent(context.RedactedArguments, out var content) ||
            !TryResolveManagedArtifactStatus(content, out var status)) {
            return null;
        }

        var primaryRef = BuildCurrentStepPrimaryManagedArtifactPath(context);
        if (string.Equals(status, "InProgress", StringComparison.OrdinalIgnoreCase)) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Governed process step '{context.SourceId}' cannot write primary managed output '{primaryRef}' with status InProgress. Primary managed step artifacts are final evidence, not progress notes. Complete the required work and write Status: Completed, or submit a concrete Blocked/Failed/WaitingApproval outcome with actionable evidence.");
        }

        if (string.Equals(status, "Blocked", StringComparison.OrdinalIgnoreCase) &&
            IsStatusOnlyManagedArtifactPlaceholder(content)) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Governed process step '{context.SourceId}' cannot write a status-only Blocked placeholder to primary managed output '{primaryRef}'. Submit Blocked only with concrete denied tool, failed command, unavailable dependency, or other actionable boundary evidence.");
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateRequiredProductMutationBeforeManagedOutput(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!context.ProcessRequiresProductMutationBeforeManagedOutput ||
            !string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
            context.Classification != ToolInvocationClassification.Mutation ||
            !CurrentStepOwnManagedOutputWriteTools.Contains(context.ToolName) ||
            string.IsNullOrWhiteSpace(context.ProcessRunId) ||
            string.IsNullOrWhiteSpace(context.SourceId)) {
            return null;
        }

        var writesPrimaryManagedOutput = ResolveManagedWorkspacePathArguments(context)
            .Select(argument => NormalizeManagedWorkspacePath(argument.Value))
            .Any(path => IsCurrentStepPrimaryManagedArtifactPath(context, path));
        if (!writesPrimaryManagedOutput ||
            !TryResolveManagedArtifactWriteContent(context.RedactedArguments, out var content) ||
            !TryResolveManagedArtifactStatus(content, out var status) ||
            !string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        var primaryRef = BuildCurrentStepPrimaryManagedArtifactPath(context);
        var mutationRequiredBranches = context.ProductMutationRequiredBranchOutcomeKeys;
        if (mutationRequiredBranches.Count > 0) {
            var artifactOutcome = ManagedProcessArtifactOutcomeReader.Read(content);
            if (!artifactOutcome.IsValid || !artifactOutcome.HasBranchOutcomeKey) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Governed process step '{context.SourceId}' cannot write primary managed output '{primaryRef}' with status Completed because the artifact {ProcessToolInvocationRecoveryPolicy.ProductMutationBranchOutcomeRequiredDenialMarker} when the step uses branch-specific mutation evidence. Select one declared branch outcome before finalizing. If the selected branch requires a product mutation, perform it under a grounded external-target alias, verify the changed product file, and then write final evidence. If it does not require a mutation, record the current proof for that branch.");
            }

            if (!mutationRequiredBranches.Contains(artifactOutcome.BranchOutcomeKey, StringComparer.OrdinalIgnoreCase)) {
                return null;
            }
        }

        if (HasSuccessfulProductTargetMutation(context)) {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Governed process step '{context.SourceId}' cannot write primary managed output '{primaryRef}' with status Completed before a successful current-execution product-target mutation. Perform the required product mutation under a grounded external-target alias first, verify the changed product file, then write the final managed artifact. A planned changed-file list or an unchanged successful build is not product mutation evidence.");
    }

    private static bool HasSuccessfulProductTargetMutation(ToolInvocationPolicyContext context) {
        if (context.RecentToolInvocationTraces.Count == 0 ||
            context.AllowedExternalTargetAliases is null ||
            context.AllowedExternalTargetAliases.Count == 0) {
            return false;
        }

        var normalizedAliases = context.AllowedExternalTargetAliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(NormalizeManagedWorkspacePath)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
        return context.RecentToolInvocationTraces.Any(trace =>
            trace.Succeeded &&
            trace.CompletedAtUtc is not null &&
            context.ProductMutationToolNames.Contains(trace.ToolName, StringComparer.OrdinalIgnoreCase) &&
            normalizedAliases.Any(alias => ToolSignatureTargetsExternalAlias(trace.Signature, alias)));
    }

    private static bool ToolSignatureTargetsExternalAlias(string signature, string normalizedAlias) {
        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(normalizedAlias)) {
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
        out string content) {
        foreach (var argument in arguments) {
            if (!argument.Key.Contains("content", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var values = EnumerateArgumentTextValues(argument.Value)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .ToArray();
            if (values.Length == 0) {
                continue;
            }

            content = string.Join(Environment.NewLine, values);
            return true;
        }

        content = string.Empty;
        return false;
    }

    private static bool TryResolveManagedArtifactStatus(string content, out string status) {
        if (string.IsNullOrWhiteSpace(content)) {
            status = string.Empty;
            return false;
        }

        var match = ManagedArtifactStatusLineRegex.Match(content);
        if (!match.Success) {
            status = string.Empty;
            return false;
        }

        status = NormalizeManagedArtifactStatus(match.Groups["status"].Value);
        return !string.IsNullOrWhiteSpace(status);
    }

    private static string NormalizeManagedArtifactStatus(string status) {
        if (string.IsNullOrWhiteSpace(status)) {
            return string.Empty;
        }

        return string.Concat(status.Where(character => !char.IsWhiteSpace(character) && character != '-' && character != '_'));
    }

    private static bool IsStatusOnlyManagedArtifactPlaceholder(string content) {
        var normalized = content.Trim();
        if (normalized.Length > 700) {
            return false;
        }

        return !ContainsConcreteBlockedEvidenceSignal(normalized);
    }

    private static bool ContainsConcreteBlockedEvidenceSignal(string content) {
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
        string primaryRef) {
        if (context.RecentToolInvocationTraces.Count == 0 ||
            string.IsNullOrWhiteSpace(primaryRef)) {
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
        string managedPath) {
        if (string.IsNullOrWhiteSpace(signature) ||
            string.IsNullOrWhiteSpace(managedPath)) {
            return false;
        }

        var normalizedSignature = NormalizeManagedWorkspacePath(signature);
        var normalizedManagedPath = NormalizeManagedWorkspacePath(managedPath);
        return normalizedSignature.Contains("path=" + normalizedManagedPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedSignature.Contains("relativepath=" + normalizedManagedPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedSignature.Contains("filepath=" + normalizedManagedPath, StringComparison.OrdinalIgnoreCase) ||
               normalizedSignature.Contains("targetpath=" + normalizedManagedPath, StringComparison.OrdinalIgnoreCase);
    }

    private static ToolCapabilityProcessOperationRequirement ResolveWorkspaceFileMutationRequirement(ToolInvocationPolicyContext context) {
        var referencedAliases = ResolveReferencedExternalTargetAliases(context);
        if (referencedAliases.Count > 0) {
            var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
            if (IsProductMutationStep(context) &&
                referencedAliases.Any(alias => IsAllowedExternalTargetAlias(alias, allowedAliases))) {
                return ToolCapabilityProcessOperationRequirement.Any(OperationMutateProductTarget);
            }

            if (referencedAliases.Any(IsExternalArtifactDestinationPath)) {
                return ToolCapabilityProcessOperationRequirement.Any(OperationWriteExternalArtifactDestination);
            }

            return ToolCapabilityProcessOperationRequirement.Any(OperationMutateProductTarget);
        }

        var normalizedPaths = ResolveManagedWorkspacePathArguments(context)
            .Select(argument => NormalizeManagedWorkspacePath(argument.Value))
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .ToArray();
        if (normalizedPaths.Length == 0) {
            return ToolCapabilityProcessOperationRequirement.Any(OperationMutateProductTarget);
        }

        return normalizedPaths.Any(path => IsAllowedExternalRunManagedPath(path) && !IsManagedOutputPath(path))
            ? ToolCapabilityProcessOperationRequirement.Any(OperationWriteManagedProcessArtifacts)
            : ToolCapabilityProcessOperationRequirement.Any(OperationMutateProductTarget);
    }

    private static ToolCapabilityProcessOperationRequirement ResolveDotnetRunOperationRequirement(ToolInvocationPolicyContext context) {
        var keepAlive = context.RedactedArguments.TryGetValue("keepAlive", out var keepAliveValue) &&
                        IsTruthyToolArgument(keepAliveValue);
        var processRunLifetime = context.RedactedArguments.TryGetValue("lifetimeScope", out var lifetimeScope) &&
                                 string.Equals(lifetimeScope, "ProcessRun", StringComparison.OrdinalIgnoreCase);

        return keepAlive || processRunLifetime
            ? ToolCapabilityProcessOperationRequirement.Any(OperationLaunchRuntime)
            : ToolCapabilityProcessOperationRequirement.Any(OperationRunValidation, OperationLaunchRuntime);
    }

    private static bool IsTruthyToolArgument(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        return value.Trim().Trim('`', '"', '\'') switch {
            "1" => true,
            var text when string.Equals(text, "true", StringComparison.OrdinalIgnoreCase) => true,
            var text when string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) => true,
            _ => false
        };
    }

    private static ToolCapabilityProcessOperationRequirement ResolveWorkspaceScriptRequirement(ToolInvocationPolicyContext context) {
        var manifest = TryParseScriptSideEffectManifestForRequirement(context);
        if (manifest?.Mode == GovernedScriptSideEffectMode.ProductMutation) {
            return ToolCapabilityProcessOperationRequirement.Any(OperationMutateProductTarget);
        }

        if (manifest?.Mode == GovernedScriptSideEffectMode.ExternalArtifactDestination) {
            return ToolCapabilityProcessOperationRequirement.Any(OperationWriteExternalArtifactDestination);
        }

        if (manifest?.Mode == GovernedScriptSideEffectMode.ManagedProcessArtifacts) {
            return ToolCapabilityProcessOperationRequirement.Any(OperationWriteManagedProcessArtifacts);
        }

        var analysis = WorkspaceScriptSideEffectAnalyzer.Analyze(context.ToolName, context.InspectedScriptContent);
        var referencedAliases = ResolveReferencedExternalTargetAliases(context)
            .Concat(ResolveExternalTargetAliasesFromText(context.InspectedScriptContent))
            .Concat(ResolveExternalTargetAliasesFromManifest(manifest))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var hasProductMutationIntent = analysis.HasWriteSignal ||
                                       (manifest?.Mode != GovernedScriptSideEffectMode.NoMutation &&
                                        IsProductMutationStep(context));
        if (hasProductMutationIntent &&
            referencedAliases.Any(alias => IsAllowedExternalTargetAlias(alias, allowedAliases))) {
            return ToolCapabilityProcessOperationRequirement.Any(OperationMutateProductTarget);
        }

        if (analysis.HasWriteSignal &&
            referencedAliases.Any(alias => !IsExternalArtifactDestinationPath(alias))) {
            return ToolCapabilityProcessOperationRequirement.Any(OperationMutateProductTarget);
        }

        if (referencedAliases.Any(IsExternalArtifactDestinationPath) ||
            ResolveScriptDeclaredOutputPaths(context)
                .Select(NormalizeManagedWorkspacePath)
                .Any(path => IsExternalTargetAliasPath(path) && IsExternalArtifactDestinationPath(NormalizeExternalTargetAlias(path)))) {
            return ToolCapabilityProcessOperationRequirement.Any(OperationWriteExternalArtifactDestination);
        }

        if (analysis.HasWriteSignal ||
            ResolveScriptDeclaredOutputPaths(context)
                .Select(NormalizeManagedWorkspacePath)
                .Any(path => IsAllowedExternalRunManagedPath(path) && !IsManagedOutputPath(path))) {
            return ToolCapabilityProcessOperationRequirement.Any(OperationWriteManagedProcessArtifacts);
        }

        return ToolCapabilityProcessOperationRequirement.Any(
            OperationRunValidation,
            OperationLaunchRuntime,
            OperationCaptureRuntimeProof,
            OperationExecuteExternalAction,
            OperationRecoverArtifactsOnly);
    }

    private static GovernedScriptSideEffectManifest? TryParseScriptSideEffectManifestForRequirement(
        ToolInvocationPolicyContext context) {
        if (string.IsNullOrWhiteSpace(context.ScriptSideEffectManifestJson)) {
            return null;
        }

        return GovernedScriptSideEffectManifest.TryParse(
            context.ScriptSideEffectManifestJson,
            out var manifest,
            out _)
                ? manifest
                : null;
    }

    private static ToolCapabilityProcessOperationRequirement ResolveProcessArtifactWriteRequirement(ToolInvocationPolicyContext context) {
        var referencedAliases = ResolveReferencedExternalTargetAliases(context);
        if (referencedAliases.Any(IsExternalArtifactDestinationPath)) {
            return ToolCapabilityProcessOperationRequirement.Any(OperationWriteExternalArtifactDestination);
        }

        return ToolCapabilityProcessOperationRequirement.Any(OperationWriteManagedProcessArtifacts, OperationRecoverArtifactsOnly);
    }

    private static IEnumerable<string> EnumerateArgumentTextValues(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            yield break;
        }

        var trimmed = value.Trim();
        if (trimmed.StartsWith("[", StringComparison.Ordinal) ||
            trimmed.StartsWith("{", StringComparison.Ordinal)) {
            var document = TryParseJsonDocument(trimmed);
            if (document is not null) {
                using (document) {
                    foreach (var text in EnumerateJsonStringValues(document.RootElement)) {
                        yield return text;
                    }
                }

                yield break;
            }
        }

        yield return trimmed;
    }

    private static JsonDocument? TryParseJsonDocument(string value) {
        try {
            return JsonDocument.Parse(value);
        }
        catch (JsonException) {
            return null;
        }
    }

    private static IEnumerable<string> EnumerateJsonStringValues(JsonElement element) {
        switch (element.ValueKind) {
            case JsonValueKind.String:
                var value = element.GetString();
                if (!string.IsNullOrWhiteSpace(value)) {
                    yield return value;
                }

                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray()) {
                    foreach (var itemValue in EnumerateJsonStringValues(item)) {
                        yield return itemValue;
                    }
                }

                break;
            case JsonValueKind.Object:
                foreach (var property in element.EnumerateObject()) {
                    foreach (var propertyValue in EnumerateJsonStringValues(property.Value)) {
                        yield return propertyValue;
                    }
                }

                break;
        }
    }

    private static bool IsProductMutationStep(ToolInvocationPolicyContext context) {
        return string.Equals(context.ProcessStepTargetScope, "ExternalProductTargetMutable", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ProcessStepTargetScope, "ManagedOutputProduct", StringComparison.OrdinalIgnoreCase) ||
               (context.ProcessStepAllowedOperations?.Any(operation =>
                   string.Equals(operation, OperationMutateProductTarget, StringComparison.OrdinalIgnoreCase)) ?? false);
    }

    private static string BuildCurrentRunManagedArtifactRoot(ToolInvocationPolicyContext context) {
        return string.IsNullOrWhiteSpace(context.ProcessRunId)
            ? "artifacts/process-runs/<current-run-id>"
            : $"artifacts/process-runs/{context.ProcessRunId.Trim()}";
    }

    private static bool IsCurrentStepPrimaryManagedArtifactPath(
        ToolInvocationPolicyContext context,
        string normalizedPath) {
        if (string.IsNullOrWhiteSpace(normalizedPath)) {
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

    private static string NormalizeScopedProcessRunArtifactPath(string normalizedPath) {
        if (!normalizedPath.StartsWith("artifacts/scopes/", StringComparison.OrdinalIgnoreCase)) {
            return normalizedPath;
        }

        var processRunsIndex = normalizedPath.IndexOf("/process-runs/", StringComparison.OrdinalIgnoreCase);
        return processRunsIndex < 0
            ? normalizedPath
            : "artifacts" + normalizedPath[processRunsIndex..];
    }

    private static string SanitizeProcessStepPathSegment(string value) {
        var normalized = string.IsNullOrWhiteSpace(value)
            ? "step"
            : value.Trim();
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized) {
            builder.Append(char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '-');
        }

        return builder.Length == 0 ? "step" : builder.ToString();
    }

}
