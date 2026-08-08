using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.AgentFramework.Core;

/// <summary>
/// Source kinds with a canonical authority rule. The values match the module
/// publications and the trusted external-target attachment source kind.
/// </summary>
public static class AgentChatTrustedSourceKinds
{
    public const string ProjectStructure = "project-structure";
}

/// <summary>
/// Canonical authority resolution request for one turn admission. The
/// observation-supplied source identity and workspace scope are requests to
/// validate, never grants; the UI access entry is a hint usable only for early
/// denial and compatibility projection.
/// </summary>
public sealed record AgentExecutionAuthorityResolutionRequest(
    Guid AgentId,
    AgentChatContextSourceKind SourceKind,
    AgentChatContextSourceId SourceId,
    WorkspaceScopeDescriptor? ObservedWorkspaceScope,
    DatabaseProfileGeneration ExpectedDatabaseProfileGeneration,
    AgentChatContextAgentAccess? UiAccessHint);

/// <summary>
/// Resolves what an admitted turn may do from canonical authorization data.
/// Implementations live in the product/module layer; Core owns only the
/// orchestration contract and invariant validation.
/// </summary>
public interface IAgentExecutionAuthorityResolver
{
    ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(
        AgentExecutionAuthorityResolutionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Raised when the captured observation and the canonically resolved authority
/// disagree about source or workspace scope. Admission fails before any
/// runtime construction or provider work.
/// </summary>
public sealed class AgentExecutionAuthorityMismatchException(string message)
    : InvalidOperationException(message);

/// <summary>
/// Command for capturing one immutable turn context at Send admission. The
/// optional conversation key identifies the floating conversation whose
/// binding classifies the transition and owns the context epoch.
/// </summary>
public sealed record AgentTurnContextCaptureCommand(
    Guid AgentId,
    Guid? ChatSessionId,
    string Prompt,
    AgentExecutionOperationId ActivityOperationId,
    DatabaseProfileGeneration ExpectedDatabaseProfileGeneration,
    AgentChatExecutionBehavior Behavior,
    AgentConversationKey? ConversationKey = null);

/// <summary>
/// Result of one turn capture: the composed invocation (prompt, run options,
/// metadata, transient lease payload), the captured live observation, the
/// admitted turn reference plus canonical authority when application context
/// participated in the turn, and the classified transition with the
/// conversation binding revision the classification was based on.
/// </summary>
public sealed record AgentTurnContextCaptureResult(
    AgentChatContextInvocation Invocation,
    AgentChatContextSnapshot? Context,
    AgentTurnContextReference? TurnReference,
    AgentExecutionAuthorityRecord? Authority,
    AgentContextTransition? Transition = null,
    AgentConversationContextBinding? ConversationBinding = null);

/// <summary>
/// Owns the ordered turn admission pipeline: strict route-fenced observation
/// capture, database-generation fencing, canonical authority resolution,
/// observation/authority consistency validation, bounded model-context
/// composition, and turn-reference/digest creation. The workspace scope
/// carried by the transient lease is taken from the resolved authority
/// snapshot, not from the UI publication.
/// </summary>
public interface IAgentTurnContextCaptureService
{
    Task<AgentTurnContextCaptureResult> CaptureAsync(
        AgentTurnContextCaptureCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class AgentTurnContextCaptureService(
    IAgentChatContextRegistry contextRegistry,
    IAgentExecutionAuthorityResolver authorityResolver,
    IAgentExecutionProfileGenerationSource profileGenerationSource,
    TimeProvider timeProvider,
    IAgentConversationContextService? conversationContextService = null) : IAgentTurnContextCaptureService
{
    public async Task<AgentTurnContextCaptureResult> CaptureAsync(
        AgentTurnContextCaptureCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (command.AgentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(command));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(command.Prompt);
        ArgumentNullException.ThrowIfNull(command.Behavior);

        var binding = command.ConversationKey is { } conversationKey
            ? conversationContextService?.GetOrCreateBinding(conversationKey)
            : null;

        // An explicitly detached conversation receives no application context
        // and no context-derived authority; the observation is not consulted.
        if (binding is { Mode: AgentConversationContextMode.Detached })
        {
            EnsureCurrentProfileGeneration(command.ExpectedDatabaseProfileGeneration);
            return CreateDetachedResult(
                command,
                binding,
                new AgentContextTransition(
                    AgentContextTransitionKind.ContextDetached,
                    AgentContextTransitionDecision.Detached,
                    AgentContextEpochBehavior.NewEpoch,
                    summary: "The conversation is detached from the application surface."));
        }

        // 1. One strict, route-fenced live observation.
        var context = await contextRegistry
            .CaptureAsync(cancellationToken)
            .ConfigureAwait(false);

        // 2. Database profile generation fence.
        EnsureCurrentProfileGeneration(command.ExpectedDatabaseProfileGeneration);

        var nowUtc = timeProvider.GetUtcNow();
        if (context is null)
        {
            return CreateDetachedResult(command, binding, transition: null);
        }

        // 3-4. Requested source identity and canonical authority resolution.
        var authority = await authorityResolver
            .ResolveAsync(
                new AgentExecutionAuthorityResolutionRequest(
                    command.AgentId,
                    context.Scope.Source.Kind,
                    context.Scope.Source.Id,
                    context.Scope.WorkspaceScope,
                    command.ExpectedDatabaseProfileGeneration,
                    context.FindAccess(command.AgentId)),
                cancellationToken)
            .ConfigureAwait(false);

        // 5. Observation and authority must agree before anything is built.
        ValidateAuthorityAgainstObservation(command, context, authority);

        // 6a. Classify the transition against the conversation binding and
        // resolve the context epoch: view/selection changes keep the epoch,
        // source-entity/kind changes and first adoptions of a new source start
        // a new one.
        var transition = binding is not null
            ? AgentContextTransitionClassifier.Classify(binding, context)
            : AgentContextTransition.None;
        var contextEpochId = transition.EpochBehavior == AgentContextEpochBehavior.KeepEpoch
            ? binding?.ContextEpochId ?? AgentContextEpochId.Create()
            : AgentContextEpochId.Create();

        // 6b. Compose the bounded model context and V1 metadata exactly as before.
        var invocation = AgentChatContextInvocationFactory.Create(
            context,
            command.AgentId,
            command.ChatSessionId,
            command.Prompt,
            command.ActivityOperationId,
            command.ExpectedDatabaseProfileGeneration,
            nowUtc,
            command.Behavior);

        // 6c. Rebuild the transient lease: the workspace scope originates from
        // the resolved authority snapshot (value-equal to the published scope
        // by validation) and the trusted application-context header is
        // prepended above the untrusted observation payload. The metadata
        // digest is re-applied afterwards so the lease digest stays bound.
        var options = invocation.Options;
        if (options.TransientContext is { } transientContext)
        {
            var trustedHeader = BuildTrustedContextHeader(context, transition, contextEpochId, binding);
            var content = transientContext.HasContent
                ? $"{trustedHeader}\n\n{transientContext.Content}"
                : trustedHeader;
            var rebuiltContext = new AgentRuntimeTransientContext(
                content,
                authority.WorkspaceScope,
                transientContext.Attachments);
            options = options with { TransientContext = rebuiltContext };
            if (options.Context is { } contextWithDigest)
            {
                options = options with
                {
                    Context = contextWithDigest with
                    {
                        MetadataJson = ExecutionInvocationMetadata.ApplyTransientContextRequirement(
                            contextWithDigest.MetadataJson,
                            AgentChatContextDigest.Compute(rebuiltContext))
                    }
                };
            }
        }

        // 7. Immutable turn reference bound to the captured observation.
        var surfacePosition = context.Scope.SurfacePosition;
        var contextDigest = options.TransientContext is { } lease
            ? AgentChatContextDigest.Compute(lease)
            : string.Empty;
        var turnReference = new AgentTurnContextReference(
            AgentTurnContextId.Create(),
            contextEpochId,
            context.Scope.Source.Kind,
            context.Scope.Source.Id,
            surface: surfacePosition?.Surface ?? context.Scope.Source.Kind.Value,
            view: surfacePosition?.View ?? string.Empty,
            observationVersion: context.Version,
            modelContextDigest: string.IsNullOrWhiteSpace(contextDigest)
                ? AgentTurnContextMetadata.EmptyModelContextDigest
                : contextDigest,
            capturedAtUtc: nowUtc);

        // 8. Admit with safe metadata only: identifiers and fingerprints.
        if (options.Context is { } invocationContext)
        {
            options = options with
            {
                Context = invocationContext with
                {
                    MetadataJson = AgentTurnContextMetadata.Apply(
                        invocationContext.MetadataJson,
                        turnReference,
                        authority,
                        transition)
                }
            };
        }

        return new AgentTurnContextCaptureResult(
            new AgentChatContextInvocation(invocation.Prompt, options),
            context,
            turnReference,
            authority,
            transition,
            binding);
    }

    private AgentTurnContextCaptureResult CreateDetachedResult(
        AgentTurnContextCaptureCommand command,
        AgentConversationContextBinding? binding,
        AgentContextTransition? transition)
    {
        var detachedInvocation = AgentChatContextInvocationFactory.Create(
            context: null,
            command.AgentId,
            command.ChatSessionId,
            command.Prompt,
            command.ActivityOperationId,
            command.ExpectedDatabaseProfileGeneration,
            timeProvider.GetUtcNow(),
            command.Behavior);
        return new AgentTurnContextCaptureResult(
            detachedInvocation,
            Context: null,
            TurnReference: null,
            Authority: null,
            transition,
            binding);
    }

    private static string BuildTrustedContextHeader(
        AgentChatContextSnapshot context,
        AgentContextTransition transition,
        AgentContextEpochId contextEpochId,
        AgentConversationContextBinding? binding)
    {
        var surfacePosition = context.Scope.SurfacePosition;
        var transitionLine = transition.Kind == AgentContextTransitionKind.None
            ? "None"
            : string.IsNullOrWhiteSpace(transition.Summary)
                ? transition.Kind.ToString()
                : $"{transition.Kind}: {transition.Summary}";
        var epochNotice = transition.EpochBehavior == AgentContextEpochBehavior.NewEpoch && binding is not null
            ? "\n- A new context epoch started with this turn. Prior UI facts from earlier context epochs are historical and must not be treated as current."
            : "\n- Prior UI facts from earlier context epochs are historical and must not be treated as current.";
        return $"""
Current application context (application-generated, trusted metadata; not an authorization grant):
- Context epoch: {contextEpochId}
- Source: {context.Scope.Source.Kind.Value} / {context.Scope.DisplayName}
- Current surface: {surfacePosition?.Surface ?? context.Scope.Source.Kind.Value}
- Current view: {(string.IsNullOrWhiteSpace(surfacePosition?.View) ? "(none)" : surfacePosition!.View)}
- Transition since the previous accepted turn: {transitionLine}{epochNotice}
- Use authorized product tools for exact current data and mutations.
""";
    }

    private void EnsureCurrentProfileGeneration(DatabaseProfileGeneration expectedGeneration)
    {
        if (profileGenerationSource.GetGeneration() != expectedGeneration)
        {
            throw new InvalidOperationException(
                "The current database profile changed while the agent context was being captured.");
        }
    }

    private static void ValidateAuthorityAgainstObservation(
        AgentTurnContextCaptureCommand command,
        AgentChatContextSnapshot context,
        AgentExecutionAuthorityRecord authority)
    {
        if (authority.AgentId != command.AgentId)
        {
            throw new AgentExecutionAuthorityMismatchException(
                "The resolved execution authority does not belong to the requesting agent.");
        }

        if (authority.DatabaseProfileGeneration != command.ExpectedDatabaseProfileGeneration)
        {
            throw new AgentExecutionAuthorityMismatchException(
                "The resolved execution authority was produced for a different database profile generation.");
        }

        var observedScope = context.Scope.WorkspaceScope;
        if (observedScope is not null && observedScope != authority.WorkspaceScope)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"The published workspace scope '{observedScope.DisplayName}' does not match the canonical execution scope '{authority.WorkspaceScope.DisplayName}'.");
        }

        if (observedScope is null && !authority.WorkspaceScope.IsDefaultSandbox)
        {
            throw new AgentExecutionAuthorityMismatchException(
                "The canonical execution scope claims workspace access that the published context does not carry.");
        }
    }
}
