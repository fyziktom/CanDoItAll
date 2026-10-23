using System.Globalization;
using System.Data.Common;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Processes.Application;

namespace CanDoItAll.Web.Api;

internal static class ProcessCancellationApi {
    private const string Route = "/runs/{runId:guid}/cancellation";

    internal static void MapProcessCancellationApi(this RouteGroupBuilder processes) {
        processes.MapGet(Route, ReadAsync)
            .WithName("GetProcessCancellationProgress")
            .WithSummary("Observe cancellation and cleanup progress without advancing recovery.")
            .WithDescription("Requires Process read permission. ChildRun describes only the selected child; it does not report root cleanup. Cancelled runtime status alone is not completed recovery. LegacyUntracked and ReconciliationRequired need operator attention. Progress revisions are decimal strings, not edit preconditions. This read does not promise an enabled recovery worker.")
            .Produces<ProcessCancellationApiView>()
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status403Forbidden,
                StatusCodes.Status404NotFound);
        processes.MapPost(Route, AdmitAsync)
            .WithName("RequestRootProcessCancellation")
            .WithApiPermission(ApiAccessScopeNames.ExecuteProcesses)
            .WithSummary("Save one root cancellation barrier and return before descendant cleanup.")
            .WithDescription("Requires Process read and execute grants and an enabled, registered recovery worker. Accepts no body. A child identity is refused without cancelling its root or siblings. Accepted means pending cleanup, not completion. On an unconfirmed response or timeout, observe the Location GET before another action; do not assume rollback. The legacy /cancel route retains its existing behavior during migration.")
            .Produces<ProcessCancellationAdmissionApiView>(StatusCodes.Status202Accepted)
            .Produces<ProcessCancellationAdmissionApiView>()
            .Produces<ProcessCancellationAdmissionApiView>(StatusCodes.Status409Conflict)
            .Produces<ProcessCancellationAdmissionApiView>(StatusCodes.Status503ServiceUnavailable)
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

    internal static async Task<IResult> AdmitAsync(Guid runId, HttpContext context, ProcessCancellationHttpAccess access,
        ProcessRootCancellationAdmissionService admissions, TimeProvider clock, ILoggerFactory logs, CancellationToken cancellationToken) {
        context.Response.Headers.CacheControl = "no-store";
        if (runId == Guid.Empty || context.Request.ContentLength is > 0 || context.Request.Headers.ContainsKey("Transfer-Encoding")) {
            return ApiEndpointResults.BadRequest("A non-empty root run identity and an empty request body are required.", "process.cancellation_invalid");
        }
        var location = $"/api/processes/runs/{runId:D}/cancellation";
        context.Response.Headers.Location = location;
        using var deadline = new CancellationTokenSource(ProcessRootCancellationAdmissionService.MaximumDuration, clock);
        using var bounded = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, deadline.Token);
        try {
            var requester = await access.CaptureAsync(bounded.Token);
            var result = await admissions.AdmitAsync(new(runId), requester, bounded.Token);
            if (result.Status == ProcessRootCancellationAdmissionStatus.NotFound) {
                return ApiEndpointResults.NotFound($"Process run '{runId:D}' was not found.", "process.run_not_found");
            }
            var statusCode = result.Status switch {
                ProcessRootCancellationAdmissionStatus.Accepted => StatusCodes.Status202Accepted,
                ProcessRootCancellationAdmissionStatus.Completed => StatusCodes.Status200OK,
                ProcessRootCancellationAdmissionStatus.Unavailable or ProcessRootCancellationAdmissionStatus.TimedOut or
                    ProcessRootCancellationAdmissionStatus.Unconfirmed => StatusCodes.Status503ServiceUnavailable,
                _ => StatusCodes.Status409Conflict
            };
            return Results.Json(new ProcessCancellationAdmissionApiView(runId, result.Status.ToString(), result.BarrierCommandId?.Value,
                result.Progress is { } progress ? ProcessCancellationApiView.From(progress) : null), statusCode: statusCode);
        } catch (WebCurrentPrincipalDeniedException) {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
            throw;
        } catch (Exception failure) when (failure is IOException or DbException or TimeoutException ||
                failure is OperationCanceledException && deadline.IsCancellationRequested) {
            logs.CreateLogger(typeof(ProcessCancellationApi)).LogWarning("Root cancellation admission for {RunId} is unconfirmed after {FailureType}; observe the original root before retrying.",
                runId, failure.GetType().FullName);
            return Results.Json(new ProcessCancellationAdmissionApiView(runId, nameof(ProcessRootCancellationAdmissionStatus.Unconfirmed), null, null),
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}

internal sealed record ProcessCancellationAdmissionApiView(Guid RunId, string Status, Guid? BarrierCommandId, ProcessCancellationApiView? Progress);

internal sealed record ProcessCancellationApiView(Guid RunId, Guid RootRunId, string RuntimeStatus, DateTimeOffset RuntimeUpdatedAtUtc,
    string Tracking, ProcessCancellationRecoveryApiView? Recovery) {
    internal static ProcessCancellationApiView From(ProcessCancellationProgress progress)
        => new(progress.RunId.Value, progress.RootRunId.Value, progress.RuntimeStatus.ToString(), progress.RuntimeUpdatedAtUtc,
            progress.Tracking.ToString(), progress.Recovery is { } recovery ? new(recovery.Origin.ToString(), recovery.BarrierCommandId?.Value,
                recovery.Revision.ToString(CultureInfo.InvariantCulture), recovery.UpdatedAtUtc, recovery.Stage?.ToString(), recovery.Pending.ToString()) : null);
}

internal sealed record ProcessCancellationRecoveryApiView(string Origin, Guid? BarrierCommandId, string Revision, DateTimeOffset UpdatedAtUtc,
    string? Stage, string Pending);
