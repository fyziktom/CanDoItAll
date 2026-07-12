using CanDoItAll.Memory.Abstractions;

namespace CanDoItAll.Memory.Application;

public static class MemoryEventAdmissionRules
{
    public static MemoryEventAdmissionResult EvaluateIncoming(
        MemoryEventInboxRecord record,
        IReadOnlyCollection<MemoryEventDedupeKey> knownDedupeKeys,
        MemoryEventLoopGuardPolicy loopGuardPolicy)
    {
        ArgumentNullException.ThrowIfNull(record);
        ArgumentNullException.ThrowIfNull(knownDedupeKeys);
        ArgumentNullException.ThrowIfNull(loopGuardPolicy);

        if (knownDedupeKeys.Contains(record.DedupeKey))
        {
            return MemoryEventAdmissionResult.Rejected(
                MemoryEventAdmissionStatus.Duplicate,
                $"Memory provider event '{record.ProviderEventId}' was already admitted.");
        }

        if (record.LoopContext.HopCount > loopGuardPolicy.MaxHopCount)
        {
            return MemoryEventAdmissionResult.Rejected(
                MemoryEventAdmissionStatus.LoopRejected,
                $"Memory provider event '{record.ProviderEventId}' exceeded loop hop limit {loopGuardPolicy.MaxHopCount}.");
        }

        var providerReentryCount = record.LoopContext.ProviderHops.Count(provider => provider == record.ProviderInstanceId);
        if (providerReentryCount > loopGuardPolicy.MaxProviderReentryCount)
        {
            return MemoryEventAdmissionResult.Rejected(
                MemoryEventAdmissionStatus.LoopRejected,
                $"Memory provider event '{record.ProviderEventId}' exceeded provider re-entry limit {loopGuardPolicy.MaxProviderReentryCount}.");
        }

        return new MemoryEventAdmissionResult(
            MemoryEventAdmissionStatus.Accepted,
            DispatchAllowed: true,
            "Memory provider event accepted.");
    }
}

public sealed record MemoryEventLoopGuardPolicy(
    int MaxHopCount,
    int MaxProviderReentryCount)
{
    public static readonly MemoryEventLoopGuardPolicy Default = new(
        MaxHopCount: 8,
        MaxProviderReentryCount: 2);
}

public sealed record MemoryEventAdmissionResult(
    MemoryEventAdmissionStatus Status,
    bool DispatchAllowed,
    string Diagnostic)
{
    public static MemoryEventAdmissionResult Rejected(
        MemoryEventAdmissionStatus status,
        string diagnostic) =>
        new(status, DispatchAllowed: false, diagnostic);
}
