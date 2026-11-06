using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.AgentFramework;

namespace CanDoItAll.Web.Api;

internal static class AgentCatalogApiResults {
    internal const string OutcomeHeader = "CDA-Agent-Catalog-Outcome";
    internal const string ReconciliationPending = "committed-reconciliation-pending";

    public static IResult Committed(IResult result) => new CommittedResult(result);

    public static async Task<IResult> CopyAsync(Func<Task<Guid>> copy) {
        try {
            return Results.Ok(await copy());
        } catch (AgentCatalogCommittedException<Guid> exception) {
            return Committed(Results.Ok(exception.Accepted));
        } catch (AgentDirectoryProjectionSynchronizationException exception) {
            return Committed(Results.Ok(exception.AgentId));
        }
    }

    private sealed class CommittedResult(IResult result) : IResult {
        public Task ExecuteAsync(HttpContext context) {
            context.Response.Headers[OutcomeHeader] = ReconciliationPending;
            return result.ExecuteAsync(context);
        }
    }
}
