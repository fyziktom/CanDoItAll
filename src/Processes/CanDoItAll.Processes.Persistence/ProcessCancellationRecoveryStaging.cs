using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessCancellationRecoveryStaging {
    internal const string StoppedRootIndexName = "IX_process_runtime_states_stopped_root";
    internal const string StoppedRootFilter = "\"RootRunId\" = \"RunId\" AND \"Status\" IN ('CancelRequested', 'Cancelled')";

    internal static void Stage(ProcessPersistenceDbContext context, ProcessRuntimeCommitRequest request, ProcessRuntimeStatus? previous) {
        var state = request.Mutation.State;
        if (state.RunId != state.RootRunId || state.Status is not (ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled) ||
                previous is ProcessRuntimeStatus.CancelRequested or ProcessRuntimeStatus.Cancelled) {
            return;
        }
        context.CancellationRecoveries.Add(new() {
            RootRunId = state.RootRunId.Value, Origin = ProcessCancellationRecoveryOrigin.LiveBarrier, BarrierCommandId = request.CommandId.Value,
            PositionJson = ProcessCancellationRecoveryCodec.WritePosition(ProcessCancellationRecoveryPosition.Initial)
        });
    }
}
