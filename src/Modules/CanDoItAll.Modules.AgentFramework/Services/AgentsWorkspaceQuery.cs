using System.Collections.Immutable;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

[Flags]
public enum AgentsHeaderFailure {
    None = 0,
    HrAgent = 1,
    Avatars = 2,
    BoundResources = 4
}

public sealed record AgentsHeaderAgent(Guid Id, string Name, string? AvatarImageUrl);

public sealed record AgentsHeaderSnapshot(
    AgentsHeaderAgent? HrAgent,
    IReadOnlyDictionary<string, string?> AvatarImageUrls,
    int? BoundResourceCount,
    AgentsHeaderFailure Failures);

public interface IAgentsWorkspaceQuery {
    Task<AgentsHeaderSnapshot> ReadHeaderAsync(CancellationToken cancellationToken = default);
    Task<AgentOverviewSnapshot> ReadOverviewAsync(CancellationToken cancellationToken = default);
    ValueTask<ProviderUsageSnapshot> ReadUsageAsync(
        ProviderUsageWorkloadSelection selection,
        CancellationToken cancellationToken = default);
}

public sealed class AgentsWorkspaceQuery(
    IAgentFrameworkWorkspaceService workspace,
    ProviderUsageQueryService usage,
    IBoundAgentResourceQuery boundResources,
    ILogger<AgentsWorkspaceQuery> logger) : IAgentsWorkspaceQuery {
    public async Task<AgentsHeaderSnapshot> ReadHeaderAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var agentsTask = ReadAgentHeaderAsync(cancellationToken);
        var boundTask = ReadBoundResourcesAsync(cancellationToken);
        await Task.WhenAll(agentsTask, boundTask);
        var agents = await agentsTask;
        var bound = await boundTask;
        return agents with {
            BoundResourceCount = bound.Count,
            Failures = agents.Failures | (bound.Failed ? AgentsHeaderFailure.BoundResources : AgentsHeaderFailure.None)
        };
    }

    public Task<AgentOverviewSnapshot> ReadOverviewAsync(CancellationToken cancellationToken = default)
        => workspace.GetAgentOverviewAsync(cancellationToken);

    public ValueTask<ProviderUsageSnapshot> ReadUsageAsync(
        ProviderUsageWorkloadSelection selection,
        CancellationToken cancellationToken = default)
        => usage.QueryAsync(selection, cancellationToken);

    private async Task<AgentsHeaderSnapshot> ReadAgentHeaderAsync(CancellationToken token) {
        IReadOnlyList<AgentDefinition> agents;
        try {
            agents = await workspace.ListAgentsAsync(includeTemplates: false, token);
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            LogFailure(AgentsHeaderFailure.HrAgent | AgentsHeaderFailure.Avatars, exception);
            return new(null, ImmutableDictionary<string, string?>.Empty, null,
                AgentsHeaderFailure.HrAgent | AgentsHeaderFailure.Avatars);
        }

        var failures = AgentsHeaderFailure.None;
        AgentsHeaderAgent? hr = null;
        try {
            if (agents.SingleOrDefault(HrAgentIdentity.Matches) is { } agent) {
                hr = new(agent.Id, agent.Name, agent.AvatarImageUrl);
            } else {
                failures |= AgentsHeaderFailure.HrAgent;
                logger.LogWarning("Managed HR agent {AgentId} is unavailable in the header catalog.", HrAgentIdentity.AgentId);
            }
        } catch (Exception exception) {
            failures |= AgentsHeaderFailure.HrAgent;
            LogFailure(AgentsHeaderFailure.HrAgent, exception);
        }

        IReadOnlyDictionary<string, string?> avatars = ImmutableDictionary<string, string?>.Empty;
        try {
            avatars = agents.ToImmutableDictionary(agent => agent.Id.ToString("D"), agent => agent.AvatarImageUrl,
                StringComparer.OrdinalIgnoreCase);
        } catch (Exception exception) {
            failures |= AgentsHeaderFailure.Avatars;
            LogFailure(AgentsHeaderFailure.Avatars, exception);
        }
        return new(hr, avatars, null, failures);
    }

    private async Task<(int? Count, bool Failed)> ReadBoundResourcesAsync(CancellationToken token) {
        try {
            var count = await boundResources.CountAsync(token);
            if (count < 0) {
                throw new InvalidDataException("The bound-resource count is invalid.");
            }
            return (count, false);
        } catch (OperationCanceledException) when (token.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            LogFailure(AgentsHeaderFailure.BoundResources, exception);
            return (null, true);
        }
    }

    private void LogFailure(AgentsHeaderFailure source, Exception exception)
        => logger.LogWarning("Agents header source {Source} failed ({FailureType}).", source, exception.GetType().Name);
}
