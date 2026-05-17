using CanDoItAll.Modules.CognitiveMemory;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryFoundationTests
{
    [Fact]
    public void CognitiveMemoryHash_FromUtf8_IsDeterministicSha256()
    {
        var first = CognitiveMemoryHash.FromUtf8("source text");
        var second = CognitiveMemoryHash.FromUtf8("source text");

        Assert.Equal(CognitiveMemoryHashAlgorithm.Sha256, first.Algorithm);
        Assert.Equal(first, second);
        Assert.Equal(64, first.Value.Length);
        Assert.Equal(first.Value.ToLowerInvariant(), first.Value);
    }

    [Fact]
    public void CognitiveMemoryIds_RejectEmptyValues()
    {
        Assert.Throws<ArgumentException>(() => new CognitiveMemoryRecordId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new CognitiveMemorySourceManifestId(Guid.Empty));
        Assert.Throws<ArgumentException>(() => new CognitiveMemorySourceItemId(Guid.Empty));
    }

    [Fact]
    public void CognitiveMemoryVersionObjects_RejectBlankValues()
    {
        Assert.Throws<ArgumentException>(() => new CognitiveMemoryAlgorithmVersion(" "));
        Assert.Throws<ArgumentException>(() => new CognitiveMemoryPolicyProfileId(""));
        Assert.Throws<ArgumentException>(() => new CognitiveMemoryIdempotencyKey("\t"));
    }

    [Fact]
    public void RecordValidator_RejectsMachineGeneratedRecordWithoutEvidenceOrGeneratedReason()
    {
        var record = CreateValidRecord();
        record.Origin = CognitiveMemoryRecordOrigin.MachineGenerated;
        record.SourceEvidenceCount = 0;
        record.GeneratedReason = string.Empty;

        var result = new CognitiveMemoryRecordValidator().ValidateForPersistence(record);

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "cognitive-memory-generated-evidence-required");
    }

    [Fact]
    public void RecordValidator_AllowsMachineGeneratedRecordWithExplicitGeneratedReason()
    {
        var record = CreateValidRecord();
        record.Origin = CognitiveMemoryRecordOrigin.MachineGenerated;
        record.SourceEvidenceCount = 0;
        record.GeneratedReason = "Generated from a review-approved synthesis task.";

        var result = new CognitiveMemoryRecordValidator().ValidateForPersistence(record);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DefaultAccessPolicy_DeniesRestrictedRecordsWithoutRestrictedPolicyGrant()
    {
        var projectId = Guid.NewGuid();
        var allowed = CreateValidRecord(projectId);
        var restricted = CreateValidRecord(projectId);
        restricted.AccessLevel = CognitiveMemoryAccessLevel.Restricted;

        var request = new CognitiveMemoryAccessRequest(
            CognitiveMemoryOperationMode.Recall,
            new CognitiveMemoryPolicyContext(
                projectId,
                "agent:test",
                CognitiveMemoryAccessLevel.Project,
                new CognitiveMemoryPolicyProfileId("default"),
                CognitiveMemoryRiskLevel.Low,
                AllowRestrictedContent: false),
            [allowed, restricted]);

        var decision = await new CognitiveMemoryDefaultAccessPolicy().EvaluateAsync(request);

        Assert.Contains(decision.AllowedRecordIds, id => id.Value == allowed.Id);
        Assert.Contains(decision.Denials, denial => denial.RecordId.Value == restricted.Id);
    }

    private static CognitiveMemoryRecord CreateValidRecord(Guid? projectId = null)
        => new()
        {
            ProjectId = projectId,
            Title = "Container build policy",
            CanonicalText = "Docker production, test, local, and CI contexts are distinct.",
            SummaryText = "Docker contexts are separate.",
            Origin = CognitiveMemoryRecordOrigin.SourceDerived,
            Kind = CognitiveMemoryRecordKind.Semantic,
            ValidationState = CognitiveMemoryValidationState.Approved,
            StabilityState = CognitiveMemoryStabilityState.Active,
            CreatedInMode = CognitiveMemoryOperationMode.Observe,
            AlgorithmVersion = "foundation-v1",
            ContentHash = CognitiveMemoryHash.FromUtf8("Docker contexts are separate.").Value,
            SourceEvidenceCount = 1,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedAtUtc = DateTimeOffset.UtcNow
        };
}
