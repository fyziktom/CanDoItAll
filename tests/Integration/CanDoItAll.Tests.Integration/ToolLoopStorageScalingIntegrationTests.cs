using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Persistence;
using CanDoItAll.AgentFramework.Tooling;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Xunit.Abstractions;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
[Trait("Category", "HostPlatform")]
public sealed class ToolLoopStorageScalingIntegrationTests(ITestOutputHelper output) {
    private const string ToolName = "read_fixture_value";

    [Theory]
    [InlineData(8)]
    [InlineData(72)]
    [InlineData(256)]
    public async Task ToolLoopBaseline_SeparatesProviderToolAndOwnerOverhead(int turns) {
        for (var sample = 0; sample < 3; sample++) {
            await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync();
            var observations = new List<(string Operation, double Milliseconds, long Bytes)>();
            using var listener = new ActivityListener {
                ShouldListenTo = source => source.Name == AgentFrameworkTelemetry.SourceName,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
                ActivityStopped = activity => {
                    lock (observations) {
                        observations.Add((activity.OperationName, activity.Duration.TotalMilliseconds,
                            activity.GetTagItem("payload.bytes") is long length ? length :
                            activity.GetTagItem("payload.bytes") is int size ? size : 0));
                    }
                }
            };
            ActivitySource.AddActivityListener(listener);
            var journal = fixture.NewJournal();
            using var client = new SequenceClient(turns);
            var calls = 0;
            var tool = AIFunctionFactory.Create((int value) => {
                using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("fixture.tool");
                calls++;
                return $"read:{value}";
            }, ToolName);
            var capabilities = new RuntimeCapabilityState();
            capabilities.Tools.Add(tool);
            capabilities.RuntimeToolMetadata.Add(new("fixture", ToolName, AgentRuntimeToolOperationKind.Read, false) {
                PrepareAdmission = arguments => new(ToolName, 1, MafToolProtocolCodec.Digest(arguments),
                    arguments.GetRawText(), AgentToolProposalEffect.Read, AgentToolProposalRecovery.RevalidateAndRead),
                AuthorizeAdmissionAsync = async (_, token) => {
                    await journal.RequireSessionAsync(fixture.Session, token);
                    return new EmptyScope();
                }
            });
            var options = MafChatClientAgentOptionsFactory.Create(new ChatOptions { ModelId = fixture.Provider.DefaultModel, Tools = [tool] });
            options.ChatHistoryProvider = new InMemoryChatHistoryProvider(new InMemoryChatHistoryProviderOptions {
                JsonSerializerOptions = MafToolProtocolCodec.SerializationOptions
            });
            var agent = new ChatClientAgent(new MafToolAdmissionChatClient(client), options).AsBuilder()
                .Use(async (_, context, next, token) => {
                    using var effects = AgentToolInvocationEffectScope.Begin();
                    return await MafToolRunContext.Current!.InvokeAsync(context.CallContent,
                        async cancellation => await next(context, cancellation), effects, token);
                }).Build();
            var executionOptions = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
                AdmittedToolSession = fixture.Session,
                RequireDurableToolProtocol = true,
                Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson),
                AuthorityPolicyFingerprint = "fixture-authority",
                ToolsetFingerprint = "fixture-toolset",
                ModelContextDigest = "fixture-context",
                CapabilityPolicyFingerprint = "fixture-capabilities",
                HistoryMode = AgentChatHistoryMode.FrameworkManaged,
                ContextIntent = AgentRuntimeContextIntent.Empty with { Purpose = AgentRuntimeContextPurpose.InteractiveChat }
            };
            var elapsed = Stopwatch.StartNew();
            AgentResponse response;
            await using (var lease = await journal.AcquireRunAsync(fixture.Session, default)) {
                using var bound = lease.Bind();
                var opened = await MafToolRunContext.OpenAsync(journal, lease, agent, await agent.CreateSessionAsync(), fixture.Agent,
                    fixture.Provider, fixture.Provider.DefaultModel, fixture.Detail.ChatSession!, executionOptions, capabilities,
                    [new(ChatRole.User, "Read each numbered value.")], new MafRuntimeSessionPersistenceDriver(), false,
                    (_, _, _) => Task.CompletedTask, default);
                using var active = opened.Context.Bind();
                response = await agent.RunAsync(opened.Input, opened.Session, new ChatClientAgentRunOptions {
                    ChatClientFactory = client => {
                        client.GetService<FunctionInvokingChatClient>()!.MaximumIterationsPerRequest = turns + 1;
                        return client;
                    }
                });
            }
            elapsed.Stop();
            output.WriteLine($"BASELINE turns={turns} sample={sample + 1} elapsed-ms={elapsed.Elapsed.TotalMilliseconds:F1} payload-chars={client.Payload.Length}");
            foreach (var group in observations.GroupBy(item => item.Operation).OrderBy(group => group.Key)) {
                output.WriteLine($"PHASE {group.Key} count={group.Count()} ms={group.Sum(item => item.Milliseconds):F1} bytes={group.Sum(item => item.Bytes)}");
            }
            Assert.EndsWith("completed", response.Text, StringComparison.Ordinal);
            Assert.Equal(turns, calls);
            Assert.Equal(turns + 1, client.Calls);
            foreach (var owner in new[] { fixture.Store, fixture.NewStore() }) {
                for (var read = 0; read < 2; read++) {
                    observations.Clear();
                    var readTime = Stopwatch.StartNew();
                    var saved = await owner.GetExecutionRunDetailAsync(fixture.Session.ExecutionRunId);
                    Assert.Equal(turns + 1, saved!.Run.ToolAdmission!.Batches.Length);
                    Assert.Equal(turns, saved.Run.ToolAdmission.Batches.Sum(batch => batch.Proposals.Length));
                    Assert.All(saved.Run.ToolAdmission.Batches.SelectMany(batch => batch.Proposals), proposal =>
                        Assert.Equal(AgentToolProposalState.Completed, proposal.State));
                    output.WriteLine($"READ owner={(ReferenceEquals(owner, fixture.Store) ? "original" : "independent")} attempt={read + 1} ms={readTime.Elapsed.TotalMilliseconds:F1} deserializations={observations.Count(item => item.Operation == "workspace.json.deserialize")} bytes={observations.Where(item => item.Operation == "workspace.json.read").Sum(item => item.Bytes)}");
                }
            }
        }
    }

    private sealed class EmptyScope : IAsyncDisposable {
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class SequenceClient(int turns) : IChatClient {
        public int Calls { get; private set; }
        public string Payload { get; } = CreatePayload(turns == 256 ? 3 * 1024 : 36 * 1024);

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            using var activity = AgentFrameworkTelemetry.ActivitySource.StartActivity("fixture.provider");
            var ordinal = Calls++;
            return Task.FromResult(new ChatResponse(ordinal < turns
                ? new ChatMessage(ChatRole.Assistant, [new TextContent(Payload), new FunctionCallContent($"call_{ordinal}",
                    ToolName, new Dictionary<string, object?> { ["value"] = ordinal })])
                : new ChatMessage(ChatRole.Assistant, "completed")) { ResponseId = $"response_{ordinal}" });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            foreach (var update in (await GetResponseAsync(messages, options, cancellationToken)).ToChatResponseUpdates()) {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() {
        }

        private static string CreatePayload(int bytesPerResponse) {
            var bytes = new byte[bytesPerResponse];
            new Random(8217).NextBytes(bytes);
            return Convert.ToBase64String(bytes);
        }
    }
}
