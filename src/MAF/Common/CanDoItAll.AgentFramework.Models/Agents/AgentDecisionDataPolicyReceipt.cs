using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace CanDoItAll.AgentFramework.Models;

[JsonConverter(typeof(AgentDecisionDataPolicyReceiptJsonConverter))]
public sealed record AgentDecisionDataPolicyReceipt {
    public const int SchemaVersion = 1;
    public const int MaximumUtf8Bytes = 4096;

    public AgentDecisionDataPolicyReceipt(ReplaceAgentDecisionDataPolicyCommand command, DateTimeOffset acceptedAtUtc) {
        ArgumentNullException.ThrowIfNull(command);
        var scope = command.Expectation.Workspace.WorkspaceScope;
        if (!Enum.IsDefined(scope.Kind) || scope.Key is null || scope.Key.Length > 256 ||
            new WorkspaceScopeDescriptor(scope.Kind, scope.Key) != scope ||
            acceptedAtUtc == default || acceptedAtUtc.Offset != TimeSpan.Zero ||
            acceptedAtUtc > DateTimeOffset.MaxValue - AgentDecisionDataPolicyReceiptLog.Retention) {
            throw new ArgumentException("The Agent policy receipt requires canonical scope and acceptance time.");
        }
        Command = command;
        AcceptedAtUtc = acceptedAtUtc;
        Snapshot = new(new(command.Expectation.Workspace, command.Expectation.AgentId,
            command.Expectation.CatalogRevision.Next()), command.Policy);
    }

    public ReplaceAgentDecisionDataPolicyCommand Command { get; }
    public DateTimeOffset AcceptedAtUtc { get; }
    public AgentDecisionDataPolicySnapshot Snapshot { get; }
}

[JsonConverter(typeof(AgentDecisionDataPolicyReceiptLogJsonConverter))]
public sealed class AgentDecisionDataPolicyReceiptLog {
    public const int MaximumReceipts = 1024;
    public static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    public static AgentDecisionDataPolicyReceiptLog Empty { get; } = new([]);

    public AgentDecisionDataPolicyReceiptLog(ImmutableArray<AgentDecisionDataPolicyReceipt> receipts) {
        if (receipts.IsDefault || receipts.Length > MaximumReceipts || receipts.Any(receipt => receipt is null) ||
            receipts.Select(receipt => receipt.Command.OperationId).Distinct().Count() != receipts.Length) {
            throw new ArgumentException("The Agent policy receipt log requires bounded unique receipts.");
        }
        Receipts = receipts;
    }

    public ImmutableArray<AgentDecisionDataPolicyReceipt> Receipts { get; }

    public AgentDecisionDataPolicyReceiptLog Append(ReplaceAgentDecisionDataPolicyCommand command, DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(command);
        if (Receipts.Any(receipt => receipt.Command.OperationId == command.OperationId)) {
            throw new AgentDecisionDataPolicyReceiptConflictException();
        }
        var retained = Receipts.Where(receipt => IsRetained(receipt, now)).ToImmutableArray();
        if (retained.Length == MaximumReceipts) {
            throw new AgentDecisionDataPolicyReceiptCapacityException();
        }
        return new(retained.Add(new(command, now)));
    }

    public AgentDecisionDataPolicyLookup Find(ReplaceAgentDecisionDataPolicyCommand command, DateTimeOffset now) {
        ArgumentNullException.ThrowIfNull(command);
        var receipt = Receipts.FirstOrDefault(item => item.Command.OperationId == command.OperationId);
        if (receipt is null || !IsRetained(receipt, now)) {
            return new AgentDecisionDataPolicyLookup.NotObserved();
        }
        return receipt.Command == command ? new AgentDecisionDataPolicyLookup.Accepted(receipt)
            : new AgentDecisionDataPolicyLookup.Conflict();
    }

    private static bool IsRetained(AgentDecisionDataPolicyReceipt receipt, DateTimeOffset now)
        => now < receipt.AcceptedAtUtc + Retention;
}

public sealed class AgentDecisionDataPolicyReceiptCapacityException()
    : InvalidOperationException("The Agent policy receipt limit was reached; no retained receipt may be evicted.");

public sealed class AgentDecisionDataPolicyReceiptConflictException()
    : InvalidOperationException("The Agent policy operation identity already has retained evidence.");

public abstract record AgentDecisionDataPolicyLookup {
    private AgentDecisionDataPolicyLookup() { }
    public sealed record Accepted(AgentDecisionDataPolicyReceipt Receipt) : AgentDecisionDataPolicyLookup;
    public sealed record NotObserved : AgentDecisionDataPolicyLookup;
    public sealed record Conflict : AgentDecisionDataPolicyLookup;
    public sealed record ContextChanged : AgentDecisionDataPolicyLookup;
    public sealed record Unavailable(AgentDecisionDataPolicyFailure? Failure = null) : AgentDecisionDataPolicyLookup;
}
