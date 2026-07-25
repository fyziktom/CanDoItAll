using CanDoItAll.Processes.Projections;

namespace CanDoItAll.Processes.Application;

public interface IProcessRunRecordReader
{
    Task<ProcessRunRecordPage> ListAsync(
        ProcessRunRecordListQuery query,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessRunRecordReader(IProcessRunRecordStore store) : IProcessRunRecordReader
{
    public Task<ProcessRunRecordPage> ListAsync(
        ProcessRunRecordListQuery query,
        CancellationToken cancellationToken = default)
    {
        return store.ListAsync(query, cancellationToken);
    }
}
