using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessOriginalReceiptAuthorityReader(
    EfProcessExecutionAuthorityQuery owner,
    ICanonicalRuntimeDatabase database) : IProcessOriginalReceiptAuthorityReader {
    public async Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalForReceiptReadAsync(ProcessExecutionClaimEvidence original,
        CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(original);
        var result = await owner.ReadDispatchAsync(original, cancellationToken).ConfigureAwait(false);
        if (result.Snapshot?.SourceAuthority is { } authority && authority.DatabaseProfileId != database.Profile.Profile.Id) {
            throw new ProcessExecutionAuthorityMismatchException("The saved Process authority belongs to a different runtime profile.");
        }
        return result;
    }

    public async Task<ProcessExecutionDispatchAuthorityResult> ObserveOriginalDecisionForReceiptReadAsync(ProcessExecutionClaimEvidence original,
        ProcessDecisionChildMaterialBinding binding, ProcessDecisionExecutionContract supportedExecution,
        ProcessDecisionContentContract? supportedContent, CancellationToken cancellationToken = default) {
        ArgumentNullException.ThrowIfNull(binding);
        if (binding.Scope.DatabaseProfileId != database.Profile.Profile.Id) {
            throw new ProcessExecutionAuthorityMismatchException("The original Process material belongs to a different runtime profile.");
        }
        var result = await owner.ReadOriginalDecisionAsync(original, binding, supportedExecution, supportedContent, cancellationToken).ConfigureAwait(false);
        if (result.Snapshot?.SourceAuthority is { } authority && authority.DatabaseProfileId != database.Profile.Profile.Id) {
            throw new ProcessExecutionAuthorityMismatchException("The saved Process authority belongs to a different runtime profile.");
        }
        return result;
    }
}
