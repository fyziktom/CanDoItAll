using System.Text.Json;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService {
    public async Task<Result> RerunAgentStepAsync(
        ProcessAgentStepRerunRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (request.StepRunId == Guid.Empty)
        {
            return Result.Failure(Error.Validation("Select an agent-owned step before rerunning it.", "processes.agent-rerun-step-required"));
        }

        ProcessStepRun stepRun;
        ProcessRun run;
        string recoveryDirective;
        await using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            stepRun = await dbContext.Set<ProcessStepRun>()
                .AsNoTracking()
                .SingleOrDefaultAsync(item => item.Id == request.StepRunId, cancellationToken)
                ?? throw new InvalidOperationException($"Process step run '{request.StepRunId:D}' was not found.");
            if (HasConcurrencyTokenMismatch(request.StepRunConcurrencyToken, stepRun.ConcurrencyToken))
            {
                return Result.Failure(CreateStepTransitionConflictError());
            }

            run = await dbContext.Set<ProcessRun>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == stepRun.ProcessRunId, cancellationToken);
            var validationResult = ValidateAgentRerunEligibility(run, stepRun);
            if (validationResult.IsFailure)
            {
                return validationResult;
            }

            var expectedArtifacts = await dbContext.Set<ProcessArtifactExpectation>()
                .AsNoTracking()
                .Where(item => item.StepDefinitionId == stepRun.StepDefinitionId)
                .OrderBy(item => item.Title)
                .ToListAsync(cancellationToken);
            var stepArtifacts = await dbContext.Set<ProcessArtifactRecord>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == run.Id && item.StepRunId == stepRun.Id)
                .ToListAsync(cancellationToken);
            var latestDecisions = await dbContext.Set<ProcessDecisionRecord>()
                .AsNoTracking()
                .Where(item => item.ProcessRunId == run.Id && item.StepRunId == stepRun.Id)
                .ToListAsync(cancellationToken);
            latestDecisions = latestDecisions
                .OrderByDescending(item => item.CreatedAtUtc)
                .Take(3)
                .ToList();

            recoveryDirective = BuildManualRerunDirective(
                request,
                stepRun,
                expectedArtifacts,
                stepArtifacts,
                latestDecisions);
        }

        var transitionResult = await TransitionStepAsync(
            new ProcessStepTransitionRequest
            {
                StepRunId = request.StepRunId,
                StepRunConcurrencyToken = request.StepRunConcurrencyToken,
                TargetStatus = ProcessStepRunStatus.InProgress,
                Reason = BuildManualRerunTransitionReason(request, recoveryDirective),
                DecidedBy = string.IsNullOrWhiteSpace(request.OperatorReason)
                    ? "process-workspace"
                    : "process-workspace",
                SuppressAutomationDispatch = true
            },
            cancellationToken);
        if (transitionResult.IsFailure)
        {
            return transitionResult;
        }

        await using (var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken))
        {
            var refreshedStepRun = await dbContext.Set<ProcessStepRun>()
                .AsNoTracking()
                .SingleAsync(item => item.Id == request.StepRunId, cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                new ProcessJournalEntry
                {
                    ProcessRunId = run.Id,
                    StepRunId = refreshedStepRun.Id,
                    EventType = ProcessRuntimeEventTypes.ManualAgentStepRerun,
                    Title = "Agent step rerun requested",
                    Description = recoveryDirective,
                    CorrelationId = Guid.NewGuid().ToString("N"),
                    OperatingMode = run.OperatingMode,
                    PolicyVersion = $"definition-version:{run.ProcessDefinitionVersionId:D}",
                    EnvironmentMode = run.OperatingMode.ToString(),
                    ReplayContextJson = JsonSerializer.Serialize(new
                    {
                        RunId = run.Id,
                        StepRunId = refreshedStepRun.Id,
                        Classification = ProcessRecoveryClassification.ManualRerun.ToString(),
                        RecoveryDirective = recoveryDirective
                    }),
                    OccurredAtUtc = clock.GetUtcNow()
                },
                cancellationToken);
            await processOutboxService.EnqueueAutomationDispatchAsync(
                dbContext,
                run.ProjectId,
                run.ProcessDefinitionId,
                run.Id,
                refreshedStepRun.Id,
                ProcessRuntimeEventTypes.ManualAgentStepRerun,
                cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    private static Result ValidateAgentRerunEligibility(ProcessRun run, ProcessStepRun stepRun)
    {
        if (run.Status is ProcessRunStatus.Completed or ProcessRunStatus.Cancelled)
        {
            return Result.Failure(Error.Validation("Completed or cancelled process runs cannot rerun agent steps.", "processes.agent-rerun-terminal-run"));
        }

        if (stepRun.Status is not ProcessStepRunStatus.Blocked and not ProcessStepRunStatus.Failed)
        {
            return Result.Failure(Error.Validation("Only blocked or failed agent-owned steps can be rerun manually.", "processes.agent-rerun-invalid-status"));
        }

        if (!stepRun.CurrentExecutorPartyId.HasValue)
        {
            return Result.Failure(Error.Validation("Only steps with an assigned agent executor can be rerun.", "processes.agent-rerun-missing-agent"));
        }

        return Result.Success();
    }

    private static string BuildManualRerunTransitionReason(
        ProcessAgentStepRerunRequest request,
        string recoveryDirective)
    {
        var operatorReason = string.IsNullOrWhiteSpace(request.OperatorReason)
            ? "Operator requested the agent to do the job again with explicit recovery instructions."
            : request.OperatorReason.Trim();
        return $"{operatorReason} Recovery directive: {recoveryDirective}";
    }

    private static string BuildManualRerunDirective(
        ProcessAgentStepRerunRequest request,
        ProcessStepRun stepRun,
        IReadOnlyList<ProcessArtifactExpectation> expectedArtifacts,
        IReadOnlyList<ProcessArtifactRecord> stepArtifacts,
        IReadOnlyList<ProcessDecisionRecord> latestDecisions)
    {
        var missingArtifacts = expectedArtifacts
            .Where(item => item.IsRequired)
            .Where(item => !stepArtifacts.Any(artifact => SatisfiesManualRerunArtifactExpectation(artifact, item)))
            .Select(item => item.Title)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        var builder = new System.Text.StringBuilder();
        builder.Append("Manual rerun requested for step '")
            .Append(stepRun.Title)
            .Append("'. Start a fresh attempt and preserve all previous execution runs and artifacts.");

        if (!string.IsNullOrWhiteSpace(request.OperatorReason))
        {
            builder.Append(" Operator reason: ")
                .Append(request.OperatorReason.Trim());
        }

        if (!string.IsNullOrWhiteSpace(stepRun.BlockedReason))
        {
            builder.Append(" Prior blocked reason: ")
                .Append(stepRun.BlockedReason.Trim());
        }

        if (!string.IsNullOrWhiteSpace(stepRun.ExceptionSummary))
        {
            builder.Append(" Prior failure: ")
                .Append(stepRun.ExceptionSummary.Trim());
        }

        if (missingArtifacts.Count > 0)
        {
            builder.Append(" Required artifacts still missing: ")
                .Append(string.Join(", ", missingArtifacts))
                .Append('.');
        }

        var decisionSummaries = latestDecisions
            .Select(item => item.Reason)
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Take(2)
            .ToList();
        if (decisionSummaries.Count > 0)
        {
            builder.Append(" Recent process decisions: ")
                .Append(string.Join(" | ", decisionSummaries))
                .Append('.');
        }

        builder.Append(" Do not mark the step complete until governed process completion, required tool proof, branch selection, and required artifacts are satisfied.");
        return builder.ToString();
    }

    private static bool SatisfiesManualRerunArtifactExpectation(
        ProcessArtifactRecord artifact,
        ProcessArtifactExpectation expectation)
    {
        if (artifact.ArtifactKind != expectation.ArtifactKind)
        {
            return false;
        }

        if (artifact.SensitivityLevel < expectation.SensitivityLevel)
        {
            return false;
        }

        if (!SatisfiesManualRerunTrustRequirement(artifact.TrustStatus, expectation.TrustRequirement))
        {
            return false;
        }

        if (artifact.ArtifactExpectationId.HasValue)
        {
            return artifact.ArtifactExpectationId.Value == expectation.Id;
        }

        return string.Equals(artifact.Title, expectation.Title, StringComparison.OrdinalIgnoreCase);
    }

    private static bool SatisfiesManualRerunTrustRequirement(
        ProcessArtifactTrustStatus trustStatus,
        ProcessArtifactTrustRequirement trustRequirement)
    {
        return trustRequirement switch
        {
            ProcessArtifactTrustRequirement.None => true,
            ProcessArtifactTrustRequirement.ReviewRequired => trustStatus is
                ProcessArtifactTrustStatus.ReviewRequired or
                ProcessArtifactTrustStatus.Approved or
                ProcessArtifactTrustStatus.TrustedSource,
            ProcessArtifactTrustRequirement.HumanApproved => trustStatus == ProcessArtifactTrustStatus.Approved,
            ProcessArtifactTrustRequirement.TrustedSource => trustStatus == ProcessArtifactTrustStatus.TrustedSource,
            _ => false
        };
    }
}
