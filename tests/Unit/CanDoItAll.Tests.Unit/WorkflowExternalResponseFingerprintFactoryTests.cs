using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExternalResponseFingerprintFactoryTests
{
    private static readonly WorkflowExternalRequestId RequestId = new(Guid.Parse("d1202d31-2587-44fa-baa3-986f058ed9e9"));

    [Fact]
    public void Create_ReorderedObjectsAndWhitespace_ProducesEquivalentFingerprint()
    {
        var first = Create(
            RequestId,
            WorkflowExternalRequestVersion.Initial,
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "actor-1"),
            Scope(),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            "response-key",
            """ { "z": [{"b": 2, "a": 1}], "a": true } """);
        var equivalent = Create(
            RequestId,
            WorkflowExternalRequestVersion.Initial,
            new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "actor-1"),
            Scope(),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            "response-key",
            """{"a":true,"z":[{"a":1,"b":2}]}""");

        Assert.Equal(first.IdempotencyKeyHash, equivalent.IdempotencyKeyHash);
        Assert.Equal(first.PayloadHash, equivalent.PayloadHash);
        Assert.Equal(first.CanonicalPayload, equivalent.CanonicalPayload);
        Assert.Equal("""{"a":true,"z":[{"a":1,"b":2}]}""", first.CanonicalPayload.Json);
    }

    [Fact]
    public void Create_ArrayOrderChangesPayloadHashButNotScopedKeyHash()
    {
        var first = Create(
            RequestId,
            WorkflowExternalRequestVersion.Initial,
            Actor(),
            Scope(),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            "response-key",
            "[1,2,3]");
        var reordered = Create(
            RequestId,
            WorkflowExternalRequestVersion.Initial,
            Actor(),
            Scope(),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            "response-key",
            "[3,2,1]");

        Assert.Equal(first.IdempotencyKeyHash, reordered.IdempotencyKeyHash);
        Assert.NotEqual(first.PayloadHash, reordered.PayloadHash);
    }

    [Theory]
    [InlineData("null", "null")]
    [InlineData(" true ", "true")]
    [InlineData("\"answer\"", "\"answer\"")]
    [InlineData("42", "42")]
    public void CanonicalizeJson_AllJsonRootKinds_AreSupported(string input, string expected)
        => Assert.Equal(expected, WorkflowExternalResponseFingerprintFactory.CanonicalizeJson(input));

    [Fact]
    public void Create_RequestActorVersionAndCallerKey_AllScopeIdempotencyHash()
    {
        var baseline = Create(
            RequestId,
            WorkflowExternalRequestVersion.Initial,
            Actor(),
            Scope(),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            "response-key",
            "true");
        var mismatches = new[]
        {
            Create(WorkflowExternalRequestId.New(), WorkflowExternalRequestVersion.Initial, Actor(), Scope(), WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint, "response-key", "true"),
            Create(RequestId, new WorkflowExternalRequestVersion(2), Actor(), Scope(), WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint, "response-key", "true"),
            Create(RequestId, WorkflowExternalRequestVersion.Initial, new WorkflowLaunchActor(WorkflowLaunchActorKind.Agent, "actor-1"), Scope(), WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint, "response-key", "true"),
            Create(RequestId, WorkflowExternalRequestVersion.Initial, new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "actor-2"), Scope(), WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint, "response-key", "true"),
            Create(RequestId, WorkflowExternalRequestVersion.Initial, Actor(), WorkspaceScopeDescriptor.Project("other-project"), WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint, "response-key", "true"),
            Create(RequestId, WorkflowExternalRequestVersion.Initial, Actor(), Scope(), "other-policy", "response-key", "true"),
            Create(RequestId, WorkflowExternalRequestVersion.Initial, Actor(), Scope(), WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint, "other-key", "true")
        };

        Assert.All(mismatches, mismatch => Assert.NotEqual(baseline.IdempotencyKeyHash, mismatch.IdempotencyKeyHash));
    }

    [Fact]
    public void RawIdempotencyKey_IsRedactedAndNotPartOfDurableFingerprint()
    {
        var key = new WorkflowExternalResponseIdempotencyKey("super-secret-key");
        var fingerprint = WorkflowExternalResponseFingerprintFactory.Create(
            RequestId,
            WorkflowExternalRequestVersion.Initial,
            Actor(),
            Scope(),
            WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint,
            key,
            "true");

        Assert.Equal("[REDACTED]", key.ToString());
        Assert.DoesNotContain("super-secret-key", fingerprint.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(
            typeof(WorkflowExternalResponseOperationRecord).GetProperties(),
            property => property.PropertyType == typeof(WorkflowExternalResponseIdempotencyKey));
    }

    [Fact]
    public void CanonicalizeJson_DuplicateObjectProperties_FailsClosed()
        => Assert.Throws<ArgumentException>(
            () => WorkflowExternalResponseFingerprintFactory.CanonicalizeJson("""{"a":1,"a":2}"""));

    [Fact]
    public void CanonicalizeJson_CaseInsensitiveDuplicateObjectProperties_FailsClosed()
        => Assert.Throws<ArgumentException>(
            () => WorkflowExternalResponseFingerprintFactory.CanonicalizeJson("""{"approved":true,"Approved":false}"""));

    private static WorkflowExternalResponseFingerprint Create(
        WorkflowExternalRequestId requestId,
        WorkflowExternalRequestVersion version,
        WorkflowLaunchActor actor,
        WorkspaceScopeDescriptor scope,
        string policyFingerprint,
        string key,
        string json)
        => WorkflowExternalResponseFingerprintFactory.Create(
            requestId,
            version,
            actor,
            scope,
            policyFingerprint,
            new WorkflowExternalResponseIdempotencyKey(key),
            json);

    private static WorkflowLaunchActor Actor()
        => new(WorkflowLaunchActorKind.User, "actor-1");

    private static WorkspaceScopeDescriptor Scope()
        => WorkspaceScopeDescriptor.Project("project-1");
}
