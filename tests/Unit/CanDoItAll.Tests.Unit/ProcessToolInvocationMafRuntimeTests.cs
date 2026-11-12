using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Processes;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class ProcessToolInvocationMafRuntimeTests {
    private const string PrimaryRef = "artifacts/process-runs/run-a/steps/record-evidence.md";
    private const string FinalContent = "Status: Completed\nCurrent execution evidence was recorded after review.";

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, false)]
    [InlineData(true, true)]
    [InlineData(false, true)]
    public async Task Failed_validation_allows_completion_only_with_an_inspected_no_mutation_declaration(bool declaresValidation, bool powerShell) {
        var directory = Directory.CreateTempSubdirectory(nameof(ProcessToolInvocationMafRuntimeTests));
        RuntimeCapabilityState? capabilities = null;
        try {
            var script = $"artifacts/process-runs/run-a/verify.{(powerShell ? "ps1" : "py")}";
            var scriptTool = powerShell ? ToolContractCatalog.WorkspacePowerShellRunScript : ToolContractCatalog.WorkspacePythonRunFile;
            var scriptFile = Path.Combine(directory.FullName, script);
            Directory.CreateDirectory(Path.GetDirectoryName(scriptFile)!);
            await File.WriteAllTextAsync(scriptFile, powerShell
                ? "Invoke-WebRequest -Uri 'http://127.0.0.1:5000/missing.js' -ErrorAction Stop"
                : "raise AssertionError('validation did not pass')");
            var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
            services.AddLogging();
            services.AddSingleton<IToolInvocationPolicyContextContributor, ProcessToolInvocationPolicyContextContributor>();
            await using var provider = services.BuildServiceProvider();
            var dependencies = MafAgentRuntimeDependencies.FromServices(provider);
            var composer = RuntimeCapabilityComposer.CreateDefault(directory.FullName, provider);
            var agent = CreateAgent();
            var profile = CreateProvider();
            var commands = new FailingScriptHost();
            await using var original = WorkspaceRuntimeServicesTestFactory.Create(directory.FullName);
            var commandService = TestWorkspaceServices.CreateCommandExecutionService(directory.FullName, commands);
            await using var workspace = new WorkspaceRuntimeServices(original.Scope, original.FileService, commandService,
                original.ArtifactToolService, original.ImageOperationService, commands, original.ExternalTargetPathRegistry, [commandService]);
            capabilities = await composer.CreateCapabilityStateAsync(agent, profile, [], [], workspace,
                static (_, _, _) => Task.CompletedTask, CancellationToken.None, suppressApprovalRequirements: true);
            var tool = Assert.IsAssignableFrom<AIFunction>(Assert.Single(capabilities.Tools, tool => tool.Name == scriptTool));
            var manifestSchema = tool.JsonSchema.GetProperty("properties").GetProperty(GovernedScriptSideEffectManifest.ArgumentName);
            var properties = manifestSchema.GetProperty("properties");
            Assert.Contains(properties.GetProperty("mode").GetProperty("enum").EnumerateArray(),
                item => item.GetString() == nameof(GovernedScriptSideEffectMode.NoMutation));
            Assert.True(properties.TryGetProperty("declaredWritePaths", out _));
            Assert.False(manifestSchema.GetProperty("additionalProperties").GetBoolean());

            Dictionary<string, object?> ScriptArguments(string? mode) {
                var arguments = new Dictionary<string, object?> { ["path"] = script };
                if (mode is not null) {
                    arguments[GovernedScriptSideEffectManifest.ArgumentName] = JsonSerializer.SerializeToElement(new { mode });
                }
                return arguments;
            }

            using var client = new ReadWriteReadClient(index => index switch {
                0 => ReadWriteReadClient.Call("malformed", scriptTool, ScriptArguments("ReadOnly")),
                1 => ReadWriteReadClient.Call("unknown-field", scriptTool, new() {
                    ["path"] = script,
                    [GovernedScriptSideEffectManifest.ArgumentName] = JsonSerializer.SerializeToElement(new {
                        mode = nameof(GovernedScriptSideEffectMode.ProductMutation), allowedWriteRoots = Array.Empty<string>()
                    })
                }),
                2 => ReadWriteReadClient.Call("validation", scriptTool,
                    ScriptArguments(declaresValidation ? nameof(GovernedScriptSideEffectMode.NoMutation) : null)),
                3 => ReadWriteReadClient.Call("write", ToolContractCatalog.WorkspaceWriteFile,
                    new() { ["path"] = PrimaryRef, ["content"] = FinalContent, ["overwrite"] = true }),
                _ => new(new ChatMessage(ChatRole.Assistant, "completed"))
            });
            var inner = new ChatClientAgent(client, new ChatClientAgentOptions {
                ChatOptions = new ChatOptions { Tools = capabilities.Tools, AllowMultipleToolCalls = false },
                UseProvidedChatClientAsIs = false
            });
            var recorder = new ToolInvocationTraceRecorder();
            var factory = new MafRuntimeAgentFactory(directory.FullName, WorkspaceScopeDescriptor.Sandbox,
                dependencies.ProviderCredentialService, dependencies.ProviderAgentFactory, composer,
                dependencies.PhysicalPathPolicyFactory, provider.GetRequiredService<ILoggerFactory>(), dependencies.ToolInvocationPolicyPipeline);
            var runtime = factory.CreateInstrumentedAgent(inner, profile, agent, capabilities,
                suppressApprovalRequirements: true, toolInvocationTraceRecorder: recorder, finalizerPolicy: null,
                finalizerMode: AgentFinalizerMode.Disabled, executionGovernance: null,
                scriptPolicyInspectionService: new(directory.FullName, WorkspaceScopeDescriptor.Sandbox,
                    TestWorkspaceServices.PhysicalPathPolicyFactory, new ExternalTargetPathRegistryFactory().Create([])));

            using var audit = WorkspaceExecutionAuditContext.BeginScope(CreateRun(agent.Id, allowsProductMutation: true));
            var result = await runtime.RunAsync("Run the validation and write the evidence.", await runtime.CreateSessionAsync());
            Assert.Equal("completed", result.Text);
            Assert.Equal(1, commands.Invocations);
            var traces = recorder.Snapshot();
            Assert.Equal(4, traces.Count);
            Assert.All(traces.Take(2), trace => {
                Assert.Equal(ToolInvocationClassification.Validation, trace.Classification);
                Assert.Equal(AgentToolEffectState.NotCommitted, trace.EffectState);
                Assert.Equal(AgentToolInvocationOutcome.Failed, trace.Outcome);
            });
            Assert.Equal(AgentToolInvocationOutcome.Failed, traces[2].Outcome);
            Assert.Equal(declaresValidation ? ToolInvocationClassification.Validation : ToolInvocationClassification.Mutation, traces[2].Classification);
            Assert.Equal(declaresValidation ? AgentToolEffectState.None : AgentToolEffectState.Unknown, traces[2].EffectState);
            Assert.Equal(AgentToolEffectState.Committed, traces[3].EffectState);
            Assert.Equal(declaresValidation ? ExecutionState.Completed : ExecutionState.Failed,
                AgentToolCompletionAssessment.Create(traces, 0, portableOutputValid: true).State);
        } finally {
            if (capabilities is not null) {
                Assert.Empty(await capabilities.DisposeAcquiredResourcesAsync());
            }
            directory.Delete(recursive: true);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actual_workspace_tools_recover_precreation_read_then_wait_for_approval_and_read_the_written_output(bool useStreaming) {
        var directory = Directory.CreateTempSubdirectory(nameof(ProcessToolInvocationMafRuntimeTests));
        RuntimeCapabilityState? capabilityState = null;
        try {
            var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
            services.AddLogging();
            services.AddSingleton<IToolInvocationPolicyContextContributor, ProcessToolInvocationPolicyContextContributor>();
            await using var provider = services.BuildServiceProvider();
            var dependencies = MafAgentRuntimeDependencies.FromServices(provider);
            var composer = RuntimeCapabilityComposer.CreateDefault(directory.FullName, provider);
            var agentDefinition = CreateAgent();
            var providerProfile = CreateProvider();
            await using var workspace = WorkspaceRuntimeServicesTestFactory.Create(directory.FullName);
            capabilityState = await composer.CreateCapabilityStateAsync(agentDefinition, providerProfile, [], [], workspace,
                static (_, _, _) => Task.CompletedTask, CancellationToken.None);
            Assert.IsType<ApprovalRequiredAIFunction>(Assert.Single(capabilityState.Tools,
                tool => tool.Name == ToolContractCatalog.WorkspaceWriteFile));
            Assert.IsNotType<ApprovalRequiredAIFunction>(Assert.Single(capabilityState.Tools,
                tool => tool.Name == ToolContractCatalog.WorkspaceReadFile));

            using var client = new ReadWriteReadClient();
            var innerAgent = new ChatClientAgent(client, new ChatClientAgentOptions {
                ChatOptions = new ChatOptions { Tools = capabilityState.Tools, AllowMultipleToolCalls = false },
                UseProvidedChatClientAsIs = false
            });
            var factory = new MafRuntimeAgentFactory(directory.FullName, WorkspaceScopeDescriptor.Sandbox,
                dependencies.ProviderCredentialService, dependencies.ProviderAgentFactory, composer,
                dependencies.PhysicalPathPolicyFactory, provider.GetRequiredService<ILoggerFactory>(),
                dependencies.ToolInvocationPolicyPipeline);
            var traces = new ToolInvocationTraceRecorder();
            var runtime = factory.CreateInstrumentedAgent(innerAgent, providerProfile, agentDefinition, capabilityState,
                suppressApprovalRequirements: false, toolInvocationTraceRecorder: traces, finalizerPolicy: null,
                finalizerMode: AgentFinalizerMode.Disabled, executionGovernance: null,
                scriptPolicyInspectionService: new MafScriptPolicyInspectionService(directory.FullName, WorkspaceScopeDescriptor.Sandbox,
                    TestWorkspaceServices.PhysicalPathPolicyFactory, new ExternalTargetPathRegistryFactory().Create([])));
            var run = CreateRun(agentDefinition.Id);
            using var audit = WorkspaceExecutionAuditContext.BeginScope(run);
            var session = await runtime.CreateSessionAsync();
            var initial = await RunAsync(runtime, [new ChatMessage(ChatRole.User, "Record the governed evidence.")], session, useStreaming);
            var approval = Assert.Single(initial.Messages.SelectMany(message => message.Contents).OfType<ToolApprovalRequestContent>());
            Assert.Equal(ToolContractCatalog.WorkspaceWriteFile, Assert.IsType<FunctionCallContent>(approval.ToolCall).Name);
            Assert.False(File.Exists(Path.Combine(directory.FullName, PrimaryRef)));
            Assert.Contains("Do not retry the read, stat, list, or search.", Assert.Single(client.ToolResults)?.ToString(), StringComparison.Ordinal);
            var deniedRead = Assert.Single(traces.Snapshot());
            Assert.False(deniedRead.Succeeded);
            Assert.Equal(AgentToolInvocationOutcome.Failed, deniedRead.Outcome);
            Assert.Equal(AgentToolEffectState.NotCommitted, deniedRead.EffectState);
            Assert.Equal("ToolPolicyDenied", deniedRead.FailureCode);
            Assert.Equal(ToolContractCatalog.WorkspaceReadFile, deniedRead.ToolName);

            var completed = await RunAsync(runtime, [new ChatMessage(ChatRole.User, [approval.CreateResponse(approved: true)])],
                session, useStreaming);

            Assert.Equal("completed", completed.Text);
            Assert.Equal(FinalContent, await File.ReadAllTextAsync(Path.Combine(directory.FullName, PrimaryRef)));
            Assert.Equal(3, client.ToolResults.Count);
            Assert.Equal(FinalContent, Assert.IsType<JsonElement>(client.ToolResults[2]).GetProperty("content").GetString());
            var recorded = traces.Snapshot();
            Assert.Equal([ToolContractCatalog.WorkspaceReadFile, ToolContractCatalog.WorkspaceWriteFile, ToolContractCatalog.WorkspaceReadFile],
                recorded.Select(trace => trace.ToolName).ToArray());
            Assert.All(recorded.Skip(1), trace => Assert.True(trace.Succeeded));
        } finally {
            if (capabilityState is not null) {
                Assert.Empty(await capabilityState.DisposeAcquiredResourcesAsync());
            }
            directory.Delete(recursive: true);
        }
    }

    private static async Task<AgentResponse> RunAsync(AIAgent agent, IReadOnlyList<ChatMessage> messages,
        AgentSession session, bool useStreaming)
        => useStreaming
            ? await agent.RunStreamingAsync(messages, session).ToAgentResponseAsync()
            : await agent.RunAsync(messages, session);

    private static AgentDefinition CreateAgent() {
        var now = DateTimeOffset.UtcNow;
        return new(Guid.NewGuid(), "Process evidence operator", "Test operator", string.Empty, "Use the supplied workspace tools.",
            AgentLifecycleStatus.Active, Guid.NewGuid(), "gpt-4.1", AgentWorkloadKind.Programming, AgentChatHistoryMode.FrameworkManaged,
            0, false, false, AgentWorkspaceToolAccessMetadata.Write("{}", new AgentWorkspaceToolAccessSettings {
                Profile = AgentWorkspaceToolProfileKind.Custom,
                CanReadFiles = true,
                CanWriteFiles = true,
                CanRunLocalScripts = true
            }), false, string.Empty, AgentPermissionsPolicy.Default with {
                CanUseTools = true,
                CanAskOtherAgents = false,
                RequiresApprovalForExternalCalls = false
            }, [], [], now, now);
    }

    private static ProviderProfile CreateProvider()
        => new(Guid.NewGuid(), "Process test provider", ProviderKind.OpenAi, "https://api.openai.com/v1", "OPENAI_API_KEY",
            "gpt-4.1", ProviderTransportKind.ChatCompletions, true, true, true, false, false, "{}", string.Empty,
            "Not checked", null, []);

    private static ExecutionRunRecord CreateRun(Guid agentId, bool allowsProductMutation = false) {
        var now = DateTimeOffset.UtcNow;
        var metadata = JsonSerializer.Serialize(new Dictionary<string, object?> {
            [ExecutionInvocationMetadata.ProcessStepAllowedOperationsMetadataKey] = new[] {
                ProcessOperationContractNames.ReadProcessContext, ProcessOperationContractNames.WriteManagedProcessArtifacts,
                ProcessOperationContractNames.RunValidation
            },
            [ExecutionInvocationMetadata.ProcessStepTargetScopeMetadataKey] = ProcessOperationContractNames.ManagedProcessArtifactsOnly,
            [ExecutionInvocationMetadata.ProcessStepAllowsProductMutationMetadataKey] = allowsProductMutation
        });
        return new(Guid.NewGuid(), agentId, null, "Governed evidence fixture", "process-step", "record-evidence", string.Empty,
            string.Empty, "process-runtime", "system", metadata, string.Empty, string.Empty, "OpenAI", "gpt-4.1",
            ExecutionState.Running, null, now, now, now, null, string.Empty, null, [], ProcessRunId: "run-a", ProcessStepId: "step-instance-a");
    }

    private sealed class ReadWriteReadClient(Func<int, ChatResponse>? script = null) : IChatClient {
        private int responseCount;
        public List<object?> ToolResults { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            if (responseCount > 0) {
                var result = messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Last().Result;
                ToolResults.Add(result);
            }
            var next = responseCount++;
            return Task.FromResult(script?.Invoke(next) ?? next switch {
                0 => Call("read-before", ToolContractCatalog.WorkspaceReadFile, new() { ["path"] = PrimaryRef }),
                1 => Call("write-evidence", ToolContractCatalog.WorkspaceWriteFile, new() {
                    ["path"] = PrimaryRef, ["content"] = FinalContent, ["overwrite"] = true
                }),
                2 => Call("read-after", ToolContractCatalog.WorkspaceReadFile, new() { ["path"] = PrimaryRef }),
                _ => new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed"))
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default) {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            foreach (var update in response.ToChatResponseUpdates()) {
                yield return update;
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null)
            => serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose() {
        }

        internal static ChatResponse Call(string id, string tool, Dictionary<string, object?> arguments)
            => new(new ChatMessage(ChatRole.Assistant, [new FunctionCallContent(id, tool, arguments)]));
    }

    private sealed class FailingScriptHost : IWorkspaceProcessHost {
        public int Invocations { get; private set; }
        public ExecutionBoundaryDescriptor DescribeBoundary() => ExecutionBoundaryDescriptor.Unknown;

        public Task<WorkspaceProcessExecutionResult> ExecuteAsync(WorkspaceProcessExecutionRequest request, CancellationToken cancellationToken = default) {
            Invocations++;
            var now = DateTimeOffset.UtcNow;
            return Task.FromResult(new WorkspaceProcessExecutionResult(true, 1, "", "AssertionError", false, false,
                now, now, false, DescribeBoundary(), ""));
        }
    }
}
