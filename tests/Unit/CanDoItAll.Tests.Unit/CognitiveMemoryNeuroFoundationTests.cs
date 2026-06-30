using CanDoItAll.Modules.CognitiveMemory;

namespace CanDoItAll.Tests.Unit;

public sealed class CognitiveMemoryNeuroFoundationTests
{
    [Theory]
    [InlineData(CognitiveMemoryMutationCommandKind.ProposeClaim)]
    [InlineData(CognitiveMemoryMutationCommandKind.SupportClaim)]
    [InlineData(CognitiveMemoryMutationCommandKind.AttackClaim)]
    [InlineData(CognitiveMemoryMutationCommandKind.ValidateClaim)]
    [InlineData(CognitiveMemoryMutationCommandKind.RecordEvidence)]
    public void ClaimMutationCommands_RequireEvidenceAnchors(CognitiveMemoryMutationCommandKind commandKind)
    {
        Assert.True(CognitiveMemoryNeuroFoundationPolicies.RequiresEvidenceAnchors(commandKind));
    }

    [Fact]
    public void ClaimRecords_PersistBeliefScoreTraceInsteadOfSupportMinusAttackTotals()
    {
        var properties = typeof(CognitiveMemoryClaimRecord).GetProperties()
            .Select(property => property.Name)
            .ToList();

        Assert.Contains(nameof(CognitiveMemoryClaimRecord.CurrentBeliefScoreEvaluationTraceId), properties);
        Assert.Contains(nameof(CognitiveMemoryClaimRecord.CurrentBeliefBucket), properties);
        Assert.DoesNotContain("SupportTotal", properties);
        Assert.DoesNotContain("AttackTotal", properties);
        Assert.DoesNotContain("SupportMinusAttack", properties);
    }

    [Fact]
    public void ContextBoundary_CanRepresentRelatedButNotSubstitutableDockerContexts()
    {
        var boundary = new CognitiveMemoryContextBoundaryRecord
        {
            ProjectId = Guid.NewGuid(),
            SourceContextFrameId = Guid.NewGuid(),
            TargetContextFrameId = Guid.NewGuid(),
            BoundaryKind = CognitiveMemoryContextBoundaryKind.EnvironmentBoundary,
            BoundaryPolicy = CognitiveMemoryContextBoundaryPolicy.RelatedNotSubstitutable,
            Explanation = "Docker local/test context is related to production deployment but must not substitute for it."
        };

        Assert.Equal(CognitiveMemoryContextBoundaryPolicy.RelatedNotSubstitutable, boundary.BoundaryPolicy);
        Assert.Contains("must not substitute", boundary.Explanation, StringComparison.Ordinal);
    }

    [Fact]
    public void NeuroFoundationContracts_DoNotExposeDirectAuthoritativeUpsertSurface()
    {
        var offenders = typeof(ICognitiveMemoryMutationAuthority).Assembly.GetTypes()
            .Where(type => string.Equals(type.Namespace, typeof(ICognitiveMemoryMutationAuthority).Namespace, StringComparison.Ordinal))
            .SelectMany(type => type.GetMethods().Select(method => new { Type = type, Method = method }))
            .Where(item =>
                item.Method.Name.Contains("Upsert", StringComparison.OrdinalIgnoreCase) ||
                item.Method.Name.Contains("DirectWrite", StringComparison.OrdinalIgnoreCase))
            .Select(item => $"{item.Type.Name}.{item.Method.Name}")
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void NeuroFoundationQueryContracts_UseExplicitStateSetsAndPaging()
    {
        var query = new CognitiveMemoryClaimQuery(
            projectId: Guid.NewGuid(),
            memoryRecordId: new CognitiveMemoryRecordId(Guid.NewGuid()),
            primaryContextFrameId: new CognitiveMemoryContextFrameId(Guid.NewGuid()),
            claimKinds: [CognitiveMemoryClaimKind.ProcedureConstraint],
            beliefStates: [CognitiveMemoryBeliefStateKind.Supported, CognitiveMemoryBeliefStateKind.Validated],
            validationStates: [CognitiveMemoryValidationState.HumanReviewed, CognitiveMemoryValidationState.Approved],
            validAtUtc: DateTimeOffset.UnixEpoch,
            page: new CognitiveMemoryPageRequest(take: 25));

        Assert.Equal(25, query.Page.Take);
        Assert.Contains(CognitiveMemoryValidationState.Approved, query.ValidationStates);
        Assert.DoesNotContain(
            "MinimumValidationState",
            typeof(CognitiveMemoryClaimQuery).GetProperties().Select(property => property.Name));
    }

    [Fact]
    public void ProjectionPayloadValidator_RequiresClaimContextAndBeliefMetadata()
    {
        var payload = new CognitiveMemoryClaimProjectionPayload(
            schemaVersion: new CognitiveMemoryPayloadSchemaVersion("projection-v1"),
            schemaKind: CognitiveMemoryProjectionPayloadSchemaKind.ClaimContainer,
            memoryRecordId: new CognitiveMemoryRecordId(Guid.NewGuid()),
            claimIds: [],
            contextFrameIds: [],
            entityIds: [],
            beliefStates: [],
            contextBoundaryPolicies: [],
            beliefBucket: CognitiveMemoryScoreProjectionBucket.Unknown);

        var result = CognitiveMemoryProjectionPayloadValidator.Validate(payload);

        Assert.False(result.IsValid);
        Assert.Contains(CognitiveMemoryProjectionPayloadValidationIssue.MissingClaimIds, result.Issues);
        Assert.Contains(CognitiveMemoryProjectionPayloadValidationIssue.MissingContextFrameIds, result.Issues);
        Assert.Contains(CognitiveMemoryProjectionPayloadValidationIssue.MissingBeliefStates, result.Issues);
        Assert.Contains(CognitiveMemoryProjectionPayloadValidationIssue.MissingEntityOrBoundaryMetadata, result.Issues);
    }

    [Fact]
    public void ProjectionPayloadContracts_AvoidUntypedJsonPayloadShape()
    {
        var payload = new CognitiveMemoryClaimProjectionPayload(
            schemaVersion: new CognitiveMemoryPayloadSchemaVersion("projection-v1"),
            schemaKind: CognitiveMemoryProjectionPayloadSchemaKind.ClaimContainer,
            memoryRecordId: new CognitiveMemoryRecordId(Guid.NewGuid()),
            claimIds: [new CognitiveMemoryClaimId(Guid.NewGuid())],
            contextFrameIds: [new CognitiveMemoryContextFrameId(Guid.NewGuid())],
            entityIds: [new CognitiveMemoryEntityId(Guid.NewGuid())],
            beliefStates: [CognitiveMemoryBeliefStateKind.Supported],
            contextBoundaryPolicies: [CognitiveMemoryContextBoundaryPolicy.RelatedNotSubstitutable],
            beliefBucket: CognitiveMemoryScoreProjectionBucket.StrongAccept);

        var propertyTypes = typeof(CognitiveMemoryClaimProjectionPayload).GetProperties()
            .Select(property => property.PropertyType)
            .ToList();

        Assert.True(CognitiveMemoryProjectionPayloadValidator.Validate(payload).IsValid);
        Assert.DoesNotContain("PayloadJson", typeof(CognitiveMemoryClaimProjectionPayload).GetProperties().Select(property => property.Name));
        Assert.DoesNotContain(typeof(Dictionary<string, object>), propertyTypes);
    }
}
