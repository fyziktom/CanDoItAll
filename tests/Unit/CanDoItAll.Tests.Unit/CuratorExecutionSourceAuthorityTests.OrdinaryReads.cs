using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Tooling;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Conversations.Components.Presentation;
using CanDoItAll.Conversations.Shell;
using CanDoItAll.Modules.AgentFramework;
using CanDoItAll.Modules.Prompts;
using CanDoItAll.Modules.SchedulerPlanner;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed partial class CuratorExecutionSourceAuthorityTests {
    public enum ReadSurface { Prompts, Workflows, Scheduler }

    [Theory]
    [InlineData(ReadSurface.Prompts, true)]
    [InlineData(ReadSurface.Prompts, false)]
    [InlineData(ReadSurface.Workflows, true)]
    [InlineData(ReadSurface.Workflows, false)]
    [InlineData(ReadSurface.Scheduler, true)]
    [InlineData(ReadSurface.Scheduler, false)]
    public async Task Ordinary_floating_selection_keeps_original_source_reads_and_only_existing_generic_tools(
        ReadSurface source, bool canUseTools) {
        var fixture = new Fixture(Curator.Prompts);
        fixture.Agent = fixture.Agent with {
            Id = Guid.NewGuid(), TemplateKey = string.Empty, Capabilities = [],
            Permissions = fixture.Agent.Permissions with { CanUseTools = canUseTools }
        };
        var surface = source switch {
            ReadSurface.Prompts => PromptGalleryAgentChatContextBuilder.Build(),
            ReadSurface.Workflows => WorkflowSurface(AgentFrameworkWorkflowsChatView.Editor, true, true),
            ReadSurface.Scheduler => SchedulerAgentChatContextBuilder.Build(SchedulerAgentChatView.Calendar, null, null, null),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        IAgentExecutionSourceAuthorityProvider owner = source switch {
            ReadSurface.Prompts => new PromptGalleryExecutionAuthorityProvider(fixture.CatalogLeases, fixture.Profile),
            ReadSurface.Workflows => new WorkflowsExecutionAuthorityProvider(fixture.CatalogLeases, fixture.Profile),
            ReadSurface.Scheduler => new SchedulerExecutionAuthorityProvider(fixture.CatalogLeases, fixture.Profile),
            _ => throw new ArgumentOutOfRangeException(nameof(source))
        };
        var resolver = fixture.CreateResolver([owner]);
        var contexts = new AgentChatContextRegistry(TimeProvider.System);
        var navigation = new ReadSurfaceNavigation(surface.Position.Route);
        var navigationIdentity = AgentChatNavigationIdentity.CreateForLocation(navigation.BaseUri, navigation.Uri);
        using var position = contexts.RegisterWorkspacePosition(new("ordinary-read", "Catalog", surface.Position.Route, "module"), navigationIdentity);
        using var scope = contexts.ActivateScope(surface.ToScope(AgentChatContextScopeId.Create()));
        scope.SynchronizeNavigation(navigationIdentity);
        var bindings = new AgentConversationContextService(TimeProvider.System);
        var chats = new ActiveAgentChatRegistry(TimeProvider.System);
        await using var coordinator = new FloatingAgentChatCoordinator(fixture.Workspace, chats,
            new ReadPreparation(fixture.Agent), new ReadSettings(), TimeProvider.System,
            NullLogger<FloatingAgentChatCoordinator>.Instance, bindings);
        var shell = new ConversationShellCoordinator();
        using var dialogs = new DialogService(navigation);
        var notifications = new NotificationService();
        await using var selector = new AgentConversationShellContributor(coordinator, shell, contexts,
            fixture.Workspace, new ReadReferenceData(fixture.Context), new AgentReferenceDataInvalidationHub(),
            dialogs, notifications, NullLogger<AgentConversationShellContributor>.Instance);
        await selector.InitializeAsync();
        var available = selector.Snapshot();
        Assert.Null(available.FailureMessage);
        var participant = Assert.Single(available.Available);
        Assert.Equal($"floating-agent-chat-agent-list-item-{fixture.Agent.Id:N}", participant.Presentation.ShellTestId);
        var action = Assert.Single(participant.Presentation.Actions, item =>
            item.TestId == $"floating-agent-chat-agent-list-new-chat-{fixture.Agent.Id:N}");
        Assert.False(action.IsDisabled);
        await selector.HandleParticipantActionAsync(new(participant.Presentation.Participant.Key, action.Key));
        var chat = Assert.Single(coordinator.Snapshot().ActiveChats);
        Assert.Equal(fixture.Agent.Id, chat.Agent.AgentId);
        Assert.NotNull(chat.ChatSessionId);
        Assert.Equal(AgentConversationShellContributor.BuildWindowId(chat.HandleId), shell.Snapshot().FocusedWindow!.WindowId);
        var conversation = AgentConversationKey.ForSession(chat.ChatSessionId.Value);
        Assert.Equal(AgentConversationContextMode.FollowCurrentSurface, bindings.TryGetBinding(conversation)!.Mode);
        var snapshot = Assert.IsType<AgentChatContextSnapshot>(await contexts.CaptureAsync());
        Assert.Equal(surface.Source, snapshot.Scope.Source);
        Assert.True(snapshot.CanRead(fixture.Agent.Id));
        Assert.Null(snapshot.FindAccess(fixture.Agent.Id));
        var capture = new AgentTurnContextCaptureService(contexts, resolver,
            new FixedAgentExecutionProfileGenerationSource(new(1)), TimeProvider.System, bindings);
        var admitted = await capture.CaptureAsync(new(fixture.Agent.Id, chat.ChatSessionId,
            "Read the current catalog without changing anything.", AgentExecutionOperationId.New(), new(1),
            AgentChatExecutionBehavior.Default, conversation));
        var authority = Assert.IsType<AgentExecutionAuthorityRecord>(admitted.Authority);
        var reference = Assert.IsType<AgentTurnContextReference>(admitted.TurnReference);
        Assert.Equal(surface.Source.Kind, reference.SourceKind);
        Assert.Equal(surface.Source.Id, reference.SourceId);
        Assert.Equal(WorkspaceScopeDescriptor.Sandbox, authority.WorkspaceScope);
        Assert.True(authority.ReadAllowed);
        Assert.False(authority.MutationAllowed);
        Assert.Equal(0, fixture.Probe.CatalogReads);
        Assert.Empty((await fixture.ComposeAsync(authority)).Tools);

        var gallery = PromptGalleryTestSupport.CreateService(PromptGalleryTestSupport.CreateFactory(
            $"ordinary-surface-{source}-{canUseTools}"));
        var provider = new PromptGalleryAgentRuntimeToolProvider(gallery, new PromptGalleryCompatibilityEvaluator());
        var state = await fixture.ComposeAsync(authority, provider);
        Assert.False(state.HasApprovalTools);
        Assert.All(state.RuntimeToolMetadata, metadata => Assert.Equal(AgentRuntimeToolOperationKind.Read, metadata.OperationKind));
        if (canUseTools) {
            Assert.Equal(2, state.Tools.Count);
            const string body = "Immutable generic read for an ordinary agent.";
            var save = await gallery.SaveDraftAsync(new(null, null, null, "Ordinary source read", "Public runtime prompt",
                PromptGalleryItemKind.FullPrompt, "agent-runtime", body, Tags: [],
                SupportedModels: [new(ProviderKind.OpenAi.ToString(), fixture.Agent.Model)],
                SupportedConsumers: [PromptGalleryConsumer.AgentRuntime], Recommendations: new(0.2, 100)));
            Assert.True(save.IsSuccess);
            Assert.NotEqual(Guid.Empty, save.Value.PromptArtifactId);
            var version = await gallery.CreateVersionAsync(save.Value.PromptArtifactId, new("Immutable read", save.Value.UpdatedAtUtc));
            Assert.True(version.IsSuccess);
            Assert.NotNull(version.Value);
            var search = await InvokeReadAsync<PromptGalleryAgentSearchResult>(state.Tools.Single(tool =>
                tool.Name == PromptGalleryToolPolicy.PromptGallerySearch), new PromptGalleryAgentSearchInput(text: "Ordinary source read"));
            Assert.Equal(save.Value.PromptArtifactId, Assert.Single(search.Items).PromptArtifactId);
            var input = new PromptGalleryAgentItemInput(save.Value.PromptArtifactId);
            var item = await InvokeReadAsync<PromptGalleryAgentItemResult>(state.Tools.Single(tool =>
                tool.Name == PromptGalleryToolPolicy.PromptGalleryItemGet), input);
            Assert.Equal(body, item.Content);
            Assert.Equal(version.Value.PromptVersionId, item.PromptVersionId);
            var metadata = provider.GetToolMetadata(fixture.Context).Single(item => item.ToolName == PromptGalleryToolPolicy.PromptGalleryItemGet);
            var disclosure = ManagedToolDisclosureTestData.Create(metadata, input, item);
            await using (var lease = await metadata.AuthorizeResultDisclosureAsync!(disclosure, default)) {
                Assert.Null(lease);
            }
            Assert.True((await gallery.ArchiveAsync(save.Value.PromptArtifactId, true)).IsSuccess);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => metadata.AuthorizeResultDisclosureAsync!(disclosure, default).AsTask());
            var retained = await gallery.GetItemAsync(save.Value.PromptArtifactId);
            Assert.True(retained.IsSuccess);
            Assert.NotNull(retained.Value);
            Assert.Equal(body, retained.Value.DraftContent);
            Assert.Single(retained.Value.Versions);
        } else {
            Assert.Empty(state.Tools);
        }
        var restored = JsonSerializer.Deserialize<AgentExecutionGovernanceSnapshot>(JsonSerializer.Serialize(
            AgentExecutionGovernanceSnapshot.FromAuthority(authority)))!;
        var restoredReference = JsonSerializer.Deserialize<AgentTurnContextReference>(JsonSerializer.Serialize(reference))!;
        var original = AgentExecutionAuthorityResolutionRequest.FromCaptured(restoredReference, restored);
        var current = await resolver.ResolveAsync(original);
        Assert.Equal(authority.PolicyFingerprint, current.PolicyFingerprint);
        Assert.True(current.ReadAllowed);
        Assert.False(current.MutationAllowed);
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => resolver.ResolveAsync(original with {
            SourceId = new("different-original-source")
        }).AsTask());
        foreach (var untrustedScope in new[] { WorkspaceScopeDescriptor.Sandbox,
            WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D")),
            WorkspaceScopeDescriptor.Organization(fixture.Profile.Id.ToString("N")) }) {
            await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => resolver.ResolveAsync(new(
                fixture.Agent.Id, reference.SourceKind, reference.SourceId, untrustedScope, new(1), null)).AsTask());
        }
        fixture.Profile.Id = Guid.NewGuid();
        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(() => resolver.ResolveAsync(original).AsTask());
        Assert.False(restored.MutationAllowed);
        Assert.Equal(authority.PolicyFingerprint, restored.PolicyFingerprint);
    }

    private static async Task<T> InvokeReadAsync<T>(AITool tool, object request) {
        var raw = await Assert.IsAssignableFrom<AIFunction>(tool).InvokeAsync(new AIFunctionArguments { ["request"] = request });
        return raw switch {
            T result => result,
            JsonElement json => json.Deserialize<T>(new JsonSerializerOptions(JsonSerializerDefaults.Web) {
                Converters = { new JsonStringEnumConverter() }
            }) ?? throw new InvalidOperationException("The registered prompt read returned no result."),
            _ => throw new InvalidOperationException("The registered prompt read returned an unexpected result type.")
        };
    }

    private sealed class ReadReferenceData(AgentRuntimeToolProviderContext context) : IAgentReferenceDataProvider {
        public Task<AgentReferenceDataSnapshot> GetAsync(AgentReferenceDataRequest request, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.True(request.ActiveAgentsOnly);
            return Task.FromResult(new AgentReferenceDataSnapshot(request.Sections, [context.Agent], [context.Provider],
                new Dictionary<Guid, ProviderProfile> { [context.Provider.Id] = context.Provider }, DateTimeOffset.UtcNow, TimeSpan.Zero));
        }
    }

    private sealed class ReadPreparation(AgentDefinition agent) : IAgentChatPreparationPool {
        public bool HasPreparedEntries => false;
        public void Configure(FloatingAgentChatSettings settings) { }
        public Task WarmAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<AgentDefinition?> AcquireAsync(Guid agentId, CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult<AgentDefinition?>(agent.Id == agentId ? agent : null);
        }
        public int PruneExpired() => 0;
        public AgentChatPreparationPoolSnapshot Snapshot() => new(0, 0, 0, 0, []);
    }

    private sealed class ReadSettings : IFloatingAgentChatSettingsService {
        public Task<FloatingAgentChatSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(FloatingAgentChatSettings.Default);
        public Task<FloatingAgentChatSettings> SaveSettingsAsync(FloatingAgentChatSettings settings, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class ReadSurfaceNavigation : NavigationManager {
        public ReadSurfaceNavigation(string route) => Initialize("https://surface.test/", "https://surface.test" + route);
        protected override void NavigateToCore(string uri, bool forceLoad) => throw new InvalidOperationException("Selecting an agent must preserve its source route.");
    }
}
