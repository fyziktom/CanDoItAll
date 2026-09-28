using CanDoItAll.AgentFramework.Core;
using System.Text.Json.Serialization;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Web.Api;

/// <summary>
/// HTTP 202 answer of a provider operation whose provider-profile change was stored but whose follow-up
/// reconciliation did not finish. The response also carries the header <c>CDA-Provider-Outcome:
/// committed-reconciliation-pending</c>. The change is saved: do not repeat the request; read the provider profile to
/// see its stored state.
/// </summary>
/// <param name="ProviderId">Identifier of the provider profile whose change was stored.</param>
/// <param name="CanonicalCommitSucceeded">Always true: the provider-profile change itself was stored.</param>
/// <param name="ReconciliationRequired">Always true: follow-up reconciliation of the change is still pending.</param>
/// <param name="Warning">
/// Human-readable explanation, currently "The provider change is saved; secondary reconciliation is pending." Do not
/// parse it.
/// </param>
public sealed record ProviderCommittedApiResponse(
    Guid ProviderId,
    bool CanonicalCommitSucceeded,
    bool ReconciliationRequired,
    string Warning);

/// <summary>
/// HTTP 409 answer of a provider-profile write whose outcome could not be confirmed. The response carries the headers
/// <c>CDA-Provider-Outcome: unconfirmed-verification-required</c> and <c>Cache-Control: no-store</c>. The write may or
/// may not have taken effect: never repeat it automatically; send <c>attempt</c> unchanged to
/// <c>POST /api/agents/providers/mutations/verify</c> first.
/// </summary>
/// <param name="Code">Stable code of this condition: <c>agents.provider-write-unconfirmed</c>.</param>
/// <param name="ProviderId">Identifier of the provider profile the write targeted.</param>
/// <param name="Attempt">Receipt of the write attempt, to be sent unchanged to the verification operation.</param>
/// <param name="AutomaticReplaySafe">Always false: repeating the write without verification is not safe.</param>
/// <param name="VerificationPath">
/// Path of the verification operation: <c>/api/agents/providers/mutations/verify</c>.
/// </param>
/// <param name="Message">Human-readable explanation for operators; do not parse it.</param>
public sealed record ProviderUnconfirmedApiResponse(
    string Code,
    Guid ProviderId,
    ProviderMutationAttempt Attempt,
    bool AutomaticReplaySafe,
    string VerificationPath,
    string Message);

/// <summary>
/// Result of verifying an unconfirmed provider-profile write, returned by
/// <c>POST /api/agents/providers/mutations/verify</c> with the header <c>Cache-Control: no-store</c>.
/// </summary>
/// <param name="ProviderId">Identifier of the provider profile named by the receipt.</param>
/// <param name="Outcome">
/// Whether the write took effect, as text: <c>Committed</c> (the stored profile shows the write),
/// <c>DefinitelyNotCommitted</c> (the stored profile shows it did not happen; it may be submitted again after reading
/// the current profile) or <c>StillUnconfirmed</c> (not provable, or the profile could not be read; verify again
/// later).
/// </param>
/// <param name="ConcurrencyToken">
/// Concurrency token of the stored profile as read during verification; null when the profile does not exist or could
/// not be read. Use a fresh editor read, not this value, before another write.
/// </param>
/// <param name="AutomaticReplaySafe">Always false: never repeat the original write automatically.</param>
public sealed record ProviderVerificationApiResponse(
    Guid ProviderId,
    [property: JsonConverter(typeof(JsonStringEnumConverter<ProviderVerificationDisposition>))]
    ProviderVerificationDisposition Outcome,
    Guid? ConcurrencyToken,
    bool AutomaticReplaySafe);

/// <summary>
/// HTTP 400 body of <c>POST /api/agents/providers/mutations/verify</c> when the receipt is structurally invalid (an
/// all-zero <c>attemptId</c> or <c>providerId</c>, or an undefined <c>kind</c>). This operation does not use the
/// general <c>errors</c> envelope for this rejection. Describes the response only; the server writes the same JSON
/// members.
/// </summary>
/// <param name="Code">Stable code: <c>agents.provider-receipt-invalid</c>.</param>
/// <param name="Message">Human-readable explanation, currently "The mutation receipt is invalid."</param>
internal sealed record ProviderMutationReceiptInvalidApiResponse(string Code, string Message);

internal static class ProviderApiResults {
    internal const string OutcomeHeader = "CDA-Provider-Outcome";
    internal const string ReconciliationPending = "committed-reconciliation-pending";
    internal const string ConflictCode = "agents.provider-concurrency-conflict";
    internal const string ReferenceConflictCode = "agents.provider-reference-conflict";
    internal const string UnconfirmedCode = "agents.provider-write-unconfirmed";
    internal const string NotFoundCode = "agents.provider-not-found";
    internal const string UnavailableCode = "agents.provider-unavailable";
    internal const string DiagnosticUnavailableCode = "agents.provider-diagnostic-unavailable";

    public static async Task<IResult> ExecuteAsync(
        HttpContext context, Func<Task<IResult>> operation,
        Func<ProviderMutationCommit, IResult>? committedResponse = null) {
        try {
            return await operation();
        } catch (ProviderMutationCommittedException exception) {
            context.Response.Headers[OutcomeHeader] = ReconciliationPending;
            return committedResponse?.Invoke(exception.Commit) ?? Results.Accepted(value: new ProviderCommittedApiResponse(exception.ProviderId, true, true,
                "The provider change is saved; secondary reconciliation is pending."));
        } catch (ProviderProfileValidationException) {
            return ApiEndpointResults.AgentValidationFailure(context,
                "The provider configuration or requested operation is invalid.", ApiEndpointResults.ProviderRequestInvalidCode);
        } catch (SharedProviderPublicationEligibilityException) {
            return ApiEndpointResults.AgentValidationFailure(context,
                "The provider is not eligible for publication.", ApiEndpointResults.ProviderRequestInvalidCode);
        } catch (Exception exception) when (exception is ProviderProfileConcurrencyException or SharedProviderConcurrencyException) {
            return Failure(context, StatusCodes.Status409Conflict, "The provider state changed. Read its current revision before another write.", ConflictCode);
        } catch (SharedProviderProfileDeletionBlockedException exception) {
            return Failure(context, StatusCodes.Status409Conflict,
                SharedProviderDeletionMessages.For(exception.ReferenceKinds), ReferenceConflictCode);
        } catch (SharedProviderSourceDeletionBlockedException) {
            return Failure(context, StatusCodes.Status409Conflict,
                SharedProviderDeletionMessages.SourceWithImports, ReferenceConflictCode);
        } catch (ProviderMutationUnconfirmedException exception) {
            context.Response.Headers[OutcomeHeader] = "unconfirmed-verification-required";
            context.Response.Headers.CacheControl = "no-store";
            return Results.Conflict(new ProviderUnconfirmedApiResponse(UnconfirmedCode, exception.Attempt.ProviderId,
                exception.Attempt, false, "/api/agents/providers/mutations/verify",
                "The write outcome is unconfirmed. Do not automatically replay it. Verify this exact receipt."));
        } catch (KeyNotFoundException) {
            return Failure(context, StatusCodes.Status404NotFound, "The provider was not found.", NotFoundCode);
        } catch (ProviderHealthDiagnosticException) {
            return Failure(context, StatusCodes.Status502BadGateway,
                "The provider diagnostic could not be completed. No health update was written.", DiagnosticUnavailableCode);
        } catch (ProviderRuntimeProfileUnavailableException) {
            return Failure(context, StatusCodes.Status503ServiceUnavailable, "The provider runtime state is unavailable.", UnavailableCode);
        }
    }

    private static IResult Failure(HttpContext context, int status, string message, string code) =>
        ApiEndpointResults.AgentFailure(context, status, message, code);
}
