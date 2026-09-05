using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Modules.AgentFramework.ProviderManagement;

namespace CanDoItAll.Web.Api;

public sealed record ProviderCommittedApiResponse(
    Guid ProviderId,
    bool CanonicalCommitSucceeded,
    bool ReconciliationRequired,
    string Warning);

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
        } catch (Exception exception) when (exception is SharedProviderProfileDeletionBlockedException or SharedProviderSourceDeletionBlockedException) {
            return Failure(context, StatusCodes.Status409Conflict,
                "Permanent publication or import history prevents deletion. Unpublish or retire the provider.", ReferenceConflictCode);
        } catch (ProviderMutationUnconfirmedException) {
            return Failure(context, StatusCodes.Status503ServiceUnavailable,
                "The write could not be confirmed. Verify canonical state before repeating it.", UnconfirmedCode);
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
