using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessExecutionProjectAuthorityReader(
    ISandboxWorkspaceExecutionRunStore executions,
    EfProcessExecutionAuthorityQuery owner,
    ICanonicalRuntimeDatabase database) : IProcessExecutionProjectAuthorityReader, IProcessExecutionDispatchAuthorityReader {
    public async Task<ProcessExecutionProjectAuthorityResult> ReadAsync(Guid executionRunId,
        CancellationToken cancellationToken = default) {
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId, Guid.Empty);
        var run = await executions.GetExecutionRunAsync(executionRunId, cancellationToken).ConfigureAwait(false);
        if (run is null) {
            return new(ProcessExecutionAuthorityDisposition.ExecutionNotFound);
        }
        var evidence = ProcessExecutionClaimEvidenceReader.Read(run, out var disposition);
        if (evidence is null) {
            return new(disposition);
        }
        var result = await owner.ReadAsync(evidence, cancellationToken).ConfigureAwait(false);
        if (result.Snapshot is { } snapshot && snapshot.SourceAuthority.DatabaseProfileId != database.Profile.Profile.Id) {
            throw new ProcessExecutionAuthorityMismatchException("The saved Process authority belongs to a different runtime profile.");
        }
        return result;
    }

    async Task<ProcessExecutionDispatchAuthorityResult> IProcessExecutionDispatchAuthorityReader.ReadAsync(Guid executionRunId,
        CancellationToken cancellationToken) {
        ArgumentOutOfRangeException.ThrowIfEqual(executionRunId, Guid.Empty);
        var run = await executions.GetExecutionRunAsync(executionRunId, cancellationToken).ConfigureAwait(false);
        if (run is null) {
            return new(ProcessExecutionAuthorityDisposition.ExecutionNotFound);
        }
        var evidence = ProcessExecutionClaimEvidenceReader.Read(run, out var disposition);
        if (evidence is null) {
            return new(disposition);
        }
        var result = await owner.ReadDispatchAsync(evidence, cancellationToken).ConfigureAwait(false);
        if (result.Snapshot?.SourceAuthority is { } authority && authority.DatabaseProfileId != database.Profile.Profile.Id) {
            throw new ProcessExecutionAuthorityMismatchException("The saved Process authority belongs to a different runtime profile.");
        }
        return result;
    }
}
