using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Text;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.ProviderHistory;
using CanDoItAll.AgentFramework.Providers;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using static CanDoItAll.Tests.Support.AgentPlanningDecisionTestData;

namespace CanDoItAll.Tests.Integration.Runtime;

public enum PlanningProviderWiring { Admitted, MissingBoundary, ChangedInput }

internal sealed class MafPlanningProviderFixture : IMafProviderAgentFactory {
    internal PlanningProviderWiring Mode { get; set; }
    internal int Created { get; private set; }
    internal int Disposed { get; private set; }
    internal Action? OnCreate { get; set; }
    internal List<ChatMessage[]> Inputs { get; } = [];
    internal Func<CancellationToken, Task<ChatResponse>> Respond { get; set; }
        = _ => Task.FromResult(Response("{\"tasks\":[]}"));

    public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
        bool frameworkManagedHistory, bool allowBackgroundResponses) {
        Assert.True(frameworkManagedHistory);
        Assert.False(allowBackgroundResponses);
        Assert.True(options.UseProvidedChatClientAsIs);
        Assert.False(options.AllowConcurrentInvocation);
        Assert.True(options.DisableApprovalNotRequiredFunctionBypassing);
        Assert.False(options.DisableApprovalResponseBinding);
        Assert.True(options.ThrowOnChatHistoryProviderConflict);
        Assert.Null(options.ChatOptions!.Tools);
        Assert.Equal(AgentPlanningProviderBounds.MaximumOutputTokens, options.ChatOptions.MaxOutputTokens);
        Created++;
        OnCreate?.Invoke();
        IChatClient client = new Client(this);
        if (Mode != PlanningProviderWiring.MissingBoundary) {
            client = new MafToolAdmissionChatClient(client);
        }
        if (Mode == PlanningProviderWiring.ChangedInput) {
            client = new ChangedInputClient(client);
        }
        return new ChatClientAgent(client, options);
    }

    internal static ChatResponse Response(string text) => new(new ChatMessage(ChatRole.Assistant, text)) {
        ResponseId = "planning-response", Usage = new() { InputTokenCount = 11, OutputTokenCount = 7 }
    };

    internal sealed class Participant : IAgentPlanningParticipant {
        public Task<AgentPlanningRunAdmission> PrepareRunAsync(AgentPlanningRunPreparation preparation, CancellationToken cancellationToken)
            => throw new NotSupportedException("This provider fixture starts with an explicitly admitted test decision.");
        internal int Captures { get; private set; }
        internal int Preparations { get; private set; }
        internal bool DenyRead { get; set; }
        internal bool ContinueStep { get; set; }
        internal bool ThrowPreparation { get; set; }
        public AgentPlanningOwner Owner => AgentPlanningOwner.ProjectTaskPlan;
        public AgentPlanningPromptContract Prompt { get; set; } = new("Use only the original request and supplied material basis.", "task_plan",
            "{\"type\":\"object\",\"properties\":{\"tasks\":{\"type\":\"array\",\"items\":{\"type\":\"string\"}}},\"required\":[\"tasks\"],\"additionalProperties\":false}");
        public Task<AgentToolProtocolEnvelope> CaptureBasisAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken) {
            cancellationToken.ThrowIfCancellationRequested();
            Captures++;
            return Task.FromResult(Envelope(Captures == 1 ? "{\"tasks\":[\"first fact\"]}" : "{\"tasks\":[\"new fact\"]}"));
        }
        public Task RequireCurrentReadAsync(AgentPlanningDecisionRecord decision, CancellationToken cancellationToken)
            => DenyRead ? Task.FromException(new UnauthorizedAccessException("The current read was revoked.")) : Task.CompletedTask;
        public AgentPlanningResponsePreparation PreparePublication(AgentPlanningDecisionRecord decision, AgentPlanningBasisRecord basis, string responseText) {
            Preparations++;
            if (ThrowPreparation) {
                throw new InvalidDataException("The deliberately failing parser must not discard output.");
            }
            return ContinueStep ? new AgentPlanningResponsePreparation.Continue() :
                new AgentPlanningResponsePreparation.Publication(AgentPlanningLiveOwnerFixture.Publication());
        }
    }

    private sealed class Client(MafPlanningProviderFixture owner) : IChatClient {
        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            owner.Inputs.Add(messages.Select(message => message.Clone()).ToArray());
            return await owner.Respond(cancellationToken);
        }
        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default) => throw new InvalidOperationException("Unexpected streaming.");
        public object? GetService(Type serviceType, object? serviceKey = null) => serviceType.IsInstanceOfType(this) ? this : null;
        public void Dispose() => owner.Disposed++;
    }

    private sealed class ChangedInputClient(IChatClient inner) : DelegatingChatClient(inner) {
        public override Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => base.GetResponseAsync(messages.Append(new(ChatRole.User, "substituted input")), options, cancellationToken);
    }

    internal sealed class Wire : HttpMessageHandler, IProviderHttpClientSelector, IMafProviderCredentialService, IProviderHistoryRecorder {
        private readonly HistoryPartition partition = new(Guid.NewGuid(), Guid.NewGuid(), "planning-test");
        internal List<string> Requests { get; } = [];
        internal List<HistoryAttemptStart> Starts { get; } = [];
        internal List<HistoryInvocationContext> Contexts { get; } = [];
        internal int Completed { get; private set; }
        internal string ResponseText { get; set; } = "{\"tasks\":[]}";
        internal HttpStatusCode StatusCode { get; set; } = HttpStatusCode.OK;
        internal Func<CancellationToken, Task>? OnRequest { get; set; }
        internal HttpClient Client { get; set; } = null!;
        public bool TryGetClient(ProviderProfile provider, [NotNullWhen(true)] out HttpClient? client) {
            client = Client;
            return true;
        }
        public ProviderCredentialResolution Resolve(ProviderProfile provider) => new("fixture-key", "intercepted transport", string.Empty);
        public string ResolveOpenAiCredentialOverride(ProviderProfile provider) => "resolved";
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Requests.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            if (OnRequest is not null) {
                await OnRequest(cancellationToken);
            }
            if (StatusCode != HttpStatusCode.OK) {
                return new(StatusCode) { Content = new StringContent("{\"error\":{\"message\":\"planning-provider-failure\"}}", Encoding.UTF8, "application/json") };
            }
            var body = JsonSerializer.Serialize(new {
                id = "chatcmpl-plan", @object = "chat.completion", created = 1_787_000_000, model = "fixture-model",
                choices = new[] { new { index = 0, message = new { role = "assistant", content = ResponseText }, finish_reason = "stop" } },
                usage = new { prompt_tokens = 11, completion_tokens = 7, total_tokens = 18 }
            });
            return new(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        }
        public Task<HistoryAttemptStart> BeginAsync(HistoryInvocation invocation, CancellationToken cancellationToken) {
            var context = invocation.Context;
            Contexts.Add(context);
            var source = context.Owner!;
            var start = new HistoryAttemptStart(HistoryEntryId.New(), partition, new(0, 0), context.RequestId, ProviderAttemptId.New(),
                DateTimeOffset.UtcNow, invocation.Provider, invocation.Operation, context.Workload, context.Caller,
                new(new() { CaptureMode = HistoryCaptureMode.Detailed }, 0),
                new(partition, source.Kind, source.OwnerId, source.EvidenceId), context.CorrelationId);
            Starts.Add(start);
            context.Attempts.Add(start);
            return Task.FromResult(start);
        }
        public Task CompleteAsync(HistoryAttemptStart start, HistoryAttemptCompletion completion, string? response, CancellationToken cancellationToken) {
            Completed++;
            return Task.CompletedTask;
        }
    }
}
