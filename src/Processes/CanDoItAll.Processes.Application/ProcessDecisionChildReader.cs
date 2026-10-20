using CanDoItAll.Processes.Runtime;

namespace CanDoItAll.Processes.Application;

public interface IProcessDecisionChildReader {
    Task<ProcessDecisionChildObservation?> ReadAsync(ProcessDecisionChildAddress address, CancellationToken cancellationToken = default);
}
