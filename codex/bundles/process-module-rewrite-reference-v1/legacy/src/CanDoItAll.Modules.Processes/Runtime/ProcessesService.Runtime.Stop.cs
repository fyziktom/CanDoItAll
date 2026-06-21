using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Processes;

public sealed partial class ProcessesService {
    public async Task<Result> StopBlockedRunAsync(
        ProcessRunStopRequest request,
        CancellationToken cancellationToken = default) {
        if (request.ProcessRunId == Guid.Empty) {
            return Result.Failure(
                Error.Validation("Select a process run before stopping it.", "processes.stop-blocked-run-id-required"));
        }

        await using var dbContext = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        await using var transaction = await BeginCoordinatedTransactionAsync(dbContext, cancellationToken);
        try {
            var run = await dbContext.Set<ProcessRun>()
                .SingleOrDefaultAsync(item => item.Id == request.ProcessRunId, cancellationToken);
            if (run is null) {
                return Result.Failure(
                    Error.Validation("Process run was not found.", "processes.stop-blocked-run-not-found"));
            }

            if (run.Status != ProcessRunStatus.Blocked) {
                return Result.Failure(
                    Error.Validation(
                        $"Process run '{run.Name}' is {run.Status} and only blocked runs can be stopped from this action.",
                        "processes.stop-blocked-run-invalid-status"));
            }

            var stepRuns = await dbContext.Set<ProcessStepRun>()
                .Where(item => item.ProcessRunId == run.Id)
                .ToListAsync(cancellationToken);
            var now = clock.GetUtcNow();
            var reason = string.IsNullOrWhiteSpace(request.Reason)
                ? "Blocked run stopped from Process Workspace."
                : request.Reason.Trim();
            var stoppedBy = string.IsNullOrWhiteSpace(request.StoppedBy)
                ? DefaultActor
                : request.StoppedBy.Trim();

            run.Status = ProcessRunStatus.Cancelled;
            run.UpdatedAtUtc = now;
            run.CompletedAtUtc = now;

            await dbContext.Set<ProcessDecisionRecord>().AddAsync(
                new ProcessDecisionRecord
                {
                    ProcessRunId = run.Id,
                    StepRunId = null,
                    DecisionKind = ProcessDecisionKind.Exception,
                    Outcome = ProcessDecisionOutcome.Rejected,
                    Title = "Stopped blocked process run",
                    Reason = reason,
                    PolicyEvaluation = "Operator stopped a blocked process run.",
                    BranchOutcomeId = null,
                    BranchOutcomeTitle = string.Empty,
                    DecidedBy = stoppedBy,
                    OperatingMode = run.OperatingMode,
                    CreatedAtUtc = now
                },
                cancellationToken);
            await dbContext.Set<ProcessJournalEntry>().AddAsync(
                BuildJournalEntry(
                    run.Id,
                    null,
                    ProcessRuntimeEventTypes.BlockedRunStopped,
                    "Stopped blocked process run",
                    reason,
                    run.OperatingMode,
                    $"definition-version:{run.ProcessDefinitionVersionId:D}",
                    stoppedBy),
                cancellationToken);
            await projectStructureBridge.SyncRunAsync(dbContext, run, stepRuns, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            NotifyRunObservationChanged(run.ProjectId, run.ProcessDefinitionId, run.Id);

            return Result.Success();
        }
        catch (DbUpdateConcurrencyException) {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CreateStopBlockedRunConflictError());
        }
        catch (DbUpdateException exception) when (DbUpdateExceptionClassifier.IsUniqueConstraintViolation(exception)) {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(CreateStopBlockedRunConflictError());
        }
        catch (DbUpdateException exception) {
            await transaction.RollbackAsync(cancellationToken);
            return Result.Failure(
                Error.Failure(
                    $"Blocked process run stop could not be persisted: {DbUpdateExceptionClassifier.GetProviderMessage(exception)}",
                    "processes.stop-blocked-run-persistence-failed"));
        }
    }

    private static Error CreateStopBlockedRunConflictError() {
        return Error.Validation(
            "The process run changed while it was being stopped. Reload the run and try again.",
            "processes.stop-blocked-run-conflict");
    }
}
