using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Api;

internal static class ProjectDefinitionApi {
    internal static void MapDefinitionOperations(this RouteGroupBuilder projects) {
        projects.MapGet("/definition-write-context", GetWriteContextAsync)
            .WithName("GetProjectDefinitionWriteContext")
            .WithSummary("Read current caller-bound definition operation context and enforced limits.")
            .Produces<ProjectDefinitionWriteContext>()
            .ProducesApiErrors(StatusCodes.Status403Forbidden);
        DescribeWrite<CreateProjectDefinitionCommand, ProjectEditorAcknowledgement>(projects.MapPost("/definitions", CreateAsync)
            .WithName("CreateProjectDefinition"))
            .Produces<ProjectDefinitionAcceptedApiResponse<ProjectEditorAcknowledgement>>(StatusCodes.Status201Created);
        DescribeWrite<ReplaceProjectDefinitionCommand, ProjectEditorAcknowledgement>(projects.MapPut("/{projectId:guid}/definition", ReplaceAsync)
            .WithName("ReplaceProjectDefinition"));
        DescribeWrite<UpdateProjectDefinitionScalarsCommand, ProjectDefinitionScalarsAcknowledgement>(projects.MapPut("/{projectId:guid}/definition/scalars", UpdateScalarsAsync)
            .WithName("UpdateProjectDefinitionScalars"));
        projects.MapGet("/definition-operations/{namespaceId:guid}/{epoch}/{operationId:guid}", LookupAsync)
            .WithName(ProjectOperationEpochHttpContract.LookupEndpointName)
            .WithSummary("Resolve an original operation under current read permission without executing it.")
            .WithDescription("Requires the original callerBinding query value. NotObserved does not prove rollback. Only an exact accepted receipt or a fully retained ClosedAbsent observation resolves prior uncertainty. Expiry, access denial and dataset changes do not authorize a replacement identity.")
            .Produces<ProjectDefinitionLookupApiResponse>()
            .Produces<ProjectDefinitionLookupApiResponse>(StatusCodes.Status403Forbidden)
            .Produces<ProjectDefinitionLookupApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ProjectDefinitionLookupApiResponse>(StatusCodes.Status409Conflict)
            .Produces<ProjectDefinitionLookupApiResponse>(StatusCodes.Status410Gone)
            .ProducesApiErrors(StatusCodes.Status400BadRequest);
    }

    private static RouteHandlerBuilder DescribeWrite<TCommand, TAcknowledgement>(RouteHandlerBuilder endpoint)
        where TCommand : class where TAcknowledgement : class
        => endpoint.Accepts<ProjectDefinitionRequest<TCommand>>("application/json")
            .WithSummary("Submit one frozen, caller-bound project definition operation.")
            .WithDescription("Requires current Projects read and write permission. Retain the full operation address and original command before dispatch. Exact retries return the historical receipt. A different payload under the same identity rejects. Unconfirmed (503), timeout or disconnect requires lookup or exact retry; never refresh only the operation identity. Scalar writes preserve all children. Replacement requires explicit complete phase and option arrays.")
            .Produces<ProjectDefinitionAcceptedApiResponse<TAcknowledgement>>()
            .Produces<ProjectDefinitionRejectedApiResponse>(StatusCodes.Status400BadRequest)
            .Produces<ProjectDefinitionRejectedApiResponse>(StatusCodes.Status404NotFound)
            .Produces<ProjectDefinitionRejectedApiResponse>(StatusCodes.Status409Conflict)
            .Produces<ProjectDefinitionRejectedApiResponse>(StatusCodes.Status410Gone)
            .Produces<ProjectDefinitionRejectedApiResponse>(StatusCodes.Status413PayloadTooLarge)
            .Produces<ProjectDefinitionUnconfirmedApiResponse>(StatusCodes.Status503ServiceUnavailable)
            .ProducesApiErrors(StatusCodes.Status403Forbidden, StatusCodes.Status415UnsupportedMediaType);

    internal static Task<IResult> GetWriteContextAsync(ProjectDefinitionHttpAccess access, ProjectDefinitionOperationProtocol protocol,
        CancellationToken cancellationToken) => GuardAsync(async () => {
            var invocation = await access.ResolveAsync(writing: false, cancellationToken);
            return Results.Ok(await protocol.GetWriteContextAsync(invocation.Caller, cancellationToken));
        });

    internal static Task<IResult> ReadAsync(Guid projectId, ProjectDefinitionHttpAccess access, ProjectDefinitionOperationProtocol protocol,
        ProjectsService projects, CancellationToken cancellationToken) => GuardAsync(async () => {
            var invocation = await access.ResolveAsync(writing: false, cancellationToken);
            var before = await protocol.GetWriteContextAsync(invocation.Caller, cancellationToken);
            var definition = await projects.GetDefinitionAsync(projectId, cancellationToken);
            if (definition.IsFailure) {
                return ApiEndpointResults.FromResult(definition, ProjectErrorCodes.NotFound);
            }
            var after = await protocol.GetWriteContextAsync(invocation.Caller, cancellationToken);
            if (before.Namespace != after.Namespace || before.CallerBinding != after.CallerBinding) {
                return ApiEndpointResults.Conflict("The dataset changed while reading the definition. Read it again before editing.", ProjectErrorCodes.DefinitionDatasetChanged);
            }
            var snapshot = definition.Value!;
            return Results.Ok(new ProjectDefinitionReadApiResponse(snapshot.Expectation, snapshot.Scalars, snapshot.ExternalIdentity,
                snapshot.Phases, snapshot.Options, after));
        });

    internal static Task<IResult> CreateAsync(HttpRequest request, ProjectDefinitionHttpAccess access, ProjectDefinitionWriter writer,
        IOptions<JsonOptions> json, CancellationToken cancellationToken) => GuardAsync(async () => {
            var invocation = await access.ResolveAsync(writing: true, cancellationToken);
            var command = await ProjectDefinitionHttpBody.ReadAsync<CreateProjectDefinitionCommand>(request, json.Value.SerializerOptions, cancellationToken);
            var outcome = await writer.CreateAsync(command, invocation, cancellationToken);
            if (outcome is ProjectDefinitionOutcome<ProjectEditorAcknowledgement>.Accepted { Replayed: false } accepted) {
                return Results.Created($"/api/projects/{accepted.Result.Acknowledgement.Project.ProjectId:D}/definition",
                    new ProjectDefinitionAcceptedApiResponse<ProjectEditorAcknowledgement>(accepted.Result, false));
            }
            return Result(outcome);
        });

    internal static Task<IResult> ReplaceAsync(Guid projectId, HttpRequest request, ProjectDefinitionHttpAccess access,
        ProjectDefinitionWriter writer, IOptions<JsonOptions> json, CancellationToken cancellationToken) => GuardAsync(async () => {
            var invocation = await access.ResolveAsync(writing: true, cancellationToken);
            var command = await ProjectDefinitionHttpBody.ReadAsync<ReplaceProjectDefinitionCommand>(request, json.Value.SerializerOptions, cancellationToken);
            if (command.Command.Expected.Project.ProjectId != projectId) {
                return InvalidTarget(command.Operation);
            }
            return Result(await writer.ReplaceAsync(command, invocation, cancellationToken));
        });

    internal static Task<IResult> UpdateScalarsAsync(Guid projectId, HttpRequest request, ProjectDefinitionHttpAccess access,
        ProjectDefinitionWriter writer, IOptions<JsonOptions> json, CancellationToken cancellationToken) => GuardAsync(async () => {
            var invocation = await access.ResolveAsync(writing: true, cancellationToken);
            var command = await ProjectDefinitionHttpBody.ReadAsync<UpdateProjectDefinitionScalarsCommand>(request, json.Value.SerializerOptions, cancellationToken);
            if (command.Command.Expected.Project.ProjectId != projectId) {
                return InvalidTarget(command.Operation);
            }
            return Result(await writer.UpdateScalarsAsync(command, invocation, cancellationToken));
        });

    internal static Task<IResult> LookupAsync(Guid namespaceId, string epoch, Guid operationId, string callerBinding,
        ProjectDefinitionHttpAccess access, ProjectDefinitionReceiptReader receipts, CancellationToken cancellationToken)
        => GuardAsync(async () => {
            var invocation = await access.ResolveAsync(writing: false, cancellationToken);
            ProjectDefinitionOperation operation;
            try {
                if (!ProjectOperationEpochHttpContract.TryParse(epoch, out var parsedEpoch)) {
                    return ApiEndpointResults.BadRequest(ProjectOperationEpochHttpContract.Description, ProjectErrorCodes.DefinitionOperationUnknown);
                }
                operation = new(new(namespaceId), parsedEpoch!, operationId, new(callerBinding));
            } catch (ArgumentException) {
                return ApiEndpointResults.BadRequest("Lookup requires the original namespace, positive epoch, nonempty operation ID and bounded caller binding.",
                    ProjectErrorCodes.DefinitionOperationUnknown);
            }
            var observed = await receipts.LookupAsync(operation, invocation, cancellationToken);
            (ProjectDefinitionLookupApiResponse Response, int Status) result = observed switch {
                ProjectDefinitionLookup.FoundFull value => (new ProjectDefinitionLookupApiResponse.FoundFull(value.Result), StatusCodes.Status200OK),
                ProjectDefinitionLookup.FoundScalars value => (new ProjectDefinitionLookupApiResponse.FoundScalars(value.Result), StatusCodes.Status200OK),
                ProjectDefinitionLookup.NotObserved value => (new ProjectDefinitionLookupApiResponse.NotObserved(value.Operation, value.State), StatusCodes.Status200OK),
                ProjectDefinitionLookup.ClosedAbsent value => (new ProjectDefinitionLookupApiResponse.ClosedAbsent(value.Operation, value.ClosedAtUtc,
                    value.ObservedAtUtc, value.RetainedUntilUtc), StatusCodes.Status200OK),
                ProjectDefinitionLookup.Expired value => (new ProjectDefinitionLookupApiResponse.Expired(value.Operation), StatusCodes.Status410Gone),
                ProjectDefinitionLookup.DatasetChanged value => (new ProjectDefinitionLookupApiResponse.DatasetChanged(value.Operation), StatusCodes.Status409Conflict),
                ProjectDefinitionLookup.CallerContextChanged value => (new ProjectDefinitionLookupApiResponse.CallerContextChanged(value.Operation), StatusCodes.Status409Conflict),
                ProjectDefinitionLookup.InvalidContext value => (new ProjectDefinitionLookupApiResponse.InvalidContext(value.Operation, value.Reason), StatusCodes.Status409Conflict),
                ProjectDefinitionLookup.DetailUnavailable value => (new ProjectDefinitionLookupApiResponse.DetailUnavailable(value.Operation, value.Reason),
                    value.Reason == ProjectDefinitionDetailUnavailableReason.AccessDenied ? StatusCodes.Status403Forbidden :
                    value.Reason == ProjectDefinitionDetailUnavailableReason.TargetUnavailable ? StatusCodes.Status404NotFound : StatusCodes.Status409Conflict),
                _ => throw new InvalidOperationException("The owner returned an unsupported lookup case.")
            };
            return Results.Json(result.Response, statusCode: result.Status);
        });

    private static IResult Result<T>(ProjectDefinitionOutcome<T> outcome) where T : class => outcome switch {
        ProjectDefinitionOutcome<T>.Accepted value => Results.Ok(new ProjectDefinitionAcceptedApiResponse<T>(value.Result, value.Replayed)),
        ProjectDefinitionOutcome<T>.Rejected value => Results.Json(new ProjectDefinitionRejectedApiResponse(value.Operation, value.Errors),
            statusCode: RejectionStatus(value.Errors)),
        ProjectDefinitionOutcome<T>.Unconfirmed value => Results.Json(new ProjectDefinitionUnconfirmedApiResponse(value.Operation, value.Reason),
            statusCode: StatusCodes.Status503ServiceUnavailable),
        _ => throw new InvalidOperationException("The owner returned an unsupported definition outcome.")
    };

    private static int RejectionStatus(IReadOnlyList<Error> errors) => errors[0].Code switch {
        ProjectErrorCodes.DefinitionLimitExceeded => StatusCodes.Status413PayloadTooLarge,
        ProjectErrorCodes.NotFound => StatusCodes.Status404NotFound,
        ProjectErrorCodes.DefinitionOperationExpired => StatusCodes.Status410Gone,
        ProjectErrorCodes.DefinitionReceiptUnavailable => StatusCodes.Status403Forbidden,
        ProjectErrorCodes.DefinitionConflict or ProjectErrorCodes.DefinitionTransactionConflict or ProjectErrorCodes.LifetimeChanged or
            ProjectErrorCodes.ExternalIdentityConflict or ProjectErrorCodes.ExternalIdentityImmutable or ProjectErrorCodes.DefinitionOperationMismatch or
            ProjectErrorCodes.DefinitionOperationClosed or ProjectErrorCodes.DefinitionDatasetChanged or ProjectErrorCodes.DefinitionCallerChanged or
            ProjectErrorCodes.DefinitionOperationUnknown => StatusCodes.Status409Conflict,
        _ => StatusCodes.Status400BadRequest
    };

    private static IResult InvalidTarget(ProjectDefinitionOperation operation)
        => Results.BadRequest(new ProjectDefinitionRejectedApiResponse(operation,
            [Error.Validation("The route target must equal the original definition expectation.", ProjectErrorCodes.DefinitionExpectationInvalid)]));

    private static async Task<IResult> GuardAsync(Func<Task<IResult>> action) {
        try {
            return await action();
        } catch (WebCurrentPrincipalDeniedException) {
            return Results.Json(new ApiErrorResponse([new(ProjectErrorCodes.DefinitionReceiptUnavailable,
                "Current Projects access is required.", ErrorSeverity.Error)]),
                statusCode: StatusCodes.Status403Forbidden);
        } catch (JsonException) {
            return Results.BadRequest(new ProjectDefinitionRejectedApiResponse(null,
                [Error.Validation("The protected definition JSON is malformed or incomplete.", ProjectErrorCodes.DefinitionExpectationInvalid)]));
        } catch (BadHttpRequestException exception) {
            return Results.Json(new ProjectDefinitionRejectedApiResponse(null,
                [Error.Validation(exception.Message, exception.StatusCode == StatusCodes.Status413PayloadTooLarge
                    ? ProjectErrorCodes.DefinitionLimitExceeded : ProjectErrorCodes.DefinitionExpectationInvalid)]), statusCode: exception.StatusCode);
        }
    }
}
