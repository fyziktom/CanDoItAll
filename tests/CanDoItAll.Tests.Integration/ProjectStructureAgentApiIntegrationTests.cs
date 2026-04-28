using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureAgentApiIntegrationTests
{
    [Fact]
    public async Task ProjectStructureAgentApi_supports_delivery_block_asset_roundtrip_and_records_analytics()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure-mcp/projects",
            new ProjectStructureProjectSaveRequest(
                "API project",
                "HTTP roundtrip validation",
                "Create and read project structure over the central API.",
                "Execution",
                ProjectStatus.Active));

        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure-mcp/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Create delivery assets",
                15));

        var deliveryBlock = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                "Delivery block",
                "Validation",
                "Root delivery work for API validation.",
                $"project:{project.Id}",
                420,
                240,
                null,
                null,
                "delivery",
                null,
                null,
                lease.LeaseToken));

        var excelAsset = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.File,
                "Delivery workbook",
                "Excel evidence",
                "Create an Excel asset through the API.",
                deliveryBlock.Id,
                620,
                360,
                null,
                null,
                "excel",
                CreateMediaPayload("delivery-workbook.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "excel payload"),
                null,
                lease.LeaseToken));

        var pdfAsset = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.File,
                "Delivery packet",
                "PDF evidence",
                "Create a PDF asset through the API.",
                deliveryBlock.Id,
                760,
                360,
                null,
                null,
                "pdf",
                CreateMediaPayload("delivery-packet.pdf", "application/pdf", "%PDF-1.4 payload"),
                null,
                lease.LeaseToken));

        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeAssets: true,
                IncludeNotes: true,
                IncludeMetadata: true));

        Assert.Contains(readback.Nodes, node => node.Id == deliveryBlock.Id && node.Title == "Delivery block");
        Assert.Contains(readback.Nodes, node => node.Id == excelAsset.Id && node.MediaOriginalFileName == "delivery-workbook.xlsx");
        Assert.Contains(readback.Nodes, node => node.Id == pdfAsset.Id && node.MediaOriginalFileName == "delivery-packet.pdf");
        Assert.Contains(readback.Links, link => link.SourceId == deliveryBlock.Id && link.TargetId == excelAsset.Id);
        Assert.Contains(readback.Links, link => link.SourceId == deliveryBlock.Id && link.TargetId == pdfAsset.Id);

        var analytics = await PostAndReadAsync<ProjectStructureAnalyticsResponse>(
            host.Client,
            "/api/project-structure-mcp/analytics/query",
            new ProjectStructureAnalyticsQueryRequest(project.Id, Take: 20));

        Assert.Contains(analytics.Entries, entry => entry.OperationName == "projects.create" && entry.Succeeded);
        Assert.Contains(analytics.Entries, entry => entry.OperationName == "structure.node-create" && entry.Succeeded);
        Assert.Contains(analytics.Entries, entry => entry.OperationName == "structure.read" && entry.Succeeded);
    }

    [Fact]
    public async Task ProjectStructureAgentApi_returns_actionable_lease_conflicts()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();
        var scopeKey = $"repo-branch:{IntegrationTestPaths.RepositoryRoot}:main";
        var expectedScopeKey = scopeKey.Replace('\\', '/').ToLowerInvariant();

        await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure-mcp/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.RepoBranch,
                scopeKey,
                "Primary branch mutation",
                15));

        using var competingClient = CreateClientForAgent(host.Client.BaseAddress!, "other-agent", "Other Agent", "other-machine");
        var token = host.Client.DefaultRequestHeaders.GetValues(ProjectStructureAgentHttpHeaders.AgentToken).Single();
        competingClient.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentToken, token);
        var response = await competingClient.PostAsJsonAsync(
            "/api/project-structure-mcp/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.RepoBranch,
                scopeKey,
                "Competing branch mutation",
                15));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var payload = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var details = payload.RootElement
            .GetProperty("error")
            .GetProperty("details");
        Assert.Equal(expectedScopeKey, details.GetProperty("scopeKey").GetString());
        Assert.Equal("api-test-agent", details.GetProperty("agentId").GetString());
        Assert.Equal("API Test Agent", details.GetProperty("agentName").GetString());
        Assert.Equal("api-test-machine", details.GetProperty("machineName").GetString());
    }

    [Fact]
    public async Task ProjectStructureAgentApi_queries_dependency_readiness()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure-mcp/projects",
            new ProjectStructureProjectSaveRequest(
                "Dependency API project",
                "HTTP dependency validation",
                "Query dependency readiness over the central API.",
                "Execution",
                ProjectStatus.Active));

        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure-mcp/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Create dependency graph",
                15));

        var note = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Note,
                "Architect note",
                string.Empty,
                "A top-level note dependency.",
                $"project:{project.Id}",
                360,
                220,
                null,
                null,
                null,
                null,
                null,
                lease.LeaseToken));

        var task = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.WorkItem,
                "Implement feature",
                string.Empty,
                "Blocked until the note is completed.",
                $"project:{project.Id}",
                620,
                340,
                new DateTimeOffset(2026, 4, 3, 8, 0, 0, TimeSpan.Zero),
                null,
                "task",
                null,
                null,
                lease.LeaseToken,
                7200));

        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
            await workbench.LinkObjectsAsync(project.Id, task.Id, note.Id, ProjectObjectLinkKind.DependsOn);
        }

        var dependencies = await PostAndReadAsync<ProjectStructureDependencyResponse>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/dependencies/query",
            new ProjectStructureDependencyQueryRequest(DefaultDurationSeconds: 5400));

        var noteItem = Assert.Single(dependencies.Items, item => item.NodeId == note.Id);
        var taskItem = Assert.Single(dependencies.Items, item => item.NodeId == task.Id);

        Assert.Equal(5400, dependencies.DefaultDurationSeconds);
        Assert.True(noteItem.CanExecute);
        Assert.False(taskItem.CanExecute);
        Assert.Equal(7200, taskItem.DurationSeconds);
        Assert.Contains(taskItem.Prerequisites, prerequisite => prerequisite.NodeId == note.Id && prerequisite.Reason == "depends-on");
    }

    [Fact]
    public async Task ProjectStructureAgentApi_accepts_typed_block_aliases_and_node_move_requests()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync();

        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure-mcp/projects",
            new ProjectStructureProjectSaveRequest(
                "Alias API project",
                "HTTP alias validation",
                "Accept typed block aliases and move requests over the central API.",
                "Execution",
                ProjectStatus.Active));

        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure-mcp/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Alias and move validation",
                15));

        var placeholder = await PostAndReadAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Note,
                "Features",
                "Scratch",
                "Placeholder note for reclassification.",
                $"project:{project.Id}",
                420,
                220,
                null,
                null,
                null,
                null,
                null,
                lease.LeaseToken));

        var updateResponse = await PutAndReadJsonAsync<ProjectStructureNodeSummary>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/nodes/{placeholder.Id}",
            """
            {
              "title": "Features",
              "subtitle": "Feature area",
              "notes": "Promoted into a typed feature block through an alias payload.",
              "objectType": "FeatureBlock",
              "leaseToken": "__LEASE__"
            }
            """.Replace("__LEASE__", lease.LeaseToken, StringComparison.Ordinal));

        Assert.Equal(ProjectObjectType.ProjectBlock, updateResponse.ObjectType);
        Assert.Equal("feature", updateResponse.ObjectSubtype);

        var moveAck = await PostAndReadAsync<OperationAck>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/nodes/move",
            new ProjectStructureNodeMoveInput(placeholder.Id, 1040, 560, lease.LeaseToken));

        Assert.True(moveAck.Ok);

        var readback = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure-mcp/projects/{project.Id}/structure/read",
            new ProjectStructureReadRequest(
                NodeIds: [placeholder.Id],
                IncludeLayout: true,
                IncludeNotes: true,
                IncludeMetadata: true));

        var movedNode = Assert.Single(readback.Nodes);
        Assert.Equal(ProjectObjectType.ProjectBlock, movedNode.ObjectType);
        Assert.Equal("feature", movedNode.ObjectSubtype);
        Assert.Equal(1040d, movedNode.X);
        Assert.Equal(560d, movedNode.Y);
    }

    private static async Task<T> PostAndReadAsync<T>(HttpClient client, string path, object request)
    {
        var response = await client.PostAsJsonAsync(path, request);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>();
        return payload ?? throw new InvalidOperationException($"No payload was returned for '{path}'.");
    }

    private static async Task<T> PutAndReadJsonAsync<T>(HttpClient client, string path, string json)
    {
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await client.PutAsync(path, content);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync();
            throw new HttpRequestException($"Response status code does not indicate success: {(int)response.StatusCode} ({response.StatusCode}). Body: {errorBody}");
        }

        var payload = await response.Content.ReadFromJsonAsync<T>();
        return payload ?? throw new InvalidOperationException($"No payload was returned for '{path}'.");
    }

    private static ProjectObjectMediaPayload CreateMediaPayload(string fileName, string contentType, string textContent)
    {
        return new ProjectObjectMediaPayload(
            fileName,
            contentType,
            Convert.ToBase64String(Encoding.UTF8.GetBytes(textContent)));
    }

    private static HttpClient CreateClientForAgent(Uri baseAddress, string agentId, string agentName, string machineName)
    {
        var client = new HttpClient
        {
            BaseAddress = baseAddress,
            Timeout = TimeSpan.FromSeconds(30)
        };
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentId, agentId);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.AgentName, agentName);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.MachineName, machineName);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.RepositoryRoot, IntegrationTestPaths.RepositoryRoot);
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.BranchName, "tests/project-structure");
        client.DefaultRequestHeaders.Add(ProjectStructureAgentHttpHeaders.SessionId, Guid.NewGuid().ToString("N"));
        return client;
    }
}
