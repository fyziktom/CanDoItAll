using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.Tests.Support;
using CanDoItAll.Tests.Integration.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration;

[Trait("Category", "FileSystemPortability")]
public sealed class ProcessLaunchWorkspaceApiTests {
    private const string DefinitionsRoute = "/api/processes/definitions";
    private const string IntentRoute = "/api/processes/launch/intents/";

    [Fact]
    public async Task Project_reads_use_the_current_overlay_and_do_not_replace_the_global_catalog() {
        await using var host = await ApiTestHost.CreateAsync(false);
        await using var scope = host.App.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var projectId = Guid.NewGuid();
        Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(projectId, new() { Name = "Scoped catalog" })).IsSuccess);
        var key = services.GetRequiredService<ProcessTemplatePackLoader>().Load().Definitions[0].Key;
        var workspace = services.GetRequiredService<ProcessAuthoringWorkspace>();
        var baseline = await workspace.ReadAsync(ProcessWorkspaceShellScope.ForProject(projectId), new(key), default);
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        content.Definition.DisplayName = "Project overlay";
        var saved = await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash("project catalog fixture"),
            content, ProcessAuthoringLifecycle.Draft, false, null, default);
        Assert.Equal(ProcessAuthoringOutcome.Accepted, saved.Outcome);
        using var global = await host.Client.GetFromJsonAsync<JsonDocument>(DefinitionsRoute);
        using var project = await host.Client.GetFromJsonAsync<JsonDocument>($"{DefinitionsRoute}?projectId={projectId:D}&scopeFilter=2");
        Assert.DoesNotContain(global!.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("name").GetString() == content.Definition.DisplayName);
        Assert.Contains(project!.RootElement.GetProperty("items").EnumerateArray(), item => item.GetProperty("name").GetString() == content.Definition.DisplayName);
        foreach (var suffix in new[] { "", "/roles", "/steps" }) {
            using var response = await host.Client.GetAsync($"{DefinitionsRoute}/{Uri.EscapeDataString(key)}{suffix}?projectId={projectId:D}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(projectId, body.RootElement.GetProperty("observation").GetProperty("projectId").GetGuid());
            Assert.Equal(saved.Snapshot!.Revision, body.RootElement.GetProperty("observation").GetProperty("revision").GetInt64());
        }
        await new ProjectLifecycleTestDriver(services).DeleteAsync(projectId);
        foreach (var suffix in new[] { "", $"/{Uri.EscapeDataString(key)}", $"/{Uri.EscapeDataString(key)}/roles", $"/{Uri.EscapeDataString(key)}/steps" }) {
            using var response = await host.Client.GetAsync($"{DefinitionsRoute}{suffix}?projectId={projectId:D}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
    }

    [Fact]
    public async Task Empty_project_scope_is_refused_by_every_definition_read() {
        await using var host = await ApiTestHost.CreateAsync(false);
        foreach (var suffix in new[] { "", "/any-key", "/any-key/roles", "/any-key/steps" }) {
            using var response = await host.Client.GetAsync($"{DefinitionsRoute}{suffix}?projectId={Guid.Empty:D}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Intent_lookup_checks_the_original_caller_without_mutating_or_accepting_the_preparation() {
        await using var host = await ApiTestHost.CreateAsync(true);
        await using var scope = host.App.Services.CreateAsyncScope();
        var authority = await scope.ServiceProvider.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureAuthenticatedAsync(null, "original-owner", DateTimeOffset.UtcNow.AddMinutes(30));
        ProcessLaunchIntentId intent = new(Guid.NewGuid());
        var preparation = ProcessPreparedLaunchFixture.Create(authority, intent);
        var store = scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>();
        var saved = await store.PrepareAsync(preparation);
        var route = IntentRoute + intent.Value.ToString("D");
        using var unauthenticated = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProjects]);
        using var unrelated = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, unrelated.StatusCode);
        ProcessPlanningReviewHttpAccessTests.UseToken(host, [ApiAccessScopeNames.ReadProcesses]);
        using var otherCaller = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.Forbidden, otherCaller.StatusCode);
        var token = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "original-owner", Scopes = [ApiAccessScopeNames.ReadProcesses], LifetimeMinutes = 30
        });
        host.Client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        using var response = await host.Client.GetAsync(route);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var observed = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(preparation.AdmissionId.Value, observed.RootElement.GetProperty("admissionId").GetGuid());
        Assert.Equal(JsonValueKind.Null, observed.RootElement.GetProperty("acceptedRunId").ValueKind);
        Assert.Equal("Prepared", observed.RootElement.GetProperty("continuationState").GetString());
        var unchanged = (await store.FindByIntentAsync(intent))!;
        Assert.Equal(saved.PreparationFingerprint, unchanged.PreparationFingerprint);
        Assert.Equal(saved.State, unchanged.State);
        Assert.Equal(saved.AdmissionSequence, unchanged.AdmissionSequence);
        Assert.Null(unchanged.AcceptedAtUtc);
        Assert.Null(unchanged.Execute);
        using var forbiddenLaunch = await host.Client.PostAsJsonAsync("/api/processes/launch/check", new { });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenLaunch.StatusCode);
    }

    [Fact]
    public async Task Missing_intent_in_another_dataset_creates_no_preparation_or_run() {
        await using var original = await ApiTestHost.CreateAsync(false);
        await using var scope = original.App.Services.CreateAsyncScope();
        var authority = await scope.ServiceProvider.GetRequiredService<IProcessLaunchOperatorAuthoritySource>()
            .CaptureLocalAsync(null, ProcessLaunchOperatorSurface.Api);
        ProcessLaunchIntentId intent = new(Guid.NewGuid());
        var preparation = ProcessPreparedLaunchFixture.Create(authority, intent);
        await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>().PrepareAsync(preparation);
        await using var other = await ApiTestHost.CreateAsync(false);
        using var response = await other.Client.GetAsync(IntentRoute + intent.Value.ToString("D"));
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        await using var otherScope = other.App.Services.CreateAsyncScope();
        await using var database = await otherScope.ServiceProvider.GetRequiredService<IDbContextFactory<ProcessPersistenceDbContext>>().CreateDbContextAsync();
        Assert.Empty(await database.PreparedLaunches.ToArrayAsync());
        Assert.Empty(await database.RuntimeStates.ToArrayAsync());
    }

    [Fact]
    public async Task OpenApi_exposes_optional_project_scope_and_read_only_intent_observation() {
        await using var host = await ApiTestHost.CreateAsync(false);
        using var document = await host.Client.GetFromJsonAsync<JsonDocument>("/openapi/v1.json");
        var paths = document!.RootElement.GetProperty("paths");
        foreach (var suffix in new[] { "", "/{definitionKey}", "/{definitionKey}/roles", "/{definitionKey}/steps" }) {
            var parameters = paths.GetProperty(DefinitionsRoute + suffix).GetProperty("get").GetProperty("parameters");
            var parameter = Assert.Single(parameters.EnumerateArray(), item => item.GetProperty("name").GetString() == "projectId");
            Assert.Equal("query", parameter.GetProperty("in").GetString());
            Assert.False(parameter.TryGetProperty("required", out var required) && required.GetBoolean());
        }
        var route = paths.GetProperty(IntentRoute + "{callerIntentId}");
        Assert.Equal("get", Assert.Single(route.EnumerateObject()).Name);
        Assert.Equal("GetProcessLaunchIntentStatus", route.GetProperty("get").GetProperty("operationId").GetString());
    }
}
