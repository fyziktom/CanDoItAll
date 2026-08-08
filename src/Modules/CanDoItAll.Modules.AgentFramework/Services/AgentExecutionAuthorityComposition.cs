using System.Security.Cryptography;
using System.Text;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;

namespace CanDoItAll.Modules.AgentFramework;

/// <summary>
/// Canonical <see cref="IAgentExecutionAuthorityResolver"/> backed by durable
/// agent configuration and the current database profile. UI-published access
/// entries never grant authority here: every source kind with a canonical
/// rule owns a registered <see cref="IAgentExecutionSourceAuthorityProvider"/>
/// that re-derives scope and rights from stored configuration, and every
/// workspace-scope claim is validated against the canonical scope before
/// admission. Source kinds without a provider fail closed to a bounded
/// read-only sandbox and can never inherit an observed workspace scope. A UI
/// access hint may deny a turn early but can never select scope, grant read,
/// or grant mutation.
/// </summary>
internal sealed class CanonicalAgentExecutionAuthorityResolver : IAgentExecutionAuthorityResolver
{
    public const string CanonicalPolicyVersion = "v2-canonical";
    public const string FailClosedSandboxPolicyVersion = "v2-fail-closed-sandbox";

    private readonly ICanDoItAllAgentWorkspaceFactory workspaceFactory;
    private readonly IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor;
    private readonly IAgentExecutionProfileGenerationSource profileGenerationSource;
    private readonly TimeProvider timeProvider;
    private readonly IReadOnlyDictionary<string, IAgentExecutionSourceAuthorityProvider> providersBySourceKind;

    public CanonicalAgentExecutionAuthorityResolver(
        ICanDoItAllAgentWorkspaceFactory workspaceFactory,
        IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
        IAgentExecutionProfileGenerationSource profileGenerationSource,
        TimeProvider timeProvider,
        IReadOnlyList<IAgentExecutionSourceAuthorityProvider>? sourceAuthorityProviders = null)
    {
        this.workspaceFactory = workspaceFactory ?? throw new ArgumentNullException(nameof(workspaceFactory));
        this.databaseProfileRuntimeAccessor = databaseProfileRuntimeAccessor ?? throw new ArgumentNullException(nameof(databaseProfileRuntimeAccessor));
        this.profileGenerationSource = profileGenerationSource ?? throw new ArgumentNullException(nameof(profileGenerationSource));
        this.timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        providersBySourceKind = BuildProviderRegistry(sourceAuthorityProviders ?? CreateDefaultProviders());
    }

    /// <summary>
    /// The built-in canonical source rules. Deterministic order; construction
    /// fails on duplicate source kinds so exactly one provider owns each key.
    /// </summary>
    internal static IReadOnlyList<IAgentExecutionSourceAuthorityProvider> CreateDefaultProviders()
        =>
        [
            new ProjectStructureExecutionAuthorityProvider(),
            new ProjectsExecutionAuthorityProvider(),
            new ProcessesExecutionAuthorityProvider("processes"),
            new ProcessesExecutionAuthorityProvider("processes-live")
        ];

    private static Dictionary<string, IAgentExecutionSourceAuthorityProvider> BuildProviderRegistry(
        IReadOnlyList<IAgentExecutionSourceAuthorityProvider> providers)
    {
        var registry = new Dictionary<string, IAgentExecutionSourceAuthorityProvider>(StringComparer.Ordinal);
        foreach (var provider in providers)
        {
            ArgumentNullException.ThrowIfNull(provider);
            ArgumentException.ThrowIfNullOrWhiteSpace(provider.SourceKind);
            if (!registry.TryAdd(provider.SourceKind, provider))
            {
                throw new InvalidOperationException(
                    $"Duplicate execution authority provider for source kind '{provider.SourceKind}'.");
            }
        }

        return registry;
    }

    public async ValueTask<AgentExecutionAuthorityRecord> ResolveAsync(
        AgentExecutionAuthorityResolutionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        EnsureCurrentGeneration(request.ExpectedDatabaseProfileGeneration);
        var currentProfile = databaseProfileRuntimeAccessor
            .ResolveCurrentProfile()
            .Profile;
        var agent = await ResolveActiveAgentAsync(request.AgentId, cancellationToken)
            .ConfigureAwait(false);
        EnsureCurrentGeneration(request.ExpectedDatabaseProfileGeneration);

        var (workspaceScope, readAllowed, mutationAllowed, policyVersion) = await ResolveSourceAuthorityAsync(
            request,
            agent,
            currentProfile.Id,
            cancellationToken)
            .ConfigureAwait(false);
        EnsureCurrentGeneration(request.ExpectedDatabaseProfileGeneration);

        return new AgentExecutionAuthorityRecord(
            AgentExecutionAuthorityId.Create(),
            agent.Id,
            currentProfile.Id,
            request.ExpectedDatabaseProfileGeneration,
            workspaceScope,
            readAllowed,
            mutationAllowed,
            policyVersion,
            ComputePolicyFingerprint(
                agent.Id,
                request.SourceKind,
                request.SourceId,
                workspaceScope,
                request.ExpectedDatabaseProfileGeneration,
                readAllowed,
                mutationAllowed,
                policyVersion),
            timeProvider.GetUtcNow());
    }

    private async Task<AgentDefinition> ResolveActiveAgentAsync(
        Guid agentId,
        CancellationToken cancellationToken)
    {
        if (agentId == Guid.Empty)
        {
            throw new ArgumentException("An agent id is required.", nameof(agentId));
        }

        var agents = await workspaceFactory
            .GetOrganizationWorkspaceService()
            .ListAgentsAsync(includeTemplates: false, cancellationToken)
            .ConfigureAwait(false);
        var agent = agents.FirstOrDefault(item => item.Id == agentId);
        if (agent is null || agent.IsTemplate || agent.Status != AgentLifecycleStatus.Active)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"Agent '{agentId:N}' is not an active executable agent in the current profile.");
        }

        return agent;
    }

    private async ValueTask<(WorkspaceScopeDescriptor Scope, bool ReadAllowed, bool MutationAllowed, string PolicyVersion)> ResolveSourceAuthorityAsync(
        AgentExecutionAuthorityResolutionRequest request,
        AgentDefinition agent,
        Guid currentProfileId,
        CancellationToken cancellationToken)
    {
        // A UI access hint can deny a turn early, but it can never select a
        // scope, grant read, or grant mutation — those come only from the
        // source-keyed durable rules below.
        if (request.UiAccessHint is { } hint &&
            !hint.Permissions.HasFlag(AgentChatContextPermission.Read))
        {
            throw new AgentChatContextAccessDeniedException(agent.Id, default);
        }

        if (providersBySourceKind.TryGetValue(request.SourceKind.Value, out var provider))
        {
            var decision = await provider
                .ResolveAsync(
                    new AgentExecutionSourceAuthorityRequest(
                        agent,
                        request.SourceKind,
                        request.SourceId,
                        request.ObservedWorkspaceScope,
                        currentProfileId),
                    cancellationToken)
                .ConfigureAwait(false);
            // Fence the database profile generation again after the provider's
            // asynchronous lookup so a mid-resolution profile switch cannot
            // smuggle a stale decision into admission.
            EnsureCurrentGeneration(request.ExpectedDatabaseProfileGeneration);
            return (decision.WorkspaceScope, decision.ReadAllowed, decision.MutationAllowed, decision.PolicyVersion);
        }

        // Unknown source kinds fail closed. A published workspace claim from a
        // source without a canonical rule is denied outright — it can never be
        // adopted or silently downgraded; without a claim the turn receives a
        // bounded read-only sandbox.
        if (request.ObservedWorkspaceScope is { } observedScope)
        {
            throw new AgentExecutionAuthorityMismatchException(
                $"The source kind '{request.SourceKind.Value}' has no canonical authority rule for the published workspace scope '{observedScope.DisplayName}'.");
        }

        return (WorkspaceScopeDescriptor.Sandbox, true, false, FailClosedSandboxPolicyVersion);
    }

    private void EnsureCurrentGeneration(DatabaseProfileGeneration expectedGeneration)
    {
        if (profileGenerationSource.GetGeneration() != expectedGeneration)
        {
            throw new InvalidOperationException(
                "The current database profile changed while execution authority was being resolved.");
        }
    }

    private static string ComputePolicyFingerprint(
        Guid agentId,
        AgentChatContextSourceKind sourceKind,
        AgentChatContextSourceId sourceId,
        WorkspaceScopeDescriptor scope,
        DatabaseProfileGeneration generation,
        bool readAllowed,
        bool mutationAllowed,
        string policyVersion)
    {
        var payload = string.Join(
            '',
            agentId.ToString("N"),
            sourceKind.Value,
            sourceId.Value,
            scope.Kind.ToString(),
            scope.Key,
            generation.Value.ToString(),
            readAllowed ? "read" : "no-read",
            mutationAllowed ? "mutate" : "no-mutate",
            policyVersion);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)))
            .ToLowerInvariant();
    }
}
