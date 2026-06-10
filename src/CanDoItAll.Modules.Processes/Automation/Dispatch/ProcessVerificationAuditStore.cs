namespace CanDoItAll.Modules.Processes;

internal interface IProcessVerificationAuditStore
{
    ProcessVerificationAuditRecord Append(ProcessVerificationAuditRecord record);

    IReadOnlyList<ProcessVerificationAuditRecord> List();
}

internal sealed class InMemoryProcessVerificationAuditStore : IProcessVerificationAuditStore
{
    private readonly object gate = new();
    private readonly List<ProcessVerificationAuditRecord> records = [];

    public ProcessVerificationAuditRecord Append(ProcessVerificationAuditRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);

        lock (gate)
        {
            records.Add(record);
        }

        return record;
    }

    public IReadOnlyList<ProcessVerificationAuditRecord> List()
    {
        lock (gate)
        {
            return Array.AsReadOnly(records.ToArray());
        }
    }
}
