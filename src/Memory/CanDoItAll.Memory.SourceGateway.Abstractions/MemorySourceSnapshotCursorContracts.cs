namespace CanDoItAll.Memory.SourceGateway;

public sealed record MemorySourceSnapshotCursorDescriptor(
    MemorySourceKind SourceKind,
    Guid ScopeId,
    string ProviderVersion,
    int Position,
    MemorySourceItemId LastItemId,
    string SnapshotAnchor);

public enum MemorySourceSnapshotCursorFailureReason
{
    InvalidFormat,
    SourceKindMismatch,
    ScopeMismatch,
    ProviderVersionMismatch,
    StaleAnchor
}

public sealed class MemorySourceSnapshotCursorException : InvalidOperationException
{
    public MemorySourceSnapshotCursorException(
        MemorySourceSnapshotCursorFailureReason reason,
        MemorySourceKind expectedSourceKind,
        Guid expectedScopeId,
        string expectedProviderVersion,
        MemorySourceSnapshotCursor cursor,
        string message,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Reason = reason;
        ExpectedSourceKind = expectedSourceKind;
        ExpectedScopeId = expectedScopeId;
        ExpectedProviderVersion = expectedProviderVersion;
        Cursor = cursor;
    }

    public MemorySourceSnapshotCursorFailureReason Reason { get; }

    public MemorySourceKind ExpectedSourceKind { get; }

    public Guid ExpectedScopeId { get; }

    public string ExpectedProviderVersion { get; }

    public MemorySourceSnapshotCursor Cursor { get; }
}
