using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

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

    private static WorkflowLaunchIntent CreateIntent()
        => new(
            new WorkflowDefinitionSelection.ExactSavedVersion(
                WorkflowId.New(),
                WorkflowVersionId.New()),
            WorkflowLaunchMode.Production,
            new WorkflowLaunchOrigin.Api(
                new WorkflowLaunchActor(WorkflowLaunchActorKind.User, "canonicalization-test"),
                new WorkflowLaunchCorrelationId("canonicalization-correlation")),
            "{}",
            WorkflowLaunchCompletionPolicy.WaitForStopped,
            new WorkflowLaunchIdempotency.CallerSupplied(
                new WorkflowLaunchIdempotencyKey("canonicalization-key")))
        {
            RequestedBackend = WorkflowRuntimeBackendKind.InProcess
        };
}
