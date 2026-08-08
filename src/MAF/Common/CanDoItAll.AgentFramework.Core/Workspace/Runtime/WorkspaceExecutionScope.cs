using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Immutable identity of one workspace execution: the physical workspace root
/// plus the effective workspace scope descriptor for the run, annotated with
/// the admission facts that produced it. Every scope-bound workspace service
/// used by one execution shares exactly this identity; scope mismatches fail
/// at construction instead of during a tool call. Root identity comparison is
/// OS-aware: case-insensitive on Windows and macOS, case-sensitive on Linux.
/// </summary>
public sealed record WorkspaceExecutionScope
{
    public WorkspaceExecutionScope(
        string workspaceRoot,
        WorkspaceScopeDescriptor scope,
        Guid? databaseProfileId = null,
        DatabaseProfileGeneration? databaseProfileGeneration = null,
        AgentExecutionAuthorityId? authorityId = null,
        string authorityFingerprint = "",
        Guid? executionRunId = null)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("A workspace root is required.", nameof(workspaceRoot));
        }

        ArgumentNullException.ThrowIfNull(scope);
        WorkspaceRoot = Path.GetFullPath(workspaceRoot);
        Scope = scope;
        DatabaseProfileId = databaseProfileId;
        DatabaseProfileGeneration = databaseProfileGeneration;
        AuthorityId = authorityId;
        AuthorityFingerprint = authorityFingerprint?.Trim() ?? string.Empty;
        ExecutionRunId = executionRunId;
    }

    public string WorkspaceRoot { get; }

    public WorkspaceScopeDescriptor Scope { get; }

    public Guid? DatabaseProfileId { get; }

    public DatabaseProfileGeneration? DatabaseProfileGeneration { get; }

    public AgentExecutionAuthorityId? AuthorityId { get; }

    public string AuthorityFingerprint { get; }

    public Guid? ExecutionRunId { get; }

    /// <summary>
    /// Stable identity used to prove that two scope-bound services belong to
    /// the same execution scope. Annotation fields (profile, authority, run)
    /// describe provenance and do not participate in service identity.
    /// </summary>
    public string Identity =>
        $"{WorkspaceRoot}|{Scope.Kind}|{Scope.Key}";

    public bool SharesIdentityWith(WorkspaceExecutionScope? other)
        => other is not null &&
           string.Equals(Identity, other.Identity, RootIdentityComparison);

    /// <summary>
    /// Builds the per-run execution scope with full provenance from the
    /// admitted governance snapshot. This is the production construction path
    /// for run-owned workspace bundles; runs without an admitted snapshot
    /// keep an unannotated scope.
    /// </summary>
    public static WorkspaceExecutionScope ForRun(
        string workspaceRoot,
        WorkspaceScopeDescriptor scope,
        AgentExecutionGovernanceSnapshot? governance,
        Guid? executionRunId = null)
        => governance is null
            ? new WorkspaceExecutionScope(workspaceRoot, scope, executionRunId: executionRunId)
            : new WorkspaceExecutionScope(
                workspaceRoot,
                scope,
                governance.DatabaseProfileId,
                governance.DatabaseProfileGeneration,
                governance.AuthorityId,
                governance.PolicyFingerprint,
                executionRunId);

    /// <summary>
    /// The current process's root identity comparison: Linux compares roots
    /// case-sensitively, Windows and macOS case-insensitively.
    /// </summary>
    public static StringComparison RootIdentityComparison { get; } =
        ResolveRootIdentityComparison(caseSensitiveFileSystem: OperatingSystem.IsLinux());

    /// <summary>Pure decision seam so both branches stay testable on any OS.</summary>
    internal static StringComparison ResolveRootIdentityComparison(bool caseSensitiveFileSystem)
        => caseSensitiveFileSystem ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
}
