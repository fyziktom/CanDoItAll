using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class MafWorkflowHumanInLoopTests {
    [Fact]
    public async Task Genuine_120_workflow_checkpoint_resumes_under_122_without_replaying_completed_work() {
        var json = await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Maf120", "Durable",
            "workflow-external-input.json"));
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web) {
            Converters = {
                new CapturedValueConverter<WorkflowBackendCheckpointFormat>(value => new(value.GetString()!)),
                new CapturedValueConverter<WorkflowBackendCheckpointFormatVersion>(value => new(value.GetInt32())),
                new CapturedValueConverter<WorkflowCheckpointCommitOrdinal>(value => new(value.GetInt64()))
            }
        };
        var fixture = JsonSerializer.Deserialize<RetainedWorkflowFixture>(json, options)!;
        Assert.Equal("1.20.0.0", fixture.MafVersion);
        Assert.Equal(new Version(1, 22, 0, 0), typeof(Microsoft.Agents.AI.AIAgent).Assembly.GetName().Version);
        Assert.True(fixture.Payload.Payload.HasValidHash);
        var marker = new CountingLlmInvoker();
        var componentId = fixture.Definition.Graph.Nodes.Single(node => node.Id.Value == "marker").Settings.ComponentId!.Value;
        var component = CreateComponent() with { Id = componentId };
        var store = new RetainedCheckpointStore(fixture.Payload);
        var backend = CreateNativeBackend(fixture.Definition, component, marker, store);
        using var response = JsonDocument.Parse("""{"answer":"continue"}""");
        var declaration = new WorkflowRunDisclosureDeclaration(fixture.Run.RunId, fixture.Definition.Id,
            fixture.Definition.VersionId, WorkflowProviderDisclosureContent.Definition(fixture.Definition),
            WorkflowProviderDisclosureContent.Source(fixture.Run.Origin), WorkflowProviderDisclosureProtocol.Current);

        var request = fixture.Request with { Continuation = fixture.Continuation, AuthorizationPolicy = fixture.AuthorizationPolicy };
        var result = await backend.ResumeAsync(CreateAuthorizedResumeRequest(fixture.Run, request,
            response.RootElement, declaration));

        Assert.Equal(WorkflowRunState.Completed, result.Run.State);
        Assert.Equal(0, marker.InvocationCount);
        Assert.Empty(result.ExternalRequests);
        Assert.Contains(result.Events, item => item.Kind == WorkflowEventKind.Completed);
    }

    private sealed class CapturedValueConverter<T>(Func<JsonElement, T> create) : JsonConverter<T> where T : struct {
        public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
            using var value = JsonDocument.ParseValue(ref reader);
            return create(value.RootElement.GetProperty("value"));
        }

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options) =>
            throw new NotSupportedException("Retained fixtures are read-only.");
    }

    private sealed record RetainedWorkflowFixture(string MafVersion, WorkflowDefinition Definition,
        WorkflowRunSnapshot Run, WorkflowExternalRequestRecord Request, WorkflowBackendCheckpointPayloadRecord Payload,
        WorkflowExternalRequestContinuation Continuation, WorkflowExternalRequestAuthorizationPolicySnapshot AuthorizationPolicy);

    private sealed class RetainedCheckpointStore(WorkflowBackendCheckpointPayloadRecord retained) : IWorkflowBackendCheckpointPayloadStore {
        private readonly Dictionary<WorkflowBackendCheckpointLink, WorkflowBackendCheckpointPayloadRecord> checkpoints =
            new() { [retained.Index.Link] = retained };

        public Task<WorkflowBackendCheckpointCreateResult> CreateAsync(WorkflowBackendCheckpointCreateRequest request,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(request.Payload.HasValidHash);
            Assert.Equal(retained.Session, request.Session);
            if (request.Parent is { } parent) {
                Assert.True(checkpoints.ContainsKey(parent));
            }
            var index = new WorkflowBackendCheckpointIndexEntry(new(request.Session.Id, WorkflowBackendCheckpointId.New()),
                request.Parent, new(checkpoints.Count + 1), DateTimeOffset.UtcNow);
            var checkpoint = new WorkflowBackendCheckpointPayloadRecord(request.Session, index, request.Payload, request.ExternalRequestLink);
            checkpoints.Add(index.Link, checkpoint);
            return Task.FromResult(new WorkflowBackendCheckpointCreateResult(WorkflowBackendCheckpointCreateOutcome.Created, checkpoint));
        }

        public Task<WorkflowBackendCheckpointListResult> ListIndexAsync(WorkflowBackendSessionId sessionId,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal(retained.Session.Id, sessionId);
            return Task.FromResult(new WorkflowBackendCheckpointListResult(WorkflowBackendCheckpointListOutcome.Found,
                checkpoints.Values.Select(item => item.Index).ToArray()));
        }

        public Task<WorkflowBackendCheckpointReadResult> ReadAsync(WorkflowBackendCheckpointLink link,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(checkpoints.TryGetValue(link, out var checkpoint)
                ? new WorkflowBackendCheckpointReadResult(WorkflowBackendCheckpointReadOutcome.Found, checkpoint)
                : new WorkflowBackendCheckpointReadResult(WorkflowBackendCheckpointReadOutcome.NotFound, null));
        }
    }
}
