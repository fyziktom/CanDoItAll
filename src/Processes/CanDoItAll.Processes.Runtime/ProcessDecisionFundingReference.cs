using System.Security.Cryptography;
using CanDoItAll.Processes.Abstractions;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Processes.Runtime;

public sealed record ProcessDecisionFundingReference(ProcessRunId RootRunId, DecisionBudgetId BudgetId) {
    public void Validate() {
        if (RootRunId.Value == Guid.Empty || BudgetId.Value == Guid.Empty) {
            throw new InvalidDataException("Process funding requires its original root run and budget.");
        }
    }
}

public static class ProcessDecisionFundingIdentity {
    public static DecisionBudgetAllocationId Derive(DecisionBudgetId parent, DecisionBudgetId child, DecisionBudgetAllocationId allocation) {
        if (parent.Value == Guid.Empty || child.Value == Guid.Empty || allocation.Value == Guid.Empty || parent == child) {
            throw new InvalidDataException("Funded Process work requires distinct original budgets and its local allocation.");
        }
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData("process-decision-funding/v1"u8);
        Append(parent.Value);
        Append(child.Value);
        Append(allocation.Value);
        var digest = hash.GetHashAndReset();
        digest[6] = (byte)((digest[6] & 0x0f) | 0x80);
        digest[8] = (byte)((digest[8] & 0x3f) | 0x80);
        return new(new Guid(digest.AsSpan(0, 16), bigEndian: true));

        void Append(Guid value) {
            Span<byte> bytes = stackalloc byte[16];
            value.TryWriteBytes(bytes, bigEndian: true, out _);
            hash.AppendData(bytes);
        }
    }
}
