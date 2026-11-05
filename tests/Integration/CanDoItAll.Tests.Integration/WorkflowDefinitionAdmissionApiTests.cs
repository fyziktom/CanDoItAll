using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Tests.Support;
using CanDoItAll.Web.Api;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.AgentFramework;

public sealed class WorkflowDefinitionAdmissionApiTests {
    private const string DefinitionsRoute = "/api/workflows/definitions";
    private const string ConflictCode = "workflows.definition-conflict";

    [Theory]
    [InlineData(null, false)]
    [InlineData(null, true)]
    [InlineData(WorkflowLifecycleStatus.Active, false)]
    [InlineData(WorkflowLifecycleStatus.Active, true)]
    [InlineData(WorkflowLifecycleStatus.Suspended, false)]
    [InlineData(WorkflowLifecycleStatus.Suspended, true)]
    [InlineData(WorkflowLifecycleStatus.Archived, false)]
    [InlineData(WorkflowLifecycleStatus.Archived, true)]
    public async Task Missing_or_stale_original_returns_conflict_and_preserves_the_native_winner(
        WorkflowLifecycleStatus? lifecycle, bool staleOriginal) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        Assert.Equal(TestDatabaseProviderKind.PostgreSql, host.ActiveProfile.Provider);
        var request = CreateRequest();
        using var created = await host.Client.PostAsJsonAsync(DefinitionsRoute, request);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<WorkflowDefinition>();
        Assert.NotNull(original);
        using var peer = await host.Client.PostAsJsonAsync(DefinitionsRoute, request with {
            Id = original.Id,
            ExpectedVersionId = original.VersionId,
            Name = "Peer winner"
        });
        Assert.Equal(HttpStatusCode.OK, peer.StatusCode);
        var winner = await peer.Content.ReadFromJsonAsync<WorkflowDefinition>();
        Assert.NotNull(winner);
        WorkflowVersionId? heldVersion = staleOriginal ? original.VersionId : null;

        using var refused = lifecycle.HasValue
            ? await host.Client.PostAsync(LifecycleRoute(original.Id, lifecycle.Value, heldVersion), null)
            : await host.Client.PostAsJsonAsync(DefinitionsRoute, request with {
                Id = original.Id,
                ExpectedVersionId = heldVersion,
                Name = "Held replacement"
            });
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        var error = await refused.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(ConflictCode, Assert.Single(error.Errors).Code);
        var current = await host.Client.GetFromJsonAsync<WorkflowDefinitionDetail>($"{DefinitionsRoute}/{original.Id.Value:D}");
        Assert.NotNull(current);
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(current.Definition));

        await using var scope = host.App.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(2, await context.Set<WorkflowDefinitionRecord>().CountAsync(row => row.WorkflowId == original.Id.Value));

        using var accepted = lifecycle.HasValue
            ? await host.Client.PostAsync(LifecycleRoute(original.Id, lifecycle.Value, winner.VersionId), null)
            : await host.Client.PostAsJsonAsync(DefinitionsRoute, request with {
                Id = original.Id,
                ExpectedVersionId = winner.VersionId,
                Name = "Reviewed replacement"
            });
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var saved = await accepted.Content.ReadFromJsonAsync<WorkflowDefinition>();
        Assert.NotNull(saved);
        Assert.NotEqual(winner.VersionId, saved.VersionId);
        Assert.Equal(lifecycle ?? WorkflowLifecycleStatus.Draft, saved.Status);
        Assert.Equal(3, await context.Set<WorkflowDefinitionRecord>().CountAsync(row => row.WorkflowId == original.Id.Value));
    }

    [Fact]
    public async Task Openapi_declares_conflicts_and_original_version_create_only_semantics() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        using var document = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var paths = document.RootElement.GetProperty("paths");
        foreach (var path in new[] { DefinitionsRoute, DefinitionsRoute + "/import", DefinitionsRoute + "/{workflowId}/publish", DefinitionsRoute + "/{workflowId}/suspend", DefinitionsRoute + "/{workflowId}/archive" }) {
            Assert.True(paths.GetProperty(path).GetProperty("post").GetProperty("responses").TryGetProperty("409", out _), path);
        }
        var properties = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(nameof(WorkflowDefinitionSaveRequest)).GetProperty("properties");
        var expectedVersion = properties.GetProperty("expectedVersionId");
        Assert.True(expectedVersion.TryGetProperty("description", out var text), expectedVersion.GetRawText());
        var description = text.GetString();
        Assert.Contains("required", description, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create-only", description, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("without this check", description, StringComparison.OrdinalIgnoreCase);
        var deletion = paths.GetProperty(DefinitionsRoute + "/{workflowId}").GetProperty("delete");
        Assert.True(deletion.GetProperty("responses").TryGetProperty("409", out _));
        var deletionOriginal = deletion.GetProperty("parameters").EnumerateArray()
            .Single(parameter => parameter.GetProperty("name").GetString() == "expectedVersionId");
        Assert.Contains("Required original", deletionOriginal.GetProperty("description").GetString(), StringComparison.Ordinal);
        var importOriginal = document.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(nameof(WorkflowDefinitionImportRequest)).GetProperty("properties").GetProperty("expectedVersionId");
        Assert.Contains("create-only", importOriginal.GetProperty("description").GetString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_requires_the_original_version_and_preserves_a_peer_winner(bool staleOriginal) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var (original, winner) = await CreatePeerWinnerAsync(host);
        var route = $"{DefinitionsRoute}/{original.Id.Value:D}";
        using var refused = await host.Client.DeleteAsync(staleOriginal
            ? $"{route}?expectedVersionId={original.VersionId.Value:D}" : route);
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        await AssertConflictWinnerAsync(host, refused, winner);

        using var accepted = await host.Client.DeleteAsync($"{route}?expectedVersionId={winner.VersionId.Value:D}");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        await using var scope = host.App.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(0, await context.Set<WorkflowDefinitionHeadRecord>().CountAsync(row => row.WorkflowId == original.Id.Value));
        Assert.Equal(0, await context.Set<WorkflowDefinitionRecord>().CountAsync(row => row.WorkflowId == original.Id.Value));
        using var recreatedResponse = await host.Client.PostAsJsonAsync(DefinitionsRoute, CreateRequest() with {
            Id = original.Id, Name = "Recreated workflow"
        });
        Assert.Equal(HttpStatusCode.OK, recreatedResponse.StatusCode);
        var recreated = await recreatedResponse.Content.ReadFromJsonAsync<WorkflowDefinition>();
        Assert.NotNull(recreated);
        Assert.NotEqual(winner.VersionId, recreated.VersionId);
        using var retired = await host.Client.DeleteAsync($"{route}?expectedVersionId={winner.VersionId.Value:D}");
        Assert.Equal(HttpStatusCode.Conflict, retired.StatusCode);
        await AssertConflictWinnerAsync(host, retired, recreated, expectedVersionCount: 1);
    }

    [Theory]
    [InlineData(ImportOriginal.Missing)]
    [InlineData(ImportOriginal.Stale)]
    [InlineData(ImportOriginal.Current)]
    public async Task Preserved_import_replaces_only_the_explicitly_observed_version(ImportOriginal observation) {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var (original, winner) = await CreatePeerWinnerAsync(host);
        var envelope = await host.Client.GetFromJsonAsync<WorkflowDefinitionExportEnvelope>(
            $"{DefinitionsRoute}/{original.Id.Value:D}/export?versionId={original.VersionId.Value:D}");
        Assert.NotNull(envelope);
        WorkflowVersionId? expected = observation switch {
            ImportOriginal.Missing => null,
            ImportOriginal.Stale => original.VersionId,
            ImportOriginal.Current => winner.VersionId,
            _ => throw new ArgumentOutOfRangeException(nameof(observation))
        };
        using var response = await host.Client.PostAsJsonAsync(DefinitionsRoute + "/import", new {
            Envelope = envelope, Name = "Reviewed import", Status = WorkflowLifecycleStatus.Draft,
            PreserveWorkflowId = true, ExpectedVersionId = expected
        });
        if (observation != ImportOriginal.Current) {
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            await AssertConflictWinnerAsync(host, response, winner);
            return;
        }
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var imported = await response.Content.ReadFromJsonAsync<WorkflowDefinition>();
        Assert.NotNull(imported);
        Assert.Equal(original.Id, imported.Id);
        Assert.NotEqual(winner.VersionId, imported.VersionId);
        Assert.Equal("Reviewed import", imported.Name);
        Assert.Equal(JsonSerializer.Serialize(original.Graph), JsonSerializer.Serialize(imported.Graph));
        await using var scope = host.App.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(3, await context.Set<WorkflowDefinitionRecord>().CountAsync(row => row.WorkflowId == original.Id.Value));
        var current = await host.Client.GetFromJsonAsync<WorkflowDefinitionDetail>($"{DefinitionsRoute}/{original.Id.Value:D}");
        Assert.NotNull(current);
        Assert.Equal(JsonSerializer.Serialize(imported), JsonSerializer.Serialize(current.Definition));
    }

    [Fact]
    public async Task Empty_delete_original_is_refused_without_changing_the_winner() {
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false);
        var (_, winner) = await CreatePeerWinnerAsync(host);
        using var response = await host.Client.DeleteAsync($"{DefinitionsRoute}/{winner.Id.Value:D}?expectedVersionId={Guid.Empty:D}");
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await AssertConflictWinnerAsync(host, response, winner);
    }

    public enum ImportOriginal { Missing, Stale, Current }

    private static async Task<(WorkflowDefinition Original, WorkflowDefinition Winner)> CreatePeerWinnerAsync(ApiTestHost host) {
        Assert.Equal(TestDatabaseProviderKind.PostgreSql, host.ActiveProfile.Provider);
        var request = CreateRequest();
        using var created = await host.Client.PostAsJsonAsync(DefinitionsRoute, request);
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var original = await created.Content.ReadFromJsonAsync<WorkflowDefinition>();
        Assert.NotNull(original);
        using var peer = await host.Client.PostAsJsonAsync(DefinitionsRoute, request with {
            Id = original.Id,
            ExpectedVersionId = original.VersionId,
            Name = "Peer winner",
            Graph = request.Graph with { Nodes = request.Graph.Nodes.Select(node => node.Kind == WorkflowNodeKind.Start
                ? node with { Name = "Peer start node" } : node).ToArray() }
        });
        Assert.Equal(HttpStatusCode.OK, peer.StatusCode);
        var winner = await peer.Content.ReadFromJsonAsync<WorkflowDefinition>();
        Assert.NotNull(winner);
        return (original, winner);
    }

    private static async Task AssertConflictWinnerAsync(ApiTestHost host, HttpResponseMessage response, WorkflowDefinition winner,
        int expectedVersionCount = 2) {
        var error = await response.Content.ReadFromJsonAsync<ApiErrorResponse>();
        Assert.NotNull(error);
        Assert.Equal(ConflictCode, Assert.Single(error.Errors).Code);
        var current = await host.Client.GetFromJsonAsync<WorkflowDefinitionDetail>($"{DefinitionsRoute}/{winner.Id.Value:D}");
        Assert.NotNull(current);
        Assert.Equal(JsonSerializer.Serialize(winner), JsonSerializer.Serialize(current.Definition));
        await using var scope = host.App.Services.CreateAsyncScope();
        var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<WorkflowDbContext>>();
        await using var context = await factory.CreateDbContextAsync();
        Assert.Equal(winner.VersionId.Value, await context.Set<WorkflowDefinitionHeadRecord>()
            .Where(row => row.WorkflowId == winner.Id.Value).Select(row => row.VersionId).SingleAsync());
        Assert.Equal(expectedVersionCount, await context.Set<WorkflowDefinitionRecord>().CountAsync(row => row.WorkflowId == winner.Id.Value));
    }

    private static string LifecycleRoute(WorkflowId id, WorkflowLifecycleStatus lifecycle, WorkflowVersionId? expected) {
        var command = lifecycle switch {
            WorkflowLifecycleStatus.Active => "publish",
            WorkflowLifecycleStatus.Suspended => "suspend",
            WorkflowLifecycleStatus.Archived => "archive",
            _ => throw new ArgumentOutOfRangeException(nameof(lifecycle))
        };
        var route = $"{DefinitionsRoute}/{id.Value:D}/{command}";
        return expected.HasValue ? $"{route}?expectedVersionId={expected.Value.Value:D}" : route;
    }

    private static WorkflowDefinitionSaveRequest CreateRequest() {
        var start = new WorkflowNodeId("start");
        var end = new WorkflowNodeId("end");
        WorkflowNode Node(WorkflowNodeId id, WorkflowNodeKind kind) => new(id, kind, id.Value, [], new(
            ComponentId: null, AgentId: null, SubworkflowId: null, ExternalRequestKind: null,
            Instructions: string.Empty, InputShape: WorkflowValueShape.Text, ResultShape: WorkflowValueShape.Text));
        return new(null, null, "Original API definition", "Native original-version admission.", WorkflowLifecycleStatus.Draft,
            new(start, [Node(start, WorkflowNodeKind.Start), Node(end, WorkflowNodeKind.End)],
                [new(new("start-end"), start, null, end, null, WorkflowEdgeKind.Direct, string.Empty)]),
            new(WorkflowRuntimeBackendKind.InProcess, true, false, false, false));
    }
}
