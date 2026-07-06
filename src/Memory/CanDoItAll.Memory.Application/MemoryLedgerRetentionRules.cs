using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public static class MemoryLedgerRetentionRules
{
    public static MemoryLedgerRetentionDecision Evaluate(
        MemoryLedgerRetentionPolicy policy,
        DateTimeOffset nowUtc)
    {
        ArgumentNullException.ThrowIfNull(policy);

        if (nowUtc >= policy.ForgetAtUtc)
        {
            return MemoryLedgerRetentionDecision.Forget;
        }

        return nowUtc >= policy.ExpiresAtUtc
            ? MemoryLedgerRetentionDecision.Expire
            : MemoryLedgerRetentionDecision.Active;
    }
}
