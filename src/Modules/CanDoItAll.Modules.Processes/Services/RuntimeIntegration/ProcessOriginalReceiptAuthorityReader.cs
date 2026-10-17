using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;

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
}
