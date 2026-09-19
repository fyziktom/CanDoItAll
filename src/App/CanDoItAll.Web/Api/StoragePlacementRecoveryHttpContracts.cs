using CanDoItAll.Infrastructure.Storage;

namespace CanDoItAll.Web.Api;

// Documentation-only contract: the handlers write an anonymous object with the same JSON shape.
/// <summary>
/// Failure body of the storage placement recovery operations: a JSON object with a single <c>failure</c> member, for
/// example <c>{ "failure": 1 }</c>. It carries no message; the status and <c>failure</c> say what happened. This family
/// does not use the general <c>errors</c> envelope or problem details. A failure does not prove that nothing changed:
/// after a failed command, read the placement again before the next step.
/// </summary>
/// <param name="Failure">
/// Why the request failed, as a JSON integer: 0 Denied (HTTP 403: the caller is not authorized, including when API
/// authorization is disabled or authority changed during the request), 1 StaleContext (HTTP 409: the database context
/// is not the host's current one), 2 NotFound (HTTP 404: no such placement intent), 3 Blocked (HTTP 409: the action is
/// not available for the placement now), 4 InvalidRequest (HTTP 400) or 5 Unavailable (HTTP 503: the request could not
/// complete; its effect is unknown until the placement is read again).
/// </param>
internal sealed record StoragePlacementRecoveryFailureResponse(StoragePlacementRecoveryFailure Failure);

internal static class StoragePlacementRecoveryHttpMetadata {
    public static RouteHandlerBuilder ProducesStoragePlacementRecoveryFailures(this RouteHandlerBuilder builder,
        params int[] statusCodes) {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(statusCodes);

        foreach (var statusCode in statusCodes.Distinct()) {
            builder.Produces<StoragePlacementRecoveryFailureResponse>(statusCode);
        }

        return builder;
    }
}
