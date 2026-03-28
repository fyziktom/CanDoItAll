using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Mcp.ProjectStructure.Tests;

public sealed class ProjectStructureToolsTests
{
    [Fact]
    public async Task ProjectStructureProjectLeaseAcquireAsync_returns_structured_lease_conflict_failure()
    {
        var tools = new ProjectStructureTools(new StubCoordinator
        {
            OnAcquireLease = (_, _, _, _) => throw new ToolInvocationException("LeaseConflict", "Another agent already owns this scope.")
        }, NullLogger<ProjectStructureTools>.Instance);

        var result = await tools.ProjectStructureProjectLeaseAcquireAsync(Guid.NewGuid(), "Mutate the project");

        Assert.False(result.Ok);
        Assert.Equal("LeaseConflict", result.Error!.Code);
        Assert.Equal("lease_conflict", result.Status);
    }

    [Fact]
    public async Task ProjectStructureImportAsync_returns_successful_structured_content()
    {
        var projectId = Guid.NewGuid();
        var tools = new ProjectStructureTools(new StubCoordinator
        {
            OnImport = (_, _, _) => Task.FromResult(new ProjectStructureImportResult(
                projectId,
                "container-1",
                null,
                ["container-1", "task-1"],
                []))
        }, NullLogger<ProjectStructureTools>.Instance);

        var result = await tools.ProjectStructureImportAsync(new ProjectStructureImportRequest(
            projectId,
            null,
            ProjectStructureImportSourceKind.JsonOutline,
            "Imported outline",
            """{"title":"Outline"}"""));

        Assert.True(result.Ok);
        Assert.Equal("container-1", result.Data!.ContainerNodeId);
    }

    [Fact]
    public async Task ProjectStructureAnalyticsQueryAsync_returns_successful_structured_content()
    {
        var tools = new ProjectStructureTools(new StubCoordinator
        {
            OnQueryAnalytics = (_, _) => Task.FromResult(new ProjectStructureAnalyticsResponse(
                [
                    new ProjectStructureAnalyticsEntry(
                        Guid.NewGuid(),
                        "structure.read",
                        Guid.NewGuid(),
                        null,
                        null,
                        null,
                        "agent-1",
                        "Agent One",
                        "machine",
                        @"C:\repositories\CanDoItAll",
                        "main",
                        true,
                        12,
                        0,
                        null,
                        null,
                        "{}",
                        "{}",
                        "[]",
                        DateTimeOffset.UtcNow)
                ]))
        }, NullLogger<ProjectStructureTools>.Instance);

        var result = await tools.ProjectStructureAnalyticsQueryAsync(new ProjectStructureAnalyticsQueryRequest(OperationName: "structure.read", Take: 5));

        Assert.True(result.Ok);
        Assert.Single(result.Data!.Entries);
        Assert.Equal("structure.read", result.Data.Entries[0].OperationName);
    }

    private sealed class StubCoordinator : IProjectStructureCoordinator
    {
        public Func<CancellationToken, Task<IReadOnlyList<ProjectSummary>>>? OnListProjects { get; init; }

        public Func<ProjectStructureProjectSaveRequest, int?, CancellationToken, Task<ProjectSummary>>? OnCreateProject { get; init; }

        public Func<Guid, ProjectStructureProjectSaveRequest, int?, CancellationToken, Task<ProjectSummary>>? OnUpdateProject { get; init; }

        public Func<Guid, CancellationToken, Task<ProjectHierarchySnapshot>>? OnGetHierarchy { get; init; }

        public Func<Guid, ProjectStructureSubprojectChangeRequest, int?, CancellationToken, Task<OperationAck>>? OnChangeSubproject { get; init; }

        public Func<Guid, ProjectStructureReadRequest, CancellationToken, Task<ProjectStructureReadToolData>>? OnRead { get; init; }

        public Func<Guid, ProjectStructureChecklistRequest, CancellationToken, Task<ProjectStructureChecklistResponse>>? OnGetChecklist { get; init; }

        public Func<Guid, ProjectStructureNodeCreateInput, int?, CancellationToken, Task<ProjectStructureNodeSummary>>? OnCreateNode { get; init; }

        public Func<Guid, string, ProjectStructureNodeEditInput, int?, CancellationToken, Task<ProjectStructureNodeSummary>>? OnUpdateNode { get; init; }

        public Func<Guid, ProjectStructureApprovalRequestCreateInput, CancellationToken, Task<ProjectStructureNodeSummary>>? OnCreateApprovalRequest { get; init; }

        public Func<Guid, string, CancellationToken, Task<ProjectStructureAssetDescriptor>>? OnGetAsset { get; init; }

        public Func<Guid, string, ProjectStructureAssetRevisionRequest, int?, CancellationToken, Task<ProjectStructureAssetDescriptor>>? OnCreateAssetRevision { get; init; }

        public Func<ProjectStructureImportRequest, int?, CancellationToken, Task<ProjectStructureImportResult>>? OnImport { get; init; }

        public Func<ProjectManagementGuidanceQueryRequest, CancellationToken, Task<ProjectManagementGuidanceResponse>>? OnQueryKnowledge { get; init; }

        public Func<ProjectStructureAnalyticsQueryRequest, CancellationToken, Task<ProjectStructureAnalyticsResponse>>? OnQueryAnalytics { get; init; }

        public Func<ProjectStructureScopeInput, string, int, CancellationToken, Task<ProjectStructureLeaseSnapshot>>? OnAcquireLease { get; init; }

        public Func<ProjectStructureScopeInput, CancellationToken, Task<ProjectStructureLeaseSnapshot?>>? OnGetCurrentLease { get; init; }

        public Func<ProjectStructureScopeInput, string, CancellationToken, Task<ProjectStructureLeaseSnapshot?>>? OnReleaseLease { get; init; }

        public Task<IReadOnlyList<ProjectSummary>> ListProjectsAsync(CancellationToken cancellationToken = default)
        {
            return OnListProjects?.Invoke(cancellationToken) ?? Task.FromResult<IReadOnlyList<ProjectSummary>>([]);
        }

        public Task<ProjectSummary> CreateProjectAsync(ProjectStructureProjectSaveRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
        {
            return OnCreateProject?.Invoke(request, estimatedMinutes, cancellationToken)
                ?? Task.FromResult(new ProjectSummary(Guid.NewGuid(), request.Name, request.Status, request.CurrentPhase, 0, 0, 0, DateTimeOffset.UtcNow));
        }

        public Task<ProjectSummary> UpdateProjectAsync(Guid projectId, ProjectStructureProjectSaveRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
        {
            return OnUpdateProject?.Invoke(projectId, request, estimatedMinutes, cancellationToken)
                ?? Task.FromResult(new ProjectSummary(projectId, request.Name, request.Status, request.CurrentPhase, 0, 0, 0, DateTimeOffset.UtcNow));
        }

        public Task<ProjectHierarchySnapshot> GetHierarchyAsync(Guid projectId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<OperationAck> ChangeSubprojectAsync(Guid parentProjectId, ProjectStructureSubprojectChangeRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProjectStructureReadToolData> ReadAsync(Guid projectId, ProjectStructureReadRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProjectStructureChecklistResponse> GetChecklistAsync(Guid projectId, ProjectStructureChecklistRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProjectStructureNodeSummary> CreateNodeAsync(Guid projectId, ProjectStructureNodeCreateInput request, int? estimatedMinutes, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProjectStructureNodeSummary> UpdateNodeAsync(Guid projectId, string nodeId, ProjectStructureNodeEditInput request, int? estimatedMinutes, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProjectStructureNodeSummary> CreateApprovalRequestAsync(Guid projectId, ProjectStructureApprovalRequestCreateInput request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProjectStructureAssetDescriptor> GetAssetAsync(Guid projectId, string nodeId, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProjectStructureAssetDescriptor> CreateAssetRevisionAsync(Guid projectId, string nodeId, ProjectStructureAssetRevisionRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProjectStructureImportResult> ImportAsync(ProjectStructureImportRequest request, int? estimatedMinutes, CancellationToken cancellationToken = default)
        {
            return OnImport?.Invoke(request, estimatedMinutes, cancellationToken)
                ?? Task.FromResult(new ProjectStructureImportResult(request.ProjectId, "container-1", null, ["container-1"], []));
        }

        public Task<ProjectManagementGuidanceResponse> QueryKnowledgeAsync(ProjectManagementGuidanceQueryRequest request, CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException();
        }

        public Task<ProjectStructureAnalyticsResponse> QueryAnalyticsAsync(ProjectStructureAnalyticsQueryRequest request, CancellationToken cancellationToken = default)
        {
            return OnQueryAnalytics?.Invoke(request, cancellationToken)
                ?? Task.FromResult(new ProjectStructureAnalyticsResponse([]));
        }

        public Task<ProjectStructureLeaseSnapshot> AcquireLeaseAsync(ProjectStructureScopeInput scope, string reason, int durationMinutes, CancellationToken cancellationToken = default)
        {
            return OnAcquireLease?.Invoke(scope, reason, durationMinutes, cancellationToken)
                ?? Task.FromResult(new ProjectStructureLeaseSnapshot(
                    scope.ScopeKind,
                    "scope-key",
                    "lease-token",
                    "agent-1",
                    "Agent",
                    "machine",
                    @"C:\repositories\CanDoItAll",
                    "main",
                    reason,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow,
                    DateTimeOffset.UtcNow.AddMinutes(durationMinutes),
                    true));
        }

        public Task<ProjectStructureLeaseSnapshot?> GetCurrentLeaseAsync(ProjectStructureScopeInput scope, CancellationToken cancellationToken = default)
        {
            return OnGetCurrentLease?.Invoke(scope, cancellationToken) ?? Task.FromResult<ProjectStructureLeaseSnapshot?>(null);
        }

        public Task<ProjectStructureLeaseSnapshot?> ReleaseLeaseAsync(ProjectStructureScopeInput scope, string leaseToken, CancellationToken cancellationToken = default)
        {
            return OnReleaseLease?.Invoke(scope, leaseToken, cancellationToken) ?? Task.FromResult<ProjectStructureLeaseSnapshot?>(null);
        }
    }
}
