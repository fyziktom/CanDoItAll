using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Persistence;

internal static class ProcessDecisionBudgetReceiptCodec {
    internal const int MaximumUtf8Bytes = 8192;
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web) {
        PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectRequiredConstructorParameters = true, MaxDepth = 8
    };

    internal static string? Write(ProcessDecisionBudgetReceipt? receipt) {
        if (receipt is null) {
            return null;
        }
        Validate(receipt);
        var command = receipt.Command;
        var change = receipt.Change;
        var balance = change.Balance;
        var document = new Document(1, command.Expectation.StepInstanceId.Value, balance.BudgetId.Value, command.AllocationId.Value,
            command.Expectation.Revision, command is ProcessDecisionBudgetReservation,
            command is ProcessDecisionBudgetReservation reservation ? reservation.Grant : null,
            command is ProcessDecisionBudgetSettlement settlement ? settlement.Settlement : null,
            change.Changed, balance.Revision, balance.Ceiling, balance.ConsumedWorkTicks, balance.ReservedWorkTicks,
            balance.ConsumedReplans, balance.ReservedReplans, balance.AllocationCount, balance.OverrunDetected,
            change.Allocation.Grant, change.Allocation.Settlement);
        var json = JsonSerializer.Serialize(document, Options);
        RequireBound(json);
        return json;
    }

    internal static ProcessDecisionBudgetReceipt? Read(string? json) {
        if (json is null) {
            return null;
        }
        RequireBound(json);
        var document = JsonSerializer.Deserialize<Document>(json, Options) ?? throw new InvalidDataException("The Process accounting receipt is empty.");
        if (document.Version != 1 || !string.Equals(json, JsonSerializer.Serialize(document, Options), StringComparison.Ordinal)) {
            throw new InvalidDataException("The Process accounting receipt is not a supported canonical record.");
        }
        var expectation = new ProcessDecisionBudgetExpectation(new(document.StepId), new(document.BudgetId), document.ExpectedRevision);
        var allocationId = new DecisionBudgetAllocationId(document.AllocationId);
        ProcessDecisionBudgetCommand command = (document.Reservation, document.RequestedGrant, document.RequestedSettlement) switch {
            (true, { } grant, null) => new ProcessDecisionBudgetReservation(expectation, allocationId, grant),
            (false, null, { } settlement) => new ProcessDecisionBudgetSettlement(expectation, allocationId, settlement),
            _ => throw new InvalidDataException("The Process accounting receipt has an invalid operation.")
        };
        var balance = new DecisionBudgetBalance(expectation.BudgetId, document.ResultRevision, document.Ceiling,
            document.ConsumedWorkTicks, document.ReservedWorkTicks, document.ConsumedReplans, document.ReservedReplans,
            document.AllocationCount, document.OverrunDetected);
        var receipt = new ProcessDecisionBudgetReceipt(command, new(document.Changed, balance,
            new(expectation.BudgetId, allocationId, document.Grant, document.Settlement)));
        Validate(receipt);
        return receipt;
    }

    internal static void RequireSameCommand(ProcessDecisionBudgetCommand? command, ProcessDecisionBudgetReceipt? receipt) {
        if (command != receipt?.Command) {
            throw new InvalidOperationException("The original runtime command has different Process accounting parameters.");
        }
    }

    private static void Validate(ProcessDecisionBudgetReceipt receipt) {
        receipt.Command.Validate();
        receipt.Change.Balance.Validate();
        receipt.Change.Allocation.Validate();
        var command = receipt.Command;
        var change = receipt.Change;
        if (change.Balance.BudgetId != command.Expectation.BudgetId || change.Allocation.BudgetId != command.Expectation.BudgetId ||
                change.Allocation.Id != command.AllocationId || change.Balance.Revision < command.Expectation.Revision ||
                change.Balance.Revision - command.Expectation.Revision != (change.Changed ? 1 : 0) ||
                command is ProcessDecisionBudgetReservation reservation && reservation.Grant != change.Allocation.Grant ||
                command is ProcessDecisionBudgetSettlement settlement && settlement.Settlement != change.Allocation.Settlement) {
            throw new InvalidDataException("The Process accounting receipt differs from its original command.");
        }
    }

    private static void RequireBound(string json) {
        if (Encoding.UTF8.GetByteCount(json) is 0 or > MaximumUtf8Bytes) {
            throw new InvalidDataException("The Process accounting receipt exceeds its retained bound.");
        }
    }

    private sealed record Document(int Version, Guid StepId, Guid BudgetId, Guid AllocationId, long ExpectedRevision,
        bool Reservation, DecisionBudgetAmount? RequestedGrant, DecisionBudgetSettlement? RequestedSettlement,
        bool Changed, long ResultRevision, DecisionDataPolicyBudget Ceiling, long ConsumedWorkTicks, long ReservedWorkTicks,
        long ConsumedReplans, long ReservedReplans, int AllocationCount, bool OverrunDetected,
        DecisionBudgetAmount Grant, DecisionBudgetSettlement? Settlement);
}
