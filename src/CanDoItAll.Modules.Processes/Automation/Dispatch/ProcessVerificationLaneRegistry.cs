using CanDoItAll.Processes.Drivers.Abstractions.Gateway;

namespace CanDoItAll.Modules.Processes;

internal sealed class ProcessVerificationLaneRegistry
{
    private readonly IReadOnlyDictionary<ProcessDriverVerificationGatewayLane, ProcessVerificationLaneRegistration> registrations;

    public ProcessVerificationLaneRegistry()
        : this(ProcessDriverVerificationGatewayLaneRules.AllowedLanes.Select(descriptor =>
            new ProcessVerificationLaneRegistration(
                descriptor.Lane,
                descriptor.RequiredScopeKind,
                descriptor.RequiredPermissionMode,
                descriptor.AllowedOperations)))
    {
    }

    internal ProcessVerificationLaneRegistry(IEnumerable<ProcessVerificationLaneRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);

        this.registrations = registrations
            .GroupBy(registration => registration.Lane)
            .ToDictionary(
                group => group.Key,
                group => group.Single(),
                EqualityComparer<ProcessDriverVerificationGatewayLane>.Default);
    }

    public IReadOnlyCollection<ProcessVerificationLaneRegistration> Registrations => registrations.Values.ToArray();

    public bool TryGet(
        ProcessDriverVerificationGatewayLane lane,
        out ProcessVerificationLaneRegistration registration)
    {
        return registrations.TryGetValue(lane, out registration!);
    }
}

internal sealed class ProcessVerificationLaneSelector
{
    private readonly ProcessVerificationLaneRegistry registry;

    public ProcessVerificationLaneSelector(ProcessVerificationLaneRegistry registry)
    {
        this.registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    public ProcessVerificationLaneRegistration Select(ProcessDriverVerificationGatewayLane lane)
    {
        if (!Enum.IsDefined(lane))
        {
            throw new ArgumentOutOfRangeException(nameof(lane), lane, "Unsupported verification lane.");
        }

        if (!registry.TryGet(lane, out var registration))
        {
            throw new InvalidOperationException($"No verification lane registration exists for lane {lane}.");
        }

        return registration;
    }
}
