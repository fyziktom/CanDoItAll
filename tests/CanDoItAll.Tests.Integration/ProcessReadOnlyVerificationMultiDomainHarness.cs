using CanDoItAll.Modules.Processes;
using CanDoItAll.Processes.Drivers.Abstractions.Permissions;
using CanDoItAll.Processes.Drivers.Abstractions.Verification;

namespace CanDoItAll.Tests.Integration;

internal sealed class ProcessReadOnlyVerificationMultiDomainHarness
{
    private static readonly IReadOnlyList<ProcessReadOnlyVerificationLaneProducerConsumer> CurrentLaneMatrix =
    [
        new(
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification,
            typeof(ProcessTranscriptVerificationReadOnlyAdapter),
            typeof(ProcessReadOnlyVerificationBatchOrchestrator)),
        new(
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead,
            typeof(ProcessRuntimeEvidenceVerificationReadOnlyAdapter),
            typeof(ProcessReadOnlyVerificationBatchOrchestrator)),
        new(
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead,
            typeof(ProcessArtifactEvidenceReadOnlyAdapter),
            typeof(ProcessReadOnlyVerificationBatchOrchestrator)),
        new(
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead,
            typeof(ProcessOfficeEvidenceReadOnlyAdapter),
            typeof(ProcessReadOnlyVerificationBatchOrchestrator)),
        new(
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead,
            typeof(ProcessBusinessAnalysisReadOnlyAdapter),
            typeof(ProcessReadOnlyVerificationBatchOrchestrator))
    ];

    private readonly ProcessReadOnlyVerificationBatchOrchestrator orchestrator = new();

    public ProcessReadOnlyVerificationBatchObservation Verify(ProcessReadOnlyVerificationBatchPayload payload)
    {
        return orchestrator.Verify(payload);
    }

    public IReadOnlyList<ProcessReadOnlyVerificationLaneProducerConsumer> AssertCurrentLaneProducerConsumerProof(
        ProcessReadOnlyVerificationBatchObservation observation)
    {
        var aggregate = Assert.IsType<ProcessReadOnlyVerificationAggregateObservation>(
            observation.AggregateObservation);
        var expectedLanes = CurrentLaneMatrix
            .Select(matrix => matrix.Lane)
            .OrderBy(static lane => lane)
            .ToArray();
        var actualLanes = aggregate.LaneSummaries
            .Select(summary => summary.Lane)
            .OrderBy(static lane => lane)
            .ToArray();

        Assert.Equal(expectedLanes, actualLanes);
        Assert.Equal(CurrentLaneMatrix.Count, observation.ResponseCount);
        Assert.Equal(CurrentLaneMatrix.Count, aggregate.ResponseCount);
        Assert.Equal(CurrentLaneMatrix.Count, aggregate.AcceptedCount);
        Assert.Equal(0, aggregate.DeniedCount);
        Assert.True(aggregate.AggregationMutationFree);
        Assert.True(aggregate.AllResponsesMutationFree);
        Assert.All(observation.Responses, AssertReadonlyResponse);

        foreach (var matrix in CurrentLaneMatrix)
        {
            var summary = Assert.Single(
                aggregate.LaneSummaries,
                laneSummary => laneSummary.Lane == matrix.Lane);

            Assert.Equal(typeof(ProcessReadOnlyVerificationBatchOrchestrator), matrix.ConsumerType);
            Assert.Equal(1, CountLaneObservations(observation, matrix.Lane));
            Assert.Equal(1, summary.ResponseCount);
            Assert.Equal(1, summary.AcceptedCount);
            Assert.Equal(0, summary.DeniedCount);
            Assert.True(summary.AllResponsesMutationFree);
        }

        return CurrentLaneMatrix;
    }

    private static void AssertReadonlyResponse(ProcessDriverVerificationResponse response)
    {
        Assert.True(response.Accepted);
        Assert.True(response.NoMutationPerformed);
        Assert.Equal(ProcessDriverDenialReason.None, response.DenialReason);
        Assert.Equal(ProcessDriverContractVersion.Current, response.ContractVersion);
        Assert.All(response.AuditFacts, fact =>
        {
            Assert.False(fact.Scope.AllowsExternalCalls);
            Assert.False(fact.Scope.AllowsProcessMutation);
            Assert.False(fact.Scope.AllowsStorageWrites);
            Assert.False(fact.Scope.AllowsWorkspaceWrites);
        });
    }

    private static int CountLaneObservations(
        ProcessReadOnlyVerificationBatchObservation observation,
        ProcessDriverCapabilityScopeKind lane)
    {
        return lane switch
        {
            ProcessDriverCapabilityScopeKind.DotNetRustTranscriptVerification => observation.TranscriptObservations.Count,
            ProcessDriverCapabilityScopeKind.RuntimeFactsRead => observation.RuntimeEvidenceObservations.Count,
            ProcessDriverCapabilityScopeKind.ArtifactEvidenceRead => observation.ArtifactEvidenceObservations.Count,
            ProcessDriverCapabilityScopeKind.OfficeEvidenceRead => observation.OfficeEvidenceObservations.Count,
            ProcessDriverCapabilityScopeKind.BusinessAnalysisRead => observation.BusinessAnalysisObservations.Count,
            _ => 0
        };
    }
}

internal sealed record ProcessReadOnlyVerificationLaneProducerConsumer(
    ProcessDriverCapabilityScopeKind Lane,
    Type ProducerType,
    Type ConsumerType);
