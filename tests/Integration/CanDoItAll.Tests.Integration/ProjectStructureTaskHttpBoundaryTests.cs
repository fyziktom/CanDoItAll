using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Projections;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureTaskHttpBoundaryTests
{
    private static readonly Guid SoftwareDeliveryDefinitionId =
        ProcessDefinitionCatalogProjectionService.CreateDefinitionId(
            new ProcessDefinitionCatalogItemKey("software-delivery")).Value;

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

    [Fact]
    public async Task Generic_process_link_is_rejected_for_task_while_typed_resource_attach_persists_link()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-task-process-http-boundary",
            environment => environment.CreatePostgreSqlProfile("task-process-http-boundary"),
            services => services.Replace(
                ServiceDescriptor.Singleton<ISecretVault>(new InMemorySecretVault())));
        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Typed task process boundary",
                "HTTP task resource mutation boundary integration coverage.",
                "Attach process definitions without bypassing task pricing invariants.",
                "Validation",
                ProjectStatus.Active));
        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Validate typed task process attachment",
                15));
        var created = await PostAndReadAsync<ProjectStructureTaskCreateResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/tasks",
            new ProjectStructureTaskCreateRequest(
                "Main App",
                DateTimeOffset.Parse("2026-07-25T12:00:00Z"),
                DateTimeOffset.Parse("2026-07-25T20:00:00Z")));

        var genericResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id:D}/nodes/{created.TaskNodeId}/process-definition",
            new ProjectStructureProcessDefinitionLinkInput(
                SoftwareDeliveryDefinitionId,
                lease.LeaseToken));

        Assert.Equal(HttpStatusCode.Conflict, genericResponse.StatusCode);
        var genericError = await ReadAsync<ApiErrorResponse>(genericResponse);
        Assert.Equal(
            ProjectStructureCanonicalTaskMutationPolicy.ErrorCode,
            genericError.Error.ErrorCode);

        var attached = await PostAndReadAsync<ProjectStructureTaskResourceAttachResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/tasks/{created.TaskNodeId}/resource",
            new ProjectStructureTaskResourceAttachRequest(
                new ProjectStructureTaskResourceSelection(
                    ProjectStructureTaskResourceKind.Process,
                    SoftwareDeliveryDefinitionId),
                ProjectTaskExecutionSnapshot.NotStarted));

        Assert.Equal(ProjectStructureTaskResourceKind.Process, attached.Resource.Kind);
        Assert.Equal(SoftwareDeliveryDefinitionId, attached.Resource.ResourceId);

        var started = await PostAndReadAsync<ProjectStructureProcessNodeStartResult>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/nodes/{created.TaskNodeId}/process/start",
            new ProjectStructureProcessNodeStartInput(
                RunHrMatch: false,
                Execute: false,
                IncludeLaunchPlan: true,
                RequestedBy: "integration-test"));

        Assert.NotNull(started.RunId);

        var structure = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/structure/read",
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeMetadata: true));
        var processLink = Assert.Single(structure.Links, link =>
            string.Equals(link.SourceId, created.TaskNodeId, StringComparison.Ordinal) &&
            string.Equals(
                link.TargetId,
                ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(SoftwareDeliveryDefinitionId),
                StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Uses);
        Assert.Equal(
            ProjectStructureProcessNodeKeys.BuildProcessDefinitionNodeKey(SoftwareDeliveryDefinitionId),
            processLink.TargetId);
        var runNodeId = ProjectStructureProcessNodeKeys.BuildProcessRunNodeKey(started.RunId!.Value);
        var runNode = Assert.Single(
            structure.Nodes,
            node => string.Equals(node.Id, runNodeId, StringComparison.Ordinal));
        Assert.Equal(created.TaskNodeId, runNode.ParentId);
        Assert.DoesNotContain(structure.Links, link =>
            link.IsUserAuthored &&
            string.Equals(link.SourceId, created.TaskNodeId, StringComparison.Ordinal) &&
            string.Equals(link.TargetId, runNodeId, StringComparison.Ordinal) &&
            link.Kind == ProjectObjectLinkKind.Uses);
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
        => await response.Content.ReadFromJsonAsync<T>(
                ProjectStructureHttpContractTestJson.SerializerOptions)
            ?? throw new InvalidOperationException(
                $"No {typeof(T).Name} payload was returned.");

    private sealed record ApiErrorResponse(ApiError Error);

    private sealed record ApiError(
        string ErrorCode,
        string Message,
        JsonElement? Details);
}
