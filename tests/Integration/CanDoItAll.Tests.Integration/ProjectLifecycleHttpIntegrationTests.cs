using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Api;

[Trait("Category", "HostPlatform")]
public sealed class ProjectLifecycleHttpIntegrationTests {
    private const string SubmitPath = "/api/projects/lifecycle-operations";
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) {
        Converters = { new ProjectOperationEpochHttpJsonConverter() }
    };

    [Fact]
    public async Task Deleted_target_keeps_original_receipt_for_exact_HTTP_lookup_and_replay() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var project = await CreateAsync(host.Client, "Delete once");
        var context = await ContextAsync(host.Client);
        var original = Request(context, new ProjectLifecycleApiCommand.Delete(project.Definition));
        var accepted = Assert.IsType<ProjectLifecycleApiResponse.Accepted>(await SubmitAsync(host.Client, original, HttpStatusCode.OK));
        Assert.False(accepted.Replayed);
        var deletion = Assert.IsType<ProjectLifecycleApiAcknowledgement.DeletionCommitted>(accepted.Receipt.Acknowledgement);
        Assert.Equal(original.Operation.DatabaseProfileId, deletion.Completion.Original.Completion.DatabaseProfileId);
        Assert.Equal(original.Operation.Namespace, deletion.Completion.Original.Completion.Namespace);
        Assert.Equal(project.Project, deletion.Completion.Original.Project);
        using (var gone = await host.Client.GetAsync($"/api/projects/{project.Project.ProjectId:D}/definition")) {
            Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
        }
        using var lookup = await host.Client.GetAsync(Lookup(original.Operation));
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        var found = Assert.IsType<ProjectLifecycleApiResponse.Found>(await lookup.Content.ReadFromJsonAsync<ProjectLifecycleApiResponse>(Json));
        Assert.Equal(Serialize(accepted.Receipt), Serialize(found.Receipt));
        var replay = Assert.IsType<ProjectLifecycleApiResponse.Accepted>(await SubmitAsync(host.Client, original, HttpStatusCode.OK));
        Assert.True(replay.Replayed);
        Assert.Equal(Serialize(accepted.Receipt), Serialize(replay.Receipt));
        var altered = original with { Command = new ProjectLifecycleApiCommand.Delete(new(project.Project, Guid.NewGuid())) };
        var refused = Assert.IsType<ProjectLifecycleApiResponse.Refused>(await SubmitAsync(host.Client, altered, HttpStatusCode.Conflict));
        Assert.Equal(ProjectLifecycleRefusal.IntentMismatch, refused.Reason);
    }

    [Fact]
    public async Task Stale_HTTP_delete_preserves_newer_definition_and_does_not_return_a_deletion_receipt() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var project = await CreateAsync(host.Client, "Original deletion view");
        var original = Request(await ContextAsync(host.Client), new ProjectLifecycleApiCommand.Delete(project.Definition));
        var definitionContext = (await host.Client.GetFromJsonAsync<ProjectDefinitionWriteContext>("/api/projects/definition-write-context", Json))!;
        var replacement = new ProjectDefinitionRequest<UpdateProjectDefinitionScalarsCommand>(
            new(definitionContext.Namespace, definitionContext.Epoch, Guid.NewGuid(), definitionContext.CallerBinding),
            ProjectDefinitionRequestVersion.VersionOne, new(project.Definition, new("Concurrent edit survives", "", "", ProjectStatus.Draft, "", null)));
        using var changed = await host.Client.PutAsJsonAsync($"/api/projects/{project.Project.ProjectId:D}/definition/scalars", replacement, Json);
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var rejected = Assert.IsType<ProjectLifecycleApiResponse.Refused>(await SubmitAsync(host.Client, original, HttpStatusCode.Conflict));
        Assert.Equal(ProjectLifecycleRefusal.TargetChanged, rejected.Reason);
        Assert.Equal(Serialize(original), Serialize(rejected.Original));
        var current = (await host.Client.GetFromJsonAsync<ProjectDefinitionReadApiResponse>($"/api/projects/{project.Project.ProjectId:D}/definition", Json))!;
        Assert.Equal("Concurrent edit survives", current.Scalars.Name);
        Assert.NotEqual(project.Definition.Revision, current.Expectation.Revision);
        using var lookup = await host.Client.GetAsync(Lookup(original.Operation));
        Assert.Equal(HttpStatusCode.OK, lookup.StatusCode);
        Assert.IsType<ProjectLifecycleApiResponse.NotObserved>(await lookup.Content.ReadFromJsonAsync<ProjectLifecycleApiResponse>(Json));
    }

    [Fact]
    public async Task Removing_original_edge_over_HTTP_does_not_remove_an_endpoint_matching_replacement() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var parent = await CreateAsync(host.Client, "Parent");
        var child = await CreateAsync(host.Client, "Child");
        var context = await ContextAsync(host.Client);
        var ensure = new ProjectLifecycleApiCommand.EnsureLink(parent.Project, child.Project);
        var first = Assert.IsType<ProjectLifecycleApiResponse.Accepted>(await SubmitAsync(host.Client, Request(context, ensure), HttpStatusCode.OK));
        var firstEdge = Assert.IsType<ProjectLifecycleApiAcknowledgement.LinkEnsured>(first.Receipt.Acknowledgement);
        Assert.True(firstEdge.Created);
        var removeCommand = new ProjectLifecycleApiCommand.RemoveLink(new(firstEdge.EdgeId, parent.Project, child.Project));
        var stale = Request(context, removeCommand);
        var removal = Request(context, removeCommand);
        var removed = Assert.IsType<ProjectLifecycleApiResponse.Accepted>(await SubmitAsync(host.Client, removal, HttpStatusCode.OK));
        Assert.IsType<ProjectLifecycleApiAcknowledgement.LinkRemoved>(removed.Receipt.Acknowledgement);
        var replacement = Assert.IsType<ProjectLifecycleApiResponse.Accepted>(await SubmitAsync(host.Client, Request(context, ensure), HttpStatusCode.OK));
        var newEdge = Assert.IsType<ProjectLifecycleApiAcknowledgement.LinkEnsured>(replacement.Receipt.Acknowledgement);
        Assert.True(newEdge.Created);
        Assert.NotEqual(firstEdge.EdgeId, newEdge.EdgeId);
        var rejected = Assert.IsType<ProjectLifecycleApiResponse.Refused>(await SubmitAsync(host.Client, stale, HttpStatusCode.Conflict));
        Assert.Equal(ProjectLifecycleRefusal.EdgeChanged, rejected.Reason);
        var replay = Assert.IsType<ProjectLifecycleApiResponse.Accepted>(await SubmitAsync(host.Client, removal, HttpStatusCode.OK));
        Assert.True(replay.Replayed);
        await using var scope = host.App.Services.CreateAsyncScope();
        await using var database = await scope.ServiceProvider.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
        var surviving = await database.Set<ProjectHierarchyLink>().AsNoTracking().SingleAsync(link => link.ParentProjectId == parent.Project.ProjectId && link.ChildProjectId == child.Project.ProjectId);
        Assert.Equal(newEdge.EdgeId, surviving.Id);
    }

    public enum LegacyRoute { Delete, Attach, Detach, Reconnect }

    [Fact]
    public async Task OpenAPI_exposes_original_operation_command_and_actual_lifecycle_failure_shapes() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, useInMemoryDatabase: true);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        var submit = root.GetProperty("paths").GetProperty(SubmitPath).GetProperty("post");
        var request = OpenApiSchemaAssertions.Resolve(root, submit.GetProperty("requestBody").GetProperty("content").GetProperty("application/json").GetProperty("schema"));
        var properties = request.GetProperty("properties");
        Assert.Equal(new[] { "command", "interpretation", "operation" }, properties.EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal));
        var operation = OpenApiSchemaAssertions.Resolve(root, properties.GetProperty("operation"));
        Assert.Equal(new[] { "callerBinding", "databaseProfileId", "epoch", "id", "namespace" }, operation.GetProperty("properties").EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal));
        var epoch = OpenApiSchemaAssertions.Resolve(root, operation.GetProperty("properties").GetProperty("epoch"));
        Assert.Equal("string", epoch.GetProperty("type").GetString());
        foreach (var status in new[] { "200", "400", "403", "409", "410", "413", "415", "503" }) {
            Assert.True(submit.GetProperty("responses").TryGetProperty(status, out _));
        }
        foreach (var status in new[] { "400", "403" }) {
            var schema = OpenApiSchemaAssertions.Resolve(root, submit.GetProperty("responses").GetProperty(status).GetProperty("content").GetProperty("application/json").GetProperty("schema"));
            Assert.Contains(schema.GetProperty("anyOf").EnumerateArray(), branch => OpenApiSchemaAssertions.Resolve(root, branch).TryGetProperty("properties", out var values) && values.TryGetProperty("errors", out _));
        }
        var legacy = root.GetProperty("paths").GetProperty("/api/projects/{projectId}").GetProperty("delete").GetProperty("responses");
        Assert.True(legacy.TryGetProperty("428", out _));
        Assert.False(legacy.TryGetProperty("200", out _));
    }

    [Theory]
    [InlineData(LegacyRoute.Delete)]
    [InlineData(LegacyRoute.Attach)]
    [InlineData(LegacyRoute.Detach)]
    [InlineData(LegacyRoute.Reconnect)]
    public async Task Identity_only_HTTP_routes_require_original_intent_without_mutating_the_project(LegacyRoute route) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var project = await CreateAsync(host.Client, "Legacy route must refuse");
        var id = project.Project.ProjectId;
        var other = Guid.NewGuid();
        var (method, path) = route switch {
            LegacyRoute.Delete => (HttpMethod.Delete, $"/api/projects/{id:D}"),
            LegacyRoute.Attach => (HttpMethod.Post, $"/api/projects/{id:D}/subprojects/{other:D}"),
            LegacyRoute.Detach => (HttpMethod.Delete, $"/api/projects/{id:D}/subprojects/{other:D}"),
            LegacyRoute.Reconnect => (HttpMethod.Post, $"/api/projects/{id:D}/reconnect-subproject"),
            _ => throw new ArgumentOutOfRangeException(nameof(route))
        };
        using var request = new HttpRequestMessage(method, path);
        using var response = await host.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.PreconditionRequired, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>(Json);
        Assert.Contains(error.GetProperty("errors").EnumerateArray(), value => value.GetProperty("code").GetString() == ProjectLifecycleApi.OriginalIntentRequiredCode);
        var current = (await host.Client.GetFromJsonAsync<ProjectDefinitionReadApiResponse>($"/api/projects/{id:D}/definition", Json))!;
        Assert.Equal(project.Definition, current.Expectation);
    }

    private static async Task<ProjectEditorAcknowledgement> CreateAsync(HttpClient client, string name) {
        var context = (await client.GetFromJsonAsync<ProjectDefinitionWriteContext>("/api/projects/definition-write-context", Json))!;
        var original = new ProjectDefinitionRequest<CreateProjectDefinitionCommand>(new(context.Namespace, context.Epoch, Guid.NewGuid(), context.CallerBinding),
            ProjectDefinitionRequestVersion.VersionOne, new(new(name, "", "", ProjectStatus.Draft, "", null), [], [], null));
        using var response = await client.PostAsJsonAsync("/api/projects/definitions", original, Json);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectDefinitionAcceptedApiResponse<ProjectEditorAcknowledgement>>(Json))!.Receipt.Acknowledgement;
    }

    private static async Task<ProjectLifecycleWriteContext> ContextAsync(HttpClient client)
        => (await client.GetFromJsonAsync<ProjectLifecycleWriteContext>("/api/projects/lifecycle-write-context", Json))!;

    private static ProjectLifecycleApiSubmission Request(ProjectLifecycleWriteContext context, ProjectLifecycleApiCommand command)
        => new(new(context.DatabaseProfileId, context.Namespace, context.Epoch, context.CallerBinding, Guid.NewGuid()), ProjectLifecycleInterpretation.VersionOne, command);

    private static async Task<ProjectLifecycleApiResponse> SubmitAsync(HttpClient client, ProjectLifecycleApiSubmission original, HttpStatusCode expected) {
        using var response = await client.PostAsJsonAsync(SubmitPath, original, Json);
        Assert.Equal(expected, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ProjectLifecycleApiResponse>(Json))!;
    }

    private static string Lookup(ProjectLifecycleOperation original)
        => $"{SubmitPath}/{original.Namespace.Value:D}/{original.Epoch.Value}/{original.Id:D}?databaseProfileId={original.DatabaseProfileId:D}&callerBinding={Uri.EscapeDataString(original.CallerBinding.Value)}";

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, Json);
}
