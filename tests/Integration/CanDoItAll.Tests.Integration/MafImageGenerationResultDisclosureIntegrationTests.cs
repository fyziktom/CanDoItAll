using System.Runtime.CompilerServices;
using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Maf;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Providers;
using CanDoItAll.AgentFramework.Runtime.Abstractions;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.FileSystem;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using ProviderMetadata = CanDoItAll.Modules.AgentFramework.ProviderManagement.ProviderMetadata;

namespace CanDoItAll.Tests.Integration.Runtime;

[Trait("Category", "FileSystemPortability")]
public sealed class MafImageGenerationResultDisclosureIntegrationTests {
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task Actual_MAF_image_approval_and_saved_acknowledgement_revalidate_current_owner_read_without_regeneration(int revocation) {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Equal(AgentToolEffectState.Unknown, saved.EffectState);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, saved.Payload.Recovery);
        Assert.Equal(ExecutionApprovalStatus.Approved, saved.ApprovalStatus);
        Assert.Equal(saved.Payload.Digest, saved.ApprovedDigest);
        var evidence = ImageGenerationDisclosureEvidenceCodec.Read(saved.DisclosureEvidence!);
        Assert.Equal(ImageGenerationDisclosureState.Complete, evidence.State);
        Assert.Equal(fixture.Source, Assert.Single(evidence.SourceProjects));
        Assert.Equal(fixture.Journal.Session, evidence.Session);
        Assert.Single(fixture.Generation.Requests);
        Assert.Single(fixture.Generation.Requests[0].Sources);
        File.Delete(evidence.Output.FullPath);

        if (revocation == 1) {
            await fixture.SetProviderEnabledAsync(false);
        } else {
            await fixture.SaveActorAsync(canGenerate: revocation != 0, canRead: revocation != 3, includeSource: revocation != 2);
        }
        var deniedClient = new ImageClient(fixture.Request);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(deniedClient));
        Assert.Contains("image-generation.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, deniedClient.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
        Assert.Single(fixture.Generation.Requests);

        await fixture.SetProviderEnabledAsync(true);
        await fixture.SaveActorAsync();
        var restoredClient = new ImageClient(fixture.Request);
        var restored = await fixture.ExecuteAsync(restoredClient);
        Assert.Contains("completed", restored.ResponseText, StringComparison.Ordinal);
        Assert.Equal(1, restoredClient.Requests);
        Assert.Contains(evidence.Output.RelativePath, Assert.Single(restoredClient.Inputs), StringComparison.Ordinal);
        Assert.False(File.Exists(evidence.Output.FullPath));
        Assert.Single(fixture.Generation.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Reused_source_project_identity_cannot_restamp_original_image_evidence(bool duringGeneration) {
        await using var fixture = await Fixture.CreateAsync();
        if (duringGeneration) {
            fixture.Generation.OnGenerate = fixture.ReplaceSourceAsync;
        }
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        if (!duringGeneration) {
            await fixture.ReplaceSourceAsync();
        }
        await fixture.SaveActorAsync();
        var evidence = ImageGenerationDisclosureEvidenceCodec.Read(saved.DisclosureEvidence!);
        Assert.NotEqual(fixture.Source.LifetimeId, Assert.Single(evidence.SourceProjects).LifetimeId);
        Assert.Equal(duringGeneration ? ImageGenerationDisclosureState.LifetimeChangedDuringOperation : ImageGenerationDisclosureState.Complete,
            evidence.State);
        var next = new ImageClient(fixture.Request);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(next));
        Assert.Contains(duringGeneration ? "image-generation.result-authority-unavailable" : "image-generation.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, next.Requests);
        Assert.Single(fixture.Generation.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Fact]
    public async Task Lost_metered_image_acknowledgement_remains_uncertain_after_restart_and_never_dispatches_again() {
        await using var fixture = await Fixture.CreateAsync();
        fixture.Generation.FailAfterDispatch = true;
        await fixture.ApproveAsync();
        var dispatchClient = new ImageClient(fixture.Request);
        var dispatchFailure = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(dispatchClient));
        var injected = Assert.IsType<IOException>(fixture.Generation.InjectedDispatchFailure);
        Assert.Equal("Injected acknowledgement loss after metered image dispatch.", injected.Message);
        Assert.Contains("tool-admission.reconciliation-required", Codes(dispatchFailure));
        Assert.Equal(0, dispatchClient.Requests);
        var saved = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.ReconciliationRequired, saved.State);
        Assert.Equal(AgentToolProposalRecovery.ReconcileBeforeRetry, saved.Payload.Recovery);
        Assert.Null(saved.DisclosureEvidence);
        Assert.Single(fixture.Generation.Requests);
        var replay = new ImageClient(fixture.Request);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(replay));
        Assert.Contains("tool-admission.reconciliation-required", Codes(denied));
        Assert.Equal(0, replay.Requests);
        Assert.Single(fixture.Generation.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Fact]
    public async Task An_original_result_cannot_be_disclosed_through_a_different_resolved_workspace_root() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CompleteThroughLostFinalAcknowledgementAsync();
        var saved = await fixture.ProposalAsync();
        var foreign = new WorkspacePathResolutionService(Path.Combine(fixture.Journal.WorkspaceRoot, "unrelated-root"),
            fixture.Services.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>());
        var next = new ImageClient(fixture.Request);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(next, foreign));
        Assert.Contains("image-generation.result-disclosure-denied", Codes(denied));
        Assert.Equal(0, next.Requests);
        Assert.Single(fixture.Generation.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
    }

    [Fact]
    public async Task A_legacy_saved_image_result_without_original_evidence_is_explicitly_unavailable_without_regeneration() {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CompleteThroughLostFinalAcknowledgementAsync(legacyAttachment: true);
        var saved = await fixture.ProposalAsync();
        Assert.Equal(AgentToolProposalState.Completed, saved.State);
        Assert.Null(saved.DisclosureEvidence);
        var next = new ImageClient(fixture.Request);
        var denied = await Assert.ThrowsAnyAsync<Exception>(() => fixture.ExecuteAsync(next));
        Assert.Contains("image-generation.result-authority-unavailable", Codes(denied));
        Assert.Equal(0, next.Requests);
        Assert.Single(fixture.Generation.Requests);
        Assert.Equal(saved, await fixture.ProposalAsync());
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

    private sealed class Fixture(TestApplication application, AsyncServiceScope scope, AgentToolAdmissionJournalFixture journal,
        ProjectWriteAdmission parent, ProjectWriteAdmission source, Guid providerId, string sourceNodeId) : IAsyncDisposable {
        internal AgentToolAdmissionJournalFixture Journal => journal;
        internal ProjectWriteAdmission Source { get; private set; } = source;
        internal GenerationCounter Generation { get; } = new();
        internal IServiceProvider Services => scope.ServiceProvider;
        internal AgentDefinition Agent { get; private set; } = journal.Agent;
        internal ImageGenerationCreateInput Request { get; } = new("Generate a reviewed image", "output/saved-image.png",
            ProviderProfileId: providerId, SourceProjectAssets: [new(source.ProjectId, sourceNodeId)]);

        internal static async Task<Fixture> CreateAsync() {
            var application = await TestApplication.CreateAsync();
            var scope = application.Services.CreateAsyncScope();
            AgentToolAdmissionJournalFixture? journal = null;
            try {
                var services = scope.ServiceProvider;
                var parent = await CreateProjectAsync(services, "Image invocation source");
                var source = await CreateProjectAsync(services, "Image source asset");
                var image = await services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(source.ProjectId,
                    new(ProjectObjectType.ImageAsset, "Original source image", string.Empty, string.Empty, $"project:{source.ProjectId:D}",
                        ObjectSubtype: "png", Media: new("source.png", "image/png", Png)) { ExpectedProjectAdmission = source });
                var profile = services.GetRequiredService<IDatabaseRuntimeState>().GetSnapshot();
                journal = await AgentToolAdmissionJournalFixture.CreateAsync(profileBinding:
                    new(profile.ActiveProfileId!.Value, profile.ActiveFingerprint!, new(profile.Generation)),
                    transientContext: new("Original admitted image scope", WorkspaceScopeDescriptor.Project(parent.ProjectId.ToString("D"))),
                    storageScope: WorkspaceScopeDescriptor.Project(parent.ProjectId.ToString("D")));
                Assert.Null(await services.GetRequiredService<ISandboxWorkspaceExecutionRunStore>().GetExecutionRunAsync(journal.Session.ExecutionRunId));
                Assert.NotNull(await journal.NewStore().GetExecutionRunAsync(journal.Session.ExecutionRunId));
                var secret = await services.GetRequiredService<SecretService>().SaveAsync(new SecretEditorModel {
                    Name = "Image disclosure synthetic provider key", Kind = SecretKind.ApiKey,
                    SecretValue = "sk-test-image-disclosure", Scope = "workspace"
                });
                Assert.True(secret.IsSuccess);
                var provider = await services.GetRequiredService<IProviderProfileRegistry>().SaveProviderAsync(new() {
                    Name = "Image disclosure provider", Kind = ProviderKind.OpenAi, Purpose = ProviderProfilePurpose.ImageGeneration,
                    Transport = ProviderTransportKind.Responses, BaseUrl = "https://image-provider.example.test",
                    DefaultModel = "gpt-image-1-mini", SuggestedModels = ["gpt-image-1-mini"], IsEnabled = true, ApiKeyEnvironmentVariable = ProviderMetadata.CreateSecretReference(secret.Value)
                });
                var fixture = new Fixture(application, scope, journal, parent, source, provider, image.Id);
                await fixture.SaveActorAsync();
                fixture.Agent = (await services.GetRequiredService<ISandboxWorkspaceCatalogStore>().LoadCatalogAsync()).Agents.Single(item => item.Id == journal.Agent.Id);
                Assert.Contains(services.GetServices<IAgentRuntimeToolProvider>(), item => item is ImageGenerationAgentRuntimeToolProvider);
                return fixture;
            } catch {
                if (journal is not null) {
                    await journal.DisposeAsync();
                }
                await scope.DisposeAsync();
                await application.DisposeAsync();
                throw;
            }
        }

        internal async Task SaveActorAsync(bool canGenerate = true, bool canRead = true, bool includeSource = true) {
            ProjectWriteAdmission[] targets = !canRead ? [] : includeSource ? [parent, Source] : [parent];
            var catalog = Services.GetRequiredService<ISandboxWorkspaceCatalogStore>();
            var currentActor = (await catalog.LoadCatalogAsync()).Agents.Single(item => item.Id == Agent.Id);
            var configuration = AgentProjectStructureAccessMetadata.Write(currentActor.ConfigurationJson, new() {
                CanRead = canRead, AllowedProjectIds = targets.Select(item => item.ProjectId).ToList(),
                AllowedProjectLifetimes = targets.Select(item => new AgentProjectStructureLifetime(item.DatabaseProfileId, item.ProjectId, item.LifetimeId)).ToList()
            });
            configuration = AgentImageGenerationAccessMetadata.Write(configuration, new() { CanGenerateImages = canGenerate, PreferredProviderProfileId = providerId });
            var actor = Agent with {
                IsTemplate = false, TemplateKey = string.Empty, Status = AgentLifecycleStatus.Active, Model = journal.Provider.DefaultModel,
                Permissions = Agent.Permissions with { CanUseTools = true },
                ConfigurationJson = AgentManagedSeedCustomizationMetadata.MarkCustomized(configuration)
            };
            await catalog.UpdateCatalogAsync(current => current with {
                Agents = current.Agents.Where(item => item.Id != actor.Id).Append(actor).ToArray()
            });
            var saved = (await catalog.LoadCatalogAsync()).Agents.Single(item => item.Id == actor.Id);
            Assert.Equal(canGenerate, AgentImageGenerationAccessMetadata.Read(saved.ConfigurationJson).CanGenerateImages);
            Assert.Equal(canRead, AgentProjectStructureAccessMetadata.Read(saved.ConfigurationJson).CanRead);
            Assert.Equal(includeSource && canRead, AgentProjectStructureAccessMetadata.Read(saved.ConfigurationJson).AllowedProjectIds.Contains(Source.ProjectId));
        }

        internal async Task SetProviderEnabledAsync(bool enabled) {
            var registry = Services.GetRequiredService<IProviderProfileRegistry>();
            var model = await registry.GetProviderEditorAsync(providerId);
            model.IsEnabled = enabled;
            await registry.SaveProviderAsync(model);
            Assert.Equal(enabled, (await Services.GetRequiredService<IProviderRuntimeProfileSource>().GetProviderAsync(providerId))!.IsEnabled);
        }

        internal async Task ReplaceSourceAsync() {
            var projects = Services.GetRequiredService<ProjectsService>();
            await projects.DeleteAsync(Source.ProjectId);
            Assert.True((await projects.CreateAsync(Source.ProjectId, new() { Name = "Unrelated replacement" })).IsSuccess);
            Source = (await Services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(Source.ProjectId))!;
        }

        internal async Task ApproveAsync(bool legacyAttachment = false) {
            var initial = new ImageClient(Request);
            var response = await ExecuteAsync(initial, legacyAttachment: legacyAttachment);
            Assert.Single(response.PendingApprovals);
            Assert.Equal(ImageGenerationToolPolicy.ImageGenerationCreate, response.PendingApprovals[0].ToolName);
            Assert.NotNull(response.PendingApprovals[0].ToolAdmission);
            Assert.Empty(Generation.Requests);
            await journal.ApproveAsync(response.PendingApprovals);
        }

        internal async Task CompleteThroughLostFinalAcknowledgementAsync(bool legacyAttachment = false) {
            await ApproveAsync(legacyAttachment);
            var completing = new ImageClient(Request, failAfterResult: true);
            var checkpointFailure = await Assert.ThrowsAnyAsync<Exception>(() => ExecuteAsync(completing, legacyAttachment: legacyAttachment));
            AgentToolAdmissionJournalFixture.RequireScriptedFault(checkpointFailure,
                "Injected final provider acknowledgement loss after the image result checkpoint.");
            Assert.Single(Generation.Requests);
        }

        internal async Task<AgentToolProposalRecord> ProposalAsync()
            => Assert.Single((await journal.NewStore().GetExecutionRunAsync(journal.Session.ExecutionRunId))!.ToolAdmission!.Batches
                .SelectMany(batch => batch.Proposals));

        internal Task<AgentRuntimeResponse> ExecuteAsync(ImageClient client, IWorkspacePathResolutionService? paths = null,
            bool legacyAttachment = false) {
            paths ??= new WorkspacePathResolutionService(journal.WorkspaceRoot, Services.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>(), journal.StorageScope);
            var disclosure = new ImageGenerationResultDisclosureService(Services.GetRequiredService<IAgentToolAdmissionVerifier>(),
                Services.GetRequiredService<IAgentExecutionAuthorityResolver>(), Services.GetRequiredService<IAgentCatalogReadLeaseStore>(),
                Services.GetRequiredService<ICanonicalRuntimeDatabase>(), Services.GetRequiredService<IDatabaseRuntimeState>(),
                Services.GetRequiredService<IProviderRuntimeProfileSource>(), paths, Services.GetRequiredService<ProjectWriteAdmissionService>(),
                Services.GetRequiredService<IPhysicalFileSystemPathPolicyFactory>());
            var provider = new ImageGenerationAgentRuntimeToolProvider(Services.GetRequiredService<IProviderRuntimeProfileSource>(),
                paths, Generation, Services, resultDisclosure: disclosure);
            IAgentRuntimeToolProvider attached = legacyAttachment ? new PriorImageProvider(provider) : provider;
            var policies = new AgentToolPolicyCatalog(ImageGenerationToolPolicy.Capabilities);
            var dependencies = MafAgentRuntimeDependencies.FromServices(Services);
            dependencies = dependencies with {
                ProviderAgentFactory = new ImageAgentFactory(client), ToolPolicies = policies,
                RuntimeToolProviderComposer = new RuntimeToolProviderComposer(new RuntimeToolProviderAccessFilter(policies), policies),
                ToolAdmissionJournal = journal.NewJournal(journal.NewStore()),
                CapabilityDependencies = dependencies.CapabilityDependencies with { RuntimeToolProviders = [attached], ContextContributors = [], ToolPolicies = policies }
            };
            var runtime = new MafAgentRuntime(journal.WorkspaceRoot, journal.StorageScope, dependencies);
            var intent = AgentRuntimeContextIntent.Empty with {
                Purpose = AgentRuntimeContextPurpose.InteractiveChat, SourceKind = "project-structure", SourceId = parent.ProjectId.ToString("D"),
                RuntimeToolProvidersEnabled = true, WorkspaceToolsEnabled = false, ToolCapabilitiesEnabled = true
            };
            var options = new AgentRuntimeExecutionOptions(null, AgentFinalizerMode.Disabled, true, 0) {
                AdmittedToolSession = journal.Session, ToolAdmissionSupport = AgentToolAdmissionSupport.Recoverable,
                Governance = AgentTurnContextMetadata.TryReadExecutionGovernanceSnapshot(journal.Detail.Run.MetadataJson),
                AuthorityPolicyFingerprint = "fixture-authority", ModelContextDigest = "fixture-context", ContextIntent = intent
            };
            return runtime.ExecutionPort.ExecuteAsync(new(Agent,
                journal.Provider with { Transport = ProviderTransportKind.ChatCompletions,
                    ConfigurationJson = ProviderModelThinkingConfiguration.Write(journal.Provider.ConfigurationJson, journal.Provider.DefaultModel,
                        new(journal.Provider.DefaultModel, AgentThinkingEffortSupportStatus.Supported,
                            AgentThinkingEffortControlMode.EffortLevels, [AgentReasoningEffortLevel.Medium], AgentReasoningEffortLevel.Medium))
                },
                journal.Detail.ChatSession!, [], [], "Generate the reviewed image.", string.Empty, (_, _, _) => Task.CompletedTask, ExecutionOptions: options));
        }

        private static async Task<ProjectWriteAdmission> CreateProjectAsync(IServiceProvider services, string name) {
            var id = Guid.NewGuid();
            Assert.True((await services.GetRequiredService<ProjectsService>().CreateAsync(id, new() { Name = name })).IsSuccess);
            return (await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(id))!;
        }

        public async ValueTask DisposeAsync() {
            await scope.DisposeAsync();
            await journal.DisposeAsync();
            await application.DisposeAsync();
        }
    }

    private const string Png = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/p9sAAAAASUVORK5CYII=";

    private sealed class GenerationCounter : IAgentImageGenerationService {
        internal List<AgentImageGenerationRequest> Requests { get; } = [];
        internal bool FailAfterDispatch { get; set; }
        internal IOException? InjectedDispatchFailure { get; private set; }
        internal Func<Task>? OnGenerate { get; set; }
        public async Task<AgentImageGenerationResult> GenerateAsync(AgentImageGenerationRequest request, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Requests.Add(request);
            if (FailAfterDispatch) {
                Assert.Null(InjectedDispatchFailure);
                InjectedDispatchFailure = new IOException("Injected acknowledgement loss after metered image dispatch.");
                throw InjectedDispatchFailure;
            }
            if (OnGenerate is not null) {
                await OnGenerate();
            }
            return new(request.Model, request.Format, [new("image/png", Convert.FromBase64String(Png))]);
        }
    }

    private sealed class PriorImageProvider(ImageGenerationAgentRuntimeToolProvider inner) : IAgentRuntimeToolProvider {
        public int Order => inner.Order;
        public AgentRuntimeToolProviderDescriptor Descriptor => inner.Descriptor;
        public ValueTask<IReadOnlyList<AITool>> CreateToolsAsync(AgentRuntimeToolProviderContext context, CancellationToken cancellationToken)
            => inner.CreateToolsAsync(context with { AdmittedToolSession = null }, cancellationToken);
    }

    private sealed class ImageAgentFactory(ImageClient client) : IMafProviderAgentFactory {
        public AIAgent CreateFrameworkAgent(ProviderProfile provider, string model, ChatClientAgentOptions options,
            bool frameworkManagedHistory, bool allowBackgroundResponses) {
            Assert.True(frameworkManagedHistory);
            Assert.Equal(AgentThinkingEffortCapabilitySource.Configured, AgentThinkingEffortPolicy.ResolveCapability(provider, model).Source);
            Assert.True(new ProviderProfileService().ResolveFeatureMatrix(provider).SupportsApprovalRequiredAIFunction);
            Assert.Contains(options.ChatOptions!.Tools!, tool => tool.Name == ImageGenerationToolPolicy.ImageGenerationCreate);
            return new ChatClientAgent(new MafToolAdmissionChatClient(client), options);
        }
    }

    private sealed class ImageClient(ImageGenerationCreateInput request, bool failAfterResult = false) : IChatClient {
        internal int Requests { get; private set; }
        internal List<string> Inputs { get; } = [];
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) {
            Requests++;
            var input = messages.ToArray();
            Inputs.Add(JsonSerializer.Serialize(input, MafToolProtocolCodec.SerializationOptions));
            if (input.SelectMany(message => message.Contents).OfType<FunctionResultContent>().Any()) {
                if (failAfterResult) {
                    throw new IOException("Injected final provider acknowledgement loss after the image result checkpoint.");
                }
                return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "completed")));
            }
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant,
                [new FunctionCallContent("image-diagnostic-call", ImageGenerationToolPolicy.ImageGenerationCreate,
                    new Dictionary<string, object?> { ["request"] = request })])));
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
