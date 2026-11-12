using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionMaterialCommandTests {
    [Theory]
    [InlineData(ProcessDecisionRunScopeCommandKind.CompleteResult, ProcessDecisionCompletion.Complete)]
    [InlineData(ProcessDecisionRunScopeCommandKind.CancelResult, ProcessDecisionCompletion.Complete)]
    [InlineData(ProcessDecisionRunScopeCommandKind.CancelResult, ProcessDecisionCompletion.Cancel)]
    public void Terminal_result_closes_scope_and_decision_without_capturing_material(
        ProcessDecisionRunScopeCommandKind kind, ProcessDecisionCompletion completion) {
        var material = Material();
        var scope = new ProcessDecisionRunScopeCommand(new(material.Scope.Id, 3), kind);
        var closure = Closure() with { Completion = completion };
        EfProcessDecisionMaterialStore.RequireCommandMaterial(closure, null, scope);
        Assert.Throws<InvalidDataException>(() => EfProcessDecisionMaterialStore.RequireCommandMaterial(closure, material, scope));
        Assert.Throws<InvalidDataException>(() => EfProcessDecisionMaterialStore.RequireCommandMaterial(
            closure with { Material = material.Stamp }, material, scope));
    }

    [Theory]
    [InlineData(ProcessDecisionRunScopeCommandKind.CompleteResult)]
    [InlineData(ProcessDecisionRunScopeCommandKind.CancelResult)]
    public void Terminal_scope_does_not_authorize_capture_or_nonterminal_local_decisions(ProcessDecisionRunScopeCommandKind kind) {
        var material = Material();
        var scope = new ProcessDecisionRunScopeCommand(new(material.Scope.Id, 3), kind);
        var closure = Closure();
        ProcessDecisionCommand[] commands = [
            closure with { Completion = null },
            closure with { Completion = ProcessDecisionCompletion.Review },
            closure with { Completion = ProcessDecisionCompletion.Reconcile },
            new ProcessDecisionStart(closure.StepInstanceId, new(Guid.NewGuid()), material.Reference.MaterialId) { Material = material.Stamp },
            new ProcessDecisionChangePhase(closure.StepInstanceId, closure.Expectation, ProcessDecisionPhaseChange.Complete)
        ];
        foreach (var command in commands) {
            command.Validate();
            Assert.Throws<InvalidDataException>(() => EfProcessDecisionMaterialStore.RequireCommandMaterial(
                command, command.Material is null ? null : material, scope));
        }
    }

    [Theory]
    [InlineData(ProcessDecisionRunScopeCommandKind.BeginReconciliation)]
    [InlineData(ProcessDecisionRunScopeCommandKind.BeginCapture)]
    [InlineData(ProcessDecisionRunScopeCommandKind.CompleteCapture)]
    [InlineData(ProcessDecisionRunScopeCommandKind.AbortCapture)]
    [InlineData(ProcessDecisionRunScopeCommandKind.Complete)]
    [InlineData(ProcessDecisionRunScopeCommandKind.Cancel)]
    public void Ordinary_scope_commands_cannot_mutate_a_local_decision(ProcessDecisionRunScopeCommandKind kind) {
        var material = Material();
        var scope = new ProcessDecisionRunScopeCommand(new(material.Scope.Id, 3), kind,
            kind == ProcessDecisionRunScopeCommandKind.BeginCapture
                ? new(new(Guid.NewGuid()), material.Reference.MaterialId, ProcessDecisionRunCaptureKind.Initial) : null,
            kind == ProcessDecisionRunScopeCommandKind.CompleteCapture ? material.Stamp : null);
        scope.Validate();
        EfProcessDecisionMaterialStore.RequireCommandMaterial(null, scope.Material is null ? null : material, scope);
        Assert.Throws<InvalidDataException>(() => EfProcessDecisionMaterialStore.RequireCommandMaterial(
            Closure(), scope.Material is null ? null : material, scope));
    }

    private static ProcessDecisionClosure Closure() {
        var result = new ProcessDecisionReviewTestData().Result;
        return new(result.Child.StepInstanceId, new(result.Child.Reference, 1), result.Child.ChildId, result.Closure) {
            Completion = ProcessDecisionCompletion.Complete
        };
    }

    private static ProcessDecisionMaterial Material() {
        var root = new ProcessDecisionReviewTestData().State.RootRunId;
        ProcessDecisionMaterialScope scope = new(new(Guid.NewGuid()), Guid.NewGuid(), Guid.NewGuid(), root, root, null);
        return ProcessDecisionMaterial.FromContent(scope, new(scope.Id, 1, new(Guid.NewGuid())),
            ProcessDecisionPolicyFixture.ContentContract(), ProcessDecisionReviewTestData.Now, "{}");
    }
}
