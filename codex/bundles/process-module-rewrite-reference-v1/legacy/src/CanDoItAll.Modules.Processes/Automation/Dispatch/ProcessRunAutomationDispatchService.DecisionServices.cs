using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.Processes;

internal sealed partial class ProcessRunAutomationDispatchService
{
    internal interface IRequiredToolResolver
    {
        RequiredToolResolution Resolve(DispatchCandidate candidate, string? additionalGroundingText = null);
    }

    internal interface IBrowserProofRequirementResolver
    {
        BrowserProofRequirement Resolve(DispatchCandidate candidate, string? additionalGroundingText = null);
    }

    internal interface IArtifactRequirementMatcher
    {
        ArtifactRequirementMatch ResolveMissingRequiredArtifact(DispatchCandidate candidate, ProcessAutomationExecutionRunDetail detail, string? inspectionText);
    }

    internal interface IStepCompletionPolicy
    {
        StepCompletionPolicyDecision Resolve(StepCompletionPolicyInput input);
    }

    internal interface IDispatchDecisionEngine
    {
        DispatchDecision Evaluate(DispatchDecisionInput input);
    }

    internal sealed record DispatchDecisionDiagnostic(
        string Code,
        string Message,
        DispatchDecisionDiagnosticSeverity Severity);

    internal enum DispatchDecisionDiagnosticSeverity
    {
        Info = 0,
        Warning = 1,
        Error = 2
    }

    internal sealed record RequiredToolResolution(
        IReadOnlyList<string> ToolNames,
        IReadOnlyList<DispatchDecisionDiagnostic> Diagnostics);

    internal sealed record BrowserProofRequirement(
        bool IsRequired,
        string Reason,
        IReadOnlyList<DispatchDecisionDiagnostic> Diagnostics);

    internal sealed record ArtifactRequirementMatch(
        string Summary,
        IReadOnlyList<DispatchDecisionDiagnostic> Diagnostics);

    internal sealed record StepCompletionPolicyInput(
        DispatchCandidate Candidate,
        ProcessAutomationExecutionRunDetail Detail,
        IReadOnlyList<string> SuccessfulToolNamesFromPriorAttempts,
        string? ResponseText,
        CarriedImplementationProof CarriedImplementationProof);

    internal sealed record StepCompletionPolicyDecision(
        ProcessStepRunStatus Status,
        IReadOnlyList<DispatchDecisionDiagnostic> Diagnostics);

    internal sealed record DispatchDecisionInput(
        DispatchCandidate Candidate,
        ProcessAutomationExecutionRunDetail Detail,
        IReadOnlyList<string> SuccessfulToolNamesFromPriorAttempts,
        string? ResponseText,
        CarriedImplementationProof CarriedImplementationProof,
        string? ArtifactInspectionText);

    internal sealed record DispatchDecision(
        ProcessStepRunStatus CompletionStatus,
        RequiredToolResolution RequiredTools,
        BrowserProofRequirement BrowserProofRequirement,
        ArtifactRequirementMatch ArtifactRequirementMatch,
        IReadOnlyList<DispatchDecisionDiagnostic> Diagnostics);

    private static readonly IRequiredToolResolver RequiredToolResolver =
        new DelegatingRequiredToolResolver(ResolveRequiredToolNamesCore);

    private static readonly IBrowserProofRequirementResolver BrowserProofRequirementResolver =
        new DelegatingBrowserProofRequirementResolver(RequiresConcreteBrowserProof);

    private static readonly IArtifactRequirementMatcher ArtifactRequirementMatcher =
        new DelegatingArtifactRequirementMatcher(ResolveMissingRequiredArtifactSummary);

    private static readonly IStepCompletionPolicy StepCompletionPolicy =
        new DelegatingStepCompletionPolicy(ResolveCompletionStatusWithCarryForward);

    private static readonly IDispatchDecisionEngine DispatchDecisionEngine =
        new DefaultDispatchDecisionEngine(
            RequiredToolResolver,
            BrowserProofRequirementResolver,
            ArtifactRequirementMatcher,
            StepCompletionPolicy);

    private sealed class DelegatingRequiredToolResolver(
        Func<DispatchCandidate, string?, IReadOnlyList<string>> resolve) : IRequiredToolResolver
    {
        public RequiredToolResolution Resolve(DispatchCandidate candidate, string? additionalGroundingText = null)
        {
            var toolNames = resolve(candidate, additionalGroundingText)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(item => item, StringComparer.Ordinal)
                .ToList();

            return new RequiredToolResolution(
                toolNames,
                [new DispatchDecisionDiagnostic(
                    "process.dispatch.required_tools.resolved",
                    $"Resolved {toolNames.Count} required tool(s).",
                    DispatchDecisionDiagnosticSeverity.Info)]);
        }
    }

    private sealed class DelegatingBrowserProofRequirementResolver(
        Func<DispatchCandidate, string?, bool> resolve) : IBrowserProofRequirementResolver
    {
        public BrowserProofRequirement Resolve(DispatchCandidate candidate, string? additionalGroundingText = null)
        {
            var isRequired = resolve(candidate, additionalGroundingText);
            return new BrowserProofRequirement(
                isRequired,
                isRequired
                    ? "Step contract requires concrete browser proof."
                    : "Step contract does not require concrete browser proof.",
                [new DispatchDecisionDiagnostic(
                    "process.dispatch.browser_proof.resolved",
                    isRequired ? "Concrete browser proof is required." : "Concrete browser proof is not required.",
                    DispatchDecisionDiagnosticSeverity.Info)]);
        }
    }

    private sealed class DelegatingArtifactRequirementMatcher(
        Func<DispatchCandidate, ProcessAutomationExecutionRunDetail, string?, string> resolve) : IArtifactRequirementMatcher
    {
        public ArtifactRequirementMatch ResolveMissingRequiredArtifact(
            DispatchCandidate candidate,
            ProcessAutomationExecutionRunDetail detail,
            string? inspectionText)
        {
            var summary = resolve(candidate, detail, inspectionText);
            var diagnostics = string.IsNullOrWhiteSpace(summary)
                ? new[]
                {
                    new DispatchDecisionDiagnostic(
                        "process.dispatch.artifacts.satisfied",
                        "Required artifact obligations are satisfied.",
                        DispatchDecisionDiagnosticSeverity.Info)
                }
                : new[]
                {
                    new DispatchDecisionDiagnostic(
                        "process.dispatch.artifacts.missing",
                        summary,
                        DispatchDecisionDiagnosticSeverity.Error)
                };

            return new ArtifactRequirementMatch(summary, diagnostics);
        }
    }

    private sealed class DelegatingStepCompletionPolicy(
        Func<DispatchCandidate, ProcessAutomationExecutionRunDetail, IEnumerable<string>, string?, CarriedImplementationProof, ProcessStepRunStatus> resolve)
        : IStepCompletionPolicy
    {
        public StepCompletionPolicyDecision Resolve(StepCompletionPolicyInput input)
        {
            var status = resolve(
                input.Candidate,
                input.Detail,
                input.SuccessfulToolNamesFromPriorAttempts,
                input.ResponseText,
                input.CarriedImplementationProof);

            return new StepCompletionPolicyDecision(
                status,
                [new DispatchDecisionDiagnostic(
                    "process.dispatch.completion_status.resolved",
                    $"Resolved step completion status {status}.",
                    status is ProcessStepRunStatus.Failed or ProcessStepRunStatus.Blocked
                        ? DispatchDecisionDiagnosticSeverity.Warning
                        : DispatchDecisionDiagnosticSeverity.Info)]);
        }
    }

    private sealed class DefaultDispatchDecisionEngine(
        IRequiredToolResolver requiredToolResolver,
        IBrowserProofRequirementResolver browserProofRequirementResolver,
        IArtifactRequirementMatcher artifactRequirementMatcher,
        IStepCompletionPolicy stepCompletionPolicy) : IDispatchDecisionEngine
    {
        public DispatchDecision Evaluate(DispatchDecisionInput input)
        {
            var requiredTools = requiredToolResolver.Resolve(input.Candidate);
            var browserProofRequirement = browserProofRequirementResolver.Resolve(input.Candidate);
            var artifactRequirementMatch = artifactRequirementMatcher.ResolveMissingRequiredArtifact(
                input.Candidate,
                input.Detail,
                input.ArtifactInspectionText);
            var completionDecision = stepCompletionPolicy.Resolve(new StepCompletionPolicyInput(
                input.Candidate,
                input.Detail,
                input.SuccessfulToolNamesFromPriorAttempts,
                input.ResponseText,
                input.CarriedImplementationProof));

            var diagnostics = requiredTools.Diagnostics
                .Concat(browserProofRequirement.Diagnostics)
                .Concat(artifactRequirementMatch.Diagnostics)
                .Concat(completionDecision.Diagnostics)
                .ToList();

            return new DispatchDecision(
                completionDecision.Status,
                requiredTools,
                browserProofRequirement,
                artifactRequirementMatch,
                diagnostics);
        }
    }
}
