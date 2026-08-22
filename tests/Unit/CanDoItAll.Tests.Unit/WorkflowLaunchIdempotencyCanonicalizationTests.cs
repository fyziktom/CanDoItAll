using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.AgentFramework.Workflows.Abstractions;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowLaunchIdempotencyCanonicalizationTests
{
    [Fact]
    public void CanonicalizeInputJson_ReorderedNestedProperties_ProducesOneCanonicalObject()
    {
        const string input = """
                             {
                               "z": [{"b": 2, "a": 1}, 3],
                               "a": {"second": false, "first": null}
                             }
                             """;

        var canonical = WorkflowLaunchIdempotencyRequestFactory.CanonicalizeInputJson(input);

        Assert.Equal(
            """{"a":{"first":null,"second":false},"z":[{"a":1,"b":2},3]}""",
            canonical);
    }

    [Fact]
    public void CreateFingerprint_EquivalentPropertyOrder_HasSameFingerprintAndCanonicalInputHash()
    {
        var intent = CreateIntent();

        var first = WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(
            intent,
            """{"customer":{"region":"EU","tier":"enterprise"},"items":[{"sku":"A","quantity":2},{"sku":"B","quantity":1}]}""");
        var reordered = WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(
            intent,
            """{"items":[{"quantity":2,"sku":"A"},{"quantity":1,"sku":"B"}],"customer":{"tier":"enterprise","region":"EU"}}""");
        var changedArrayOrder = WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(
            intent,
            """{"items":[{"quantity":1,"sku":"B"},{"quantity":2,"sku":"A"}],"customer":{"tier":"enterprise","region":"EU"}}""");

        Assert.Equal(first.Value, reordered.Value);
        Assert.Equal(first.CanonicalInputHash, reordered.CanonicalInputHash);
        Assert.NotEqual(first.Value, changedArrayOrder.Value);
        Assert.NotEqual(first.CanonicalInputHash, changedArrayOrder.CanonicalInputHash);
        Assert.Equal(64, first.Value.Length);
        Assert.Equal(64, first.CanonicalInputHash.Length);
    }

    [Fact]
    public void AuthorizationScopeAndPolicy_AreBoundIntoScopeAndRequestFingerprint()
    {
        var intent = CreateIntent();
        var otherScope = intent with
        {
            Origin = intent.Origin with
            {
                AuthorizationScope = WorkspaceScopeDescriptor.Project("other-project")
            }
        };
        var otherPolicy = intent with
        {
            Origin = intent.Origin with
            {
                AuthorizationPolicyFingerprint = "workflow-external-response.v2"
            }
        };
        var key = new WorkflowLaunchIdempotencyKey("authorization-binding");

        var baselineScope = WorkflowLaunchIdempotencyRequestFactory.CreateScope(intent, key);
        var baselineFingerprint = WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(intent, "{}");

        Assert.NotEqual(
            baselineScope.OriginScopeKey,
            WorkflowLaunchIdempotencyRequestFactory.CreateScope(otherScope, key).OriginScopeKey);
        Assert.NotEqual(
            baselineScope.OriginScopeKey,
            WorkflowLaunchIdempotencyRequestFactory.CreateScope(otherPolicy, key).OriginScopeKey);
        Assert.NotEqual(
            baselineFingerprint,
            WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(otherScope, "{}"));
        Assert.NotEqual(
            baselineFingerprint,
            WorkflowLaunchIdempotencyRequestFactory.CreateFingerprint(otherPolicy, "{}"));
    }

    private static WorkflowLaunchIntent CreateIntent()
        => new(
            new WorkflowDefinitionSelection.ExactSavedVersion(
                WorkflowId.New(),
                WorkflowVersionId.New()),
            WorkflowLaunchMode.Production,
            new WorkflowLaunchOrigin.Api(
                new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "canonicalization-test"),
                new WorkflowLaunchCorrelationId("canonicalization-correlation"))
            {
                AuthorizationScope = WorkspaceScopeDescriptor.Project("canonicalization-project"),
                AuthorizationPolicyFingerprint =
                    WorkflowExternalResponseAuthorizationPolicy.CurrentFingerprint
            },
            "{}",
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.CallerSupplied(
                new WorkflowLaunchIdempotencyKey("canonicalization-key")))
        {
            RequestedBackend = WorkflowRuntimeBackendKind.InProcess
        };
}
