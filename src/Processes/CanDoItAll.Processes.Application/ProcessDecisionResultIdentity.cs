using System.Security.Cryptography;
using System.Text.Json;
using CanDoItAll.Processes.Core;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public static class ProcessDecisionResultIdentity {
    public static RuntimeCommandId Derive(ProcessRuntimeStateSnapshot state, SubmitStrategyResultCommand captured,
        ProcessDecisionResultSelection selection) {
        selection.RequireOriginal(state, captured.StepInstanceId, captured.Result.ExecutionRunId);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new {
            Domain = "process-decision-result/v1",
            RunId = state.RunId.Value,
            RootRunId = state.RootRunId.Value,
            StepId = captured.StepInstanceId.Value,
            OwnerId = captured.OwnerId.Value,
            ClaimToken = captured.ClaimToken.Value,
            ResultKey = captured.IdempotencyKey.Value,
            BudgetId = selection.Budget.BudgetId.Value,
            OriginalChild = selection.Child is { } child ? ProcessDecisionChildMaterialBindingCodec.Serialize(child.Original) : null,
            Result = captured.Result
        });
        return new(new Guid(SHA256.HashData(bytes).AsSpan(0, 16), bigEndian: true));
    }
}
