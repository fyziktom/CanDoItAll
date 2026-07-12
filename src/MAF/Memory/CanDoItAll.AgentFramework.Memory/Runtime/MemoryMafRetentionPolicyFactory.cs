using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.AgentFramework.Memory;

public static class MemoryMafRetentionPolicyFactory
{
    private const int ExpiresAfterDays = 7;
    private const int ForgetsAfterDays = 30;

    public static MemoryLedgerRetentionPolicy Create(TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var now = timeProvider.GetUtcNow();
        return MemoryLedgerRetentionPolicy.Expiring(
            now.AddDays(ExpiresAfterDays),
            now.AddDays(ForgetsAfterDays));
    }
}
