using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Web.Api;

internal static class StoragePlacementRecoveryEndpoints {
    internal const string Route = "/api/storage-placement-recovery";

    internal static IEndpointRouteBuilder MapStoragePlacementRecoveryApi(this IEndpointRouteBuilder endpoints) {
        var group = endpoints.MapGroup(Route).WithTags("Storage recovery");
        group.MapGet("/context", (StoragePlacementRecoveryService recovery, HttpContext http, ILogger<StoragePlacementRecoveryService> logger)
            => ResultAsync(() => recovery.GetCurrentContextAsync(http.RequestAborted), logger));
        group.MapGet("/pending", (Guid databaseProfileId, long generation, Guid? projectId, Guid? storageId, int? take, int? offset,
            StoragePlacementRecoveryService recovery, HttpContext http, ILogger<StoragePlacementRecoveryService> logger) => ResultAsync(() => recovery.ListPendingAsync(
                new(new(databaseProfileId, generation), projectId, storageId, take ?? 32, offset ?? 0), http.RequestAborted), logger));
        group.MapGet("/pending-continuations", (Guid databaseProfileId, long generation, Guid? projectId, Guid? storageId, int? take, int? offset,
            StoragePlacementRecoveryService recovery, HttpContext http, ILogger<StoragePlacementRecoveryService> logger) => ResultAsync(() => recovery.ListPendingContinuationsAsync(
                new(new(databaseProfileId, generation), projectId, storageId, take ?? 32, offset ?? 0), http.RequestAborted), logger));
        group.MapGet("/{intentId:guid}", (Guid intentId, Guid databaseProfileId, long generation,
            StoragePlacementRecoveryService recovery, HttpContext http, ILogger<StoragePlacementRecoveryService> logger) => ResultAsync(() => recovery.GetAsync(
                new(new(databaseProfileId, generation), new(intentId)), http.RequestAborted), logger));
        group.MapPost("/reconcile", (StoragePlacementRecoveryCommand request, StoragePlacementRecoveryService recovery, HttpContext http,
            ILogger<StoragePlacementRecoveryService> logger) => ResultAsync(() => recovery.ReconcileAsync(request, http.RequestAborted), logger));
        group.MapPost("/verify-external-termination", (StoragePlacementExternalTerminationVerification request,
            StoragePlacementRecoveryService recovery, HttpContext http, ILogger<StoragePlacementRecoveryService> logger)
            => ResultAsync(() => recovery.RecordOperatorVerifiedExternalDispatchTerminationAsync(request, http.RequestAborted), logger));
        group.MapGet("/{intentId:guid}/owner-continuation", (Guid intentId, Guid databaseProfileId, long generation,
            IStoragePlacementOwnerContinuation continuation, HttpContext http, ILogger<StoragePlacementRecoveryService> logger)
            => ResultAsync(() => continuation.GetAsync(new(new(databaseProfileId, generation), new(intentId)), http.RequestAborted), logger));
        group.MapPost("/reconcile-cancelled-run-receipts", (StoragePlacementRecoveryCommand request,
            IStoragePlacementOwnerContinuation continuation, HttpContext http, ILogger<StoragePlacementRecoveryService> logger)
            => ResultAsync(() => continuation.ReconcileCancelledRunReceiptsAsync(request, http.RequestAborted), logger));
        group.MapPost("/continue-workflow-asset", (StoragePlacementWorkflowContinuationCommand request,
            IStoragePlacementOwnerContinuation continuation, HttpContext http, ILogger<StoragePlacementRecoveryService> logger)
            => ResultAsync(() => continuation.ReconcileWorkflowAssetAsync(request, http.RequestAborted), logger));
        return endpoints;
    }

    private static async Task<IResult> ResultAsync<T>(Func<Task<T>> operation, ILogger logger) {
        try {
            return Results.Ok(await operation());
        } catch (StoragePlacementRecoveryException exception) {
            var status = exception.Failure switch {
                StoragePlacementRecoveryFailure.Denied => StatusCodes.Status403Forbidden,
                StoragePlacementRecoveryFailure.NotFound => StatusCodes.Status404NotFound,
                StoragePlacementRecoveryFailure.InvalidRequest => StatusCodes.Status400BadRequest,
                StoragePlacementRecoveryFailure.StaleContext or StoragePlacementRecoveryFailure.Blocked => StatusCodes.Status409Conflict,
                _ => StatusCodes.Status503ServiceUnavailable
            };
            return Results.Json(new { exception.Failure }, statusCode: status);
        } catch (OperationCanceledException) {
            throw;
        } catch (Exception exception) {
            logger.LogWarning("Storage recovery could not complete ({FailureType}). Refresh the original intent status; no provider write retry was requested.",
                exception.GetType().Name);
            return Results.Json(new { Failure = StoragePlacementRecoveryFailure.Unavailable },
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }
}
