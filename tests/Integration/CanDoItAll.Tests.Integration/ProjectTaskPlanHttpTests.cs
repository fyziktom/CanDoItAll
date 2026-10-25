using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Support;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using EntryFixture = CanDoItAll.Tests.Integration.ProjectTaskPlanExecutionEntryTests.EntryFixture;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace CanDoItAll.Tests.Integration;

public sealed class ProjectTaskPlanHttpTests {
    [Fact]
    public async Task Http_requires_write_permission_bounded_unique_body_and_exact_lossless_revision() {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var fixture = await EntryFixture.CreateAsync(testEnvironment: environment, activeProfile: profile);
        var original = await ProjectTaskPlanExecutionActionTests.StartAsync(fixture);
        await using var host = await ApiTestHost.CreateAsync(true, sharedTestEnvironment: environment, sharedActiveProfile: profile);
        var options = host.App.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        var request = ProjectTaskPlanExecutionActionTests.Action(original, AgentPlanningAction.Cancel);
        var body = JsonSerializer.Serialize(new { request, activityOperationId = AgentExecutionOperationId.New() }, options);
        var before = JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id));

        await RefusedAsync(host, original.Run.Id, Content(body), HttpStatusCode.Unauthorized);
        UseToken(host, ApiAccessScopeNames.ReadAgents);
        await RefusedAsync(host, original.Run.Id, Content(body), HttpStatusCode.Forbidden);
        UseToken(host, ApiAccessScopeNames.WriteAgents);
        await RefusedAsync(host, Guid.NewGuid(), Content(body), HttpStatusCode.BadRequest);
        await RefusedAsync(host, original.Run.Id, new StringContent(body, Encoding.UTF8, "text/plain"), HttpStatusCode.UnsupportedMediaType);
        await RefusedAsync(host, original.Run.Id, Content(new string(' ', AgentPlanningActionRequest.MaximumUtf8Bytes + 1)), HttpStatusCode.RequestEntityTooLarge);
        await RefusedAsync(host, original.Run.Id, new StreamingContent(new byte[AgentPlanningActionRequest.MaximumUtf8Bytes + 1]), HttpStatusCode.RequestEntityTooLarge);
        await RefusedAsync(host, original.Run.Id, Content("null"), HttpStatusCode.BadRequest);
        await RefusedAsync(host, original.Run.Id, Content(body[..^1] + ",\"request\":" + JsonSerializer.Serialize(request, options) + "}"), HttpStatusCode.BadRequest);
        var changed = JsonNode.Parse(body)!;
        changed["request"]!["unknown"] = true;
        await RefusedAsync(host, original.Run.Id, Content(changed.ToJsonString()), HttpStatusCode.BadRequest);
        foreach (var token in new[] { "1", "null", "\"01\"", "\"9223372036854775808\"" }) {
            changed = JsonNode.Parse(body)!;
            changed["request"]!["expected"]!["runRevision"] = JsonNode.Parse(token);
            await RefusedAsync(host, original.Run.Id, Content(changed.ToJsonString()), HttpStatusCode.BadRequest);
        }
        foreach (var revision in new[] { "9007199254740993", "9223372036854775807" }) {
            changed = JsonNode.Parse(body)!;
            changed["activityOperationId"] = JsonSerializer.SerializeToNode(AgentExecutionOperationId.New(), options);
            changed["request"]!["expected"]!["runRevision"] = revision;
            await RefusedAsync(host, original.Run.Id, Content(changed.ToJsonString()), HttpStatusCode.Conflict);
        }
        Assert.Equal(before, JsonSerializer.Serialize(await fixture.Files.Store.GetExecutionRunDetailAsync(original.Run.Id)));
        Assert.Single(fixture.Wire.Requests);
        using var accepted = await host.Client.PostAsync(Route(original.Run.Id), Content(body));
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal("no-store", accepted.Headers.CacheControl?.ToString());
        var result = JsonNode.Parse(await accepted.Content.ReadAsStringAsync())!;
        var review = result["planningReview"]!.Deserialize<AgentPlanningReview>(options)!;
        Assert.Equal(AgentPlanningGenerationState.Cancelled, review.State);
        Assert.Empty(review.AvailableActions);
        await RefusedAsync(host, original.Run.Id, Content(body), HttpStatusCode.Conflict);
        await fixture.Files.Sql.VerifyAsync(0, 0);
    }

    [Fact]
    public async Task Http_review_and_publication_keep_original_binding_and_reject_duplicate_dispatch() {
        await using var environment = CanDoItAllTestEnvironment.Create("candoitall-api-tests");
        var profile = environment.CreatePostgreSqlProfile("primary");
        await using var fixture = await EntryFixture.CreateAsync(testEnvironment: environment, activeProfile: profile);
        var original = await ProjectTaskPlanExecutionActionTests.StartAsync(fixture);
        await using var host = await ApiTestHost.CreateAsync(true, sharedTestEnvironment: environment, sharedActiveProfile: profile);
        var options = host.App.Services.GetRequiredService<IOptions<HttpJsonOptions>>().Value.SerializerOptions;
        UseToken(host, ApiAccessScopeNames.ReadAgents, ApiAccessScopeNames.WriteAgents);
        using var openApi = JsonDocument.Parse(await host.Client.GetStringAsync("/openapi/v1.json"));
        var revisionSchema = openApi.RootElement.GetProperty("components").GetProperty("schemas")
            .GetProperty(nameof(AgentPlanningDecisionExpectation)).GetProperty("properties");
        foreach (var name in new[] { "runRevision", "journalRevision" }) {
            Assert.Equal("string", revisionSchema.GetProperty(name).GetProperty("type").GetString());
            Assert.Equal(19, revisionSchema.GetProperty(name).GetProperty("maxLength").GetInt32());
        }
        using var detail = await host.Client.GetAsync($"/api/agents/execution-runs/{original.Run.Id:D}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        Assert.Equal("no-store", detail.Headers.CacheControl?.ToString());
        var body = JsonNode.Parse(await detail.Content.ReadAsStringAsync())!;
        var review = body["planningReview"]!.Deserialize<AgentPlanningReview>(options)!;
        Assert.Equal(AgentPlanningDecisionExpectation.From(original.Run), review.Expected);
        Assert.Null(body["effectContinuation"]);
        Assert.Equal(JsonValueKind.String, body["planningReview"]!["expected"]!["runRevision"]!.GetValueKind());
        var publication = review.Publication!;
        var request = new AgentPlanningActionRequest(AgentPlanningAction.Approve, review.ChatSessionId,
            review.Expected, publication.Binding, publication.ApprovalId);
        var submission = JsonSerializer.Serialize(new { request, activityOperationId = AgentExecutionOperationId.New() }, options);
        using var response = await host.Client.PostAsync(Route(original.Run.Id), Content(submission));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var completed = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["planningReview"]!.Deserialize<AgentPlanningReview>(options)!;
        Assert.Equal(AgentPlanningGenerationState.Completed, completed.State);
        Assert.Empty(completed.AvailableActions);
        await RefusedAsync(host, original.Run.Id, Content(submission), HttpStatusCode.Conflict);
        Assert.Single(fixture.Wire.Requests);
        await fixture.Files.Sql.VerifyAsync(1, 1);
        await fixture.Files.Store.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Select(agent => agent.Id == fixture.Files.Agent.Id ? agent with {
                ConfigurationJson = AgentProjectStructureAccessMetadata.Write(agent.ConfigurationJson, new())
            } : agent).ToArray()
        });
        var scoped = $"/api/agents/{original.Run.AgentId:D}/execution-runs/{original.Run.Id:D}";
        foreach (var route in new[] { $"/api/agents/execution-runs/{original.Run.Id:D}", scoped,
            $"{scoped}/artifacts", $"{scoped}/checkpoints", $"{scoped}/tool-receipts", $"{scoped}/log",
            $"{scoped}/metrics", $"{scoped}/approvals" }) {
            using var denied = await host.Client.GetAsync(route);
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal("no-store", denied.Headers.CacheControl?.ToString());
            Assert.DoesNotContain(publication.Content, await denied.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    private static string Route(Guid runId) => $"/api/agents/execution-runs/{runId:D}/planning/actions";
    private static StringContent Content(string value) => new(value, Encoding.UTF8, "application/json");

    private static void UseToken(ApiTestHost host, params string[] scopes) {
        var issued = host.App.Services.GetRequiredService<IApiTokenService>().IssueToken(new() {
            Subject = "planning-http-fixture", Scopes = [.. scopes], LifetimeMinutes = 10
        });
        host.Client.DefaultRequestHeaders.Authorization = new("Bearer", issued.Token);
    }

    private static async Task RefusedAsync(ApiTestHost host, Guid runId, HttpContent body, HttpStatusCode expected) {
        using (body) {
            using var response = await host.Client.PostAsync(Route(runId), body);
            Assert.Equal(expected, response.StatusCode);
        }
    }

    private sealed class StreamingContent : HttpContent {
        private readonly byte[] bytes;
        public StreamingContent(byte[] bytes) {
            this.bytes = bytes;
            Headers.ContentType = new("application/json");
        }
        protected override bool TryComputeLength(out long length) {
            length = 0;
            return false;
        }
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context)
            => stream.WriteAsync(bytes).AsTask();
    }
}
