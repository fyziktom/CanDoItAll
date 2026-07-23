using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureTaskHttpBoundaryTests
{
    [Fact]
    public async Task Generic_task_create_is_rejected_while_typed_task_create_persists_canonical_task()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-task-http-boundary",
            environment => environment.CreatePostgreSqlProfile("task-http-boundary"));
        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Typed task boundary",
                "HTTP task mutation boundary integration coverage.",
                "Require canonical task mutations to use the typed task API.",
                "Validation",
                ProjectStatus.Active));
        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Validate typed task mutation boundary",
                15));

        var genericResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id:D}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.WorkItem,
                "Rejected generic task",
                "Task",
                string.Empty,
                $"project:{project.Id:D}",
                ObjectSubtype: "task",
                LeaseToken: lease.LeaseToken));

        Assert.Equal(HttpStatusCode.Conflict, genericResponse.StatusCode);
        var error = await ReadAsync<ApiErrorResponse>(genericResponse);
        Assert.Equal(
            ProjectStructureCanonicalTaskMutationPolicy.ErrorCode,
            error.Error.ErrorCode);

        var expectedEstimate = new ProjectTaskEstimate(
            6m,
            ProjectWorkItemEffortUnit.Hours,
            240m,
            "USD");
        var created = await PostAndReadAsync<ProjectStructureTaskCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/tasks",
            new ProjectStructureTaskCreateRequest(
                "Typed manual task",
                DateTimeOffset.Parse("2026-07-23T12:00:00Z"),
                DateTimeOffset.Parse("2026-07-23T18:00:00Z"),
                Resource: null,
                Estimate: expectedEstimate));

        Assert.Null(created.AttachedResource);
        Assert.Equal(
            ProjectStructureTaskEstimateRefreshStatus.Preserved,
            created.Pricing.Status);
        Assert.Equal(
            ProjectStructureTaskEstimateRefreshReason.NoResourceSelected,
            created.Pricing.Reason);
        Assert.Equal(expectedEstimate, created.Pricing.Estimate);
        Assert.Null(created.Pricing.CalculatedCostBasis);
        Assert.False(created.Pricing.ReplacesCostBasis);

        var structure = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/structure/read",
            new ProjectStructureReadRequest(
                IncludeMetadata: true,
                IncludeNotes: true));
        var task = Assert.Single(
            structure.Nodes,
            node => node.ObjectType == ProjectObjectType.WorkItem &&
                string.Equals(node.ObjectSubtype, "task", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(created.TaskNodeId, task.Id);
        Assert.Equal(created.BacklogNodeId, task.ParentId);

        var workItem = Assert.IsType<ProjectWorkItemMetadata>(
            ProjectObjectMetadataSerializer.Parse(task.MetadataJson).WorkItem);
        Assert.Equal(ProjectWorkItemKind.Task, workItem.WorkItemKind);
        Assert.Equal(ProjectTaskExecutionState.NotStarted, workItem.ExecutionState);
        Assert.Null(workItem.ActualStartedAtUtc);
        Assert.Null(workItem.ActualEndedAtUtc);
        Assert.Equal(expectedEstimate.ExpectedEffortHours, workItem.ExpectedEffortHours);
        Assert.Equal(expectedEstimate.ExpectedEffortUnit, workItem.ExpectedEffortUnit);
        Assert.Equal(expectedEstimate.ExpectedCostAmount, workItem.ExpectedCostAmount);
        Assert.Equal(
            expectedEstimate.ExpectedCostCurrencyCode,
            workItem.ExpectedCostCurrencyCode);
        Assert.Null(workItem.ExpectedCostBasis);
    }

    private static async Task<T> PostAndReadAsync<T>(
        HttpClient client,
        string path,
        object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException(
                $"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {body}");
        }

        return await ReadAsync<T>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
        => await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException(
                $"No {typeof(T).Name} payload was returned.");

    private sealed record ApiErrorResponse(ApiError Error);

    private sealed record ApiError(
        string ErrorCode,
        string Message,
        JsonElement? Details);
}
