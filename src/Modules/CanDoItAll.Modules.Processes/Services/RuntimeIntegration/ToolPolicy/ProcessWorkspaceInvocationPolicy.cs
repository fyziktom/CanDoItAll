using System.Text.RegularExpressions;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.SharedKernel;
using static CanDoItAll.AgentFramework.Core.ToolInvocationWorkspacePathFacts;
using static CanDoItAll.Modules.Processes.ProcessWorkspacePathPolicy;

namespace CanDoItAll.Modules.Processes;

internal static class ProcessWorkspaceInvocationPolicy {
    internal static ToolInvocationPolicyDecision? Evaluate(ToolInvocationPolicyContext context, string signature,
        ToolInvocationScopePolicyPhase phase) => phase switch {
        ToolInvocationScopePolicyPhase.PathArguments => EvaluatePathArgumentResolution(context, signature),
        ToolInvocationScopePolicyPhase.BeforeExternalTargetBoundary => EvaluateGovernedBrowserToolBounds(context, signature),
        ToolInvocationScopePolicyPhase.AfterExternalTargetBoundary =>
            EvaluateGovernedScriptExternalTargetAliasLiteral(context, signature)
            ?? EvaluateGovernedStaleExternalProductCopySource(context, signature)
            ?? EvaluateGovernedArchivedExternalProductPathAccess(context, signature)
            ?? EvaluateGovernedProcessProductMutationBoundary(context, signature)
            ?? EvaluateGovernedScriptSideEffectBoundary(context, signature)
            ?? EvaluateExternalTargetManagedWorkspaceIsolation(context, signature),
        ToolInvocationScopePolicyPhase.AfterReadOnlyTargetBoundary => EvaluateGovernedDotnetNewForce(context, signature),
        _ => null
    };

    private static readonly Regex WindowsNativeAbsolutePathRegex = new(
        "^[A-Za-z]:[\\\\/]",
        RegexOptions.CultureInvariant);

    private static readonly HashSet<string> BroadManagedWorkspaceDiscoveryTools = new(StringComparer.OrdinalIgnoreCase) {
        ToolContractCatalog.WorkspaceListFiles,
        ToolContractCatalog.WorkspaceSearch
    };

    private static readonly HashSet<string> ExternalTargetAliasLiteralUnsafeScriptTools = new(StringComparer.OrdinalIgnoreCase) {
        AgentToolInvocationPolicyMetadata.WorkspacePowerShellRunScript,
        AgentToolInvocationPolicyMetadata.WorkspacePythonRunFile,
        AgentToolInvocationPolicyMetadata.RunSkillScript
    };

    private static readonly HashSet<string> ExternalProductArchiveSourceSegments = new(StringComparer.OrdinalIgnoreCase) {
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

    private const string OperationCaptureRuntimeProof = ProcessOperationContractNames.CaptureRuntimeProof;
    private const string OperationReadProjectStructure = ProcessOperationContractNames.ReadProjectStructure;
    private const string OperationExecuteExternalAction = ProcessOperationContractNames.ExecuteExternalAction;

    private static ToolInvocationPolicyDecision? EvaluatePathArgumentResolution(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!IsGovernedProcessRun(context) ||
            !IsWorkspaceBoundaryTool(context.ToolName) ||
            context.PathArguments is not { IsComplete: false } pathArguments) {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Governed process tool '{context.ToolName}' supplied unsupported path argument shape(s): {string.Join(", ", pathArguments.UnsupportedArgumentNames)}. Path arguments must be scalar strings, while plural path arguments must be arrays containing only strings.");
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedBrowserToolBounds(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!IsGovernedProcessRun(context)) {
            return null;
        }

        if (string.Equals(context.ToolName, "browser_snapshot", StringComparison.OrdinalIgnoreCase)) {
            if (!context.RedactedArguments.TryGetValue("depth", out var depthValue) ||
                !int.TryParse(depthValue, out var depth) ||
                depth > 4) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "Governed process browser snapshots must set depth to 4 or less. Retry once with depth=2 and do not repeat this blocked call.");
            }

            if (context.RedactedArguments.TryGetValue("boxes", out var boxesValue) &&
                bool.TryParse(boxesValue, out var boxes) &&
                boxes &&
                depth > 2) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "Governed process browser snapshots with element boxes must set depth to 2 or less because deeper boxed snapshots can produce oversized tool output. Retry once with depth=2 or boxes=false and do not repeat this blocked call.");
            }

            return null;
        }

        if (string.Equals(context.ToolName, "browser_take_screenshot", StringComparison.OrdinalIgnoreCase) &&
            context.RedactedArguments.TryGetValue("fullPage", out var fullPageValue) &&
            bool.TryParse(fullPageValue, out var fullPage) &&
            fullPage) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                "Governed process browser screenshots must be viewport-bounded. Retry once with fullPage=false or omit fullPage, and do not repeat this blocked call.");
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedProcessProductMutationBoundary(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!IsGovernedProcessRun(context) ||
            context.ProcessAllowsProductMutation ||
            !IsWorkspaceFileMutationTool(context.ToolName)) {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        foreach (var pathArgument in ResolveManagedWorkspacePathArguments(context)) {
            var normalizedPath = NormalizeManagedWorkspacePath(pathArgument.Value);
            if (string.IsNullOrWhiteSpace(normalizedPath)) {
                continue;
            }

            if (IsExternalTargetAliasPath(normalizedPath)) {
                var normalizedAlias = NormalizeExternalTargetAlias(normalizedPath);
                if (IsAllowedExternalTargetAlias(normalizedAlias, allowedAliases) &&
                    IsExternalArtifactDestinationPath(normalizedAlias)) {
                    continue;
                }

                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to mutate product targets. External product path '{normalizedPath}' is read-only for this step; write managed process artifacts under the current-run artifact root instead.");
            }

            if (IsManagedOutputPath(normalizedPath)) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to mutate managed output product files. Managed output path '{normalizedPath}' is outside the current-run process artifact boundary for this step.");
            }
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedScriptExternalTargetAliasLiteral(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!IsGovernedProcessRun(context) ||
            !ExternalTargetAliasLiteralUnsafeScriptTools.Contains(context.ToolName) ||
            string.IsNullOrWhiteSpace(context.InspectedScriptContent)) {
            return null;
        }

        var referencedAliases = ResolveExternalTargetAliasesFromText(context.InspectedScriptContent);
        if (referencedAliases.Count == 0) {
            return null;
        }

        var readableAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases)
            .Concat(NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
        var matchedAlias = referencedAliases.FirstOrDefault(alias => IsAllowedExternalTargetAlias(alias, readableAliases))
            ?? referencedAliases[0];

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Governed scripts must not use external-target aliases as literal OS paths. Alias '{matchedAlias}' is only valid in structured workspace tool path arguments; PowerShell and Python treat it as a relative path and can create a wrong nested external-target folder. Use structured workspace tools such as {ToolContractCatalog.WorkspaceDotNetNew}, {ToolContractCatalog.WorkspaceReadFile}, or {ToolContractCatalog.WorkspaceCopyPath} with the alias, or use the native absolute ProductRoot/DotNet* launch variable inside a ProductMutation script.");
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedScriptSideEffectBoundary(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!IsGovernedProcessRun(context) ||
            context.ProcessAllowsProductMutation ||
            !IsWorkspaceScriptExecutionTool(context.ToolName)) {
            return null;
        }

        if (!string.IsNullOrWhiteSpace(context.ScriptInspectionFailure)) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to mutate product targets. Script '{ResolveScriptPathDisplay(context)}' could not be inspected before execution: {context.ScriptInspectionFailure}");
        }

        if (string.IsNullOrWhiteSpace(context.InspectedScriptContent)) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to mutate product targets. Script '{ResolveScriptPathDisplay(context)}' must be inspected before execution; use current-run artifact writes or a read-only validation command instead.");
        }

        if (!GovernedScriptSideEffectManifest.TryParse(
                context.ScriptSideEffectManifestJson,
                out var manifest,
                out var manifestFailureMessage)) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to run scripts without declared side effects. Script '{ResolveScriptPathDisplay(context)}' was denied because {manifestFailureMessage}");
        }

        if (manifest.Mode == GovernedScriptSideEffectMode.ProductMutation) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to mutate product targets. Script '{ResolveScriptPathDisplay(context)}' declared product mutation in its side-effect manifest.");
        }

        var analysis = WorkspaceScriptSideEffectAnalyzer.Analyze(context.ToolName, context.InspectedScriptContent);
        var manifestDecision = EvaluateScriptSideEffectManifestBoundary(context, signature, manifest, analysis);
        if (manifestDecision is not null) {
            return manifestDecision;
        }

        var declaredOutputDecision = EvaluateScriptDeclaredOutputBoundary(context, signature, manifest);
        if (declaredOutputDecision is not null) {
            return declaredOutputDecision;
        }

        var referencedAliases = ResolveReferencedExternalTargetAliases(context)
            .Concat(ResolveExternalTargetAliasesFromText(context.InspectedScriptContent))
            .Concat(ResolveExternalTargetAliasesFromManifest(manifest))
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();
        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();

        foreach (var referencedAlias in referencedAliases) {
            if (!IsAllowedExternalTargetAlias(referencedAlias, readableAliases)) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                $"Governed scripts may only reference external-target paths grounded by the current run. Script '{ResolveScriptPathDisplay(context)}' references '{referencedAlias}', which is outside the current run boundary.");
            }
        }

        if (!analysis.HasWriteSignal) {
            return null;
        }

        var productAlias = referencedAliases.FirstOrDefault(alias => !IsExternalArtifactDestinationPath(alias));
        if (!string.IsNullOrWhiteSpace(productAlias)) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to mutate product targets. Script '{ResolveScriptPathDisplay(context)}' contains write operations against product target '{productAlias}'.");
        }

        var productWorkingContext = ResolveScriptProductWorkingContext(context);
        if (!string.IsNullOrWhiteSpace(productWorkingContext)) {
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
        WorkspaceScriptSideEffectAnalysis analysis) {
        var declaredWritePaths = ResolveScriptDeclaredOutputPaths(context)
            .Concat(manifest.DeclaredWritePaths)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .ToArray();

        if (analysis.EncodedCommandSignals.Count > 0) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to run encoded script content. Script '{ResolveScriptPathDisplay(context)}' contains encoded command usage that cannot be inspected: {string.Join(", ", analysis.EncodedCommandSignals)}.");
        }

        if (analysis.ShellDelegationSignals.Count > 0 && !manifest.AllowShellDelegation) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to run undeclared shell delegation. Script '{ResolveScriptPathDisplay(context)}' contains: {string.Join(", ", analysis.ShellDelegationSignals)}.");
        }

        var undeclaredChildScripts = analysis.ChildScriptSignals
            .Where(childScript => !WorkspaceScriptSideEffectAnalyzer.IsDeclaredChildScript(childScript, manifest.DeclaredChildScripts))
            .ToArray();
        if (undeclaredChildScripts.Length > 0) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed step is not authorized to run undeclared child scripts. Script '{ResolveScriptPathDisplay(context)}' invokes: {string.Join(", ", undeclaredChildScripts)}.");
        }

        if (analysis.ChildScriptSignals.Count > 0) {
            var uninspectedChildScripts = manifest.DeclaredChildScripts
                .Where(childScript => !WorkspaceScriptSideEffectAnalyzer.HasInspectedChildScriptMarker(context.InspectedScriptContent, childScript))
                .ToArray();
            if (uninspectedChildScripts.Length > 0) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to run child scripts that were not inspected. Script '{ResolveScriptPathDisplay(context)}' declared but did not inspect: {string.Join(", ", uninspectedChildScripts)}.");
            }
        }

        if (manifest.Mode == GovernedScriptSideEffectMode.NoMutation) {
            if (declaredWritePaths.Length > 0) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Script '{ResolveScriptPathDisplay(context)}' declared no mutation but also declared write paths: {string.Join(", ", declaredWritePaths)}.");
            }

            if (analysis.HasWriteSignal) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"Script '{ResolveScriptPathDisplay(context)}' declared no mutation but contains write-capable operations.");
            }
        }

        if (analysis.HasWriteSignal &&
            manifest.Mode is not GovernedScriptSideEffectMode.ManagedProcessArtifacts and not GovernedScriptSideEffectMode.ExternalArtifactDestination) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Script '{ResolveScriptPathDisplay(context)}' contains write-capable operations but did not declare an allowed non-product write mode.");
        }

        if (analysis.HasWriteSignal && declaredWritePaths.Length == 0) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"Script '{ResolveScriptPathDisplay(context)}' contains write-capable operations but did not declare the write target paths in `{GovernedScriptSideEffectManifest.ArgumentName}`.");
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateScriptDeclaredOutputBoundary(
        ToolInvocationPolicyContext context,
        string signature,
        GovernedScriptSideEffectManifest manifest) {
        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        foreach (var outputPath in ResolveScriptDeclaredOutputPaths(context).Concat(manifest.DeclaredWritePaths)) {
            var normalizedPath = NormalizeManagedWorkspacePath(outputPath);
            if (string.IsNullOrWhiteSpace(normalizedPath)) {
                continue;
            }

            if (IsExternalTargetAliasPath(normalizedPath)) {
                var normalizedAlias = NormalizeExternalTargetAlias(normalizedPath);
                if (IsAllowedExternalTargetAlias(normalizedAlias, allowedAliases) &&
                    IsExternalArtifactDestinationPath(normalizedAlias)) {
                    continue;
                }

                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to mutate product targets. Script output path '{normalizedPath}' is outside an allowed external artifact destination.");
            }

            if (IsManagedOutputPath(normalizedPath) ||
                !IsCurrentRunManagedArtifactPath(normalizedPath, context)) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed step is not authorized to mutate managed output product files. Script output path '{normalizedPath}' is outside the current-run process artifact boundary for this step.");
            }
        }

        return null;
    }

    private static string ResolveScriptProductWorkingContext(ToolInvocationPolicyContext context) {
        foreach (var argument in ResolveManagedWorkspacePathArguments(context)) {
            if (!string.Equals(argument.Name, "workingDirectory", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(argument.Name, "path", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(argument.Name, "scriptPath", StringComparison.OrdinalIgnoreCase)) {
                continue;
            }

            var productAlias = ResolveExternalTargetAliasesFromText(argument.Value)
                .FirstOrDefault(alias => !IsExternalArtifactDestinationPath(alias));
            if (!string.IsNullOrWhiteSpace(productAlias)) {
                return productAlias;
            }
        }

        return string.Empty;
    }

    private static string ResolveScriptPathDisplay(ToolInvocationPolicyContext context) {
        return context.RedactedArguments.TryGetValue("path", out var path) && !string.IsNullOrWhiteSpace(path)
            ? path
            : context.ToolName;
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedArchivedExternalProductPathAccess(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!IsGovernedProcessRun(context) ||
            !IsWorkspaceBoundaryTool(context.ToolName)) {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
        if (readableAliases.Length == 0) {
            return null;
        }

        var archivedAlias = ResolveReferencedExternalTargetAliases(context)
            .FirstOrDefault(alias =>
                IsAllowedExternalTargetAlias(alias, allowedAliases) &&
                IsExternalProductArchiveSourceAlias(alias));
        if (string.IsNullOrWhiteSpace(archivedAlias)) {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"This governed process run cannot use archived, backup, or previous-run product material at '{archivedAlias}' as current product input. Use the grounded product root excluding archive folders and current-run managed artifacts only.");
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedStaleExternalProductCopySource(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!IsGovernedProcessRun(context) ||
            !string.Equals(context.ToolName, "workspace_copy_path", StringComparison.OrdinalIgnoreCase) ||
            !IsProductMutationStep(context)) {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
        if (readableAliases.Length == 0) {
            return null;
        }

        var sourceAliases = ResolveExternalTargetAliasesFromArguments(
            context,
            IsCopySourceArgumentName);
        if (sourceAliases.Count == 0 ||
            !sourceAliases.Any(IsExternalProductArchiveSourceAlias)) {
            return null;
        }

        var destinationAliases = ResolveExternalTargetAliasesFromArguments(
            context,
            IsCopyDestinationArgumentName);
        if (!destinationAliases.Any(alias => IsAllowedExternalTargetAlias(alias, allowedAliases))) {
            return null;
        }

        var sourceAlias = sourceAliases.First(IsExternalProductArchiveSourceAlias);
        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"This governed product mutation step cannot copy archived, backup, or previous-run product material from '{sourceAlias}' into the current product target. Use the current-run project structure and mutate the grounded product root directly.");
    }

    private static ToolInvocationPolicyDecision? EvaluateExternalTargetManagedWorkspaceIsolation(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!IsGovernedProcessRun(context) ||
            !IsWorkspaceBoundaryTool(context.ToolName)) {
            return null;
        }

        var allowedAliases = NormalizeAllowedExternalTargetAliases(context.AllowedExternalTargetAliases);
        var readOnlyAliases = NormalizeAllowedExternalTargetAliases(context.ReadOnlyExternalTargetAliases);
        var readableAliases = allowedAliases
            .Concat(readOnlyAliases)
            .Distinct(ExternalTargetAliasCodec.EqualityComparer)
            .OrderByDescending(alias => alias.Length)
            .ToArray();
        if (readableAliases.Length == 0) {
            return null;
        }

        var pathArguments = ResolveManagedWorkspacePathArguments(context);
        if (pathArguments.Count == 0) {
            return BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName)
                ? ToolInvocationPolicyDecision.Deny(
                    signature,
                    "This governed run has a grounded external product target. Broad managed-workspace root discovery is denied because it can pull stale source or helper files from unrelated runs; list or search the grounded external-target alias or current-run artifact folders instead.")
                : null;
        }

        foreach (var pathArgument in pathArguments) {
            var rawPath = NormalizeToolArgument(pathArgument.Value);
            if (BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName) &&
                IsBroadManagedWorkspacePath(rawPath)) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "This governed run has a grounded external product target. Broad managed-workspace root discovery is denied because it can pull stale source or helper files from unrelated runs; list or search the grounded external-target alias or current-run artifact folders instead.");
            }

            if (IsExplicitManagedWorkspaceFileRead(context, rawPath)) {
                continue;
            }

            var nativeAbsolutePathDecision = EvaluateNativeAbsoluteWorkspaceToolPath(
                context,
                signature,
                rawPath,
                readableAliases);
            if (nativeAbsolutePathDecision is not null) {
                return nativeAbsolutePathDecision;
            }

            var normalizedPath = NormalizeManagedWorkspacePath(pathArgument.Value);
            if (string.IsNullOrWhiteSpace(normalizedPath) ||
                IsExternalTargetAliasPath(normalizedPath)) {
                continue;
            }

            if (IsShallowSharedManagedEvidencePath(normalizedPath)) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed run has a grounded external product target. Managed workspace path '{normalizedPath}' is a shallow shared scope artifact path and may be overwritten by unrelated concurrent runs; use the current-run artifact root '{BuildCurrentRunManagedArtifactRoot(context)}' unless a required artifact input or output names a deeper managed path.");
            }

            if (BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName) &&
                IsBroadManagedEvidenceDiscoveryPath(normalizedPath)) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed run has a grounded external product target. Broad managed evidence discovery at '{normalizedPath}' is denied because it can pull stale artifacts from unrelated runs; list or search the grounded external-target alias or current-run artifact root '{BuildCurrentRunManagedArtifactRoot(context)}' instead.");
            }

            if (BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName) &&
                IsBroadManagedProcessRunDiscoveryPath(normalizedPath)) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed run has a grounded external product target. Broad process-run artifact discovery at '{normalizedPath}' is denied because it can pull stale artifacts from unrelated runs; list or search a specific current-run or child-run artifact folder instead.");
            }

            var processRunArtifactBoundaryDecision = EvaluateManagedProcessRunArtifactBoundary(
                context,
                signature,
                normalizedPath);
            if (processRunArtifactBoundaryDecision is not null) {
                return processRunArtifactBoundaryDecision;
            }

            if (IsManagedOutputPath(normalizedPath) &&
                context.Classification is ToolInvocationClassification.Mutation or ToolInvocationClassification.Validation) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    $"This governed run has a grounded external product target. Managed output path '{normalizedPath}' is not a fallback product root; use the grounded external-target alias or return Blocked with the exact access problem.");
            }

            if (IsAllowedExternalRunManagedPath(normalizedPath)) {
                continue;
            }

            if (IsReadOnlyProjectMediaImageTool(context) &&
                IsManagedProjectMediaImagePath(normalizedPath) &&
                IsManagedProjectMediaPathForCurrentProject(
                    normalizedPath,
                    context)) {
                continue;
            }

            if (IsReadOnlyProjectMediaFileTool(context) &&
                IsManagedProjectMediaFilePath(normalizedPath) &&
                IsManagedProjectMediaPathForCurrentProject(
                    normalizedPath,
                    context)) {
                continue;
            }

            if (IsBroadManagedWorkspacePath(normalizedPath) &&
                BroadManagedWorkspaceDiscoveryTools.Contains(context.ToolName)) {
                return ToolInvocationPolicyDecision.Deny(
                    signature,
                    "This governed run has a grounded external product target. Broad managed-workspace root discovery is denied because it can pull stale source or helper files from unrelated runs; list or search the grounded external-target alias or current-run artifact folders instead.");
            }

            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed run has a grounded external product target. Managed workspace path '{normalizedPath}' is outside current-run evidence folders and may contain stale source or helper files from unrelated runs; use the grounded external-target alias or current-run artifacts instead.");
        }

        return null;
    }

    private static ToolInvocationPolicyDecision? EvaluateManagedProcessRunArtifactBoundary(
        ToolInvocationPolicyContext context,
        string signature,
        string normalizedPath) {
        if (!WorkspaceProcessRunArtifactPath.TryResolveRunId(normalizedPath, out var referencedRunId, out _)) {
            return null;
        }

        var currentRunId = NormalizeToolArgument(context.ProcessRunId);
        if (string.Equals(referencedRunId, currentRunId, StringComparison.OrdinalIgnoreCase)) {
            return null;
        }

        if (WorkspaceProcessRunArtifactPath.IsMalformedRunId(referencedRunId)) {
            if (context.Classification == ToolInvocationClassification.Read &&
                WorkspaceProcessRunArtifactPath.IsRecoverableMalformedCurrentRunPath(normalizedPath, currentRunId)) {
                return null;
            }

            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed run cannot use malformed managed process-run artifact ref '{normalizedPath}'. Copy the exact current-run artifact ref from the step brief or a successful subprocess launch result; do not abbreviate, ellipsize, or guess process run ids.");
        }

        if (!Guid.TryParse(currentRunId, out _)) {
            return null;
        }

        if (ProcessStepAllows(context, OperationExecuteExternalAction)) {
            return null;
        }

        if (context.Classification == ToolInvocationClassification.Read &&
            context.AllowedManagedArtifactReadRefs.Any(allowedRef =>
                string.Equals(
                    NormalizeManagedWorkspacePath(allowedRef),
                    normalizedPath,
                    StringComparison.OrdinalIgnoreCase))) {
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
        IReadOnlyList<string> readableAliases) {
        if (string.IsNullOrWhiteSpace(rawPath) ||
            IsExternalTargetAliasPath(rawPath) ||
            !IsNativeAbsolutePath(rawPath)) {
            return null;
        }

        var normalizedAlias = NormalizeExternalTargetAlias(rawPath);
        if (string.IsNullOrWhiteSpace(normalizedAlias)) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed run has a grounded external product target. Native absolute path '{rawPath}' is outside the workspace-tool boundary; use a grounded external-target alias or a relative current-run artifact path.");
        }

        if (IsAllowedExternalTargetAlias(normalizedAlias, readableAliases)) {
            return ToolInvocationPolicyDecision.Deny(
                signature,
                $"This governed run has a grounded external product target. Workspace tools must use grounded external-target aliases, not native absolute paths. Retry this structured workspace tool with '{normalizedAlias}' instead of '{rawPath}' before treating the access problem as a blocker.");
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"This governed run has a grounded external product target. Native absolute path '{rawPath}' resolves to '{normalizedAlias}', which is outside the current-run external-target roots; use the grounded external-target alias or current-run artifact folders instead.");
    }

    private static ToolInvocationPolicyDecision? EvaluateGovernedDotnetNewForce(
        ToolInvocationPolicyContext context,
        string signature) {
        if (!IsGovernedProcessRun(context) ||
            !string.Equals(context.ToolName, ToolContractCatalog.WorkspaceDotNetNew, StringComparison.OrdinalIgnoreCase) ||
            !TryResolveTruthyToolArgument(context.RedactedArguments, "force")) {
            return null;
        }

        return ToolInvocationPolicyDecision.Deny(
            signature,
            $"Governed process steps cannot run {ToolContractCatalog.WorkspaceDotNetNew} with force=true because it can overwrite existing target files during retries. Use force=false for missing targets, inspect existing files first, and repair drift with focused product-mutation tools or a reviewed ProductMutation helper script.");
    }

    private static bool TryResolveTruthyToolArgument(
        IReadOnlyDictionary<string, string> arguments,
        string key) {
        if (!arguments.TryGetValue(key, out var value) ||
            string.IsNullOrWhiteSpace(value)) {
            return false;
        }

        return value.Trim() switch {
            "1" => true,
            var text when string.Equals(text, "yes", StringComparison.OrdinalIgnoreCase) => true,
            var text when string.Equals(text, "y", StringComparison.OrdinalIgnoreCase) => true,
            var text when bool.TryParse(text, out _) => bool.Parse(text),
            _ => false
        };
    }

    private static bool IsProductMutationStep(ToolInvocationPolicyContext context)
        => RequireScopePolicy(context).GetWorkspaceFacts(context).HasMutationIntent;
    private static string BuildCurrentRunManagedArtifactRoot(ToolInvocationPolicyContext context)
        => RequireScopePolicy(context).GetWorkspaceFacts(context).ManagedArtifactRoot;
    private static string NormalizeToolArgument(string? value) {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim().Trim('`', '"', '\'');
    }

    private static bool ProcessStepAllows(ToolInvocationPolicyContext context, string operation) {
        return !string.IsNullOrWhiteSpace(operation) &&
               (context.ProcessStepAllowedOperations?.Any(candidate =>
                   string.Equals(candidate, operation, StringComparison.OrdinalIgnoreCase)) ?? false);
    }

    private static bool IsExternalProductArchiveSourceAlias(string normalizedAlias) {
        return normalizedAlias
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Skip(2)
            .Any(segment => ExternalProductArchiveSourceSegments.Contains(segment));
    }

    private static bool IsBroadManagedWorkspacePath(string normalizedPath) {
        return string.IsNullOrWhiteSpace(normalizedPath) ||
               string.Equals(normalizedPath, ".", StringComparison.Ordinal) ||
               string.Equals(normalizedPath, "./", StringComparison.Ordinal) ||
               string.Equals(normalizedPath, "*", StringComparison.Ordinal);
    }

    private static bool IsBroadManagedEvidenceDiscoveryPath(string normalizedPath) {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0) {
            return false;
        }

        if (ManagedEvidenceRoots.Any(root => string.Equals(segments[0], root, StringComparison.OrdinalIgnoreCase))) {
            return segments.Length == 1 ||
                   (segments.Length is >= 2 and <= 4 &&
                    string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase));
        }

        return string.Equals(segments[0], "process-runs", StringComparison.OrdinalIgnoreCase) &&
               segments.Length == 1;
    }

    private static bool IsBroadManagedProcessRunDiscoveryPath(string normalizedPath) {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 2 &&
            string.Equals(segments[0], "artifacts", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(segments[1], "process-runs", StringComparison.OrdinalIgnoreCase)) {
            return true;
        }

        return segments.Length == 5 &&
               string.Equals(segments[0], "artifacts", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase) &&
               string.Equals(segments[4], "process-runs", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsShallowSharedManagedEvidencePath(string normalizedPath) {
        var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 2 &&
            IsManagedEvidenceRoot(segments[0]) &&
            HasFileExtension(segments[1])) {
            return true;
        }

        return segments.Length is 4 or 5 &&
               IsManagedEvidenceRoot(segments[0]) &&
               string.Equals(segments[1], "scopes", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsManagedEvidenceRoot(string segment) {
        return ManagedEvidenceRoots.Any(root => string.Equals(segment, root, StringComparison.OrdinalIgnoreCase));
    }

    private static bool HasFileExtension(string segment) {
        var dotIndex = segment.LastIndexOf('.');
        return dotIndex > 0 && dotIndex < segment.Length - 1;
    }

    private static bool IsNativeAbsolutePath(string path) {
        return WindowsNativeAbsolutePathRegex.IsMatch(path) ||
               path.StartsWith(@"\\", StringComparison.Ordinal) ||
               path.StartsWith("//", StringComparison.Ordinal);
    }

    private static bool IsExplicitManagedWorkspaceFileRead(
        ToolInvocationPolicyContext context,
        string path) {
        if (!string.Equals(context.ToolName, ToolContractCatalog.WorkspaceReadFile, StringComparison.OrdinalIgnoreCase) ||
            context.Classification != ToolInvocationClassification.Read) {
            return false;
        }

        var normalizedPath = NormalizeManagedWorkspacePath(path);
        var payload = ExtractAfterMarker(normalizedPath, "/workspace/");
        if (string.IsNullOrWhiteSpace(payload)) {
            return false;
        }

        payload = NormalizeManagedWorkspacePath(payload);
        if (IsBroadManagedWorkspacePath(payload)) {
            return false;
        }

        var segments = payload.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0 ||
            !HasFileExtension(segments[^1])) {
            return false;
        }

        return IsAllowedExternalRunManagedPath(payload) ||
               (segments.Length == 1 && IsManagedEvidenceFileName(segments[0]));
    }

    private static bool IsManagedEvidenceFileName(string fileName) {
        var extension = Path.GetExtension(fileName);
        return extension.Equals(".md", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".json", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".txt", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".yaml", StringComparison.OrdinalIgnoreCase) ||
               extension.Equals(".log", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadOnlyProjectMediaImageTool(ToolInvocationPolicyContext context) {
        if (context.Classification != ToolInvocationClassification.Read ||
            !HasScopedProjectMediaReadGrant(context)) {
            return false;
        }

        return string.Equals(context.ToolName, ToolContractCatalog.WorkspaceReadFile, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceStatPath, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceInspectImage, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceAnalyzeImage, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceAnalyzeImages, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsReadOnlyProjectMediaFileTool(ToolInvocationPolicyContext context) {
        if (context.Classification != ToolInvocationClassification.Read ||
            !HasScopedProjectMediaReadGrant(context)) {
            return false;
        }

        return string.Equals(context.ToolName, ToolContractCatalog.WorkspaceReadFile, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(context.ToolName, ToolContractCatalog.WorkspaceStatPath, StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasScopedProjectMediaReadGrant(ToolInvocationPolicyContext context) {
        return string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) &&
               ProcessOperationContractNames.IsTargetScopeName(context.ProcessStepTargetScope) &&
               (ProcessStepAllows(context, OperationReadProjectStructure) ||
                ProcessStepAllows(context, OperationCaptureRuntimeProof));
    }

    private static bool IsManagedProjectMediaImagePath(string normalizedPath) {
        if (!normalizedPath.StartsWith(ManagedProjectMediaPath.ImagesRoot + "/", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        var fileName = Path.GetFileName(normalizedPath);
        if (string.IsNullOrWhiteSpace(fileName)) {
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

    private static bool IsManagedProjectMediaFilePath(string normalizedPath) {
        if (!normalizedPath.StartsWith(ManagedProjectMediaPath.FilesRoot + "/", StringComparison.OrdinalIgnoreCase)) {
            return false;
        }

        var fileName = Path.GetFileName(normalizedPath);
        return !string.IsNullOrWhiteSpace(fileName) && HasFileExtension(fileName);
    }

    private static string ExtractAfterMarker(string value, string marker) {
        var index = value.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        return index < 0 ? string.Empty : value[(index + marker.Length)..];
    }

    private static bool IsCurrentRunManagedArtifactPath(string normalizedPath, ToolInvocationPolicyContext context) {
        var currentRunRoot = BuildCurrentRunManagedArtifactRoot(context);
        return !string.IsNullOrWhiteSpace(normalizedPath) &&
               (string.Equals(normalizedPath, currentRunRoot, StringComparison.OrdinalIgnoreCase) ||
                normalizedPath.StartsWith(currentRunRoot + "/", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsGovernedProcessRun(ToolInvocationPolicyContext context) {
        return string.Equals(context.SourceKind, "process-step", StringComparison.OrdinalIgnoreCase) ||
               !string.IsNullOrWhiteSpace(context.ProcessRunId) ||
               !string.IsNullOrWhiteSpace(context.ProcessStepId);
    }

    private static IToolInvocationScopePolicy RequireScopePolicy(ToolInvocationPolicyContext context)
        => context.ScopePolicy ?? throw new InvalidOperationException("This governed run has no owner invocation-scope policy.");

    private static bool IsCopySourceArgumentName(string argumentName) {
        return !string.IsNullOrWhiteSpace(argumentName) &&
               (argumentName.Contains("source", StringComparison.OrdinalIgnoreCase) ||
                argumentName.StartsWith("from", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IsCopyDestinationArgumentName(string argumentName) {
        return !string.IsNullOrWhiteSpace(argumentName) &&
               (argumentName.Contains("destination", StringComparison.OrdinalIgnoreCase) ||
                argumentName.Contains("target", StringComparison.OrdinalIgnoreCase) ||
                argumentName.StartsWith("to", StringComparison.OrdinalIgnoreCase));
    }

}
