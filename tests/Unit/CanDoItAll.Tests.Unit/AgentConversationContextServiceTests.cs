using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit;

/// <summary>
/// Conversation context affinity: binding lifecycle, epochs, transitions, and
/// the turn-admission integration that supplies the trusted context header.
/// </summary>
public sealed class AgentConversationContextServiceTests
{
    private static readonly Guid ProjectXId = Guid.NewGuid();
    private static readonly Guid ProjectYId = Guid.NewGuid();

    [Fact]
    public void Canvas_to_gantt_is_a_view_change_in_the_same_epoch()
    {
        var service = CreateService();
        var key = AgentConversationKey.ForSession(Guid.NewGuid());
        var binding = service.GetOrCreateBinding(key);
        Assert.True(service.TryCommitTurnAdoption(
            key,
            binding.Revision,
            binding.ContextEpochId,
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(ProjectXId.ToString("D")),
            "Project X",
            "project-structure",
            "canvas",
            "digest-1"));
        var adopted = service.TryGetBinding(key)!;

        var transition = AgentContextTransitionClassifier.Classify(
            adopted,
            CreateSnapshot(ProjectXId, "Project X", view: "gantt"));

        Assert.Equal(AgentContextTransitionKind.ViewChanged, transition.Kind);
        Assert.Equal(AgentContextEpochBehavior.KeepEpoch, transition.EpochBehavior);
        Assert.Equal("Canvas -> Gantt", transition.Summary);
    }

    [Fact]
    public void Selection_change_keeps_the_epoch()
    {
        var service = CreateService();
        var key = AgentConversationKey.ForSession(Guid.NewGuid());
        var binding = service.GetOrCreateBinding(key);
        Assert.True(service.TryCommitTurnAdoption(
            key,
            binding.Revision,
            binding.ContextEpochId,
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(ProjectXId.ToString("D")),
            "Project X",
            "project-structure",
            "canvas",
            "digest-1",
            selectionId: "task-a"));
        var adopted = service.TryGetBinding(key)!;

        var transition = AgentContextTransitionClassifier.Classify(
            adopted,
            CreateSnapshot(ProjectXId, "Project X", view: "canvas", selectionId: "task-b"));

        Assert.Equal(AgentContextTransitionKind.SelectionChanged, transition.Kind);
        Assert.Equal(AgentContextEpochBehavior.KeepEpoch, transition.EpochBehavior);
    }

    [Fact]
    public void Project_switch_is_a_source_entity_change_with_a_new_epoch()
    {
        var service = CreateService();
        var key = AgentConversationKey.ForSession(Guid.NewGuid());
        var binding = service.GetOrCreateBinding(key);
        Assert.True(service.TryCommitTurnAdoption(
            key,
            binding.Revision,
            binding.ContextEpochId,
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(ProjectXId.ToString("D")),
            "Project X",
            "project-structure",
            "canvas",
            "digest-1"));
        var adopted = service.TryGetBinding(key)!;

        var transition = AgentContextTransitionClassifier.Classify(
            adopted,
            CreateSnapshot(ProjectYId, "Project Y", view: "canvas"));

        Assert.Equal(AgentContextTransitionKind.SourceEntityChanged, transition.Kind);
        Assert.Equal(AgentContextEpochBehavior.NewEpoch, transition.EpochBehavior);
        Assert.Contains("Project X", transition.Summary, StringComparison.Ordinal);
        Assert.Contains("Project Y", transition.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public void Module_switch_is_a_source_kind_change_with_a_new_epoch()
    {
        var service = CreateService();
        var key = AgentConversationKey.ForSession(Guid.NewGuid());
        var binding = service.GetOrCreateBinding(key);
        Assert.True(service.TryCommitTurnAdoption(
            key,
            binding.Revision,
            binding.ContextEpochId,
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(ProjectXId.ToString("D")),
            "Project X",
            "project-structure",
            "canvas",
            "digest-1"));
        var adopted = service.TryGetBinding(key)!;

        var transition = AgentContextTransitionClassifier.Classify(
            adopted,
            CreateSnapshot(
                Guid.NewGuid(),
                "CRM partner",
                view: "detail",
                sourceKind: "crm"));

        Assert.Equal(AgentContextTransitionKind.SourceKindChanged, transition.Kind);
        Assert.Equal(AgentContextEpochBehavior.NewEpoch, transition.EpochBehavior);
    }

    [Fact]
    public void Commit_uses_compare_and_swap_on_the_binding_revision()
    {
        var service = CreateService();
        var key = AgentConversationKey.ForSession(Guid.NewGuid());
        var binding = service.GetOrCreateBinding(key);
        var staleRevision = binding.Revision;

        Assert.True(service.TryCommitTurnAdoption(
            key,
            staleRevision,
            binding.ContextEpochId,
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(ProjectXId.ToString("D")),
            "Project X",
            "project-structure",
            "canvas",
            "digest-1"));

        // A concurrent stale turn with the old revision is refused.
        Assert.False(service.TryCommitTurnAdoption(
            key,
            staleRevision,
            AgentContextEpochId.Create(),
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(ProjectYId.ToString("D")),
            "Project Y",
            "project-structure",
            "canvas",
            "digest-2"));

        var current = service.TryGetBinding(key)!;
        Assert.Equal(ProjectXId.ToString("D"), current.SourceId!.Value.Value);
        Assert.Equal(2, current.Revision.Value);
    }

    [Fact]
    public void Handle_binding_transfers_to_the_created_session_atomically_and_idempotently()
    {
        var service = CreateService();
        var handleId = new AgentChatHandleId(Guid.NewGuid());
        var handleKey = AgentConversationKey.ForHandle(handleId);
        var binding = service.GetOrCreateBinding(handleKey);
        Assert.True(service.TryCommitTurnAdoption(
            handleKey,
            binding.Revision,
            binding.ContextEpochId,
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(ProjectXId.ToString("D")),
            "Project X",
            "project-structure",
            "canvas",
            "digest-1"));

        var sessionId = Guid.NewGuid();
        var transferred = service.TransferToSession(handleId, sessionId);
        var transferredAgain = service.TransferToSession(handleId, sessionId);

        Assert.Equal(sessionId, transferred.ChatSessionId);
        Assert.Null(transferred.HandleId);
        Assert.Equal(ProjectXId.ToString("D"), transferred.SourceId!.Value.Value);
        Assert.Equal(transferred, transferredAgain);
        Assert.Null(service.TryGetBinding(handleKey));
        Assert.NotNull(service.TryGetBinding(AgentConversationKey.ForSession(sessionId)));
    }

    [Fact]
    public void Detach_and_follow_change_the_epoch_and_drop_the_followed_source()
    {
        var service = CreateService();
        var key = AgentConversationKey.ForSession(Guid.NewGuid());
        var binding = service.GetOrCreateBinding(key);
        Assert.True(service.TryCommitTurnAdoption(
            key,
            binding.Revision,
            binding.ContextEpochId,
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(ProjectXId.ToString("D")),
            "Project X",
            "project-structure",
            "canvas",
            "digest-1"));
        var adoptedEpoch = service.TryGetBinding(key)!.ContextEpochId;

        var detached = service.Detach(key);
        Assert.Equal(AgentConversationContextMode.Detached, detached.Mode);
        Assert.Null(detached.SourceKind);
        Assert.NotEqual(adoptedEpoch, detached.ContextEpochId);

        var followed = service.FollowCurrentSurface(key);
        Assert.Equal(AgentConversationContextMode.FollowCurrentSurface, followed.Mode);
        Assert.NotEqual(detached.ContextEpochId, followed.ContextEpochId);
    }

    [Fact]
    public async Task Detached_conversation_receives_no_observation_or_authority()
    {
        var service = CreateService();
        var sessionId = Guid.NewGuid();
        var key = AgentConversationKey.ForSession(sessionId);
        service.GetOrCreateBinding(key);
        service.Detach(key);

        var registry = new ThrowingContextRegistry();
        var captureService = new AgentTurnContextCaptureService(
            registry,
            new UnreachableAuthorityResolver(),
            new FixedAgentExecutionProfileGenerationSource(new DatabaseProfileGeneration(1)),
            TimeProvider.System,
            service);

        var result = await captureService.CaptureAsync(new AgentTurnContextCaptureCommand(
            Guid.NewGuid(),
            sessionId,
            "Hello",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(1),
            AgentChatExecutionBehavior.Default,
            key));

        Assert.Null(result.Context);
        Assert.Null(result.Authority);
        Assert.Null(result.TurnReference);
        Assert.Null(result.Invocation.Options.TransientContext);
        Assert.Equal(AgentContextTransitionKind.ContextDetached, result.Transition?.Kind);
        Assert.False(registry.CaptureWasCalled);
    }

    [Fact]
    public async Task Turn_capture_prepends_the_trusted_header_and_rebinds_the_digest()
    {
        var service = CreateService();
        var sessionId = Guid.NewGuid();
        var key = AgentConversationKey.ForSession(sessionId);
        var binding = service.GetOrCreateBinding(key);
        Assert.True(service.TryCommitTurnAdoption(
            key,
            binding.Revision,
            binding.ContextEpochId,
            new AgentChatContextSourceKind("project-structure"),
            new AgentChatContextSourceId(ProjectXId.ToString("D")),
            "Project X",
            "project-structure",
            "canvas",
            "digest-1"));

        var projectScope = WorkspaceScopeDescriptor.Project(ProjectXId.ToString("D"));
        var snapshot = CreateSnapshot(ProjectXId, "Project X", view: "gantt", workspaceScope: projectScope);
        var captureService = new AgentTurnContextCaptureService(
            new FixedRegistry(snapshot),
            new EchoAuthorityResolver(projectScope),
            new FixedAgentExecutionProfileGenerationSource(new DatabaseProfileGeneration(1)),
            TimeProvider.System,
            service);

        var result = await captureService.CaptureAsync(new AgentTurnContextCaptureCommand(
            Guid.NewGuid(),
            sessionId,
            "What changed?",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(1),
            AgentChatExecutionBehavior.Default,
            key));

        // The transition is classified against the binding and the epoch is kept.
        Assert.Equal(AgentContextTransitionKind.ViewChanged, result.Transition?.Kind);
        Assert.Equal(binding.ContextEpochId, result.TurnReference?.ContextEpochId);

        // The trusted header is part of the leased content and the metadata
        // digest matches the final lease payload.
        var lease = Assert.IsType<AgentRuntimeTransientContext>(result.Invocation.Options.TransientContext);
        Assert.StartsWith("Current application context", lease.Content, StringComparison.Ordinal);
        Assert.Contains("Canvas -> Gantt", lease.Content, StringComparison.Ordinal);
        Assert.Contains("historical", lease.Content, StringComparison.Ordinal);

        var metadataJson = result.Invocation.Options.Context!.MetadataJson;
        var reference = AgentTurnContextMetadata.TryReadTurnContextReference(metadataJson);
        Assert.Equal(AgentChatContextDigest.Compute(lease), reference!.ModelContextDigest);
    }

    [Fact]
    public void Affinity_services_cannot_reach_execution_or_providers()
    {
        var root = FindRepoRoot();
        var affinitySources = new[]
        {
            @"src\MAF\Common\CanDoItAll.AgentFramework.Core\Context\AgentConversationContextService.cs",
            @"src\MAF\Common\CanDoItAll.AgentFramework.Core\Context\AgentContextTransitionClassifier.cs"
        };

        foreach (var relativePath in affinitySources)
        {
            var text = File.ReadAllText(Path.Combine(root, relativePath));
            Assert.DoesNotContain("IAgentRuntime", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ExecutionService", text, StringComparison.Ordinal);
            Assert.DoesNotContain("ProviderProfile", text, StringComparison.Ordinal);
            Assert.DoesNotContain("IProviderRuntime", text, StringComparison.Ordinal);
            Assert.DoesNotContain("RunAsync", text, StringComparison.Ordinal);
        }
    }

    private static AgentConversationContextService CreateService()
        => new(TimeProvider.System);

    private static AgentChatContextSnapshot CreateSnapshot(
        Guid sourceId,
        string displayName,
        string view,
        string sourceKind = "project-structure",
        string? selectionId = null,
        WorkspaceScopeDescriptor? workspaceScope = null)
    {
        var scope = new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind(sourceKind),
                new AgentChatContextSourceId(sourceId.ToString("D"))),
            displayName,
            workspaceScope,
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            surfacePosition: new AgentChatSurfacePosition(
                "workbench",
                sourceKind,
                view,
                $"/projects/{sourceId:D}/structure",
                selectionId is null
                    ? null
                    : new AgentChatContextEntityReference("task", selectionId, selectionId)));
        return new AgentChatContextSnapshot(
            scope,
            [
                new AgentChatContextFragment(
                    new AgentChatContextContributorId("view"),
                    0,
                    $"Current view: {view}")
            ],
            Version: 42,
            CapturedAtUtc: DateTimeOffset.UtcNow);
    }

    private sealed class FixedRegistry(AgentChatContextSnapshot? context) : IAgentChatContextRegistry
    {
        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public IAgentChatWorkspacePositionLease RegisterWorkspacePosition(
            AgentChatWorkspacePosition position,
            AgentChatNavigationIdentity navigationIdentity)
            => throw new NotSupportedException();

        public IAgentChatContextScopeLease ActivateScope(AgentChatContextScope scope)
            => throw new NotSupportedException();

        public IAgentChatContextFragmentLease RegisterFragment(
            AgentChatContextScopeId scopeId,
            AgentChatContextFragment fragment)
            => throw new NotSupportedException();

        public AgentChatContextSnapshot? Capture()
            => context;

        public ValueTask<AgentChatContextSnapshot?> CaptureAsync(
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(context);
    }

    private sealed class ThrowingContextRegistry : IAgentChatContextRegistry
    {
        public bool CaptureWasCalled { get; private set; }

        public event EventHandler? Changed
        {
            add { }
            remove { }
        }

        public IAgentChatWorkspacePositionLease RegisterWorkspacePosition(
            AgentChatWorkspacePosition position,
            AgentChatNavigationIdentity navigationIdentity)
            => throw new NotSupportedException();

        public IAgentChatContextScopeLease ActivateScope(AgentChatContextScope scope)
            => throw new NotSupportedException();

        public IAgentChatContextFragmentLease RegisterFragment(
            AgentChatContextScopeId scopeId,
            AgentChatContextFragment fragment)
            => throw new NotSupportedException();

        public AgentChatContextSnapshot? Capture()
        {
            CaptureWasCalled = true;
            return null;
        }

        public ValueTask<AgentChatContextSnapshot?> CaptureAsync(
            CancellationToken cancellationToken = default)
        {
            CaptureWasCalled = true;
            return ValueTask.FromResult<AgentChatContextSnapshot?>(null);
        }
    }

    private sealed class UnreachableAuthorityResolver : IAgentExecutionAuthorityResolver
    {
        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(
            AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("A detached turn must not resolve authority.");
    }

    private sealed class EchoAuthorityResolver(WorkspaceScopeDescriptor scope)
        : IAgentExecutionAuthorityResolver
    {
        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(
            AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(new AgentExecutionAuthorityRecord(
                AgentExecutionAuthorityId.Create(),
                request.AgentId,
                Guid.NewGuid(),
                request.ExpectedDatabaseProfileGeneration,
                scope,
                readAllowed: true,
                mutationAllowed: false,
                "test",
                "test-fingerprint",
                DateTimeOffset.UtcNow));
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Unable to locate repository root.");
    }
}
