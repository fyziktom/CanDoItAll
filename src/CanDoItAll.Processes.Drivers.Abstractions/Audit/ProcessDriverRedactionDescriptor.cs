namespace CanDoItAll.Processes.Drivers.Abstractions.Audit;

public sealed record ProcessDriverRedactionDescriptor(
    ProcessDriverRedactionStatus Status,
    IReadOnlyList<ProcessDriverRedactionKind> AppliedKinds,
    string RedactedTextHash);

public enum ProcessDriverRedactionStatus
{
    None = 0,
    Redacted = 1
}

public enum ProcessDriverRedactionKind
{
    Secret = 1,
    EmailAddress = 2,
    AccessToken = 3,
    ConnectionString = 4
}
