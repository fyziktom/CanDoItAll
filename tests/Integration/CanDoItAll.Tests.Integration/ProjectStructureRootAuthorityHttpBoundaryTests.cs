using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureRootAuthorityHttpBoundaryTests
{
    [Fact]
    public async Task Node_create_rejects_an_external_project_block_root_without_an_audited_execution()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-root-authority-http-boundary",
            environment => environment.CreatePostgreSqlProfile("root-authority-http-boundary"));
        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "ProjectBlock root authority",
                "HTTP agent requests cannot mint external root authority.",
                "Keep external roots bound to an audited execution.",
                "Validation",
                ProjectStatus.Active));
        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Validate ProjectBlock root authority",
                15));

        var response = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id:D}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.ProjectBlock,
                "Rejected external root",
                "Implementation",
                "This HTTP request has no audited external-target scope.",
                $"project:{project.Id:D}",
                ObjectSubtype: "implementation",
                MetadataJson: CreateProjectBlockMetadata(
                    @"C:\operator\private\unselected-project"),
                LeaseToken: lease.LeaseToken));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var error = await ReadAsync<ApiErrorResponse>(response);
        Assert.Equal(
            ProjectStructureAgentRootAuthorityWriteGuard.FailureCode,
            error.Error.ErrorCode);

        var structure = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/structure/read",
            new ProjectStructureReadRequest(IncludeMetadata: true));
        Assert.DoesNotContain(
            structure.Nodes,
            node => node.Title == "Rejected external root");
    }

    [Fact]
    public async Task Node_create_rejects_an_external_runtime_target_without_an_audited_execution()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-runtime-authority-http-boundary",
            environment => environment.CreatePostgreSqlProfile("runtime-authority-http-boundary"));
        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Runtime target authority",
                "HTTP agent requests cannot probe arbitrary external runtime roots.",
                "Require audited external-target authority.",
                "Validation",
                ProjectStatus.Active));
        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Validate runtime target authority",
                15));
        var externalRuntimeRoot = Path.Combine(host.RootPath, "external-runtime");
        var externalProjectPath = Path.Combine(externalRuntimeRoot, "Calculator.csproj");
        Directory.CreateDirectory(externalRuntimeRoot);
        await File.WriteAllTextAsync(externalProjectPath, "<Project Sdk=\"Microsoft.NET.Sdk\" />");

        var response = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id:D}/nodes",
            new ProjectStructureNodeCreateInput(
                ProjectObjectType.Environment,
                "Rejected external runtime",
                "dotnet watch",
                "This HTTP request has no audited external-target scope.",
                $"project:{project.Id:D}",
                ObjectSubtype: "dotnet-watch",
                MetadataJson: CreateDotNetRuntimeMetadata(externalProjectPath),
                LeaseToken: lease.LeaseToken));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var error = await ReadAsync<ApiErrorResponse>(response);
        Assert.Equal("InvalidRuntimeMetadata", error.Error.ErrorCode);
        Assert.Contains(
            "not authorized for this agent execution",
            error.Error.Message,
            StringComparison.Ordinal);

        var structure = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/structure/read",
            new ProjectStructureReadRequest(IncludeMetadata: true));
        Assert.DoesNotContain(
            structure.Nodes,
            node => node.Title == "Rejected external runtime");
    }

    [Fact]
    public async Task Graph_mutations_cannot_inherit_authority_from_an_operator_created_external_owner()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-graph-root-authority-http-boundary",
            environment => environment.CreatePostgreSqlProfile("graph-root-authority-http-boundary"));
        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Graph root authority",
                "Graph edits cannot convert stored paths into agent authority.",
                "Keep authority tied to the current audited execution.",
                "Validation",
                ProjectStatus.Active));
        var projectRoot = $"project:{project.Id:D}";

        ProjectStructureNode externalOwner;
        ProjectStructureNode child;
        await using (var scope = host.App.Services.CreateAsyncScope())
        {
            var workbench = scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>();
            externalOwner = await workbench.CreateObjectAsync(
                project.Id,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.ProjectBlock,
                    "Operator-created external owner",
                    "Implementation",
                    "An operator may create this root, but an unaudited agent may not inherit it.",
                    projectRoot,
                    ObjectSubtype: "implementation",
                    MetadataJson: CreateProjectBlockMetadata(
                        @"C:\operator\chosen\external-project")));
            child = await workbench.CreateObjectAsync(
                project.Id,
                new ProjectObjectCreateRequest(
                    ProjectObjectType.Note,
                    "Unaudited child",
                    "Authority boundary",
                    "This node must remain outside the external owner.",
                    projectRoot));
        }

        var lease = await PostAndReadAsync<ProjectStructureLeaseSnapshot>(
            host.Client,
            "/api/project-structure/leases/acquire",
            new ProjectStructureLeaseAcquireRequest(
                ProjectStructureLeaseScopeKind.Project,
                project.Id.ToString(),
                "Validate graph-derived root authority",
                15));

        var reparentResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id:D}/nodes/{child.Id}/reparent",
            new ProjectStructureNodeParentInput(externalOwner.Id, lease.LeaseToken));
        var linkResponse = await host.Client.PostAsJsonAsync(
            $"/api/project-structure/projects/{project.Id:D}/links",
            new ProjectStructureLinkInput(
                externalOwner.Id,
                child.Id,
                ProjectObjectLinkKind.Contains,
                lease.LeaseToken));

        Assert.Equal(HttpStatusCode.Forbidden, reparentResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, linkResponse.StatusCode);
        Assert.Equal(
            ProjectStructureAgentRootAuthorityWriteGuard.FailureCode,
            (await ReadAsync<ApiErrorResponse>(reparentResponse)).Error.ErrorCode);
        Assert.Equal(
            ProjectStructureAgentRootAuthorityWriteGuard.FailureCode,
            (await ReadAsync<ApiErrorResponse>(linkResponse)).Error.ErrorCode);

        var structure = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            $"/api/project-structure/projects/{project.Id:D}/structure/read",
            new ProjectStructureReadRequest(
                IncludeLinks: true,
                IncludeMetadata: true,
                Source: ProjectStructureReadSource.CanonicalCurrent));
        var persistedChild = Assert.Single(
            structure.Nodes,
            node => node.Id == child.Id);
        Assert.Equal(projectRoot, persistedChild.ParentId);
        Assert.DoesNotContain(
            structure.Links,
            link =>
                link.SourceId == externalOwner.Id &&
                link.TargetId == child.Id &&
                link.Kind == ProjectObjectLinkKind.Contains);
    }

    private static string CreateProjectBlockMetadata(string outputRoot)
        => ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            ProjectBlock = new ProjectBlockMetadata
            {
                OutputRoot = outputRoot
            }
        });

    private static string CreateDotNetRuntimeMetadata(string projectPath)
        => ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope
        {
            Environment = new ProjectEnvironmentMetadata
            {
                EnvironmentKind = ProjectEnvironmentKind.DotNetWatch,
                ProjectPath = projectPath,
                WorkingDirectory = Path.GetDirectoryName(projectPath) ?? string.Empty
            }
        });

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
