using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

public interface IProcessRunNarrativeGenerator
{
    Task<ProcessRunNarrative> GenerateAsync(
        ProcessRunRecord record,
        CancellationToken cancellationToken = default);
}
