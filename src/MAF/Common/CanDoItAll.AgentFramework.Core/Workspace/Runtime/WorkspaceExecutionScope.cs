using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.SharedKernel;
using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Immutable identity of one workspace execution: the physical workspace root
/// plus the effective workspace scope descriptor for the run, annotated with
/// the admission facts that produced it. Every scope-bound workspace service
/// used by one execution shares exactly this identity; scope mismatches fail
/// at construction instead of during a tool call. Root identity comparison
/// uses the case model detected for the owned filesystem root.
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
        Guid? executionRunId = null,
        IEnumerable<ExternalTargetRootBinding>? externalTargetRootBindings = null,
        PhysicalFileSystemCaseSensitivity rootCaseSensitivity = PhysicalFileSystemCaseSensitivity.Unknown)
    {
        if (string.IsNullOrWhiteSpace(workspaceRoot))
        {
            throw new ArgumentException("A workspace root is required.", nameof(workspaceRoot));
        }

        ArgumentNullException.ThrowIfNull(scope);
        WorkspacePhysicalPathSyntaxPolicy.EnsureNativeOrRelative(workspaceRoot);
        WorkspaceRoot = Path.GetFullPath(workspaceRoot);
        Scope = scope;
        DatabaseProfileId = databaseProfileId;
        DatabaseProfileGeneration = databaseProfileGeneration;
        AuthorityId = authorityId;
        AuthorityFingerprint = authorityFingerprint?.Trim() ?? string.Empty;
        ExecutionRunId = executionRunId;
        ExternalTargetRootBindings = externalTargetRootBindings?.ToArray() ?? [];
        RootCaseSensitivity = Enum.IsDefined(rootCaseSensitivity)
            ? rootCaseSensitivity
            : throw new ArgumentOutOfRangeException(nameof(rootCaseSensitivity));
    }

    public string WorkspaceRoot { get; }

    public WorkspaceScopeDescriptor Scope { get; }

    public Guid? DatabaseProfileId { get; }

    public DatabaseProfileGeneration? DatabaseProfileGeneration { get; }

    public AgentExecutionAuthorityId? AuthorityId { get; }

    public string AuthorityFingerprint { get; }

    public Guid? ExecutionRunId { get; }

    public IReadOnlyList<ExternalTargetRootBinding> ExternalTargetRootBindings { get; }

    public PhysicalFileSystemCaseSensitivity RootCaseSensitivity { get; }

    /// <summary>
    /// Stable identity used to prove that two scope-bound services belong to
    /// the same execution scope. Annotation fields (profile, authority, run)
    /// describe provenance and do not participate in service identity.
    /// </summary>
    public string Identity =>
        $"{WorkspaceRoot}|{Scope.Kind}|{Scope.Key}";

    public bool SharesIdentityWith(WorkspaceExecutionScope? other)
        => other is not null &&
           Scope == other.Scope &&
           ResolveRootComparer(this, other).Equals(WorkspaceRoot, other.WorkspaceRoot);

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
        Guid? executionRunId = null,
        IEnumerable<ExternalTargetRootBinding>? externalTargetRootBindings = null,
        PhysicalFileSystemCaseSensitivity rootCaseSensitivity = PhysicalFileSystemCaseSensitivity.Unknown)
        => governance is null
            ? new WorkspaceExecutionScope(
                workspaceRoot,
                scope,
                executionRunId: executionRunId,
                externalTargetRootBindings: externalTargetRootBindings,
                rootCaseSensitivity: rootCaseSensitivity)
            : new WorkspaceExecutionScope(
                workspaceRoot,
                scope,
                governance.DatabaseProfileId,
                governance.DatabaseProfileGeneration,
                governance.AuthorityId,
                governance.PolicyFingerprint,
                executionRunId,
                externalTargetRootBindings,
                rootCaseSensitivity);

    private static StringComparer ResolveRootComparer(
        WorkspaceExecutionScope left,
        WorkspaceExecutionScope right)
        => left.RootCaseSensitivity == PhysicalFileSystemCaseSensitivity.Insensitive &&
           right.RootCaseSensitivity == PhysicalFileSystemCaseSensitivity.Insensitive
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
}
