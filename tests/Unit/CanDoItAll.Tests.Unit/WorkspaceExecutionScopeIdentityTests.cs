using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// The per-run workspace execution scope carries complete admission
/// provenance (profile, generation, authority identity/fingerprint), and its
/// root identity comparison is OS-aware: case-insensitive on Windows/macOS,
/// case-sensitive on Linux.
/// </summary>
public sealed class WorkspaceExecutionScopeIdentityTests
{
    [Fact]
    public void For_run_populates_identity_from_the_admitted_governance_snapshot()
    {
        var authority = new AgentExecutionAuthorityRecord(
            AgentExecutionAuthorityId.Create(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new DatabaseProfileGeneration(7),
            WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            readAllowed: true,
            mutationAllowed: true,
            "v2-canonical",
            "fingerprint-value",
            DateTimeOffset.UtcNow);
        var governance = AgentExecutionGovernanceSnapshot.FromAuthority(authority);
        var runId = Guid.NewGuid();

        var scope = WorkspaceExecutionScope.ForRun(
            Path.GetTempPath(),
            authority.WorkspaceScope,
            governance,
            runId);

        Assert.Equal(authority.DatabaseProfileId, scope.DatabaseProfileId);
        Assert.Equal(authority.DatabaseProfileGeneration, scope.DatabaseProfileGeneration);
        Assert.Equal(authority.AuthorityId, scope.AuthorityId);
        Assert.Equal(authority.PolicyFingerprint, scope.AuthorityFingerprint);
        Assert.Equal(runId, scope.ExecutionRunId);
    }

    [Fact]
    public void For_run_without_governance_keeps_an_unannotated_scope()
    {
        var scope = WorkspaceExecutionScope.ForRun(
            Path.GetTempPath(),
            WorkspaceScopeDescriptor.Sandbox,
            governance: null);

        Assert.Null(scope.DatabaseProfileId);
        Assert.Null(scope.AuthorityId);
        Assert.Equal(string.Empty, scope.AuthorityFingerprint);
    }

    [Fact]
    public void Provenance_annotations_do_not_change_service_identity()
    {
        var root = Path.GetTempPath();
        var descriptor = WorkspaceScopeDescriptor.Sandbox;
        var annotated = new WorkspaceExecutionScope(
            root,
            descriptor,
            Guid.NewGuid(),
            new DatabaseProfileGeneration(3),
            AgentExecutionAuthorityId.Create(),
            "fingerprint",
            Guid.NewGuid());
        var plain = new WorkspaceExecutionScope(root, descriptor);

        Assert.True(annotated.SharesIdentityWith(plain));
    }

    [Fact]
    public void Root_identity_comparison_is_case_sensitive_only_on_case_sensitive_file_systems()
    {
        Assert.Equal(
            StringComparison.Ordinal,
            WorkspaceExecutionScope.ResolveRootIdentityComparison(caseSensitiveFileSystem: true));
        Assert.Equal(
            StringComparison.OrdinalIgnoreCase,
            WorkspaceExecutionScope.ResolveRootIdentityComparison(caseSensitiveFileSystem: false));
        Assert.Equal(
            OperatingSystem.IsLinux() ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase,
            WorkspaceExecutionScope.RootIdentityComparison);
    }
}
