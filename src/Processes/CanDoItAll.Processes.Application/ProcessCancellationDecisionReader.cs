using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public sealed record ProcessCancellationDecisionSelection(ProcessRunId RunId, ProcessStepInstanceId StepInstanceId,
    ProcessDecisionReference Reference, bool HistoricalWorkPending);

public interface IProcessCancellationDecisionReader {
    Task<ProcessDecisionReference?> NextAsync(ProcessRunId rootRunId, ProcessDecisionId? after, CancellationToken cancellationToken);
    Task<ProcessCancellationDecisionSelection?> ReadAsync(ProcessRunId rootRunId, ProcessDecisionReference decision, CancellationToken cancellationToken);
    Task<ProcessDecisionChildId?> NextChildAsync(ProcessRunId rootRunId, ProcessDecisionReference decision,
        ProcessDecisionChildId? after, CancellationToken cancellationToken);
}
