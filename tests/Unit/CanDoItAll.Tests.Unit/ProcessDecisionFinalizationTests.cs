using System.Text.Json.Nodes;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.Processes.Persistence;
using CanDoItAll.Processes.Runtime;
using static CanDoItAll.Tests.Unit.ProcessDecisionReviewTestData;

namespace CanDoItAll.Tests.Unit;

public sealed class ProcessDecisionFinalizationTests {
    [Theory]
    [InlineData(ProcessDecisionPhase.Active, ProcessRuntimeStepStatus.Cancelled)]
    [InlineData(ProcessDecisionPhase.Active, ProcessRuntimeStepStatus.Completed)]
    [InlineData(ProcessDecisionPhase.Active, ProcessRuntimeStepStatus.Failed)]
    [InlineData(ProcessDecisionPhase.Active, ProcessRuntimeStepStatus.Skipped)]
    [InlineData(ProcessDecisionPhase.WaitingForReview, ProcessRuntimeStepStatus.Cancelled)]
    [InlineData(ProcessDecisionPhase.WaitingForReview, ProcessRuntimeStepStatus.Completed)]
    [InlineData(ProcessDecisionPhase.WaitingForReview, ProcessRuntimeStepStatus.Failed)]
    [InlineData(ProcessDecisionPhase.WaitingForReview, ProcessRuntimeStepStatus.Skipped)]
    [InlineData(ProcessDecisionPhase.Reconciling, ProcessRuntimeStepStatus.Cancelled)]
    [InlineData(ProcessDecisionPhase.Reconciling, ProcessRuntimeStepStatus.Completed)]
    [InlineData(ProcessDecisionPhase.Reconciling, ProcessRuntimeStepStatus.Failed)]
    [InlineData(ProcessDecisionPhase.Reconciling, ProcessRuntimeStepStatus.Skipped)]
    public void Terminal_steps_close_only_accounting_and_preserve_original_identity(ProcessDecisionPhase phase, ProcessRuntimeStepStatus status) {
        var current = Initial() with { Phase = phase, Revision = 8, ChildCount = 2 };
        var next = ProcessDecisionTransitions.FinalizeTerminal(current, current.Expectation, status);
        var expected = status == ProcessRuntimeStepStatus.Cancelled ? ProcessDecisionPhase.Cancelled : ProcessDecisionPhase.Completed;
        Assert.Equal(current with { Revision = 9, Phase = expected }, next);
        Assert.Equal(phase, current.Phase);
        Assert.Equal(8, current.Revision);
    }

    [Theory]
    [InlineData(ProcessDecisionPhase.Completed)]
    [InlineData(ProcessDecisionPhase.Cancelled)]
    [InlineData(ProcessDecisionPhase.Superseded)]
    public void Closed_or_superseded_generations_cannot_create_a_new_finalization(ProcessDecisionPhase phase) {
        var current = Initial() with { Phase = phase };
        var error = Assert.Throws<ProcessDecisionException>(() => ProcessDecisionTransitions.FinalizeTerminal(current, current.Expectation, ProcessRuntimeStepStatus.Cancelled));
        Assert.Equal(ProcessDecisionRefusal.InvalidPhase, error.Reason);
    }

    [Fact]
    public void Open_children_and_stale_expectations_are_refused() {
        var current = Initial() with { Revision = 2, ChildCount = 1, UnclosedChildCount = 1 };
        Assert.Equal(ProcessDecisionRefusal.UnclosedChildren, Assert.Throws<ProcessDecisionException>(() =>
            ProcessDecisionTransitions.FinalizeTerminal(current, current.Expectation, ProcessRuntimeStepStatus.Cancelled)).Reason);
        Assert.Equal(ProcessDecisionRefusal.StaleExpectation, Assert.Throws<ProcessDecisionException>(() =>
            ProcessDecisionTransitions.FinalizeTerminal(current, current.Expectation with { Revision = 1 }, ProcessRuntimeStepStatus.Cancelled)).Reason);
    }

    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Ready)]
    [InlineData(ProcessRuntimeStepStatus.Blocked)]
    [InlineData((ProcessRuntimeStepStatus)99)]
    public void Nonterminal_or_unknown_step_status_is_never_an_accounting_outcome(ProcessRuntimeStepStatus status) {
        var current = Initial();
        Assert.Throws<InvalidDataException>(() => ProcessDecisionTransitions.FinalizeTerminal(current, current.Expectation, status));
        Assert.Throws<InvalidDataException>(() => (Command(current) with { StepStatus = status }).Validate());
    }

    [Theory]
    [InlineData(InvalidCommand.Step)]
    [InlineData(InvalidCommand.BudgetStep)]
    [InlineData(InvalidCommand.BudgetId)]
    [InlineData(InvalidCommand.BudgetRevision)]
    [InlineData(InvalidCommand.Material)]
    public void Finalization_rejects_missing_original_binding_and_cannot_capture_material(InvalidCommand change) {
        var command = Command(Initial());
        command = change switch {
            InvalidCommand.Step => command with { StepInstanceId = default },
            InvalidCommand.BudgetStep => command with { Budget = command.Budget with { StepInstanceId = new(Id(40)) } },
            InvalidCommand.BudgetId => command with { Budget = command.Budget with { BudgetId = default } },
            InvalidCommand.BudgetRevision => command with { Budget = command.Budget with { Revision = 0 } },
            _ => command with { Material = new(new(new(Id(7)), 1, new(Id(2))), new string('A', 64)) }
        };
        Assert.Throws<InvalidDataException>(command.Validate);
    }

    [Theory]
    [InlineData(ProcessRuntimeStepStatus.Cancelled)]
    [InlineData(ProcessRuntimeStepStatus.Completed)]
    [InlineData(ProcessRuntimeStepStatus.Failed)]
    [InlineData(ProcessRuntimeStepStatus.Skipped)]
    public void Version_six_retains_the_exact_terminal_status_and_budget_expectation(ProcessRuntimeStepStatus status) {
        var current = Initial();
        var command = Command(current) with { StepStatus = status };
        ProcessDecisionReceipt receipt = new(command, true, ProcessDecisionTransitions.FinalizeTerminal(current, current.Expectation, status));
        var json = ProcessDecisionReceiptCodec.Write(receipt)!;
        Assert.Equal(6, JsonNode.Parse(json)!["version"]!.GetValue<int>());
        Assert.Equal(receipt, ProcessDecisionReceiptCodec.Read(json));
        Assert.Equal(json, ProcessDecisionReceiptCodec.Write(ProcessDecisionReceiptCodec.Read(json)));
        Assert.Throws<InvalidOperationException>(() => ProcessDecisionReceiptCodec.RequireSameCommand(command with {
            Budget = command.Budget with { Revision = command.Budget.Revision + 1 }
        }, receipt));
    }

    [Theory]
    [InlineData(Malformed.Downgrade)]
    [InlineData(Malformed.MissingBudget)]
    [InlineData(Malformed.MissingStatus)]
    [InlineData(Malformed.WrongKind)]
    [InlineData(Malformed.ExtraCompletion)]
    [InlineData(Malformed.WrongPhase)]
    [InlineData(Malformed.OpenChild)]
    public void Malformed_terminal_receipts_cannot_be_reinterpreted(Malformed change) {
        var current = Initial();
        var command = Command(current);
        var document = JsonNode.Parse(ProcessDecisionReceiptCodec.Write(new(command, true,
            ProcessDecisionTransitions.FinalizeTerminal(current, current.Expectation, command.StepStatus)))!)!;
        switch (change) {
            case Malformed.Downgrade:
                document["version"] = 1;
                break;
            case Malformed.MissingBudget:
                document["command"]!.AsObject().Remove("budget");
                break;
            case Malformed.MissingStatus:
                document["command"]!.AsObject().Remove("terminalStatus");
                break;
            case Malformed.WrongKind:
                document["command"]!["kind"] = 3;
                break;
            case Malformed.ExtraCompletion:
                document["command"]!["completion"] = 1;
                break;
            case Malformed.WrongPhase:
                document["current"]!["phase"] = 3;
                break;
            default:
                document["current"]!["childCount"] = 1;
                document["current"]!["unclosedChildCount"] = 1;
                break;
        }
        Assert.Throws<InvalidDataException>(() => ProcessDecisionReceiptCodec.Read(document.ToJsonString()));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void Earlier_start_receipts_keep_their_canonical_shape_without_terminal_fields(int version) {
        var current = Initial();
        ProcessDecisionStart command = new(new(Id(4)), current.Reference.DecisionId, current.MaterialId) {
            Material = version >= 2 ? new(new(new(Id(7)), 1, current.MaterialId), new string('A', 64)) : null,
            InheritedScope = version == 3 ? new(new(Id(7)), 1) : null
        };
        ProcessDecisionReceipt receipt = new(command, true, current);
        var json = ProcessDecisionReceiptCodec.Write(receipt)!;
        var parsed = JsonNode.Parse(json)!;
        Assert.Equal(version, parsed["version"]!.GetValue<int>());
        Assert.False(parsed["command"]!.AsObject().ContainsKey("budget"));
        Assert.False(parsed["command"]!.AsObject().ContainsKey("terminalStatus"));
        Assert.Equal(receipt, ProcessDecisionReceiptCodec.Read(json));
        Assert.Equal(json, ProcessDecisionReceiptCodec.Write(ProcessDecisionReceiptCodec.Read(json)));
    }

    private static ProcessDecisionGeneration Initial() => ProcessDecisionTransitions.Create(new(Id(1)), new(Id(2)), new(Id(3)));
    private static ProcessDecisionFinalization Command(ProcessDecisionGeneration current) => new(new(Id(4)), current.Expectation,
        new(new ProcessStepInstanceId(Id(4)), current.BudgetId, 3), ProcessRuntimeStepStatus.Cancelled);
    public enum InvalidCommand { Step, BudgetStep, BudgetId, BudgetRevision, Material }
    public enum Malformed { Downgrade, MissingBudget, MissingStatus, WrongKind, ExtraCompletion, WrongPhase, OpenChild }
}
