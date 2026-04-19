using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Processes;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.ScenarioSeeder;

internal sealed partial class UnitsConverterDeliveryProvisioningSeeder
{
    public async Task<UnitsConverterHumanStepResult> CompleteHumanStepAsync(
        Guid runId,
        int stepSequence,
        CancellationToken cancellationToken = default)
    {
        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var run = await dbContext.Set<ProcessRun>()
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken)
            ?? throw new InvalidOperationException($"Process run '{runId:D}' was not found.");
        var stepRun = await dbContext.Set<ProcessStepRun>()
            .SingleOrDefaultAsync(
                item => item.ProcessRunId == runId &&
                    item.Sequence == stepSequence,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Step sequence '{stepSequence}' was not found for process run '{runId:D}'.");

        if (!string.Equals(stepRun.CurrentExecutorName, HumanPartyDisplayName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Step '{stepRun.Title}' is assigned to '{stepRun.CurrentExecutorName}', not to the human delivery steward.");
        }

        var expectation = await dbContext.Set<ProcessArtifactExpectation>()
            .SingleOrDefaultAsync(
                item => item.StepDefinitionId == stepRun.StepDefinitionId &&
                    item.IsRequired,
                cancellationToken)
            ?? throw new InvalidOperationException(
                $"Step '{stepRun.Title}' is missing its required artifact expectation.");
        var existingArtifact = (await dbContext.Set<ProcessArtifactRecord>()
                .Where(item =>
                    item.ProcessRunId == runId &&
                    item.StepRunId == stepRun.Id &&
                    item.ArtifactExpectationId == expectation.Id)
                .ToListAsync(cancellationToken))
            .OrderByDescending(item => item.CreatedAtUtc)
            .FirstOrDefault();
        var artifactPlan = BuildHumanArtifactPlan(stepRun.Sequence, stepRun.Title);
        var absoluteArtifactPath = Path.Combine(
            options.WorkspaceRootPath,
            artifactPlan.RelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(absoluteArtifactPath)!);
        await File.WriteAllTextAsync(absoluteArtifactPath, artifactPlan.Content, cancellationToken);

        if (existingArtifact is null)
        {
            var recordResult = await processesService.RecordArtifactAsync(
                new ProcessArtifactRecordRequest
                {
                    ProcessRunId = runId,
                    StepRunId = stepRun.Id,
                    ArtifactExpectationId = expectation.Id,
                    ArtifactKind = expectation.ArtifactKind,
                    Title = expectation.Title,
                    TrustStatus = artifactPlan.TrustStatus,
                    SensitivityLevel = expectation.SensitivityLevel,
                    ProvenanceSummary = artifactPlan.ProvenanceSummary,
                    AllowedFutureUsageSummary = artifactPlan.AllowedFutureUsageSummary,
                    ReviewSummary = artifactPlan.ReviewSummary,
                    ManagedStoragePath = artifactPlan.RelativePath,
                    ExternalReferenceKey = $"human-step:{runId:D}:{stepRun.Sequence}:{expectation.Id:D}"
                },
                cancellationToken);
            if (recordResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Recording the required artifact for '{stepRun.Title}' failed: {string.Join("; ", recordResult.Errors.Select(item => item.Message))}");
            }
        }

        if (stepRun.Status == ProcessStepRunStatus.Ready)
        {
            var startResult = await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRun.Id,
                    TargetStatus = ProcessStepRunStatus.InProgress,
                    DecidedBy = HumanPartyDisplayName,
                    Reason = $"Human execution started for '{stepRun.Title}'."
                },
                cancellationToken);
            if (startResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Starting human step '{stepRun.Title}' failed: {string.Join("; ", startResult.Errors.Select(item => item.Message))}");
            }
        }
        else if (stepRun.Status != ProcessStepRunStatus.InProgress &&
                 stepRun.Status != ProcessStepRunStatus.Completed)
        {
            throw new InvalidOperationException(
                $"Human step '{stepRun.Title}' is in status '{stepRun.Status}' and cannot be completed yet.");
        }

        if (stepRun.Status != ProcessStepRunStatus.Completed)
        {
            var completionResult = await processesService.TransitionStepAsync(
                new ProcessStepTransitionRequest
                {
                    StepRunId = stepRun.Id,
                    TargetStatus = ProcessStepRunStatus.Completed,
                    DecidedBy = HumanPartyDisplayName,
                    Reason = artifactPlan.CompletionReason
                },
                cancellationToken);
            if (completionResult.IsFailure)
            {
                throw new InvalidOperationException(
                    $"Completing human step '{stepRun.Title}' failed: {string.Join("; ", completionResult.Errors.Select(item => item.Message))}");
            }
        }

        await dbContext.Entry(stepRun).ReloadAsync(cancellationToken);
        await dbContext.Entry(run).ReloadAsync(cancellationToken);

        return new UnitsConverterHumanStepResult(
            run.Id,
            stepRun.Id,
            stepRun.Sequence,
            stepRun.Title,
            stepRun.Status,
            run.Status,
            artifactPlan.RelativePath);
    }

    private static UnitsConverterHumanArtifactPlan BuildHumanArtifactPlan(
        int sequence,
        string stepTitle)
    {
        return sequence switch
        {
            0 => new UnitsConverterHumanArtifactPlan(
                "artifacts/deliveries/units-converter/process/feature-intake/scope-boundary-packet.md",
                ProcessArtifactTrustStatus.ReviewRequired,
                "Human Delivery Steward authored the scope boundary packet from the project brief and active process launch context.",
                "Reuse as the approved scope boundary for implementation, review, QA, and release gating inside this governed delivery.",
                "Human scope review completed with explicit inclusions, exclusions, and release gates.",
                "Human scope packet accepted. The release stays gated by QA, UI review, security review, and explicit human approval.",
                $$"""
                # Scope Boundary Packet

                - Project: {{ProjectName}}
                - Step: {{stepTitle}}
                - Owner: {{HumanPartyDisplayName}}
                - In scope:
                  - Blazor SSR application for basic unit conversion
                  - Conversion categories: length, mass, temperature, and volume
                  - Typed conversion domain in Core and SSR UI in Web
                  - Automated tests for conversion logic and key validation behavior
                  - QA, UI review, security review, and release evidence recorded through Processes and project structure
                - Explicit exclusions:
                  - User accounts, personalization, cloud deployment, and external APIs
                  - A second AI-agent registry outside AgentFramework
                  - Hidden fallbacks that bypass build, test, QA, or release gates
                - Release boundary:
                  - The app must build, pass tests, pass Playwright-backed QA, survive code review and security review, and keep durable artifact handoff visible in project structure.
                - Human governance:
                  - Product scope, release approval, and post-release learning remain human-owned.
                """),
            7 => new UnitsConverterHumanArtifactPlan(
                "artifacts/deliveries/units-converter/process/release-approval/release-approval-record.md",
                ProcessArtifactTrustStatus.Approved,
                "Human Delivery Steward recorded the governed release decision after reviewing delivery evidence.",
                "Reuse as the human-approved release decision and rollback accountability record for this run.",
                "Human release decision recorded after reviewing QA, UI, security, and rollout readiness evidence.",
                "Release readiness approved by the human steward after reviewing the governed delivery evidence set.",
                $$"""
                # Release Approval Record

                - Project: {{ProjectName}}
                - Step: {{stepTitle}}
                - Approver: {{HumanPartyDisplayName}}
                - Decision: Approved
                - Approval basis:
                  - Architecture, implementation, review, QA, UI review, and security evidence were reviewed in the active run context.
                  - The release remains accountable to explicit rollout and post-release learning steps.
                - Residual risk:
                  - Limited to the scoped basic conversion surface and the recorded follow-up observations from this run.
                - Rollback owner:
                  - {{HumanPartyDisplayName}}
                """),
            9 => new UnitsConverterHumanArtifactPlan(
                "artifacts/deliveries/units-converter/process/post-release-learning/post-release-learning-review.md",
                ProcessArtifactTrustStatus.ReviewRequired,
                "Human Delivery Steward recorded the post-release learning review from the real multi-agent execution.",
                "Reuse as a corrective-learning input for future process, architecture, and runtime improvements.",
                "Human post-release learning review captured from the end-to-end governed delivery run.",
                "Post-release learning captured with concrete architecture and process follow-up items.",
                $$"""
                # Post-Release Learning Review

                - Project: {{ProjectName}}
                - Step: {{stepTitle}}
                - Facilitator: {{HumanPartyDisplayName}}
                - Summary:
                  - The governed run exposed real gaps in runtime tooling, process ergonomics, and candidate-ranking clarity.
                - Observed weak spots:
                  - Published-slot atomic update can fail because of a file-lock defect even when source build succeeds.
                  - Human step control through the process canvas is cumbersome for automation and difficult to drive reliably.
                  - The launch candidate matrix still surfaces weak cross-role recommendations and new-agent proposals even when strong AgentFramework matches already exist.
                - Corrective direction:
                  - Harden the runtime updater, simplify human-step execution paths, and improve role-to-agent recommendation precision.
                """),
            _ => throw new InvalidOperationException(
                $"Units-converter human-step automation does not support sequence '{sequence}'.")
        };
    }
}

internal sealed record UnitsConverterHumanStepResult(
    Guid RunId,
    Guid StepRunId,
    int StepSequence,
    string StepTitle,
    ProcessStepRunStatus StepStatus,
    ProcessRunStatus RunStatus,
    string ArtifactRelativePath);

internal sealed record UnitsConverterHumanArtifactPlan(
    string RelativePath,
    ProcessArtifactTrustStatus TrustStatus,
    string ProvenanceSummary,
    string AllowedFutureUsageSummary,
    string ReviewSummary,
    string CompletionReason,
    string Content);
