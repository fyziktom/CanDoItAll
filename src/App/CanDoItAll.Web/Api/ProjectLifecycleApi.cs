using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace CanDoItAll.Web.Api;

internal static class ProjectLifecycleApi {
    internal const string InvalidIntentCode = "projects.lifecycle-invalid-intent";
    internal const string OriginalIntentRequiredCode = "projects.lifecycle-original-intent-required";
    internal const string AccessDeniedCode = "projects.lifecycle-access-denied";

    internal static void MapLifecycleOperations(this RouteGroupBuilder projects) {
        projects.MapGet("/lifecycle-write-context", GetContextAsync)
            .WithName("GetProjectLifecycleWriteContext")
            .WithSummary("Read the caller-bound original context for project deletion and hierarchy operations.")
            .Produces<ProjectLifecycleWriteContext>()
            .ProducesApiErrors(StatusCodes.Status403Forbidden);
        projects.MapPost("/lifecycle-operations", SubmitAsync)
            .WithName("SubmitProjectLifecycleOperation")
            .WithSummary("Submit one immutable conditional deletion or hierarchy operation.")
            .WithDescription("Retain the complete original request before dispatch. Delete requires the original project revision and lifetime; removing or reconnecting an edge requires its original ID and endpoint lifetimes. Exact replay returns the historical receipt. Accepted confirms the database change and separately identifies cleanup obligations; it does not confirm all cleanup finished. Unconfirmed requires original-address lookup or exact retry, never a fresh identity or refreshed expectations.")
            .Accepts<ProjectLifecycleApiSubmission>("application/json")
            .Produces<ProjectLifecycleApiResponse>()
            .Produces<ProjectLifecycleApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<ProjectLifecycleApiResponse>(StatusCodes.Status409Conflict)
            .Produces<ProjectLifecycleApiResponse>(StatusCodes.Status410Gone)
            .Produces<ProjectLifecycleApiResponse>(StatusCodes.Status503ServiceUnavailable)
            .ProducesApiErrors(StatusCodes.Status400BadRequest, StatusCodes.Status413PayloadTooLarge, StatusCodes.Status415UnsupportedMediaType)
            .AddOpenApiOperationTransformer(AddInvalidRequestResponseAsync)
            .AddOpenApiOperationTransformer(ProjectDefinitionOperationOpenApiSchemas.AddAccessDeniedResponseAsync);
        projects.MapGet("/lifecycle-operations/{namespaceId:guid}/{epoch}/{operationId:guid}", LookupAsync)
            .WithName("LookupProjectLifecycleOperation")
            .WithSummary("Observe an original lifecycle operation without executing it.")
            .WithDescription("Supply the original databaseProfileId and callerBinding. NotObserved is not proof of rollback. Expiry, unavailable evidence and a changed dataset never authorize replacement intent. Deletion receipts remain readable after the target was deleted, subject to current Projects read permission.")
            .Produces<ProjectLifecycleApiResponse>()
            .Produces<ProjectLifecycleApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<ProjectLifecycleApiResponse>(StatusCodes.Status409Conflict)
            .Produces<ProjectLifecycleApiResponse>(StatusCodes.Status410Gone)
            .Produces<ProjectLifecycleApiResponse>(StatusCodes.Status503ServiceUnavailable)
            .ProducesApiErrors(StatusCodes.Status400BadRequest)
            .AddOpenApiOperationTransformer(ProjectDefinitionOperationOpenApiSchemas.AddAccessDeniedResponseAsync);
    }

    private static async Task AddInvalidRequestResponseAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context,
        CancellationToken cancellationToken) {
        var response = operation.Responses!["400"].Content!["application/json"];
        response.Schema = new OpenApiSchema {
            AnyOf = [response.Schema!, await context.GetOrCreateSchemaAsync(typeof(ProjectLifecycleApiResponse.Refused), null, cancellationToken)]
        };
    }

    internal static Task<IResult> GetContextAsync(ProjectDefinitionHttpAccess access, ProjectLifecycleOperationProtocol protocol,
        CancellationToken cancellationToken) => GuardAsync(async () => {
            var invocation = await access.ResolveLifecycleReadAsync(cancellationToken);
            return Results.Ok(await protocol.GetWriteContextAsync(invocation.Caller, cancellationToken));
        });

    internal static Task<IResult> SubmitAsync(HttpRequest request, ProjectDefinitionHttpAccess access,
        ProjectLifecycleWriter writer, IOptions<JsonOptions> json, CancellationToken cancellationToken) => GuardAsync(async () => {
            var invocation = await access.ResolveLifecycleWriteAsync(cancellationToken);
            ProjectLifecycleSubmission original;
            try {
                var document = await ProjectDefinitionHttpBody.ReadLifecycleAsync(request, json.Value.SerializerOptions, cancellationToken);
                original = document.ToSubmission();
            } catch (ArgumentException) {
                return InvalidIntent();
            } catch (JsonException) {
                return InvalidIntent();
            } catch (BadHttpRequestException failure) {
                return Results.Json(new ApiErrorResponse([new(InvalidIntentCode,
                    "The lifecycle request must contain bounded JSON using the supported media type.", ErrorSeverity.Error)]), statusCode: failure.StatusCode);
            }
            return Result(await writer.SubmitAsync(original, invocation, cancellationToken));
        });

    internal static Task<IResult> LookupAsync(Guid namespaceId, string epoch, Guid operationId, Guid databaseProfileId,
        string callerBinding, ProjectDefinitionHttpAccess access, ProjectLifecycleReceiptReader receipts,
        CancellationToken cancellationToken) => GuardAsync(async () => {
            var invocation = await access.ResolveLifecycleReadAsync(cancellationToken);
            if (!ProjectOperationEpochHttpContract.TryParse(epoch, out var parsed)) {
                return ApiEndpointResults.BadRequest(ProjectOperationEpochHttpContract.Description, InvalidIntentCode);
            }
            ProjectLifecycleOperation operation;
            try {
                operation = new(databaseProfileId, new(namespaceId), parsed!, new(callerBinding), operationId);
            } catch (ArgumentException) {
                return InvalidIntent();
            }
            return Result(await receipts.LookupAsync(operation, invocation, cancellationToken));
        });

    internal static IResult OriginalIntentRequired()
        => Results.Json(new ApiErrorResponse([new(OriginalIntentRequiredCode,
            "This route requires replacement with an original lifecycle submission. Read the lifecycle context and preserve the original project or edge expectation before submitting.", ErrorSeverity.Error)]),
            statusCode: StatusCodes.Status428PreconditionRequired);

    internal static IResult Result(ProjectLifecycleOutcome outcome) {
        (ProjectLifecycleApiResponse Response, int Status) result = outcome switch {
            ProjectLifecycleOutcome.Accepted value => (new ProjectLifecycleApiResponse.Accepted(ProjectLifecycleApiReceipt.From(value.Result),
                value.Replayed, value.CompletionIssue, value.DiagnosticsUnavailable), StatusCodes.Status200OK),
            ProjectLifecycleOutcome.Refused value => (new ProjectLifecycleApiResponse.Refused(ProjectLifecycleApiSubmission.From(value.Original),
                value.Reason, value.DiagnosticsUnavailable), RefusalStatus(value.Reason)),
            ProjectLifecycleOutcome.Unconfirmed value => (new ProjectLifecycleApiResponse.Unconfirmed(ProjectLifecycleApiSubmission.From(value.Original),
                value.Reason, value.DiagnosticsUnavailable), StatusCodes.Status503ServiceUnavailable),
            _ => throw new InvalidOperationException("The lifecycle owner returned an unsupported outcome.")
        };
        return Results.Json(result.Response, statusCode: result.Status);
    }

    internal static IResult Result(ProjectLifecycleLookup lookup) {
        (ProjectLifecycleApiResponse Response, int Status) result = lookup switch {
            ProjectLifecycleLookup.Found value => (new ProjectLifecycleApiResponse.Found(ProjectLifecycleApiReceipt.From(value.Accepted)), StatusCodes.Status200OK),
            ProjectLifecycleLookup.NotObserved value => (new ProjectLifecycleApiResponse.NotObserved(value.Original, value.State), StatusCodes.Status200OK),
            ProjectLifecycleLookup.ClosedAbsent value => (new ProjectLifecycleApiResponse.ClosedAbsent(value.Original, value.ClosedAtUtc,
                value.ObservedAtUtc, value.ReplayUntilUtc), StatusCodes.Status200OK),
            ProjectLifecycleLookup.Expired value => (new ProjectLifecycleApiResponse.Expired(value.Original), StatusCodes.Status410Gone),
            ProjectLifecycleLookup.UnknownFutureEpoch value => (new ProjectLifecycleApiResponse.UnknownFutureEpoch(value.Original), StatusCodes.Status409Conflict),
            ProjectLifecycleLookup.ContextChanged value => (new ProjectLifecycleApiResponse.ContextChanged(value.Original, value.Change), StatusCodes.Status409Conflict),
            ProjectLifecycleLookup.Unavailable value => (new ProjectLifecycleApiResponse.Unavailable(value.Original, value.Reason),
                value.Reason == ProjectLifecycleUnavailableReason.AccessDenied ? StatusCodes.Status403Forbidden : StatusCodes.Status503ServiceUnavailable),
            _ => throw new InvalidOperationException("The lifecycle reader returned an unsupported observation.")
        };
        return Results.Json(result.Response, statusCode: result.Status);
    }

    private static int RefusalStatus(ProjectLifecycleRefusal reason) => reason switch {
        ProjectLifecycleRefusal.WriteDenied => StatusCodes.Status403Forbidden,
        ProjectLifecycleRefusal.Expired => StatusCodes.Status410Gone,
        ProjectLifecycleRefusal.UnsupportedInterpretation => StatusCodes.Status400BadRequest,
        ProjectLifecycleRefusal.AuthorityUnavailable => StatusCodes.Status503ServiceUnavailable,
        _ => StatusCodes.Status409Conflict
    };

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> action) {
        try {
            return await action();
        } catch (WebCurrentPrincipalDeniedException) {
            return Results.Json(new ApiErrorResponse([new(AccessDeniedCode, "Current Projects access is required.", ErrorSeverity.Error)]),
                statusCode: StatusCodes.Status403Forbidden);
        }
    }

    private static IResult InvalidIntent()
        => ApiEndpointResults.BadRequest("The lifecycle JSON or original operation address is malformed or incomplete.", InvalidIntentCode);
}
