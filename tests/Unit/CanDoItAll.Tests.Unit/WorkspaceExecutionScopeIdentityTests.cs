using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.FileSystem;

namespace CanDoItAll.Tests.Unit.AgentFramework;

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
    public void Root_identity_uses_captured_root_case_model_and_logical_scope_remains_ordinal()
    {
        string root = Path.Combine(Path.GetTempPath(), "workspace-root");
        var insensitive = new WorkspaceExecutionScope(
            root,
            WorkspaceScopeDescriptor.Project("Project-A"),
            rootCaseSensitivity: PhysicalFileSystemCaseSensitivity.Insensitive);
        var sameInsensitiveRoot = new WorkspaceExecutionScope(
            root.ToUpperInvariant(),
            WorkspaceScopeDescriptor.Project("Project-A"),
            rootCaseSensitivity: PhysicalFileSystemCaseSensitivity.Insensitive);
        var sensitive = new WorkspaceExecutionScope(
            root.ToUpperInvariant(),
            WorkspaceScopeDescriptor.Project("Project-A"),
            rootCaseSensitivity: PhysicalFileSystemCaseSensitivity.Sensitive);
        var differentLogicalScope = new WorkspaceExecutionScope(
            root,
            WorkspaceScopeDescriptor.Project("project-a") with { Key = "PROJECT-A" },
            rootCaseSensitivity: PhysicalFileSystemCaseSensitivity.Insensitive);

        Assert.True(insensitive.SharesIdentityWith(sameInsensitiveRoot));
        Assert.False(insensitive.SharesIdentityWith(sensitive));
        Assert.False(insensitive.SharesIdentityWith(differentLogicalScope));
    }
}
