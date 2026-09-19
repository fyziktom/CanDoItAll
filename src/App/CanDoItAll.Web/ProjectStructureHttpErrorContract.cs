namespace CanDoItAll.Web;

/// <summary>
/// Error envelope of the Project Structure operations: a single <c>error</c> object describing why the operation
/// rejected or failed the request. This family does not return ProblemDetails or the <c>errors</c> array used by the
/// other API families. The status alone does not prove that nothing was written: a 409 or 500 can follow a partially
/// applied change, so read the affected project state again before retrying.
/// </summary>
/// <param name="Error">The rejection or failure.</param>
internal sealed record ProjectStructureErrorResponse(ProjectStructureErrorDetail Error);

/// <summary>
/// Project Structure rejection or failure: a stable error code, a human-readable message and optional structured
/// details.
/// </summary>
/// <param name="ErrorCode">
/// Stable machine-readable reason, for example <c>StaleTask</c>, <c>ProjectLifetimeRefreshRequired</c> or
/// <c>NodeNotFound</c>. Branch on this value, not on the message.
/// </param>
/// <param name="Message">
/// Human-readable explanation intended for operators and logs. Its wording can change and must not be parsed.
/// </param>
internal sealed record ProjectStructureErrorDetail(string ErrorCode, string Message)
{
    /// <summary>
    /// Optional operation-specific details, such as the rejected field and the supported representation or the
    /// maximum body size. Null or absent when the failure has no details.
    /// </summary>
    public object? Details { get; init; }
}

internal static class ProjectStructureHttpErrorMetadata
{
    public static RouteHandlerBuilder ProducesProjectStructureErrors(
        this RouteHandlerBuilder builder,
        params int[] statusCodes)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(statusCodes);

        foreach (var statusCode in statusCodes.Distinct())
        {
            builder.Produces<ProjectStructureErrorResponse>(statusCode);
        }

        return builder;
    }
}
