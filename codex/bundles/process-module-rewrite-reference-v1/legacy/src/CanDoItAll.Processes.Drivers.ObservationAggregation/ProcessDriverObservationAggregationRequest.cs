using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.ObservationAggregation;

public sealed record ProcessDriverObservationAggregationRequest(
    IReadOnlyList<ProcessDriverVerificationResponse> Responses,
    DateTimeOffset RequestedAt,
    string CallerContext);
