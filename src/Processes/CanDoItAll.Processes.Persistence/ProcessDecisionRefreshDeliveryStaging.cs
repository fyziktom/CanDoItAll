using CanDoItAll.Processes.Runtime;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessDecisionRefreshDeliveryStaging {
    internal static void Stage(ProcessPersistenceDbContext context, ProcessRuntimeCommitRequest request) {
        if (request.Decision is ProcessDecisionBeginRefresh) {
            context.RefreshDeliveries.Add(new() { RunId = request.Mutation.State.RunId.Value,
                RootRunId = request.Mutation.State.RootRunId.Value, AdmissionCommandId = request.CommandId.Value });
        }
    }

    internal static Task<int> WakeAsync(ProcessPersistenceDbContext context, ProcessDecisionRefreshCaptureReservation reservation, CancellationToken cancellationToken)
        => context.Database.ExecuteSqlInterpolatedAsync($"""
            WITH moment AS MATERIALIZED (SELECT clock_timestamp() AS now)
            UPDATE process_decision_refresh_delivery SET "WakeRevision" = "WakeRevision" + 1,
                "Disposition" = 0, "Reason" = CASE WHEN "LeaseToken" IS NULL THEN 0 ELSE "Reason" END,
                "UpdatedAtUtc" = moment.now, "DueAtUtc" = COALESCE("LeaseExpiresAtUtc", moment.now)
            FROM moment WHERE "RunId" = {reservation.RunId.Value} AND "AdmissionCommandId" = {reservation.AdmissionId.Value}
                AND "RootRunId" = {reservation.RootRunId.Value} AND "Disposition" IN (0, 1)
            """, cancellationToken);
}
