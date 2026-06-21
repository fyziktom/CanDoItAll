using CanDoItAll.Processes.Drivers.Abstractions.Audit;
using CanDoItAll.Processes.Drivers.Abstractions.Evidence;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Processes.Drivers.ObservationAggregation;

public sealed class ProcessDriverObservationAggregator
{
    public ProcessDriverObservationAggregate Aggregate(ProcessDriverObservationAggregationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.Responses.Count == 0)
        {
            throw new ArgumentException("At least one verification response is required.", nameof(request));
        }

        var responseObservations = request.Responses
            .Select(response => new ResponseObservation(response, ResolveLane(response)))
            .ToArray();
        var diagnosticSummary = string.Join(
            " ",
            responseObservations.SelectMany(observation =>
                observation.Response.Diagnostics.Select(diagnostic => diagnostic.Message)));
        var redaction = CreateAggregateRedaction(request, responseObservations, diagnosticSummary);

        return new ProcessDriverObservationAggregate(
            request.RequestedAt,
            request.CallerContext,
            request.Responses.Count,
            request.Responses.Count(response => response.Accepted),
            request.Responses.Count(response => !response.Accepted),
            request.Responses.Sum(response => response.Diagnostics.Count),
            request.Responses.Sum(response => response.Diagnostics.Count(diagnostic =>
                diagnostic.Severity == ProcessDriverDiagnosticSeverity.Error)),
            request.Responses.Sum(response => response.Diagnostics.Count(diagnostic =>
                diagnostic.Severity == ProcessDriverDiagnosticSeverity.Warning)),
            AggregationMutationFree: true,
            request.Responses.All(response => response.NoMutationPerformed),
            CreateLaneSummaries(responseObservations),
            CreateEvidenceReferences(request.Responses),
            redaction,
            ProcessDriverContractVersion.Current);
    }

    private static ProcessDriverCapabilityScopeKind ResolveLane(ProcessDriverVerificationResponse response)
    {
        ArgumentNullException.ThrowIfNull(response);

        var lanes = response.AuditFacts
            .Select(fact => fact.Lane)
            .Distinct()
            .ToArray();
        return lanes.Length switch
        {
            1 => lanes[0],
            0 => throw new ArgumentException("Every verification response must include an audit lane."),
            _ => throw new ArgumentException("A verification response cannot mix audit lanes.")
        };
    }

    private static IReadOnlyList<ProcessDriverObservationLaneSummary> CreateLaneSummaries(
        IReadOnlyList<ResponseObservation> responseObservations)
    {
        return CreateReadonlyList(responseObservations
            .GroupBy(observation => observation.Lane)
            .OrderBy(group => group.Key)
            .Select(group =>
            {
                var responses = group.Select(observation => observation.Response).ToArray();
                return new ProcessDriverObservationLaneSummary(
                    group.Key,
                    responses.Length,
                    responses.Count(response => response.Accepted),
                    responses.Count(response => !response.Accepted),
                    responses.Sum(response => response.Diagnostics.Count),
                    responses.Sum(response => response.Diagnostics.Count(diagnostic =>
                        diagnostic.Severity == ProcessDriverDiagnosticSeverity.Error)),
                    responses.Sum(response => response.Diagnostics.Count(diagnostic =>
                        diagnostic.Severity == ProcessDriverDiagnosticSeverity.Warning)),
                    responses.Count(response => response.Redaction.Status == ProcessDriverRedactionStatus.Redacted),
                    responses.All(response => response.NoMutationPerformed),
                    CreateReadonlyList(responses
                        .SelectMany(response => response.Diagnostics.Select(diagnostic => diagnostic.Category))
                        .Distinct()
                        .OrderBy(category => category)));
            })
            .ToArray());
    }

    private static IReadOnlyList<ProcessDriverEvidenceReference> CreateEvidenceReferences(
        IReadOnlyList<ProcessDriverVerificationResponse> responses)
    {
        return CreateReadonlyList(ProcessDriverEvidencePolicy.NormalizeEvidenceReferences(
            responses
                .SelectMany(response => response.EvidenceReferences
                    .Concat(response.AuditFacts.SelectMany(fact => fact.EvidenceReferences)))
                .ToArray()));
    }

    private static ProcessDriverRedactionDescriptor CreateAggregateRedaction(
        ProcessDriverObservationAggregationRequest request,
        IReadOnlyList<ResponseObservation> responseObservations,
        string diagnosticSummary)
    {
        var appliedKinds = responseObservations
            .SelectMany(observation => observation.Response.Redaction.AppliedKinds)
            .Distinct()
            .OrderBy(kind => kind)
            .ToArray();
        var redactedSummary = ProcessDriverRedactionPolicy.RedactDiagnosticSummary(
            string.Join(" ", request.CallerContext, diagnosticSummary));
        var status = appliedKinds.Length > 0 ||
            responseObservations.Any(observation => observation.Response.Redaction.Status == ProcessDriverRedactionStatus.Redacted) ||
            redactedSummary.Descriptor.Status == ProcessDriverRedactionStatus.Redacted
                ? ProcessDriverRedactionStatus.Redacted
                : ProcessDriverRedactionStatus.None;
        var aggregateKinds = CreateReadonlyList(appliedKinds
            .Concat(redactedSummary.Descriptor.AppliedKinds)
            .Distinct()
            .OrderBy(kind => kind));

        return new ProcessDriverRedactionDescriptor(
            status,
            aggregateKinds,
            ProcessDriverEvidencePolicy.ComputeSha256(redactedSummary.RedactedText));
    }

    private static IReadOnlyList<T> CreateReadonlyList<T>(IEnumerable<T> items)
    {
        return Array.AsReadOnly(items.ToArray());
    }

    private sealed record ResponseObservation(
        ProcessDriverVerificationResponse Response,
        ProcessDriverCapabilityScopeKind Lane);
}
