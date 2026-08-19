using System.Text.Json;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

/// <summary>
/// Direct tests for the turn admission pipeline: strict capture, generation
/// fencing, canonical authority resolution, observation/authority consistency,
/// and safe turn/authority metadata persistence.
/// </summary>
public sealed class AgentTurnContextCaptureServiceTests
{
    private static readonly Guid ProjectId = Guid.NewGuid();

    [Fact]
    public async Task Authorized_capture_binds_turn_reference_authority_and_digest()
    {
        var agentId = Guid.NewGuid();
        var projectScope = WorkspaceScopeDescriptor.Project(ProjectId.ToString("D"));
        var registry = new FixedContextRegistry(CreateProjectStructureSnapshot(projectScope));
        var resolver = new RecordingAuthorityResolver(request => CreateAuthority(
            request,
            projectScope,
            mutationAllowed: true));
        var service = CreateService(registry, resolver);

        var result = await service.CaptureAsync(CreateCommand(agentId));

        Assert.NotNull(result.TurnReference);
        Assert.NotNull(result.Authority);
        Assert.True(result.Authority.MutationAllowed);
        Assert.Equal(projectScope, result.Authority.WorkspaceScope);

        // The transient lease scope comes from the authority snapshot and the
        // digest binds the metadata to the exact lease payload.
        var transientContext = Assert.IsType<AgentRuntimeTransientContext>(
            result.Invocation.Options.TransientContext);
        Assert.Same(result.Authority.WorkspaceScope, transientContext.WorkspaceScope);
        Assert.Equal(
            AgentChatContextDigest.Compute(transientContext),
            result.TurnReference.ModelContextDigest);

        var metadataJson = result.Invocation.Options.Context?.MetadataJson;
        Assert.False(string.IsNullOrWhiteSpace(metadataJson));
        var persistedReference = AgentTurnContextMetadata.TryReadTurnContextReference(metadataJson);
        Assert.NotNull(persistedReference);
        Assert.Equal(result.TurnReference.TurnContextId, persistedReference.TurnContextId);
        Assert.Equal(result.TurnReference.ModelContextDigest, persistedReference.ModelContextDigest);
        Assert.Equal(result.Context!.Version, persistedReference.ObservationVersion);

        var persistedAuthority = AgentTurnContextMetadata.TryReadExecutionAuthority(metadataJson);
        Assert.NotNull(persistedAuthority);
        Assert.Equal(result.Authority.AuthorityId, persistedAuthority.AuthorityId);
        Assert.Equal(projectScope, persistedAuthority.WorkspaceScope);
        Assert.True(persistedAuthority.MutationAllowed);

        // V1 metadata keys are preserved unchanged next to the V2 keys.
        using var document = JsonDocument.Parse(metadataJson!);
        Assert.True(document.RootElement.TryGetProperty(
            ExecutionInvocationMetadata.TransientContextDigestMetadataKey,
            out _));
    }

    [Fact]
    public async Task Canonical_read_only_authority_wins_over_ui_mutate_hint()
    {
        var agentId = Guid.NewGuid();
        var projectScope = WorkspaceScopeDescriptor.Project(ProjectId.ToString("D"));
        var registry = new FixedContextRegistry(CreateProjectStructureSnapshot(
            projectScope,
            hintedAgentId: agentId,
            hintedPermissions: AgentChatContextPermission.Read | AgentChatContextPermission.Mutate));
        var resolver = new RecordingAuthorityResolver(request => CreateAuthority(
            request,
            projectScope,
            mutationAllowed: false));
        var service = CreateService(registry, resolver);

        var result = await service.CaptureAsync(CreateCommand(agentId));

        Assert.NotNull(result.Authority);
        Assert.True(result.Authority.ReadAllowed);
        Assert.False(result.Authority.MutationAllowed);
        Assert.NotNull(resolver.LastRequest);
        Assert.True(resolver.LastRequest!.UiAccessHint?.Permissions
            .HasFlag(AgentChatContextPermission.Mutate));
    }

    [Fact]
    public async Task Forged_workspace_scope_fails_before_invocation_is_built()
    {
        var publishedScope = WorkspaceScopeDescriptor.Project(ProjectId.ToString("D"));
        var canonicalScope = WorkspaceScopeDescriptor.Project(Guid.NewGuid().ToString("D"));
        var registry = new FixedContextRegistry(CreateProjectStructureSnapshot(publishedScope));
        var resolver = new RecordingAuthorityResolver(request => CreateAuthority(
            request,
            canonicalScope,
            mutationAllowed: false));
        var service = CreateService(registry, resolver);

        await Assert.ThrowsAsync<AgentExecutionAuthorityMismatchException>(
            () => service.CaptureAsync(CreateCommand(Guid.NewGuid())));
    }

    [Fact]
    public async Task Profile_generation_change_during_capture_fails_admission()
    {
        var projectScope = WorkspaceScopeDescriptor.Project(ProjectId.ToString("D"));
        var registry = new FixedContextRegistry(CreateProjectStructureSnapshot(projectScope));
        var resolver = new RecordingAuthorityResolver(request => CreateAuthority(
            request,
            projectScope,
            mutationAllowed: false));
        var service = new AgentTurnContextCaptureService(
            registry,
            resolver,
            new FixedAgentExecutionProfileGenerationSource(new DatabaseProfileGeneration(7)),
            TimeProvider.System);

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.CaptureAsync(CreateCommand(Guid.NewGuid())));

        Assert.Contains("database profile changed", exception.Message, StringComparison.Ordinal);
        Assert.Null(resolver.LastRequest);
    }

    [Fact]
    public async Task Detached_capture_produces_no_turn_reference_or_authority()
    {
        var registry = new FixedContextRegistry(context: null);
        var resolver = new RecordingAuthorityResolver(_ =>
            throw new InvalidOperationException("The resolver must not run for a detached turn."));
        var service = CreateService(registry, resolver);

        var result = await service.CaptureAsync(CreateCommand(Guid.NewGuid()));

        Assert.Null(result.Context);
        Assert.Null(result.TurnReference);
        Assert.Null(result.Authority);
        Assert.Null(result.Invocation.Options.TransientContext);
        Assert.Null(resolver.LastRequest);
    }

    private static AgentTurnContextCaptureService CreateService(
        IAgentChatContextRegistry registry,
        IAgentExecutionAuthorityResolver resolver)
        => new(
            registry,
            resolver,
            new FixedAgentExecutionProfileGenerationSource(new DatabaseProfileGeneration(1)),
            TimeProvider.System);

    private static AgentTurnContextCaptureCommand CreateCommand(Guid agentId)
        => new(
            agentId,
            ChatSessionId: null,
            Prompt: "Summarize the current view.",
            AgentExecutionOperationId.New(),
            new DatabaseProfileGeneration(1),
            AgentChatExecutionBehavior.Default);

    private static AgentChatContextSnapshot CreateProjectStructureSnapshot(
        WorkspaceScopeDescriptor workspaceScope,
        Guid? hintedAgentId = null,
        AgentChatContextPermission hintedPermissions = AgentChatContextPermission.Read)
    {
        var agentAccess = hintedAgentId is { } agentId
            ? new[]
            {
                new AgentChatContextAgentAccess(agentId, hintedPermissions, "This project")
            }
            : null;
        var scope = new AgentChatContextScope(
            AgentChatContextScopeId.Create(),
            new AgentChatContextSource(
                new AgentChatContextSourceKind(AgentChatTrustedSourceKinds.ProjectStructure),
                new AgentChatContextSourceId(ProjectId.ToString("D"))),
            "Project X",
            workspaceScope,
            agentAccess: agentAccess,
            accessMode: AgentChatContextScopeAccessMode.Unrestricted,
            surfacePosition: new AgentChatSurfacePosition(
                "workbench",
                "project-structure",
                "canvas",
                $"/projects/{ProjectId:D}/structure"));
        return new AgentChatContextSnapshot(
            scope,
            [
                new AgentChatContextFragment(
                    new AgentChatContextContributorId("view"),
                    0,
                    "Current view: Canvas")
            ],
            Version: 41,
            CapturedAtUtc: DateTimeOffset.UtcNow);
    }

    private static AgentExecutionAuthorityRecord CreateAuthority(
        AgentExecutionAuthorityResolutionRequest request,
        WorkspaceScopeDescriptor scope,
        bool mutationAllowed)
        => new(
            AgentExecutionAuthorityId.Create(),
            request.AgentId,
            Guid.NewGuid(),
            request.ExpectedDatabaseProfileGeneration,
            scope,
            readAllowed: true,
            mutationAllowed: mutationAllowed,
            "v2-canonical",
            "canonical-fingerprint",
            DateTimeOffset.UtcNow);

    private sealed class FixedContextRegistry(AgentChatContextSnapshot? context)
        : IAgentChatContextRegistry
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

    private sealed class RecordingAuthorityResolver(
        Func<AgentExecutionAuthorityResolutionRequest, AgentExecutionAuthorityRecord> resolve)
        : IAgentExecutionAuthorityResolver
    {
        public AgentExecutionAuthorityResolutionRequest? LastRequest { get; private set; }

        public ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(
            AgentExecutionAuthorityResolutionRequest request,
            CancellationToken cancellationToken = default)
        {
            LastRequest = request;
            return ValueTask.FromResult(resolve(request));
        }
    }
}
