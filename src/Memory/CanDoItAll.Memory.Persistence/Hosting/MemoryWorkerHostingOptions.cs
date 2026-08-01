namespace CanDoItAll.Memory.Persistence.Hosting;

public sealed record MemoryWorkerHostingOptions
{
    public static readonly TimeSpan DefaultCycleInterval = TimeSpan.FromSeconds(5);
    public static readonly TimeSpan MinimumCycleInterval = TimeSpan.FromMilliseconds(100);
    public static readonly TimeSpan DefaultLeaseDuration = TimeSpan.FromMinutes(1);
    public static readonly TimeSpan DefaultLeaseRenewalInterval = TimeSpan.FromSeconds(20);

    public MemoryWorkerHostingOptions(
        bool enabled,
        TimeSpan cycleInterval,
        TimeSpan? leaseDuration = null,
        TimeSpan? leaseRenewalInterval = null)
    {
        Enabled = enabled;
        CycleInterval = cycleInterval;
        LeaseDuration = leaseDuration ?? DefaultLeaseDuration;
        LeaseRenewalInterval = leaseRenewalInterval ?? DefaultLeaseRenewalInterval;
    }

    public bool Enabled { get; }

    public TimeSpan CycleInterval { get; }

    public TimeSpan LeaseDuration { get; }

    public TimeSpan LeaseRenewalInterval { get; }

    public static readonly MemoryWorkerHostingOptions Disabled = new(
        enabled: false,
        DefaultCycleInterval);

    public static MemoryWorkerHostingOptions EnabledWithInterval(TimeSpan cycleInterval) =>
        new(enabled: true, cycleInterval);

    public void Validate()
    {
        if (CycleInterval < MinimumCycleInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(CycleInterval),
                $"Memory worker cycle interval must be at least {MinimumCycleInterval}.");
        }

        if (LeaseRenewalInterval <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LeaseRenewalInterval),
                "Memory worker lease renewal interval must be positive.");
        }

        if (LeaseDuration <= LeaseRenewalInterval + LeaseRenewalInterval)
        {
            throw new ArgumentOutOfRangeException(
                nameof(LeaseDuration),
                "Memory worker lease duration must be more than twice the renewal interval.");
        }
    }
}
