using System.Text.Json.Serialization;
using CanDoItAll.Modules.Projects;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Web.Api;

internal sealed record ProjectDefinitionReadApiResponse(ProjectDefinitionExpectation Expectation,
    ProjectDefinitionScalars Scalars, ProjectDefinitionExternalIdentity? ExternalIdentity,
    IReadOnlyList<ProjectDefinitionPhase> Phases, IReadOnlyList<ProjectDefinitionOption> Options,
    ProjectDefinitionWriteContext WriteContext);

internal sealed record ProjectDefinitionAcceptedApiResponse<T>(ProjectDefinitionAccepted<T> Receipt, bool Replayed) where T : class {
    public string Status => "accepted";
}

internal sealed record ProjectDefinitionRejectedApiResponse(ProjectDefinitionOperation? Operation, IReadOnlyList<Error> Errors) {
    public string Status => "rejected";
}

internal sealed record ProjectDefinitionUnconfirmedApiResponse(ProjectDefinitionOperation Operation, ProjectDefinitionRecoveryReason Reason) {
    public string Status => "unconfirmed";
}

[JsonPolymorphic(TypeDiscriminatorPropertyName = "status")]
[JsonDerivedType(typeof(ProjectDefinitionLookupApiResponse.FoundFull), "foundFull")]
[JsonDerivedType(typeof(ProjectDefinitionLookupApiResponse.FoundScalars), "foundScalars")]
[JsonDerivedType(typeof(ProjectDefinitionLookupApiResponse.NotObserved), "notObserved")]
[JsonDerivedType(typeof(ProjectDefinitionLookupApiResponse.ClosedAbsent), "closedAbsent")]
[JsonDerivedType(typeof(ProjectDefinitionLookupApiResponse.Expired), "expired")]
[JsonDerivedType(typeof(ProjectDefinitionLookupApiResponse.DatasetChanged), "datasetChanged")]
[JsonDerivedType(typeof(ProjectDefinitionLookupApiResponse.CallerContextChanged), "callerContextChanged")]
[JsonDerivedType(typeof(ProjectDefinitionLookupApiResponse.InvalidContext), "invalidContext")]
[JsonDerivedType(typeof(ProjectDefinitionLookupApiResponse.DetailUnavailable), "detailUnavailable")]
internal abstract record ProjectDefinitionLookupApiResponse {
    private ProjectDefinitionLookupApiResponse() {
    }

    public sealed record FoundFull(ProjectDefinitionAccepted<ProjectEditorAcknowledgement> Receipt) : ProjectDefinitionLookupApiResponse;
    public sealed record FoundScalars(ProjectDefinitionAccepted<ProjectDefinitionScalarsAcknowledgement> Receipt) : ProjectDefinitionLookupApiResponse;
    public sealed record NotObserved(ProjectDefinitionOperation Operation, ProjectOperationObservationState State) : ProjectDefinitionLookupApiResponse;
    public sealed record ClosedAbsent(ProjectDefinitionOperation Operation, DateTimeOffset ClosedAtUtc,
        DateTimeOffset ObservedAtUtc, DateTimeOffset RetainedUntilUtc) : ProjectDefinitionLookupApiResponse;
    public sealed record Expired(ProjectDefinitionOperation Operation) : ProjectDefinitionLookupApiResponse;
    public sealed record DatasetChanged(ProjectDefinitionOperation Operation) : ProjectDefinitionLookupApiResponse;
    public sealed record CallerContextChanged(ProjectDefinitionOperation Operation) : ProjectDefinitionLookupApiResponse;
    public sealed record InvalidContext(ProjectDefinitionOperation Operation, ProjectDefinitionInvalidContextReason Reason) : ProjectDefinitionLookupApiResponse;
    public sealed record DetailUnavailable(ProjectDefinitionOperation Operation, ProjectDefinitionDetailUnavailableReason Reason) : ProjectDefinitionLookupApiResponse;
}
