using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using System.Text;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    private static bool TryResolveDeclaredStepOutcome(
        DispatchCandidate candidate,
        string? responseText,
        out DeclaredStepOutcome declaredOutcome)
    {
        declaredOutcome = default;
        if (!TryResolveDeclaredStepOutcome(responseText, out var parsedOutcome))
        {
            return false;
        }

        declaredOutcome = parsedOutcome with
        {
            SelectedBranchOutcomeId = ResolveSelectedBranchOutcomeId(
                candidate,
                parsedOutcome.Status,
                parsedOutcome.BranchOutcomeKey,
                parsedOutcome.BranchOutcomeTitle)
        };
        return true;
    }

    private static Guid? ResolveSelectedBranchOutcomeId(
        DispatchCandidate candidate,
        ProcessStepRunStatus completionStatus,
        string? responseText)
    {
        if (completionStatus != ProcessStepRunStatus.Completed ||
            !TryResolveDeclaredStepOutcome(responseText, out var declaredOutcome))
        {
            return null;
        }

        var selectedBranchOutcomeId = ResolveSelectedBranchOutcomeId(
            candidate,
            completionStatus,
            declaredOutcome.BranchOutcomeKey,
            declaredOutcome.BranchOutcomeTitle);
        if (selectedBranchOutcomeId.HasValue)
        {
            return selectedBranchOutcomeId;
        }

        if (declaredOutcome.Status == ProcessStepRunStatus.Blocked &&
            TryResolveRepairBranchOutcome(candidate, out var repairBranchOutcome) &&
            IsRepairableBlockedBranchDispositionReason(declaredOutcome.Reason, responseText))
        {
            return repairBranchOutcome.Id;
        }

        if (string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeKey) &&
            string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeTitle) &&
            TryResolveExplicitDispositionBranchOutcome(candidate, responseText, out var explicitDisposition))
        {
            return explicitDisposition.Id;
        }

        return null;
    }

    private static Guid? ResolveSelectedBranchOutcomeId(
        DispatchCandidate candidate,
        ProcessStepRunStatus completionStatus,
        string? branchOutcomeKey,
        string? branchOutcomeTitle)
    {
        if (completionStatus != ProcessStepRunStatus.Completed || candidate.BranchOutcomes.Count == 0)
        {
            return null;
        }

        var normalizedBranchOutcomeKey = NormalizeBranchOutcomeToken(branchOutcomeKey);
        if (!string.IsNullOrWhiteSpace(normalizedBranchOutcomeKey))
        {
            var matchByKey = candidate.BranchOutcomes.FirstOrDefault(
                item => NormalizeBranchOutcomeToken(item.Key).Equals(normalizedBranchOutcomeKey, StringComparison.Ordinal));
            if (matchByKey is not null)
            {
                return IsInvalidExplicitSystemBranchSelection(candidate, matchByKey)
                    ? null
                    : matchByKey.Id;
            }
        }

        var normalizedBranchOutcomeTitle = NormalizeBranchOutcomeToken(branchOutcomeTitle);
        if (string.IsNullOrWhiteSpace(normalizedBranchOutcomeTitle))
        {
            return ResolveImplicitCompletedDefaultBranchOutcomeId(candidate);
        }

        var matchByTitle = candidate.BranchOutcomes.FirstOrDefault(
            item => NormalizeBranchOutcomeToken(item.Title).Equals(normalizedBranchOutcomeTitle, StringComparison.Ordinal));
        if (matchByTitle is null)
        {
            return null;
        }

        return IsInvalidExplicitSystemBranchSelection(candidate, matchByTitle)
            ? null
            : matchByTitle.Id;
    }

    private static bool TryResolveExplicitDispositionBranchOutcome(
        DispatchCandidate candidate,
        string? responseText,
        out DispatchBranchOutcome branchOutcome)
    {
        branchOutcome = null!;
        if (candidate.BranchOutcomes.Count == 0 || string.IsNullOrWhiteSpace(responseText))
        {
            return false;
        }

        if (TryResolveDeclaredStepOutcome(responseText, out var declaredOutcome) &&
            (!string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeKey) ||
             !string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeTitle)))
        {
            var declaredBranchOutcomeId = ResolveSelectedBranchOutcomeId(
                candidate,
                ProcessStepRunStatus.Completed,
                declaredOutcome.BranchOutcomeKey,
                declaredOutcome.BranchOutcomeTitle);
            if (declaredBranchOutcomeId.HasValue)
            {
                branchOutcome = candidate.BranchOutcomes.Single(item => item.Id == declaredBranchOutcomeId.Value);
                return true;
            }
        }

        var normalizedText = NormalizeBranchDispositionText($"{responseText} {ResolveOutputInspectionText(responseText)}");
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        var matches = candidate.BranchOutcomes
            .Where(outcome => !IsInvalidExplicitSystemBranchSelection(candidate, outcome))
            .Where(outcome => ContainsExplicitBranchDispositionSignal(normalizedText, outcome))
            .Take(2)
            .ToList();
        if (matches.Count != 1)
        {
            return false;
        }

        branchOutcome = matches[0];
        return true;
    }

    private static bool TryRecoverExplicitDispositionBranchSelection(
        DispatchCandidate candidate,
        DeclaredStepOutcome declaredOutcome,
        AgentOutputValidationResult contextValidation,
        string? responseText,
        out DispatchBranchOutcome branchOutcome)
    {
        branchOutcome = null!;
        return declaredOutcome.Status == ProcessStepRunStatus.Completed &&
               contextValidation.Errors.Count > 0 &&
               contextValidation.Errors.All(error =>
                   error.Code is "process.step_outcome.context.branch_required" or
                       "process.step_outcome.context.branch_invalid") &&
               TryResolveExplicitDispositionBranchOutcome(candidate, responseText, out branchOutcome);
    }

    private static bool ContainsExplicitBranchDispositionSignal(string normalizedText, DispatchBranchOutcome outcome)
    {
        var tokens = new[]
            {
                NormalizeBranchOutcomeToken(outcome.Key),
                NormalizeBranchOutcomeToken(outcome.Title)
            }
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (tokens.Length == 0)
        {
            return false;
        }

        var prefixes = new[]
        {
            "branchoutcomekey",
            "branchoutcometitle",
            "branchoutcome",
            "selectedbranch",
            "branch",
            "disposition",
            "acceptancedecisionstatus",
            "acceptancedecision",
            "acceptancestatus",
            "status",
            "decision",
            "outcome"
        };

        return tokens.Any(token => prefixes.Any(prefix => normalizedText.Contains(prefix + token, StringComparison.Ordinal)));
    }

    private static Guid? ResolveImplicitCompletedDefaultBranchOutcomeId(DispatchCandidate candidate)
    {
        if (!candidate.RequiresExplicitBranchOutcomeSelection)
        {
            return null;
        }

        var nonErrorOutcomes = candidate.BranchOutcomes
            .Where(outcome => !IsErrorBranchOutcome(outcome))
            .ToList();
        if (nonErrorOutcomes.Count != 1)
        {
            return null;
        }

        var defaultOutcome = nonErrorOutcomes[0];
        return IsDefaultBranchOutcome(defaultOutcome)
            ? defaultOutcome.Id
            : null;
    }

    private static bool IsDefaultBranchOutcome(DispatchBranchOutcome outcome)
    {
        return string.Equals(NormalizeBranchOutcomeToken(outcome.Key), "default", StringComparison.Ordinal) ||
               string.Equals(NormalizeBranchOutcomeToken(outcome.Title), "default", StringComparison.Ordinal);
    }

    private static bool IsErrorBranchOutcome(DispatchBranchOutcome outcome)
    {
        return string.Equals(NormalizeBranchOutcomeToken(outcome.Key), "error", StringComparison.Ordinal) ||
               string.Equals(NormalizeBranchOutcomeToken(outcome.Title), "error", StringComparison.Ordinal);
    }

    private static bool IsInvalidExplicitSystemBranchSelection(
        DispatchCandidate candidate,
        DispatchBranchOutcome outcome)
    {
        return candidate.RequiresExplicitBranchOutcomeSelection &&
               (IsDefaultBranchOutcome(outcome) || IsErrorBranchOutcome(outcome)) &&
               candidate.BranchOutcomes.Any(item => !IsDefaultBranchOutcome(item) && !IsErrorBranchOutcome(item));
    }

    private static string? ResolveBranchOutcomeSelectionFailure(
        DispatchCandidate candidate,
        DeclaredStepOutcome declaredOutcome)
    {
        if (declaredOutcome.Status != ProcessStepRunStatus.Completed || !candidate.RequiresExplicitBranchOutcomeSelection)
        {
            return null;
        }

        if (declaredOutcome.SelectedBranchOutcomeId.HasValue)
        {
            return null;
        }

        var availableOutcomes = string.Join(
            ", ",
            candidate.BranchOutcomes.Select(item => string.IsNullOrWhiteSpace(item.Key) ? item.Title : $"{item.Key} ({item.Title})"));
        if (string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeKey) &&
            string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeTitle))
        {
            return $"Step '{candidate.StepRun.Title}' completed without selecting a required branch outcome. Available branch outcomes: {availableOutcomes}.";
        }

        var declaredOutcomeLabel = string.IsNullOrWhiteSpace(declaredOutcome.BranchOutcomeKey)
            ? declaredOutcome.BranchOutcomeTitle
            : declaredOutcome.BranchOutcomeKey;
        return $"Step '{candidate.StepRun.Title}' declared branch outcome '{declaredOutcomeLabel}', but it is not valid for this step. Available branch outcomes: {availableOutcomes}.";
    }

    private static string BuildBranchOutcomePromptSummary(IReadOnlyList<DispatchBranchOutcome> branchOutcomes)
    {
        if (branchOutcomes.Count == 0)
        {
            return "None";
        }

        var builder = new StringBuilder();
        foreach (var outcome in branchOutcomes.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase))
        {
            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.Append("- ");
            builder.Append(string.IsNullOrWhiteSpace(outcome.Key) ? outcome.Title : $"{outcome.Key} ({outcome.Title})");
            if (!string.IsNullOrWhiteSpace(outcome.Description))
            {
                builder.Append(": ");
                builder.Append(outcome.Description.Trim());
            }
        }

        return builder.ToString();
    }

    private static string NormalizeBranchOutcomeToken(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Trim()
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static bool TryResolveRepairBranchCompletionFromBlockedOutcome(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        DeclaredStepOutcome declaredOutcome,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        CarriedImplementationProof carriedImplementationProof,
        out DispatchBranchOutcome repairBranchOutcome)
    {
        repairBranchOutcome = null!;
        if (declaredOutcome.Status != ProcessStepRunStatus.Blocked ||
            !RequiresGovernedStepOutcome(candidate.StepRun) ||
            !TryResolveRepairBranchOutcome(candidate, out repairBranchOutcome) ||
            HasUnrecoverableMissingRepairDispositionTool(missingRequiredTools) ||
            ResolveUnresolvedCriticalToolFailures(candidate, detail).Count > 0)
        {
            return false;
        }

        var inspectionText = ResolveOutputInspectionText(responseText);
        if (!IsRepairableBlockedBranchDispositionReason(declaredOutcome.Reason, inspectionText))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(ResolveMissingUpstreamArtifactInputSummary(candidate)) ||
            !string.IsNullOrWhiteSpace(ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail)) ||
            !string.IsNullOrWhiteSpace(ResolveOutOfScopeExternalTargetReferenceSummary(detail, inspectionText)) ||
            !string.IsNullOrWhiteSpace(ResolveShallowSharedManagedArtifactReferenceSummary(detail, inspectionText)))
        {
            return false;
        }

        return true;
    }

    private static bool HasUnrecoverableMissingRepairDispositionTool(IReadOnlyList<string> missingRequiredTools)
    {
        return missingRequiredTools.Any(toolName =>
            !ImplementationProofToolNames.Contains(toolName, StringComparer.Ordinal) &&
            !ConcreteProductMutationToolNames.Contains(toolName, StringComparer.Ordinal) &&
            !toolName.StartsWith("project_structure_", StringComparison.Ordinal) &&
            !IsImplementationValidationToolName(toolName) &&
            !IsBrowserLaunchOrProofToolName(toolName));
    }

    private static bool TryResolveTerminalEscalationCompletionFromBlockedOutcome(
        DispatchCandidate candidate,
        ExecutionRunDetail detail,
        DeclaredStepOutcome declaredOutcome,
        string? responseText,
        IReadOnlyList<string> missingRequiredTools,
        out string escalationDispositionTitle)
    {
        escalationDispositionTitle = string.Empty;
        if (declaredOutcome.Status != ProcessStepRunStatus.Blocked ||
            !RequiresGovernedStepOutcome(candidate.StepRun) ||
            candidate.BranchOutcomes.Count > 0 ||
            missingRequiredTools.Count > 0 ||
            ResolveUnresolvedCriticalToolFailures(candidate, detail).Count > 0 ||
            !IsTerminalEscalationStep(candidate))
        {
            return false;
        }

        var inspectionText = ResolveOutputInspectionText(responseText);
        if (!string.IsNullOrWhiteSpace(ResolveMissingUpstreamArtifactInputSummary(candidate)) ||
            !string.IsNullOrWhiteSpace(ResolveMissingRequiredArtifactSummary(candidate, detail, inspectionText)) ||
            !string.IsNullOrWhiteSpace(ResolveMissingUpstreamArtifactInspectionSummary(candidate, detail)) ||
            !string.IsNullOrWhiteSpace(ResolveOutOfScopeExternalTargetReferenceSummary(detail, inspectionText)) ||
            !string.IsNullOrWhiteSpace(ResolveShallowSharedManagedArtifactReferenceSummary(detail, inspectionText)))
        {
            return false;
        }

        if (!IsTerminalEscalationDispositionReason(declaredOutcome.Reason, inspectionText))
        {
            return false;
        }

        escalationDispositionTitle = ResolveTerminalEscalationDispositionTitle(candidate);
        return true;
    }

    private static bool TryResolveRepairBranchOutcome(
        DispatchCandidate candidate,
        out DispatchBranchOutcome repairBranchOutcome)
    {
        repairBranchOutcome = candidate.BranchOutcomes.FirstOrDefault(outcome =>
            IsRepairBranchOutcomeCandidate(outcome, IsPrimaryRepairBranchOutcomeToken))!;
        if (repairBranchOutcome is not null)
        {
            return true;
        }

        repairBranchOutcome = candidate.BranchOutcomes.FirstOrDefault(outcome =>
            IsRepairBranchOutcomeCandidate(outcome, IsSecondaryRepairBranchOutcomeToken))!;
        return repairBranchOutcome is not null;
    }

    private static bool IsRepairBranchOutcomeCandidate(
        DispatchBranchOutcome outcome,
        Func<string, bool> tokenMatcher)
    {
        var keyTitleToken = NormalizeBranchOutcomeToken($"{outcome.Key} {outcome.Title}");
        if (IsAcceptingBranchOutcomeToken(keyTitleToken))
        {
            return false;
        }

        if (tokenMatcher(keyTitleToken))
        {
            return true;
        }

        var fullToken = NormalizeBranchOutcomeToken($"{outcome.Key} {outcome.Title} {outcome.Description}");
        return !IsAcceptingBranchOutcomeToken(fullToken) && tokenMatcher(fullToken);
    }

    private static bool IsAcceptingBranchOutcomeToken(string token)
    {
        return token.Contains("accepted", StringComparison.Ordinal) ||
               token.Contains("approved", StringComparison.Ordinal) ||
               token.Contains("approval", StringComparison.Ordinal) ||
               token.Contains("sufficient", StringComparison.Ordinal) ||
               token.Contains("ready", StringComparison.Ordinal) ||
               token.Contains("passed", StringComparison.Ordinal) ||
               token.Contains("pass", StringComparison.Ordinal) ||
               token.Contains("continue", StringComparison.Ordinal) ||
               token.Contains("releasegovernance", StringComparison.Ordinal) ||
               token.Contains("release-ready", StringComparison.Ordinal) ||
               token.Contains("releaseready", StringComparison.Ordinal);
    }

    private static bool IsPrimaryRepairBranchOutcomeToken(string token)
    {
        return token.Contains("repair", StringComparison.Ordinal) ||
               token.Contains("remediation", StringComparison.Ordinal) ||
               token.Contains("remediate", StringComparison.Ordinal) ||
               token.Contains("rework", StringComparison.Ordinal) ||
               token.Contains("changerequired", StringComparison.Ordinal) ||
               token.Contains("changesrequired", StringComparison.Ordinal) ||
               token.Contains("requiredchanges", StringComparison.Ordinal) ||
               token.Contains("needschanges", StringComparison.Ordinal) ||
               token.Contains("fixrequired", StringComparison.Ordinal) ||
               token.Contains("fixesrequired", StringComparison.Ordinal);
    }

    private static bool IsSecondaryRepairBranchOutcomeToken(string token)
    {
        return token.Contains("qualityrejected", StringComparison.Ordinal) ||
               token.Contains("validationrejected", StringComparison.Ordinal) ||
               token.Contains("rejectedvalidation", StringComparison.Ordinal) ||
               token.Contains("defectsfound", StringComparison.Ordinal) ||
               token.Contains("failedvalidation", StringComparison.Ordinal);
    }

    private static bool IsRepairableBlockedBranchDispositionReason(string reason, string? inspectionText)
    {
        var normalizedText = NormalizeBranchDispositionText($"{reason} {inspectionText}");
        if (string.IsNullOrWhiteSpace(normalizedText) ||
            ContainsAnyBranchDispositionToken(
                normalizedText,
                "requiredinput",
                "missingupstream",
                "upstreamartifactmissing",
                "requiredartifactmissing",
                "toolunavailable",
                "toolpolicydenied",
                "deniedbypolicy",
                "permission",
                "credential",
                "secret",
                "authority",
                "browsercannotbereached",
                "cannotreachbrowser",
                "cannotlaunch",
                "cannotbelaunched",
                "appcannotbelaunched",
                "nowritabletarget",
                "safeexecutionboundary",
                "environmentunavailable",
                "projectstructureread",
                "workspacewritefilefailed"))
        {
            return false;
        }

        return ContainsAnyBranchDispositionToken(
            normalizedText,
            "defect",
            "bug",
            "error",
            "console",
            "runtime",
            "validation",
            "insufficient",
            "missingproof",
            "missingevidence",
            "proofrisk",
            "proofgap",
            "missingimplemented",
            "missingbehavior",
            "requiredflowmissing",
            "requiredworkflowmissing",
            "placeholder",
            "stockscaffold",
            "repair",
            "remediation",
            "rework",
            "changesrequired",
            "unresolved",
            "notready",
            "failedassertion",
            "failedbrowser",
            "uiflow");
    }

    private static bool IsTerminalEscalationStep(DispatchCandidate candidate)
    {
        var normalizedText = NormalizeBranchDispositionText(string.Join(
            " ",
            candidate.StepRun.Title,
            candidate.WorkBrief?.WorkBriefText,
            candidate.WorkBrief?.ExpectedOutcome,
            candidate.WorkBrief?.EvidenceExpectationSummary,
            string.Join(" ", candidate.ExpectedArtifacts.Select(item => item.Title))));
        if (string.IsNullOrWhiteSpace(normalizedText))
        {
            return false;
        }

        return ContainsAnyBranchDispositionToken(
            normalizedText,
            "escalate",
            "escalation",
            "nogo",
            "scopereset",
            "replan",
            "unresolvedrepair",
            "repairfindings",
            "repairescalationrecord",
            "postrepairqaescalation");
    }

    private static string ResolveTerminalEscalationDispositionTitle(DispatchCandidate candidate)
    {
        var expectedArtifactTitle = candidate.ExpectedArtifacts
            .Select(item => item.Title.Trim())
            .FirstOrDefault(title => !string.IsNullOrWhiteSpace(title) &&
                                     NormalizeBranchDispositionText(title).Contains("escalation", StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(expectedArtifactTitle))
        {
            return expectedArtifactTitle;
        }

        return string.IsNullOrWhiteSpace(candidate.StepRun.Title)
            ? "Escalation"
            : candidate.StepRun.Title.Trim();
    }

    private static bool IsTerminalEscalationDispositionReason(string reason, string? inspectionText)
    {
        var normalizedText = NormalizeBranchDispositionText($"{reason} {inspectionText}");
        if (string.IsNullOrWhiteSpace(normalizedText) ||
            ContainsAnyBranchDispositionToken(
                normalizedText,
                "requiredinput",
                "missingupstream",
                "upstreamartifactmissing",
                "requiredartifactmissing",
                "toolunavailable",
                "toolpolicydenied",
                "deniedbypolicy",
                "permission",
                "credential",
                "secret",
                "authority",
                "cannotmake",
                "cannotproduce",
                "cannotwrite",
                "cannotread",
                "cannotinspect",
                "safeexecutionboundary"))
        {
            return false;
        }

        return ContainsAnyBranchDispositionToken(
            normalizedText,
            "unresolved",
            "notready",
            "nogo",
            "releaseblocking",
            "runtime",
            "console",
            "defect",
            "error",
            "repair",
            "escalation",
            "blocked",
            "replan",
            "scopereset");
    }

    private static string NormalizeBranchDispositionText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value
            .Where(char.IsLetterOrDigit)
            .Select(char.ToLowerInvariant)
            .ToArray());
    }

    private static bool ContainsAnyBranchDispositionToken(string text, params string[] tokens)
    {
        return tokens.Any(token => text.Contains(token, StringComparison.Ordinal));
    }

    private static bool IsRecoverableGovernedOutcomeGap(
        DispatchCandidate candidate,
        string? responseText)
    {
        if (!RequiresGovernedStepOutcome(candidate.StepRun))
        {
            return false;
        }

        if (!TryResolveDeclaredStepOutcome(candidate, responseText, out var declaredOutcome))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(ResolveBranchOutcomeSelectionFailure(candidate, declaredOutcome));
    }
}
