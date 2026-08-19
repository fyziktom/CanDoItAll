using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureAgentRootAuthorityWriteGuardTests : IDisposable
{
    private readonly string workspaceRoot = TestFileSystem.CreateTemporaryRoot("project-root-guard-workspace");
    private readonly string authorizedExternalRoot = TestFileSystem.CreateTemporaryRoot("project-root-guard-external");
    private readonly string unauthorizedExternalRoot = TestFileSystem.CreateTemporaryRoot("project-root-guard-unscoped");
    private readonly IExternalTargetPathRegistryFactory externalTargetPathRegistryFactory = new ExternalTargetPathRegistryFactory();
    private readonly IReadOnlyList<ExternalTargetRootBinding> externalTargetRootBindings;
    private readonly string authorizedExternalAlias;
    private readonly string authorizedExternalChildAlias;

    public ProjectStructureAgentRootAuthorityWriteGuardTests()
    {
        var registry = externalTargetPathRegistryFactory.Create([]);
        Assert.True(registry.TryCreateAlias(authorizedExternalRoot, out authorizedExternalAlias));
        Assert.True(registry.TryCreateAlias(
            Path.Combine(authorizedExternalRoot, "src"),
            out authorizedExternalChildAlias));
        externalTargetRootBindings = registry.ExportBindings([authorizedExternalAlias]);
    }

    [Fact]
    public void Unaudited_agent_write_allows_a_managed_workspace_root()
    {
        ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
            CreateProjectBlockMetadata(@"src\Calculator"),
            workspaceRoot,
            externalTargetPathRegistryFactory);
    }

    [Fact]
    public void Unaudited_agent_write_rejects_an_external_root()
    {
        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
                CreateProjectBlockMetadata(
                    Path.Combine(authorizedExternalRoot, "unscoped")),
                workspaceRoot,
                externalTargetPathRegistryFactory));

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
                JsonSerializer.Serialize(new { outputRoot = Path.Combine(authorizedExternalRoot, "legacy") }),
                workspaceRoot,
                externalTargetPathRegistryFactory));

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
            workspaceRoot,
            externalTargetPathRegistryFactory);
    }

    [Fact]
    public void Audited_agent_write_allows_an_external_root_already_in_the_run_scope()
    {
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRun([authorizedExternalAlias]));

        ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
            CreateProjectBlockMetadata(
                authorizedExternalChildAlias),
            workspaceRoot,
            externalTargetPathRegistryFactory);
    }

    [Fact]
    public void Audited_agent_write_rejects_an_arbitrary_external_root_with_retryable_typed_failure()
    {
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRun([authorizedExternalAlias]));

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
                CreateProjectBlockMetadata(
                    Path.Combine(unauthorizedExternalRoot, "unselected-project")),
                workspaceRoot,
                externalTargetPathRegistryFactory));

        Assert.Equal(
            ProjectStructureAgentRootAuthorityWriteGuard.FailureCode,
            exception.ErrorCode);
        Assert.True(exception.IsSafeToExpose);
        Assert.True(exception.CanRetryWithCorrectedInput);
        Assert.DoesNotContain(
            Path.Combine(unauthorizedExternalRoot, "unselected-project"),
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
            CreateProjectBlockMetadata(Path.Combine(authorizedExternalRoot, "unselected-project")));

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureParentAllowed(
                [projectRoot, externalOwner],
                externalOwner.Id,
                workspaceRoot,
                externalTargetPathRegistryFactory));

        Assert.Equal(
            ProjectStructureAgentRootAuthorityWriteGuard.FailureCode,
            exception.ErrorCode);
    }

    [Fact]
    public void Audited_agent_parent_mutation_allows_only_an_already_authorized_external_project_block()
    {
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRun([authorizedExternalAlias]));
        var projectRoot = CreateNode("project:root", parentId: null);
        var authorizedOwner = CreateNode(
            "custom:authorized-owner",
            projectRoot.Id,
            ProjectObjectType.ProjectBlock,
            CreateProjectBlockMetadata(authorizedExternalAlias));

        ProjectStructureAgentRootAuthorityWriteGuard.EnsureParentAllowed(
            [projectRoot, authorizedOwner],
            authorizedOwner.Id,
            workspaceRoot,
            externalTargetPathRegistryFactory);
    }

    [Fact]
    public void Audited_agent_parent_mutation_allows_a_bound_physical_root_in_the_execution_scope()
    {
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRun(
                [authorizedExternalAlias],
                externalTargetRootBindings));
        var projectRoot = CreateNode("project:root", parentId: null);
        var authorizedOwner = CreateNode(
            "custom:authorized-owner",
            projectRoot.Id,
            ProjectObjectType.ProjectBlock,
            CreateProjectBlockMetadata(authorizedExternalRoot));
        var targetWorkItem = CreateNode(
            "custom:target-work-item",
            authorizedOwner.Id,
            ProjectObjectType.WorkItem);

        ProjectStructureAgentRootAuthorityWriteGuard.EnsureParentAllowed(
            [projectRoot, authorizedOwner, targetWorkItem],
            targetWorkItem.Id,
            workspaceRoot,
            externalTargetPathRegistryFactory);
    }

    [Fact]
    public void Audited_agent_parent_mutation_rejects_a_physical_root_without_its_bound_identity()
    {
        using var audit = WorkspaceExecutionAuditContext.BeginScope(
            CreateExecutionRun([authorizedExternalAlias]));
        var projectRoot = CreateNode("project:root", parentId: null);
        var externalOwner = CreateNode(
            "custom:external-owner",
            projectRoot.Id,
            ProjectObjectType.ProjectBlock,
            CreateProjectBlockMetadata(authorizedExternalRoot));
        var targetWorkItem = CreateNode(
            "custom:target-work-item",
            externalOwner.Id,
            ProjectObjectType.WorkItem);

        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureParentAllowed(
                [projectRoot, externalOwner, targetWorkItem],
                targetWorkItem.Id,
                workspaceRoot,
                externalTargetPathRegistryFactory));

        Assert.Equal(ProjectStructureAgentRootAuthorityWriteGuard.FailureCode, exception.ErrorCode);
        Assert.Contains("requested parent", exception.SafeMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Managed_workspace_containment_is_case_sensitive_when_the_filesystem_is()
    {
        if (new PhysicalFileSystemPathPolicyFactory().Create(workspaceRoot).CaseSensitivity !=
            PhysicalFileSystemCaseSensitivity.Sensitive)
        {
            return;
        }

        var differentlyCasedRoot = workspaceRoot.ToUpperInvariant();
        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
                CreateProjectBlockMetadata(Path.Combine(differentlyCasedRoot, "src")),
                workspaceRoot,
                externalTargetPathRegistryFactory));

        Assert.Equal(ProjectStructureAgentRootAuthorityWriteGuard.FailureCode, exception.ErrorCode);
    }

    [Fact]
    public void Foreign_host_workspace_root_is_rejected()
    {
        var foreignWorkspaceRoot = OperatingSystem.IsWindows()
            ? "/tmp/project-root-guard"
            : @"C:\project-root-guard";
        var exception = Assert.Throws<ProjectStructureAgentException>(() =>
            ProjectStructureAgentRootAuthorityWriteGuard.EnsureAllowed(
                CreateProjectBlockMetadata("src"),
                foreignWorkspaceRoot,
                externalTargetPathRegistryFactory));

        Assert.Equal(ProjectStructureAgentRootAuthorityWriteGuard.FailureCode, exception.ErrorCode);
    }

    public void Dispose()
    {
        TestFileSystem.DeleteDirectoryWithRetry(authorizedExternalRoot);
        TestFileSystem.DeleteDirectoryWithRetry(unauthorizedExternalRoot);
        TestFileSystem.DeleteDirectoryWithRetry(workspaceRoot);
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
        IReadOnlyList<string> readOnlyExternalTargetAliases,
        IReadOnlyList<ExternalTargetRootBinding>? externalTargetRootBindings = null)
    {
        var now = DateTimeOffset.UtcNow;
        var metadataJson = JsonSerializer.Serialize(
            new Dictionary<string, IReadOnlyList<string>>
            {
                [ExecutionInvocationMetadata.ReadOnlyExternalTargetAliasesMetadataKey] =
                    readOnlyExternalTargetAliases
            });
        if (externalTargetRootBindings is not null)
        {
            metadataJson = ExecutionInvocationMetadata.ApplyExternalTargetRootBindings(
                metadataJson,
                externalTargetRootBindings);
        }

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
