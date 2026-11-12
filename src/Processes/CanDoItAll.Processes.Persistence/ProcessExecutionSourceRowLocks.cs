using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Application;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessExecutionSourceRowLocks {
    internal static async Task AcquireAsync(ProcessPersistenceDbContext context, ProcessRunId rootRunId,
        ProcessExecutionClaimEvidence evidence, CancellationToken cancellationToken) {
        var state = await context.RuntimeStates.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_states WHERE "RunId" = {evidence.RunId.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (state is null || state.RootRunId != rootRunId.Value) {
            throw new ProcessExecutionAuthorityMismatchException("The Process root binding no longer matches the admitted native effect.");
        }
        await context.RuntimeStepAssignments.FromSqlInterpolated($"""
            SELECT * FROM process_runtime_step_assignments
            WHERE "RunId" = {evidence.RunId.Value} AND "StepInstanceId" = {evidence.StepInstanceId.Value} FOR SHARE
            """).AsNoTracking().SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }
}
