using System.ComponentModel;
using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Modules.Workspace.ApiAccess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal static class AgentsUsageApi {
    internal static void MapUsage(this RouteGroupBuilder agents) {
        agents.MapGet("/usage", ReadAsync)
            .WithName("GetAgentsUsage")
            .DescribeApi("Read usage for a rolling UTC interval",
                "Defaults to both and 7d. Periods: 7d, 14d, 1m, 1q (previous three calendar months), 1y. " +
                "FromUtc is inclusive; ToUtc is exclusive. Requires api.agents.read and, for both or simple-chats, api.llm-chats.read. " +
                "HTTP 200 includes exact query, generation time, latest evidence time and per-source coverage. Partial, indexing and failed sources " +
                "are explicit; inspect isComplete before interpreting zero totals. Repeated or invalid parameters return 400 with an ApiErrorResponse errors array.",
                "Usage aggregates, exact query and per-source completeness. Unavailable sources remain explicit in this 200 response.")
            .Produces<ProviderUsageSnapshot>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden);
    }

    private static async Task<IResult> ReadAsync(HttpContext http, ProviderUsageQueryService usage,
        IAuthorizationService authorization, IOptions<ApiAccessOptions> options,
        [Description("agents, simple-chats or both; defaults to both")] string? usageScope = null,
        [Description("7d, 14d, 1m, 1q or 1y; defaults to 7d")] string? usagePeriod = null,
        CancellationToken cancellationToken = default) {
        if (http.Request.Query[ProviderUsagePeriods.ScopeQueryKey].Count > 1 ||
            http.Request.Query[ProviderUsagePeriods.QueryKey].Count > 1 ||
            !ProviderUsagePeriods.TryParseSelection(usageScope, out var selection) ||
            !ProviderUsagePeriods.TryParse(usagePeriod, out var period)) {
            return ApiEndpointResults.BadRequest("Supply one supported usageScope and usagePeriod value.", "agents.usage.invalid-query");
        }
        if (options.Value.Authorization.Enabled && selection.HasFlag(ProviderUsageWorkloadSelection.SimpleChats) &&
            !(await authorization.AuthorizeAsync(http.User, ApiAuthorizationPolicies.ReadLlmChats)).Succeeded) {
            return ApiEndpointResults.AgentFailure(http, StatusCodes.Status403Forbidden,
                "The selected usage scope requires Simple Chat read access.", "agents.usage.forbidden");
        }
        var query = usage.Resolve(selection, period);
        return Results.Ok(await usage.QueryWindowAsync(query, cancellationToken));
    }
}
