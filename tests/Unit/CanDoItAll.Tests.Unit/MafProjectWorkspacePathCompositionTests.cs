using System.Runtime.CompilerServices;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Modules.Projects;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class MafProjectWorkspacePathCompositionTests {
    public enum ReadTarget { Current, Foreign, LegacyBrief }

    [Theory]
    [InlineData(false, ReadTarget.Current)]
    [InlineData(true, ReadTarget.Current)]
    [InlineData(false, ReadTarget.Foreign)]
    [InlineData(true, ReadTarget.Foreign)]
    [InlineData(false, ReadTarget.LegacyBrief)]
    [InlineData(true, ReadTarget.LegacyBrief)]
    public async Task Real_Maf_composition_uses_Projects_policy_for_Rag_and_configured_file_tools_without_Structure_tools(
        bool streaming, ReadTarget target) {
        var root = Path.Combine(Path.GetTempPath(), nameof(MafProjectWorkspacePathCompositionTests), Guid.NewGuid().ToString("N"));
        var project = Guid.NewGuid();
        var scope = WorkspaceScopeDescriptor.Project(project.ToString("D"));
        var relativePath = $"managed-files/project-media/files/{project:N}/current.md";
        var current = Path.Combine(root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var foreign = Path.Combine(root, "managed-files", "project-media", "files", Guid.NewGuid().ToString("N"), "foreign.md");
        Directory.CreateDirectory(Path.GetDirectoryName(current)!);
        Directory.CreateDirectory(Path.GetDirectoryName(foreign)!);
        await File.WriteAllTextAsync(current, "analyze project structure summary CURRENT-PROJECT-CONTEXT");
        await File.WriteAllTextAsync(foreign, "analyze project structure summary FOREIGN-PROJECT-CONTEXT");
        await File.WriteAllTextAsync(Path.Combine(root, "project-structure-context-brief.md"),
            "analyze project structure summary SHARED-ROOT-CONTEXT");
        try {
            var services = MafRuntimeTestServices.CreateProviderRuntimeServiceCollection();
            services.RemoveAll<IToolInvocationPolicyContextContributor>();
            services.AddProjectsModule();
            var requestedPath = target switch {
                ReadTarget.Current => relativePath,
                ReadTarget.Foreign => Path.GetRelativePath(root, foreign).Replace(Path.DirectorySeparatorChar, '/'),
                ReadTarget.LegacyBrief => "project-structure-context-brief.md",
                _ => throw new ArgumentOutOfRangeException(nameof(target))
            };
            var client = new ReadFileClient(requestedPath);
            services.AddSingleton<IMafProviderAgentFactory>(new Factory(client));
            using var container = services.BuildServiceProvider();
            var provider = CreateProvider();
            var capability = new CapabilityCatalogItem(Guid.NewGuid(), CapabilityKind.Rag,
                "project-path-rag", "Workspace source", "Read the current project source", string.Empty,
                "{}", CapabilityProofStatus.Verified, string.Empty, null, false);
            var definition = CreateAgent(provider.Id, capability);
            var intent = AgentRuntimeContextIntent.Empty with {
                WorkspaceScope = scope,
                RuntimeToolProvidersEnabled = false,
                WorkspaceToolsEnabled = true,
                ToolCapabilitiesEnabled = true,
                Purpose = AgentRuntimeContextPurpose.InteractiveChat
            };
            var options = MafRuntimeExecutionOptionsResolver.CreateDisabled(null) with {
                ContextWorkspaceScope = scope,
                ContextIntent = intent
            };
            var now = DateTimeOffset.UtcNow;
            var run = new ExecutionRunRecord(Guid.NewGuid(), definition.Id, null, "Project path proof",
                "project-structure", scope.Key, "path-proof", string.Empty, "user", "test", "{}", "input", string.Empty,
                provider.Name, provider.DefaultModel, ExecutionState.Running, null, now, now, now, null, string.Empty, null, []);
            using var audit = WorkspaceExecutionAuditContext.BeginScope(run, scope);
            var runtime = new MafAgentRuntime(root, container, scope);
            var agent = await runtime.CreateHostedAgentAsync(definition, provider, [capability], [], executionOptions: options);
            try {
                var session = await agent.CreateSessionAsync();
                ChatMessage[] messages = [new(ChatRole.User, "analyze project structure summary and read the current asset")];
                async Task<AgentResponse> RunAsync() => streaming
                    ? await agent.RunStreamingAsync(messages, session).ToAgentResponseAsync()
                    : await agent.RunAsync(messages, session);
                if (target == ReadTarget.Current) {
                    var response = await RunAsync();
                    Assert.Equal("completed", response.Text);
                    Assert.Equal(2, client.Requests);
                    var result = Assert.Single(client.Results);
                    Assert.Equal("current-read", result.CallId);
                    Assert.Contains("CURRENT-PROJECT-CONTEXT", result.Result?.ToString(), StringComparison.Ordinal);
                    Assert.DoesNotContain("FOREIGN-PROJECT-CONTEXT", result.Result?.ToString(), StringComparison.Ordinal);
                } else {
                    var response = await RunAsync();
                    Assert.Equal("completed", response.Text);
                    Assert.Equal(2, client.Requests);
                    var result = Assert.Single(client.Results);
                    Assert.Equal("current-read", result.CallId);
                    var failure = Assert.IsAssignableFrom<Exception>(result.Exception);
                    var denied = Assert.Single(ExceptionChain(failure).OfType<AgentToolPolicyBlockedException>());
                    Assert.Equal(ToolInvocationDecisionKind.Deny, denied.DecisionKind);
                    Assert.Contains(target == ReadTarget.Foreign ? "not owned by project" : "not project-owned",
                        denied.Reason, StringComparison.Ordinal);
                    Assert.DoesNotContain("FOREIGN-PROJECT-CONTEXT", result.Result?.ToString(), StringComparison.Ordinal);
                    Assert.DoesNotContain("SHARED-ROOT-CONTEXT", result.Result?.ToString(), StringComparison.Ordinal);
                }
                Assert.Contains(ToolContractCatalog.WorkspaceReadFile, client.ToolNames);
                Assert.DoesNotContain(client.ToolNames, name => name.StartsWith("project_structure_", StringComparison.Ordinal));
                Assert.Contains("CURRENT-PROJECT-CONTEXT", client.InitialContext, StringComparison.Ordinal);
                Assert.DoesNotContain("FOREIGN-PROJECT-CONTEXT", client.InitialContext, StringComparison.Ordinal);
                Assert.DoesNotContain("SHARED-ROOT-CONTEXT", client.InitialContext, StringComparison.Ordinal);
            } finally {
                await Assert.IsAssignableFrom<IAsyncDisposable>(agent).DisposeAsync();
            }
        } finally {
            Directory.Delete(root, recursive: true);
        }
    }

    private static IEnumerable<Exception> ExceptionChain(Exception failure) {
        for (Exception? current = failure; current is not null; current = current.InnerException) {
            yield return current;
        }
    }

    private static AgentDefinition CreateAgent(Guid providerId, CapabilityCatalogItem capability) {
        var configuration = AgentWorkspaceToolAccessMetadata.Write("{}", new AgentWorkspaceToolAccessSettings {
            Profile = AgentWorkspaceToolProfileKind.Custom,
            CanReadFiles = true
        });
        return new(Guid.NewGuid(), "Project file reader", "Reader", "Project path policy proof", "Use the supplied read tool.",
            AgentLifecycleStatus.Active, providerId, string.Empty, AgentWorkloadKind.Programming,
            AgentChatHistoryMode.FrameworkManaged, 0, false, false, configuration, false, string.Empty,
            AgentPermissionsPolicy.Default with { CanUseTools = true, CanAskOtherAgents = false, RequiresApprovalForExternalCalls = false },
            [new(capability.Id, capability.Key, capability.Kind, CapabilityProofStatus.Verified, DateTimeOffset.UtcNow, string.Empty)],
            [], DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
    }

    private static ProviderProfile CreateProvider()
        => new(Guid.NewGuid(), "Synthetic transport", ProviderKind.OpenAi, "https://api.openai.com/v1", "OPENAI_API_KEY",
            "gpt-4.1", ProviderTransportKind.ChatCompletions, true, true, true, false, true, "{}", string.Empty, "Not checked", null, []);

    private sealed class Factory(IChatClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) => client.AsAIAgent(options: options);
    }

    private sealed class ReadFileClient(string relativePath) : IChatClient {
        public int Requests { get; private set; }
        public string InitialContext { get; private set; } = string.Empty;
        public IReadOnlyList<string> ToolNames { get; private set; } = [];
        public IReadOnlyList<FunctionResultContent> Results { get; private set; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            var snapshot = messages.ToArray();
            Requests++;
            if (Requests == 1) {
                InitialContext = string.Join(Environment.NewLine, snapshot.Select(message => message.Text));
                ToolNames = options?.Tools?.Select(tool => tool.Name).ToArray() ?? [];
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, [
                    new FunctionCallContent("current-read", ToolContractCatalog.WorkspaceReadFile,
                        new Dictionary<string, object?> { ["path"] = relativePath })
                ])));
            }
            Results = snapshot.SelectMany(message => message.Contents).OfType<FunctionResultContent>().ToArray();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
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

        public void Dispose() { }
    }
}
