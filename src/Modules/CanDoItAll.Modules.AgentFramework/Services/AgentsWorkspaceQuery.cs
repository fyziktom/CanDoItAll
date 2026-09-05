using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Usage;

namespace CanDoItAll.Modules.AgentFramework;

public sealed record AgentsShellSnapshot(
    AgentOverviewSnapshot? Overview,
    ProviderUsageSnapshot? Usage,
    AgentDefinition? HrAgent,
    IReadOnlyDictionary<string, string?> AvatarImageUrls,
    int BoundResourceCount,
    string? HrAgentError);

public interface IAgentsWorkspaceQuery {
    Task<AgentsShellSnapshot> ReadShellAsync(
        AgentWorkspaceSection section,
        ProviderUsageWorkloadSelection usageSelection,
        CancellationToken cancellationToken = default);

    ValueTask<ProviderUsageSnapshot> ReadUsageAsync(
        ProviderUsageWorkloadSelection selection,
        CancellationToken cancellationToken = default);
}

public sealed class AgentsWorkspaceQuery(
    IAgentFrameworkWorkspaceService workspace,
    ProviderUsageQueryService usage,
    IBoundAgentResourceQuery boundResources) : IAgentsWorkspaceQuery {
    public async Task<AgentsShellSnapshot> ReadShellAsync(
        AgentWorkspaceSection section,
        ProviderUsageWorkloadSelection usageSelection,
        CancellationToken cancellationToken = default) {
        if (!Enum.IsDefined(section)) {
            throw new ArgumentOutOfRangeException(nameof(section));
        }
        var overviewTask = section.IsHistoryHost()
            ? Task.FromResult<AgentOverviewSnapshot?>(null)
            : ReadOverviewAsync(cancellationToken);
        var usageTask = section.IsHistoryHost()
            ? Task.FromResult<ProviderUsageSnapshot?>(null)
            : ReadInitialUsageAsync(usageSelection, cancellationToken);
        var hrTask = ReadHrAgentAsync(cancellationToken);
        var boundTask = boundResources.CountAsync(cancellationToken);
        await Task.WhenAll(overviewTask, usageTask, hrTask, boundTask);
        var hr = await hrTask;
        return new(await overviewTask, await usageTask, hr.Agent,
            hr.Agents.ToDictionary(agent => agent.Id.ToString("D"), agent => agent.AvatarImageUrl,
                StringComparer.OrdinalIgnoreCase),
            await boundTask, hr.Error);
    }

    public ValueTask<ProviderUsageSnapshot> ReadUsageAsync(
        ProviderUsageWorkloadSelection selection,
        CancellationToken cancellationToken = default)
        => usage.QueryAsync(selection, cancellationToken);

    private async Task<AgentOverviewSnapshot?> ReadOverviewAsync(CancellationToken cancellationToken)
        => await workspace.GetAgentOverviewAsync(cancellationToken);

    private async Task<ProviderUsageSnapshot?> ReadInitialUsageAsync(
        ProviderUsageWorkloadSelection selection, CancellationToken cancellationToken)
        => await ReadUsageAsync(selection, cancellationToken);

    private async Task<(AgentDefinition? Agent, IReadOnlyList<AgentDefinition> Agents, string? Error)> ReadHrAgentAsync(
        CancellationToken cancellationToken) {
        try {
            var agents = await workspace.ListAgentsAsync(includeTemplates: false, cancellationToken);
            var agent = agents.SingleOrDefault(HrAgentIdentity.Matches);
            return (agent, agents, agent is null ? $"The managed agent '{HrAgentIdentity.AgentId:D}' is not available." : null);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception exception) {
            return (null, [], exception.Message);
        }
    }
}
