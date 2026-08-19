namespace CanDoItAll.Infrastructure.FileSystem;

public enum PhysicalFileSystemCaseSensitivity
{
    Unknown,
    Sensitive,
    Insensitive
}

public enum PhysicalPathValidationErrorCode
{
    InvalidPath,
    InvalidRoot,
    OutsideRoot,
    LinkTraversal
}

public sealed class PhysicalPathValidationException(
    PhysicalPathValidationErrorCode errorCode,
    string message,
    Exception? innerException = null) : IOException(message, innerException)
{
    public PhysicalPathValidationErrorCode ErrorCode { get; } = errorCode;
}

public interface IPhysicalFileSystemPathPolicy
{
    string RootPath { get; }

    PhysicalFileSystemCaseSensitivity CaseSensitivity { get; }

    StringComparer PathComparer { get; }

    StringComparison PathComparison { get; }

    bool IsWithinRoot(string fullPath);

    string ResolveContainedPath(string path);

    void EnsureSafePath(string fullPath, bool allowMissingLeaf = false);

    void RevalidateMutationTarget(string fullPath);
}

public interface IPhysicalFileSystemPathPolicyFactory
{
    IPhysicalFileSystemPathPolicy Create(string managedRoot);
}
