using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.Infrastructure.Persistence;
using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafConfiguredWorkspaceImageAdmissionIntegrationTests {
    [Theory]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImage, false)]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImage, true)]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImages, false)]
    [InlineData(ToolContractCatalog.WorkspaceAnalyzeImages, true)]
    public async Task Actual_configured_image_call_is_not_redispatched_after_a_metered_acknowledgement_is_lost(string name, bool approvalRequired) {
        await using var application = await TestApplication.CreateAsync();
        await using var scope = application.Services.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
        await using var fixture = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding:
            new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)),
            configureAgent: actor => actor with {
                Id = Guid.NewGuid(), Name = "Configured image admission fixture", TemplateKey = string.Empty,
                IsTemplate = false, Workload = AgentWorkloadKind.General, Model = "qwen3.5:9b", ConfigurationJson = "{}", Capabilities = []
            });
        await File.WriteAllBytesAsync(Path.Combine(fixture.WorkspaceRoot, "source.png"), Convert.FromBase64String(Png));
        var counter = new MeteredAnalysis();
        CapabilityCatalogItem[] capabilities = approvalRequired ? [new(Guid.NewGuid(), CapabilityKind.Tool, "reviewed-workspace-image",
            "Reviewed image analysis", string.Empty, string.Empty, JsonSerializer.Serialize(new { tool = name, approvalRequired = true }),
            CapabilityProofStatus.Verified, string.Empty, DateTimeOffset.UtcNow, IsBuiltIn: true)] : [];
        var agent = fixture.Agent with {
            Permissions = fixture.Agent.Permissions with { CanUseTools = true },
            ConfigurationJson = AgentWorkspaceToolAccessMetadata.Write("{}", new() {
                Profile = AgentWorkspaceToolProfileKind.Custom, CanReadFiles = true, CanTransformArtifacts = true
            }),
            Capabilities = capabilities.Select(item => new AgentCapabilityAssignment(item.Id, item.Key, item.Kind,
                item.ProofStatus, item.LastVerifiedAtUtc, item.ProofNotes)).ToArray()
        };
        agent = agent with {
            IsTemplate = false, TemplateKey = string.Empty, Status = AgentLifecycleStatus.Active,
            ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(agent.ConfigurationJson)
        };
        var catalogStore = services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
        await catalogStore.UpdateCatalogAsync(current => current with {
            Agents = current.Agents.Where(item => item.Id != agent.Id).Append(agent).ToArray(),
            Capabilities = current.Capabilities.Where(item => !capabilities.Any(capability => capability.Id == item.Id))
                .Concat(capabilities).ToArray()
        });
        var savedActor = (await catalogStore.LoadCatalogAsync()).Agents.Single(item => item.Id == agent.Id);
        Assert.True(savedActor.Permissions.CanUseTools);
        Assert.True(AgentWorkspaceToolAccessMetadata.Read(savedActor.ConfigurationJson).CanTransformArtifacts);
        Assert.Equal(capabilities.Select(item => item.Id).Order(), savedActor.Capabilities.Select(item => item.CapabilityId).Order());
        var provider = fixture.Provider with {
            Kind = ProviderKind.Ollama, Transport = ProviderTransportKind.ChatCompletions,
            DefaultModel = "qwen3.5:9b", SuggestedModels = ["qwen3.5:9b"], ConfigurationJson = "{\"supportsVision\":true}"
        };
        if (approvalRequired) {
            var waiting = await ExecuteAsync(new(name));
            var approval = Assert.Single(waiting.PendingApprovals);
            Assert.Equal(name, approval.ToolName);
            Assert.NotNull(approval.ToolAdmission);
            Assert.Empty(counter.Requests);
            await fixture.ApproveAsync(waiting.PendingApprovals);
        }
        var dispatchClient = new ImageClient(name);
        var dispatchFailure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(dispatchClient));
        var injected = Assert.IsType<IOException>(counter.InjectedDispatchFailure);
        Assert.Equal("Injected acknowledgement loss after the image-analysis provider accepted the request.", injected.Message);
        Assert.Contains("tool-admission.reconciliation-required", Codes(dispatchFailure));
        Assert.Equal(approvalRequired ? 0 : 1, dispatchClient.Requests);
        var saved = Assert.Single(Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!
            .ToolAdmission!.Batches).Proposals);
        Assert.Equal(name, saved.Payload.ToolName);
        Assert.Equal(AgentToolProposalEffect.Read, saved.Payload.Effect);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, saved.Payload.Recovery);
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, saved.State);
        Assert.Equal(ExecutionApprovalStatus.Approved, saved.ApprovalStatus);
        Assert.Equal(approvalRequired, saved.RequiresApproval);
        Assert.Equal(saved.Payload.Digest, saved.ApprovedDigest);
        Assert.Single(counter.Requests);
        Assert.Single(counter.Requests[0].Sources);
        var retry = new ImageClient(name);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(retry));
        Assert.Contains("tool-admission.reconciliation-required", Codes(denied));
        Assert.Equal(0, retry.Requests);
        Assert.Single(counter.Requests);
        Assert.Equal(saved, Assert.Single(Assert.Single((await fixture.NewStore().GetExecutionRunAsync(fixture.Session.ExecutionRunId))!
            .ToolAdmission!.Batches).Proposals));

        Task<AgentRuntimeResponse> ExecuteAsync(ImageClient client) {
            var dependencies = MafAgentRuntimeDependencies.FromServices(services);
            dependencies = dependencies with {
                ProviderAgentFactory = new ImageAgentFactory(client, name, approvalRequired), ImageAnalysisService = counter,
                ToolAdmissionJournal = fixture.NewJournal(fixture.NewStore()),
                CapabilityDependencies = dependencies.CapabilityDependencies with { RuntimeToolProviders = [], ContextContributors = [] }
            };
            var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
                AdmittedToolSession = fixture.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
                Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(fixture.Detail.Run.MetadataJson),
                AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context",
                ContextIntent = AgentRuntimeContextIntent.Empty with {
                    Purpose = AgentRuntimeContextPurpose.InteractiveChat, WorkspaceToolsEnabled = true,
                    ToolCapabilitiesEnabled = true, RuntimeToolProvidersEnabled = false
                }
            };
            return new MafAgentRuntime(fixture.WorkspaceRoot, fixture.StorageScope, dependencies).ExecutionPort.ExecuteAsync(
                new(agent, provider, fixture.Detail.ChatSession!, capabilities, [], "Analyze the reviewed workspace image.", string.Empty,
                    (_, _, _) => Task.CompletedTask, ExecutionOptions: options));
        }
    }

    private static IEnumerable<string> Codes(Exception exception) {
        if (exception is AgentToolAdmissionException admission) {
            yield return admission.Code;
        }
        if (exception.InnerException is { } inner) {
            foreach (var code in Codes(inner)) {
                yield return code;
            }
        }
    }

    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    private sealed class MeteredAnalysis : IAgentImageAnalysisService {
        internal List<AgentImageAnalysisRequest> Requests { get; } = [];
        internal IOException? InjectedDispatchFailure { get; private set; }
        public Task<AgentImageAnalysisResult> AnalyzeAsync(AgentImageAnalysisRequest request, CancellationToken cancellationToken = default) {
            Requests.Add(request);
            Assert.Null(InjectedDispatchFailure);
            InjectedDispatchFailure = new IOException("Injected acknowledgement loss after the image-analysis provider accepted the request.");
            throw InjectedDispatchFailure;
        }
    }

    private sealed class ImageAgentFactory(ImageClient client, string name, bool requiresApproval) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            var tool = Assert.Single(options.ChatOptions!.Tools!, tool => tool.Name == name);
            Assert.Equal(requiresApproval, tool is ApprovalRequiredAIFunction);
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ImageClient(string name) : IChatClient {
        internal int Requests { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) {
            Requests++;
            if (messages.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()) {
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            Dictionary<string, object?> arguments = new() { ["prompt"] = "Describe the reviewed image" };
            arguments[name == ToolContractCatalog.WorkspaceAnalyzeImage ? "path" : "paths"] =
                name == ToolContractCatalog.WorkspaceAnalyzeImage ? "source.png" : new[] { "source.png" };
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("diagnostic-workspace-image-call", name, arguments)])));
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
