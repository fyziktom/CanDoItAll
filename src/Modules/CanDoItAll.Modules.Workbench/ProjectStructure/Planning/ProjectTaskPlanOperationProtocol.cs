using CanDoItAll.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CanDoItAll.Modules.Workbench;

internal static class ProjectTaskPlanOperationProtocol {
    internal static string OperationKey(ProjectTaskPlanOperation operation, ProjectTaskPlanCaller caller)
        => $"workbench:task-plan:operation:v1:{WorkbenchTaskPlanFiniteOperations.Pool.Value:N}:{WorkbenchTaskPlanFiniteOperations.Family.Value:N}:{operation.Namespace.Value:N}:{operation.Epoch}:{(int)caller.Kind}:{Convert.ToHexStringLower(caller.Key.AsSpan())}:{operation.Id:N}";

    internal static async Task<bool> ExistsLockedAsync(WorkbenchDbContext context,
        ProjectTaskPlanOperation operation, ProjectTaskPlanCaller caller, CancellationToken cancellationToken) {
        await SerializableMutationScope.AcquireRelationalScopeLocksAsync(context, [OperationKey(operation, caller)], cancellationToken);
        var key = caller.Key.ToArray();
        return await context.Set<ProjectTaskPlanReceiptRecord>().AnyAsync(row =>
            row.NamespaceId == operation.Namespace.Value && row.Epoch == operation.Epoch && row.CallerKind == (int)caller.Kind &&
            row.CallerKey == key && row.OperationId == operation.Id, cancellationToken);
    }

    internal static ProjectTaskPlanReceiptRecord CreateReceipt(ProjectTaskPlanAccepted accepted, ProjectTaskPlanCaller caller) => new() {
        NamespaceId = accepted.Operation.Namespace.Value,
        Epoch = accepted.Operation.Epoch,
        CallerKind = (int)caller.Kind,
        CallerKey = caller.Key.ToArray(),
        OperationId = accepted.Operation.Id,
        SchemaVersion = 1,
        NormalizationVersion = 1,
        ResultVersion = ProjectTaskPlanOperationCodec.CurrentVersion,
        SemanticDigest = Convert.FromHexString(accepted.SemanticDigest),
        ProjectId = accepted.OriginalDefinition.Project.ProjectId,
        LifetimeId = accepted.OriginalDefinition.Project.LifetimeId,
        OriginalDefinitionRevision = accepted.OriginalDefinition.Revision,
        DecisionId = accepted.Source.DecisionId,
        DecisionGeneration = accepted.Source.Generation,
        SourceKind = (int)accepted.Source.Kind,
        SourceVersion = accepted.Source.Version,
        SourceFingerprint = Convert.FromHexString(accepted.Source.Fingerprint),
        AcceptedAtUtc = accepted.AcceptedAtUtc,
        AcceptedPayload = ProjectTaskPlanOperationCodec.EncodeAccepted(accepted)
    };

    internal static ProjectTaskPlanAccepted Decode(ProjectTaskPlanReceiptRecord receipt, ProjectTaskPlanOperation operation,
        ProjectTaskPlanCaller caller, DateTimeOffset replayUntilUtc) {
        if (receipt.SchemaVersion != 1 || receipt.NormalizationVersion != 1 || receipt.ResultVersion != 1 || receipt.SourceVersion != 1) {
            throw new ProjectTaskPlanReceiptDecodeException();
        }
        var accepted = ProjectTaskPlanOperationCodec.DecodeAccepted(receipt.AcceptedPayload);
        if (accepted.Operation != operation || receipt.NamespaceId != operation.Namespace.Value || receipt.Epoch != operation.Epoch ||
                receipt.OperationId != operation.Id || receipt.CallerKind != (int)caller.Kind ||
                !receipt.CallerKey.AsSpan().SequenceEqual(caller.Key.AsSpan()) ||
                accepted.SemanticDigest != Convert.ToHexStringLower(receipt.SemanticDigest) ||
                accepted.OriginalDefinition.Project.ProjectId != receipt.ProjectId ||
                accepted.OriginalDefinition.Project.LifetimeId != receipt.LifetimeId ||
                accepted.OriginalDefinition.Revision != receipt.OriginalDefinitionRevision ||
                accepted.Source.DecisionId != receipt.DecisionId || accepted.Source.Generation != receipt.DecisionGeneration ||
                (int)accepted.Source.Kind != receipt.SourceKind || accepted.Source.Version != receipt.SourceVersion ||
                accepted.Source.Fingerprint != Convert.ToHexStringLower(receipt.SourceFingerprint) ||
                accepted.AcceptedAtUtc != receipt.AcceptedAtUtc || accepted.ReplayUntilUtc != replayUntilUtc) {
            throw new ProjectTaskPlanReceiptDecodeException();
        }
        return accepted;
    }
}
