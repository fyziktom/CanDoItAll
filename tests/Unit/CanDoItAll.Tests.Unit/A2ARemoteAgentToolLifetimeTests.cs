using System.Net;
using System.Text.Json;
using A2A;
using Microsoft.Extensions.AI;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class A2ARemoteAgentToolLifetimeTests {
    [Theory]
    [InlineData(Failure.CardFetch)]
    [InlineData(Failure.NoSkills)]
    [InlineData(Failure.NoAllowedSkill)]
    [InlineData(Failure.DuplicateName)]
    [InlineData(Failure.Cancelled)]
    public async Task Failed_endpoint_releases_its_owned_http_resources(Failure failure) {
        var handler = new CardHandler(failure);
        var factory = new A2ARemoteAgentToolFactory(null, null, () => handler);
        var endpoint = Endpoint("first");
        if (failure == Failure.NoAllowedSkill) {
            endpoint.AllowedSkillNames = ["missing"];
        }

        var exception = await Record.ExceptionAsync(() => factory.CreateSkillToolsAsync([endpoint]));

        Assert.NotNull(exception);
        var expectedMessage = failure switch {
            Failure.CardFetch => "Scripted card fetch failed",
            Failure.Cancelled => "Scripted card fetch canceled",
            Failure.NoSkills => "did not publish any skills",
            Failure.NoAllowedSkill => "did not expose any skill",
            Failure.DuplicateName => "duplicated after sanitization",
            _ => throw new ArgumentOutOfRangeException(nameof(failure))
        };
        Assert.Contains(expectedMessage, exception.ToString(), StringComparison.Ordinal);
        Assert.True(handler.RequestCount > 0);
        Assert.Equal(1, handler.DisposeCount);
    }

    [Fact]
    public async Task Later_endpoint_failure_releases_both_completed_and_current_resources() {
        var first = new CardHandler();
        var second = new CardHandler(Failure.NoSkills);
        var handlers = new Queue<CardHandler>([first, second]);
        var factory = new A2ARemoteAgentToolFactory(null, null, () => handlers.Dequeue());

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            factory.CreateSkillToolsAsync([Endpoint("first"), Endpoint("second")]));

        Assert.Contains("did not publish any skills", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, first.DisposeCount);
        Assert.Equal(1, second.DisposeCount);
    }

    [Fact]
    public async Task Successful_endpoint_transfers_live_resources_to_the_owner() {
        var handler = new CardHandler();
        var factory = new A2ARemoteAgentToolFactory(null, null, () => handler);

        var result = await factory.CreateSkillToolsAsync([Endpoint("first")]);

        Assert.Single(result.Tools);
        Assert.Equal(0, handler.DisposeCount);
        foreach (var disposable in result.Disposables.Reverse()) {
            disposable.Dispose();
        }
        Assert.Equal(1, handler.DisposeCount);
    }

    [Theory]
    [InlineData(TaskState.Working)]
    [InlineData(TaskState.InputRequired)]
    [InlineData(TaskState.Failed)]
    [InlineData(TaskState.Canceled)]
    [InlineData(TaskState.Rejected)]
    public async Task Non_success_remote_task_is_not_reported_as_completed(TaskState state) {
        var handler = new CardHandler(taskState: state);
        var result = await new A2ARemoteAgentToolFactory(null, null, () => handler).CreateSkillToolsAsync([Endpoint("first")]);
        try {
            var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(result.Tools));
            var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                tool.InvokeAsync(new AIFunctionArguments { ["input"] = "Create the note" }).AsTask());
            Assert.Contains(state.ToString(), failure.Message, StringComparison.Ordinal);
            Assert.Equal(1, handler.RemoteRequestCount);
        } finally {
            foreach (var resource in result.Disposables.Reverse()) {
                resource.Dispose();
            }
        }
    }

    [Fact]
    public async Task Completed_remote_task_with_no_text_is_a_valid_empty_completion() {
        var handler = new CardHandler(taskState: TaskState.Completed);
        var result = await new A2ARemoteAgentToolFactory(null, null, () => handler).CreateSkillToolsAsync([Endpoint("first")]);
        try {
            var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(result.Tools));
            var response = await tool.InvokeAsync(new AIFunctionArguments { ["input"] = "Create the note" });
            Assert.Contains("completed without text output", response!.ToString(), StringComparison.Ordinal);
            Assert.Equal(1, handler.RemoteRequestCount);
        } finally {
            foreach (var resource in result.Disposables.Reverse()) {
                resource.Dispose();
            }
        }
    }

    private static AgentA2ARemoteEndpointSettings Endpoint(string name) => new() {
        EndpointId = name,
        BaseUri = $"https://{name}.example.test",
        ToolNamePrefix = name
    };

    public enum Failure {
        None,
        CardFetch,
        NoSkills,
        NoAllowedSkill,
        DuplicateName,
        Cancelled
    }

    private sealed class CardHandler(Failure failure = Failure.None, TaskState? taskState = null) : HttpMessageHandler {
        public int DisposeCount { get; private set; }
        public int RequestCount { get; private set; }
        public int RemoteRequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            RequestCount++;
            if (request.Method == HttpMethod.Post) {
                RemoteRequestCount++;
                using var rpcRequest = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
                var result = new SendMessageResponse {
                    Task = new AgentTask { Id = "fixture-task", ContextId = "fixture-context",
                        Status = new A2A.TaskStatus { State = taskState!.Value } }
                };
                var rpcResponse = JsonSerializer.Serialize(new { jsonrpc = "2.0", id = rpcRequest.RootElement.GetProperty("id"), result },
                    JsonSerializerOptions.Web);
                return new HttpResponseMessage(HttpStatusCode.OK) {
                    Content = new StringContent(rpcResponse, System.Text.Encoding.UTF8, "application/json")
                };
            }
            if (failure == Failure.CardFetch) {
                throw new HttpRequestException("Scripted card fetch failed.");
            }
            if (failure == Failure.Cancelled) {
                throw new OperationCanceledException("Scripted card fetch canceled.", new CancellationToken(true));
            }
            var card = new AgentCard {
                Name = "Fixture agent",
                Description = "A deterministic lifetime fixture",
                Version = "1.0",
                Capabilities = new AgentCapabilities(),
                DefaultInputModes = ["text"],
                DefaultOutputModes = ["text"],
                Skills = failure == Failure.NoSkills ? [] : [Skill("write-note")],
                SupportedInterfaces = [new AgentInterface {
                    Url = "https://first.example.test/a2a",
                    ProtocolBinding = ProtocolBindingNames.JsonRpc,
                    ProtocolVersion = "1.0"
                }]
            };
            if (failure == Failure.DuplicateName) {
                card.Skills.Add(Skill("write note"));
            }
            return new HttpResponseMessage(HttpStatusCode.OK) {
                Content = new StringContent(JsonSerializer.Serialize(card, new JsonSerializerOptions(JsonSerializerDefaults.Web)),
                    System.Text.Encoding.UTF8, "application/json")
            };
        }

        protected override void Dispose(bool disposing) {
            if (disposing) {
                DisposeCount++;
            }
            base.Dispose(disposing);
        }

        private static AgentSkill Skill(string name) => new() {
            Id = name,
            Name = name,
            Description = "Write a note",
            Tags = ["fixture"]
        };
    }
}
