using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectStructureReadHttpBoundaryTests
{
    [Fact]
    public async Task Structure_read_enforces_http_source_policy()
    {
        await using var host = await ProjectStructureAgentApiTestHost.CreateAsync(
            "project-structure-read-source-http-boundary",
            environment => environment.CreatePostgreSqlProfile("read-source-http-boundary"));
        var project = await PostAndReadAsync<ProjectSummary>(
            host.Client,
            "/api/project-structure/projects",
            new ProjectStructureProjectSaveRequest(
                "Read source boundary",
                "HTTP reads cannot access invocation-local snapshots.",
                "Fail closed instead of silently switching data sources.",
                "Validation",
                ProjectStatus.Active));
        var path =
            $"/api/project-structure/projects/{project.Id:D}/structure/read";

        var contextDefault = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            path,
            new ProjectStructureReadRequest());
        var canonical = await PostAndReadAsync<ProjectStructureReadResponse>(
            host.Client,
            path,
            new ProjectStructureReadRequest(
                Source: ProjectStructureReadSource.CanonicalCurrent));
        Assert.Equal(project.Id, contextDefault.ProjectId);
        Assert.Equal(project.Id, canonical.ProjectId);

        var snapshotResponse = await host.Client.PostAsJsonAsync(
            path,
            new ProjectStructureReadRequest(
                Source: ProjectStructureReadSource.InvocationSnapshot));
        Assert.Equal(HttpStatusCode.BadRequest, snapshotResponse.StatusCode);
        var snapshotError = await ReadAsync<ApiErrorResponse>(snapshotResponse);
        Assert.Equal(
            "ProjectStructureReadSourceUnavailable",
            snapshotError.Error.ErrorCode);
        Assert.Equal(
            ProjectStructureReadSource.InvocationSnapshot,
            snapshotError.Error.Details?.RequestedSource);
        Assert.Equal(
            ProjectStructureReadSource.CanonicalCurrent,
            snapshotError.Error.Details?.SupportedSource);

        var invalidResponse = await host.Client.PostAsJsonAsync(
            path,
            new ProjectStructureReadRequest(
                Source: (ProjectStructureReadSource)999));
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);
        var invalidError = await ReadAsync<ApiErrorResponse>(invalidResponse);
        Assert.Equal(
            "ProjectStructureReadSourceInvalid",
            invalidError.Error.ErrorCode);
        Assert.Equal(
            (ProjectStructureReadSource)999,
            invalidError.Error.Details?.RequestedSource);
        Assert.Equal(
            ProjectStructureReadSource.CanonicalCurrent,
            invalidError.Error.Details?.SupportedSource);
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
        ProjectStructureReadSourceErrorDetails? Details);

    private sealed record ProjectStructureReadSourceErrorDetails(
        ProjectStructureReadSource RequestedSource,
        ProjectStructureReadSource SupportedSource);
}
