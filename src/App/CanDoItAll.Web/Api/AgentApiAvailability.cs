using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Web.Api;

internal static class AgentApiAvailability {
    public static RouteHandlerBuilder DescribeAgentAvailability(this RouteHandlerBuilder route) =>
        route.ProducesApiErrors(StatusCodes.Status404NotFound)
            .DescribeApiResponse(StatusCodes.Status404NotFound, "The agent is unavailable through this API.");

    public static RouteHandlerBuilder RequireAvailableAgent(this RouteHandlerBuilder route) =>
        RequireAvailableAgent<Guid>(route, id => id, parameterName: "agentId");

    public static RouteHandlerBuilder RequireAvailableAgent<T>(this RouteHandlerBuilder route, Func<T, Guid?> selectId,
        bool allowNew = false, string? parameterName = null) {
        return route.DescribeAgentAvailability()
            .AddEndpointFilterFactory((factory, next) => {
                var parameter = factory.MethodInfo.GetParameters().Single(p =>
                    p.ParameterType == typeof(T) && (parameterName is null || p.Name == parameterName));
                return async invocation => {
                    if (selectId(invocation.GetArgument<T>(parameter.Position)) is { } id && id != Guid.Empty) {
                        var context = invocation.HttpContext;
                        var workspace = context.RequestServices.GetRequiredService<IAgentFrameworkWorkspaceService>();
                        var available = await workspace.FindAgentApiAvailabilityAsync(id, context.RequestAborted);
                        if (!(available ?? allowNew)) {
                            return NotFound();
                        }
                    }
                    return await next(invocation);
                };
            });
    }

    public static IResult NotFound() => ApiEndpointResults.NotFound("Agent was not found.", "agents.not-found");

    public static async Task<HashSet<Guid>> HiddenIdsAsync(IAgentFrameworkWorkspaceService workspace, CancellationToken token) =>
        (await workspace.ListAgentsAsync(true, token)).Where(agent => !agent.ApiAvailable).Select(agent => agent.Id).ToHashSet();

    public static AgentTeamDefinition Project(AgentTeamDefinition team, IReadOnlySet<Guid> hidden) =>
        team with { AgentIds = team.AgentIds.Where(id => !hidden.Contains(id)).ToArray() };

    public static async Task<(IReadOnlySet<Guid> Hidden, List<Guid>? Members)> PrepareTeamMembersAsync(
        Guid? teamId, IReadOnlyList<Guid> proposed, IAgentFrameworkWorkspaceService workspace, CancellationToken token) {
        var hidden = await HiddenIdsAsync(workspace, token);
        if (proposed.Any(hidden.Contains)) {
            return (hidden, null);
        }
        if (hidden.Count == 0 || teamId is null) {
            return (hidden, proposed.ToList());
        }
        var teams = await workspace.ListAgentTeamsAsync(token);
        var original = teams.FirstOrDefault(team => team.Id == teamId);
        var retained = original?.AgentIds.Where(hidden.Contains) ?? [];
        return (hidden, [.. proposed, .. retained]);
    }

    public static async Task<bool> IsHiddenNoteAsync(Guid? noteId, IAgentFrameworkWorkspaceService workspace, CancellationToken token) {
        if (noteId is null) {
            return false;
        }
        var catalog = (await workspace.LoadCatalogSnapshotAsync(token)).Snapshot.Catalog;
        var note = catalog.Memory.FirstOrDefault(item => item.Id == noteId);
        return note is not null && catalog.Agents.Any(agent => agent.Id == note.AgentId && !agent.ApiAvailable);
    }
}
