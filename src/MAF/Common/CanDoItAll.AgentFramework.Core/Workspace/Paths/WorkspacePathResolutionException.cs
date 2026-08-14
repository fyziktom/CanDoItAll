namespace CanDoItAll.AgentFramework.Core;

public enum WorkspacePathResolutionFailureKind
{
    InvalidPath,
    OutsideWorkspace,
    FileRequired,
    DirectoryRequired,
    PathMissing,
    ManagedPathAliasMismatch,
    ReparsePointTraversal,
    ForeignManagedScope,
    ForeignHostPath
}

public sealed class WorkspacePathResolutionException : InvalidOperationException
{
    private WorkspacePathResolutionException(
        WorkspacePathResolutionFailureKind kind,
        string diagnosticMessage,
        string safeMessage,
        Exception? innerException = null)
        : base(diagnosticMessage, innerException)
    {
        Kind = kind;
        SafeMessage = safeMessage;
    }

    public WorkspacePathResolutionFailureKind Kind { get; }

    public string SafeMessage { get; }

    public static WorkspacePathResolutionException InvalidPath(
        string diagnosticMessage,
        Exception? innerException = null)
        => Create(
            WorkspacePathResolutionFailureKind.InvalidPath,
            diagnosticMessage,
            "The requested workspace path is invalid.",
            innerException);

    public static WorkspacePathResolutionException OutsideWorkspace(string diagnosticMessage)
        => Create(
            WorkspacePathResolutionFailureKind.OutsideWorkspace,
            diagnosticMessage,
            "The requested path is outside the allowed workspace scope.");

    public static WorkspacePathResolutionException FileRequired(string diagnosticMessage)
        => Create(
            WorkspacePathResolutionFailureKind.FileRequired,
            diagnosticMessage,
            "The requested path identifies a directory, but a file is required.");

    public static WorkspacePathResolutionException DirectoryRequired(string diagnosticMessage)
        => Create(
            WorkspacePathResolutionFailureKind.DirectoryRequired,
            diagnosticMessage,
            "The requested path identifies a file, but a directory is required.");

    public static WorkspacePathResolutionException PathMissing(string diagnosticMessage)
        => Create(
            WorkspacePathResolutionFailureKind.PathMissing,
            diagnosticMessage,
            "The requested workspace path does not exist.");

    public static WorkspacePathResolutionException ManagedPathAliasMismatch(string diagnosticMessage)
        => Create(
            WorkspacePathResolutionFailureKind.ManagedPathAliasMismatch,
            diagnosticMessage,
            "The requested managed workspace path uses an invalid alias.");

    public static WorkspacePathResolutionException ReparsePointTraversal(string diagnosticMessage)
        => Create(
            WorkspacePathResolutionFailureKind.ReparsePointTraversal,
            diagnosticMessage,
            "The requested workspace path crosses a disallowed filesystem reparse point.");

    public static WorkspacePathResolutionException ForeignManagedScope(string diagnosticMessage)
        => Create(
            WorkspacePathResolutionFailureKind.ForeignManagedScope,
            diagnosticMessage,
            "The requested path targets a different managed workspace scope.");

    public static WorkspacePathResolutionException ForeignHostPath(string diagnosticMessage)
        => Create(
            WorkspacePathResolutionFailureKind.ForeignHostPath,
            diagnosticMessage,
            "The requested path belongs to a different host platform and requires explicit rebind or migration.");

    private static WorkspacePathResolutionException Create(
        WorkspacePathResolutionFailureKind kind,
        string diagnosticMessage,
        string safeMessage,
        Exception? innerException = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(diagnosticMessage);
        ArgumentException.ThrowIfNullOrWhiteSpace(safeMessage);
        return new WorkspacePathResolutionException(
            kind,
            diagnosticMessage,
            safeMessage,
            innerException);
    }
}
