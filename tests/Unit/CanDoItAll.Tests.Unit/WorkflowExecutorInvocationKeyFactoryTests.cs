using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Tests.Unit.AgentFramework;

public sealed class WorkflowExecutorInvocationKeyFactoryTests
{
    [Fact]
    public void Create_IdenticalMaterial_ProducesStableIdentity()
    {
        var material = CreateMaterial();

        var first = Create(material);
        var second = Create(material);

        Assert.Equal(first, second);
        Assert.Equal(64, first.ScopeKey.Value.Length);
        Assert.Equal(64, first.Key.Value.Length);
        Assert.Equal(64, first.IdempotencyKey.Value.Length);
    }

    [Fact]
    public void Create_EveryStableIdentityField_ChangesScopeAndInvocationKey()
    {
        var baselineMaterial = CreateMaterial();
        var baseline = Create(baselineMaterial);
        var mismatches = new[]
        {
            baselineMaterial with { RunId = WorkflowRunId.New() },
            baselineMaterial with { WorkflowVersionId = WorkflowVersionId.New() },
            baselineMaterial with { NodeId = new WorkflowNodeId("other-node") },
            baselineMaterial with { ExecutorId = new WorkflowExecutorId("other.executor") },
            baselineMaterial with { ExecutorContractVersion = new WorkflowExecutorContractVersion("2") },
            baselineMaterial with { CausationRequestId = WorkflowExternalRequestId.New() },
            baselineMaterial with { CausationRequestVersion = new WorkflowExternalRequestVersion(2) },
            baselineMaterial with { CausationOperationId = WorkflowExternalResponseOperationId.New() },
            baselineMaterial with { LogicalGeneration = new WorkflowExecutorInvocationGeneration(2) }
        };

        Assert.All(
            mismatches,
            mismatch =>
            {
                var changed = Create(mismatch);
                Assert.NotEqual(baseline.ScopeKey, changed.ScopeKey);
                Assert.NotEqual(baseline.Key, changed.Key);
                Assert.NotEqual(baseline.IdempotencyKey, changed.IdempotencyKey);
            });
    }

    [Fact]
    public void Create_InputMismatch_PreservesScopeAndChangesInvocationKey()
    {
        var material = CreateMaterial();

        var baseline = Create(material);
        var changed = Create(material with { Input = new WorkflowNodeInput("""{"value":2}""") });

        Assert.Equal(baseline.ScopeKey, changed.ScopeKey);
        Assert.NotEqual(baseline.InputHash, changed.InputHash);
        Assert.NotEqual(baseline.Key, changed.Key);
        Assert.NotEqual(baseline.IdempotencyKey, changed.IdempotencyKey);
    }

    private static WorkflowExecutorInvocationIdentity Create(InvocationMaterial material)
        => WorkflowExecutorInvocationKeyFactory.Create(
            material.RunId,
            material.WorkflowVersionId,
            material.NodeId,
            material.ExecutorId,
            material.ExecutorContractVersion,
            material.CausationRequestId,
            material.CausationRequestVersion,
            material.CausationOperationId,
            material.LogicalGeneration,
            material.Input);

    private static InvocationMaterial CreateMaterial()
        => new(
            new WorkflowRunId(Guid.Parse("951df89f-cbcf-46e4-b4bd-2887bad35038")),
            new WorkflowVersionId(Guid.Parse("2995f9aa-ec52-47b4-a898-f4e5ac684d89")),
            new WorkflowNodeId("governed-node"),
            new WorkflowExecutorId("governed.executor"),
            new WorkflowExecutorContractVersion("1"),
            new WorkflowExternalRequestId(Guid.Parse("dbd7b54e-2b99-40ac-8f7a-1984db446fb1")),
            WorkflowExternalRequestVersion.Initial,
            new WorkflowExternalResponseOperationId(Guid.Parse("cb43aa3c-87e0-4346-b8bd-3c0e9fd0c03a")),
            WorkflowExecutorInvocationGeneration.Initial,
            new WorkflowNodeInput("""{"value":1}"""));

    private sealed record InvocationMaterial(
        WorkflowRunId RunId,
        WorkflowVersionId WorkflowVersionId,
        WorkflowNodeId NodeId,
        WorkflowExecutorId ExecutorId,
        WorkflowExecutorContractVersion ExecutorContractVersion,
        WorkflowExternalRequestId CausationRequestId,
        WorkflowExternalRequestVersion CausationRequestVersion,
        WorkflowExternalResponseOperationId CausationOperationId,
        WorkflowExecutorInvocationGeneration LogicalGeneration,
        WorkflowNodeInput Input);
}
