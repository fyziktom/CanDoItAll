using CanDoItAll.Modules.Processes;

namespace CanDoItAll.Tests.Integration;

public sealed class ProcessRuntimeReadQueryServiceTests
{
    [Fact]
    public void ResolveBestArtifactForExpectation_prefers_concrete_agent_artifact_over_browser_projection()
    {
        var expectationId = Guid.NewGuid();
        var expectation = new ProcessArtifactExpectation
        {
            Id = expectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Implementation summary",
            TrustRequirement = ProcessArtifactTrustRequirement.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal
        };
        var concreteSummary = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = expectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "Implementation summary",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ManagedStoragePath = "external-target/C/programovani/demo/implementation-summary.md",
            ExternalReferenceKey = "agentframework-artifact:summary-001",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(-5)
        };
        var browserSnapshot = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = expectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "browser-snapshot.md",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ManagedStoragePath = "artifacts/scopes/organization/demo/process-runs/run/browser-snapshot.md",
            ExternalReferenceKey = "agentframework-browser-artifact:run:browser-snapshot",
            CreatedAtUtc = DateTimeOffset.UtcNow
        };
        var unrelatedAgentArtifact = new ProcessArtifactRecord
        {
            Id = Guid.NewGuid(),
            ArtifactExpectationId = expectationId,
            ArtifactKind = ProcessArtifactKind.Evidence,
            Title = "favicon.svg",
            TrustStatus = ProcessArtifactTrustStatus.ReviewRequired,
            SensitivityLevel = ProcessSensitivityLevel.Internal,
            ManagedStoragePath = "external-target/C/programovani/demo/favicon.svg",
            ExternalReferenceKey = "agentframework-artifact:favicon-001",
            CreatedAtUtc = DateTimeOffset.UtcNow.AddSeconds(5)
        };

        var resolved = ProcessRuntimeReadQueryService.ResolveBestArtifactForExpectation(
            expectation,
            [browserSnapshot, unrelatedAgentArtifact, concreteSummary]);

        Assert.Same(concreteSummary, resolved);
    }
}
