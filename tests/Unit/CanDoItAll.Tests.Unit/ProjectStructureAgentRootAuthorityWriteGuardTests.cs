using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Unit;

public sealed class ProjectStructureAgentRootAuthorityWriteGuardTests
{
    private const string WorkspaceRoot = @"C:\repositories\CanDoItAll";
    private const string AuthorizedExternalRoot =
        @"C:\programovani\dotnet\calculator-e2e-test";

    [Fact]
    public void Unaudited_agent_write_allows_a_managed_workspace_root()
    {
        ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
            CreateProjectBlockMetadata(@"src\Calculator"),
            WorkspaceRoot);
    }

    [Fact]
    public void Unaudited_agent_write_rejects_an_external_root()
    {
        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
                CreateProjectBlockMetadata(
                    @"C:\operator\chosen\external-project"),
                WorkspaceRoot));

        Assert.Equal(
            ProjectStructureAgentRootAuthorityWriteGuard.FailureCode,
            exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
    }

    [Fact]
    public void Unaudited_agent_write_rejects_a_legacy_root_level_external_root()
    {
        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
                """{ "outputRoot": "C:\\operator\\chosen\\external-project" }""",
                WorkspaceRoot));

        Assert.Equal(
            ProjectStructureAgentRootAuthorityWriteGuard.FailureCode,
            exception.ErrorCode);
    }

    [Fact]
    public void Audited_agent_write_allows_a_workspace_relative_root()
    {
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRun([]));

        ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
            CreateProjectBlockMetadata(@"src\Calculator"),
            WorkspaceRoot);
    }

    [Fact]
    public void Audited_agent_write_allows_an_external_root_already_in_the_run_scope()
    {
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRun([AuthorizedExternalRoot]));

        ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
            CreateProjectBlockMetadata(
                AuthorizedExternalRoot + @"\src"),
            WorkspaceRoot);
    }

    [Fact]
    public void Audited_agent_write_rejects_an_arbitrary_external_root_with_retryable_typed_failure()
    {
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRun([AuthorizedExternalRoot]));

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
                CreateProjectBlockMetadata(
                    @"C:\operator\private\unselected-project"),
                WorkspaceRoot));

        Assert.Equal(
            ProjectStructureAgentRootAuthorityWriteGuard.FailureCode,
            exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(
            @"C:\operator\private\unselected-project",
            exception.SafeMessage,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Unaudited_agent_parent_mutation_cannot_attach_below_an_external_project_block()
    {
        var projectRoot = CreateNode("project:root", parentId: null);
        var externalOwner = CreateNode(
            "custom:external-owner",
            projectRoot.Id,
            ProjectObjectType.ProjectBlock,
            CreateProjectBlockMetadata(@"C:\operator\private\unselected-project"));

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureParentAllowed(
                [projectRoot, externalOwner],
                externalOwner.Id,
                WorkspaceRoot));

        Assert.Equal(
            ProjectStructureAgentRootAuthorityWriteGuard.FailureCode,
            exception.ErrorCode);
    }

    [Fact]
    public void Audited_agent_parent_mutation_allows_only_an_already_authorized_external_project_block()
    {
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRun([AuthorizedExternalRoot]));
        var projectRoot = CreateNode("project:root", parentId: null);
        var authorizedOwner = CreateNode(
            "custom:authorized-owner",
            projectRoot.Id,
            ProjectObjectType.ProjectBlock,
            CreateProjectBlockMetadata(AuthorizedExternalRoot));

        ProjectStructureAgentRootAuthorityWriteGuard.EnsureParentAllowed(
            [projectRoot, authorizedOwner],
            authorizedOwner.Id,
            WorkspaceRoot);
    }

    private static string CreateProjectBlockMetadata(string outputRoot)
        => ProjectObjectMetadataSerializer.Serialize(
            new ProjectObjectMetadataEnvelope
            {
                ProjectBlock = new ProjectBlockMetadata
                {
                    OutputRoot = outputRoot
                }
            });

    private static ProjectStructureNode CreateNode(
        string id,
        string? parentId,
        ProjectObjectType objectType = ProjectObjectType.Note,
        string metadataJson = "{}")
        => new(
            id,
            parentId,
            objectType,
            objectType == ProjectObjectType.ProjectBlock ? "delivery" : "note",
            id,
            string.Empty,
            "Draft",
            string.Empty,
            string.Empty,
            string.Empty,
            null,
            string.Empty,
            string.Empty,
            string.Empty,
            0,
            0,
            new ProjectObjectVisualProfile("pill", "#64748b", "NT", "Note"),
            [],
            string.Empty,
            0,
            string.Empty,
            string.Empty,
            string.Empty,
            [],
            0,
            MetadataJson: metadataJson);

    private static ExecutionRunRecord CreateExecutionRun(
        IReadOnlyList<string> readOnlyExternalTargetAliases)
    {
        var now = DateTimeOffset.UtcNow;
        var metadataJson = JsonSerializer.Serialize(
            new Dictionary<string, IReadOnlyList<string>>
            {
                [ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] =
                    readOnlyExternalTargetAliases
            });
        return new ExecutionRunRecord(
            Id: Guid.NewGuid(),
            AgentId: Guid.NewGuid(),
            ChatSessionId: null,
            Title: "ProjectBlock root write guard test",
            SourceKind: "test",
            SourceId: "project-structure-root-write-guard",
            CorrelationId: Guid.NewGuid().ToString("D"),
            CausationId: string.Empty,
            RequestedBy: "unit-test",
            RequestedByKind: "system",
            MetadataJson: metadataJson,
            InputSummary: string.Empty,
            ResultSummary: string.Empty,
            ProviderName: "test",
            Model: "test",
            State: ExecutionState.Running,
            Outcome: null,
            CreatedAtUtc: now,
            UpdatedAtUtc: now,
            StartedAtUtc: now,
            CompletedAtUtc: null,
            RuntimeSessionKey: string.Empty,
            SerializedSessionStateJson: null,
            PendingApprovals: []);
    }
}
