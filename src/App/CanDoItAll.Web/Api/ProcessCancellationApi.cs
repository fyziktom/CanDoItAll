using System.Globalization;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Web.Api;

internal static class ProcessCancellationApi {
    internal static void MapProcessCancellationApi(this RouteGroupBuilder processes) {
        processes.MapGet("/runs/{runId:guid}/cancellation", ReadAsync)
            .WithName("GetProcessCancellationProgress")
            .WithSummary("Observe cancellation and cleanup progress without advancing recovery.")
            .WithDescription("Requires Process read permission. ChildRun describes only the selected child; it does not report root cleanup. Cancelled runtime status alone is not completed recovery. LegacyUntracked and ReconciliationRequired need operator attention. Progress revisions are decimal strings, not edit preconditions. This read does not promise an enabled recovery worker.")
            .Produces<ProcessCancellationApiView>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);
    }

    internal static async Task<IResult> ReadAsync(Guid runId, HttpContext context, IProcessCancellationProgressReader reader,
        CancellationToken cancellationToken) {
        context.Response.Headers.CacheControl = "no-store";
        if (runId == Guid.Empty) {
            return ApiEndpointResults.BadRequest("A non-empty process run identity is required.", "process.run_invalid");
        }
        var progress = await reader.ReadAsync(new(runId), cancellationToken).ConfigureAwait(false);
        return progress is null
            ? ApiEndpointResults.NotFound($"Process run '{runId:D}' was not found.", "process.run_not_found")
            : Results.Ok(ProcessCancellationApiView.From(progress));
    }
}

internal sealed record ProcessCancellationApiView(Guid RunId, Guid RootRunId, string RuntimeStatus, DateTimeOffset RuntimeUpdatedAtUtc,
    string Tracking, ProcessCancellationRecoveryApiView? Recovery) {
    internal static ProcessCancellationApiView From(ProcessCancellationProgress progress)
        => new(progress.RunId.Value, progress.RootRunId.Value, progress.RuntimeStatus.ToString(), progress.RuntimeUpdatedAtUtc,
            progress.Tracking.ToString(), progress.Recovery is { } recovery ? new(recovery.Origin.ToString(), recovery.BarrierCommandId?.Value,
                recovery.Revision.ToString(CultureInfo.InvariantCulture), recovery.UpdatedAtUtc, recovery.Stage?.ToString(), recovery.Pending.ToString()) : null);
}

internal sealed record ProcessCancellationRecoveryApiView(string Origin, Guid? BarrierCommandId, string Revision, DateTimeOffset UpdatedAtUtc,
    string? Stage, string Pending);
