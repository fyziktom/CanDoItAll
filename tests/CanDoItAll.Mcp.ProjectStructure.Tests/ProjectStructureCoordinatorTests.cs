using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.Mcp.Core.Contracts;
using CanDoItAll.Mcp.Core.Identity;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.ProjectStructure.Tests;

public sealed class ProjectStructureCoordinatorTests
{
    [Fact]
    public async Task ReadAsync_posts_compact_defaults_when_request_is_omitted()
    {
        var projectId = Guid.NewGuid();
        var capturedBody = string.Empty;
        var handler = new DelegateHttpMessageHandler(async request =>
        {
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(new ProjectStructureReadResponse(
                projectId,
                "Planning project",
                [
                    new ProjectStructureNodeSummary(
                        "node-1",
                        null,
                        ProjectObjectType.ProjectBlock,
                        "delivery",
                        "Delivery",
                        "Plan",
                        "todo",
                        null,
                        "/projects/test",
                        "project-structure-node",
                        null,
                        null,
                        null,
                        null,
                        [],
                        "percent",
                        0,
                        "dot",
                        "info",
                        "Open",
                        0,
                        0,
                        null,
                        null,
                        null,
                        ProjectStructureProjectRole.ActiveProject,
                        null,
                        0,
                        null,
                        null)
                ],
                [],
                []));
        });

        var coordinator = CreateCoordinator(handler);
        var result = await coordinator.ReadAsync(projectId, new ProjectStructureReadRequest());

        using var document = JsonDocument.Parse(capturedBody);
        Assert.False(document.RootElement.GetProperty("includeNotes").GetBoolean());
        Assert.False(document.RootElement.GetProperty("includeAssets").GetBoolean());
        Assert.False(document.RootElement.GetProperty("includeMetadata").GetBoolean());
        Assert.False(document.RootElement.GetProperty("includeLayout").GetBoolean());
        Assert.Single(result.Nodes);
        Assert.Null(result.Nodes[0].Notes);
        Assert.Null(result.Nodes[0].MediaOriginalFileName);
        Assert.Null(result.Nodes[0].X);
    }

    [Fact]
    public async Task ImportAsync_posts_to_import_route_and_returns_result()
    {
        var projectId = Guid.NewGuid();
        string? capturedPath = null;
        var handler = new DelegateHttpMessageHandler(request =>
        {
            capturedPath = request.RequestUri?.ToString();
            return Task.FromResult(JsonResponse(new ProjectStructureImportResult(
                projectId,
                "container-1",
                "source-1",
                ["container-1", "child-1"],
                ["Mermaid indentation normalized."])));
        });

        var coordinator = CreateCoordinator(handler);
        var result = await coordinator.ImportAsync(
            new ProjectStructureImportRequest(
                projectId,
                null,
                ProjectStructureImportSourceKind.Mermaid,
                "Imported plan",
                """
                mindmap
                  Root
                    Delivery
                """),
            estimatedMinutes: null);

        Assert.Equal("/api/project-structure-mcp/imports", new Uri(capturedPath!, UriKind.Absolute).AbsolutePath);
        Assert.Equal("container-1", result.ContainerNodeId);
        Assert.Contains("Mermaid indentation normalized.", result.Warnings);
    }

    [Fact]
    public async Task ReparentNodeAsync_posts_to_reparent_route_and_returns_node_summary()
    {
        var projectId = Guid.NewGuid();
        string? capturedPath = null;
        string? capturedBody = null;
        var handler = new DelegateHttpMessageHandler(async request =>
        {
            capturedPath = request.RequestUri?.ToString();
            capturedBody = await request.Content!.ReadAsStringAsync();
            return JsonResponse(new ProjectStructureNodeSummary(
                "node-1",
                "parent-1",
                ProjectObjectType.ProjectBlock,
                "feature",
                "Child node",
                "Execution",
                "Draft",
                "Reparented through the coordinator.",
                "/projects/test",
                "project-structure-node",
                null,
                null,
                null,
                null,
                [],
                "percent",
                0,
                "dot",
                "info",
                "Open",
                0,
                0,
                null,
                null,
                null,
                ProjectStructureProjectRole.None,
                null,
                0,
                640,
                360));
        });

        var coordinator = CreateCoordinator(handler);
        var result = await coordinator.ReparentNodeAsync(
            projectId,
            new ProjectStructureNodeReparentInput("node-1", "parent-1"),
            estimatedMinutes: 15);

        using var document = JsonDocument.Parse(capturedBody!);
        Assert.Equal("/api/project-structure-mcp/projects/" + projectId + "/nodes/reparent", new Uri(capturedPath!, UriKind.Absolute).AbsolutePath);
        Assert.Equal("node-1", document.RootElement.GetProperty("nodeId").GetString());
        Assert.Equal("parent-1", document.RootElement.GetProperty("parentNodeKey").GetString());
        Assert.Equal("parent-1", result.ParentId);
    }

    [Fact]
    public async Task CreateProjectAsync_maps_remote_error_envelope_to_tool_invocation_exception()
    {
        var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent(
                """{"error":{"errorCode":"ApprovalRequired","message":"Approval is required.","details":{"threshold":60}}}""",
                Encoding.UTF8,
                "application/json")
        }));

        var coordinator = CreateCoordinator(handler);
        var exception = await Assert.ThrowsAsync<ToolInvocationException>(() => coordinator.CreateProjectAsync(
            new ProjectStructureProjectSaveRequest(
                "Blocked",
                "Needs approval",
                "Exercise deterministic error mapping.",
                "Planning"),
            estimatedMinutes: 90));

        Assert.Equal("ApprovalRequired", exception.Code);
        Assert.Equal("Approval is required.", exception.Message);
    }

    [Fact]
    public async Task GetCurrentLeaseAsync_returns_null_when_api_returns_no_content()
    {
        var handler = new DelegateHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.NoContent)));

        var coordinator = CreateCoordinator(handler);
        var result = await coordinator.GetCurrentLeaseAsync(new ProjectStructureScopeInput(
            ProjectStructureLeaseScopeKind.Project,
            ProjectId: Guid.NewGuid()));

        Assert.Null(result);
    }

    [Fact]
    public async Task QueryAnalyticsAsync_posts_to_analytics_route_and_returns_entries()
    {
        string? capturedPath = null;
        var handler = new DelegateHttpMessageHandler(request =>
        {
            capturedPath = request.RequestUri?.ToString();
            return Task.FromResult(JsonResponse(new ProjectStructureAnalyticsResponse(
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
                        "tests/project-structure",
                        true,
                        18,
                        0,
                        null,
                        null,
                        "{}",
                        "{}",
                        "[]",
                        DateTimeOffset.UtcNow)
                ])));
        });

        var coordinator = CreateCoordinator(handler);
        var result = await coordinator.QueryAnalyticsAsync(new ProjectStructureAnalyticsQueryRequest(OperationName: "structure.read", Take: 5));

        Assert.Equal("/api/project-structure-mcp/analytics/query", new Uri(capturedPath!, UriKind.Absolute).AbsolutePath);
        Assert.Single(result.Entries);
        Assert.Equal("structure.read", result.Entries[0].OperationName);
    }

    private static ProjectStructureCoordinator CreateCoordinator(HttpMessageHandler handler)
    {
        var httpClient = new HttpClient(handler);
        var runtime = new RuntimeConfiguration(
            Options.Create(new McpServerOptions
            {
                Server = new ServerOptions
                {
                    BaseUrl = "http://127.0.0.1:6001",
                    AgentToken = "test-token",
                    AgentName = "Project Structure Test Agent",
                    RepositoryRoot = @"C:\repositories\CanDoItAll",
                    BranchName = "tests/project-structure"
                }
            }),
            new ServerInstanceIdentity());

        var apiClient = new ProjectStructureHttpClient(httpClient, runtime, NullLogger<ProjectStructureHttpClient>.Instance);
        return new ProjectStructureCoordinator(apiClient, runtime);
    }

    private static HttpResponseMessage JsonResponse<T>(T payload)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
    }

    private sealed class DelegateHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> callback) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return callback(request);
        }
    }
}
