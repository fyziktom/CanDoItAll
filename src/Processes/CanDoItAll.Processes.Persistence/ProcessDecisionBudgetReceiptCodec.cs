using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Abstractions;
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
        var json = JsonSerializer.Serialize(ToDocument(receipt), Options);
        RequireBound(json);
        return json;
    }

    private static Document ToDocument(ProcessDecisionBudgetReceipt receipt) {
        var command = receipt.Command;
        var change = receipt.Change;
        var balance = change.Balance;
        return new Document(command.Expectation.StepInstanceId is null || receipt.Funding is not null ? 2 : 1,
            command.Expectation.StepInstanceId?.Value, balance.BudgetId.Value, command.AllocationId.Value,
            command.Expectation.Revision, command is ProcessDecisionBudgetReservation,
            command is ProcessDecisionBudgetReservation reservation ? reservation.Grant : null,
            command is ProcessDecisionBudgetSettlement settlement ? settlement.Settlement : null,
            change.Changed, balance.Revision, balance.Ceiling, balance.ConsumedWorkTicks, balance.ReservedWorkTicks,
            balance.ConsumedReplans, balance.ReservedReplans, balance.AllocationCount, balance.OverrunDetected,
            change.Allocation.Grant, change.Allocation.Settlement,
            receipt.Funding is { } funding ? new(funding.RootRunId.Value, ToDocument(funding.Receipt)) : null);
    }

    internal static ProcessDecisionBudgetReceipt? Read(string? json) {
        if (json is null) {
            return null;
        }
        RequireBound(json);
        var document = JsonSerializer.Deserialize<Document>(json, Options) ?? throw new InvalidDataException("The Process accounting receipt is empty.");
        if (!string.Equals(json, JsonSerializer.Serialize(document, Options), StringComparison.Ordinal)) {
            throw new InvalidDataException("The Process accounting receipt is not a supported canonical record.");
        }
        var receipt = FromDocument(document);
        Validate(receipt);
        return receipt;
    }

    private static ProcessDecisionBudgetReceipt FromDocument(Document document) {
        var version = document.StepId is null || document.Funding is not null ? 2 : 1;
        if (document.Version != version || document.Funding is { Receipt: null } || document.Funding?.Receipt.Funding is not null) {
            throw new InvalidDataException("The Process accounting receipt has an unsupported owner or funding schema.");
        }
        var expectation = new ProcessDecisionBudgetExpectation(document.StepId is { } step ? new ProcessStepInstanceId(step) : null,
            new(document.BudgetId), document.ExpectedRevision);
        var allocationId = new DecisionBudgetAllocationId(document.AllocationId);
        ProcessDecisionBudgetCommand command = (document.Reservation, document.RequestedGrant, document.RequestedSettlement) switch {
            (true, { } grant, null) => new ProcessDecisionBudgetReservation(expectation, allocationId, grant),
            (false, null, { } settlement) => new ProcessDecisionBudgetSettlement(expectation, allocationId, settlement),
            _ => throw new InvalidDataException("The Process accounting receipt has an invalid operation.")
        };
        var balance = new DecisionBudgetBalance(expectation.BudgetId, document.ResultRevision, document.Ceiling,
            document.ConsumedWorkTicks, document.ReservedWorkTicks, document.ConsumedReplans, document.ReservedReplans,
            document.AllocationCount, document.OverrunDetected);
        return new ProcessDecisionBudgetReceipt(command, new(document.Changed, balance,
            new(expectation.BudgetId, allocationId, document.Grant, document.Settlement))) {
            Funding = document.Funding is { } funding ? new(new(funding.RootRunId), FromDocument(funding.Receipt)) : null
        };
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
        if (receipt.Funding is not { } funding) {
            return;
        }
        if (command.Expectation.StepInstanceId is null || funding.RootRunId.Value == Guid.Empty || funding.Receipt is null ||
            funding.Receipt.Funding is not null || funding.Receipt.Command.Expectation.StepInstanceId is not null) {
            throw new InvalidDataException("A funded accounting receipt requires one original root result and a local step.");
        }
        Validate(funding.Receipt);
        var parent = funding.Receipt;
        if (parent.Command.GetType() != command.GetType() || parent.Change.Changed != change.Changed ||
            parent.Command.AllocationId != ProcessDecisionFundingIdentity.Derive(parent.Change.Balance.BudgetId, change.Balance.BudgetId, command.AllocationId) ||
            parent.Change.Allocation.Grant != change.Allocation.Grant || parent.Change.Allocation.Settlement != change.Allocation.Settlement) {
            throw new InvalidDataException("The local accounting and root funding receipts disagree about original work.");
        }
    }

    private static void RequireBound(string json) {
        if (Encoding.UTF8.GetByteCount(json) is 0 or > MaximumUtf8Bytes) {
            throw new InvalidDataException("The Process accounting receipt exceeds its retained bound.");
        }
    }

    private sealed record Document(int Version, Guid? StepId, Guid BudgetId, Guid AllocationId, long ExpectedRevision,
        bool Reservation, DecisionBudgetAmount? RequestedGrant, DecisionBudgetSettlement? RequestedSettlement,
        bool Changed, long ResultRevision, DecisionDataPolicyBudget Ceiling, long ConsumedWorkTicks, long ReservedWorkTicks,
        long ConsumedReplans, long ReservedReplans, int AllocationCount, bool OverrunDetected,
        DecisionBudgetAmount Grant, DecisionBudgetSettlement? Settlement,
        [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] FundingDocument? Funding = null);

    private sealed record FundingDocument(Guid RootRunId, Document Receipt);
}
