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
        var result = SelectExact(lane);
        return result.Status switch
        {
            ProcessVerificationLaneSelectionStatus.Selected => result.Registration!,
            ProcessVerificationLaneSelectionStatus.UnsupportedLane => throw new ArgumentOutOfRangeException(
                nameof(lane),
                lane,
                "Unsupported verification lane."),
            ProcessVerificationLaneSelectionStatus.MissingRegistration => throw new InvalidOperationException(
                $"No verification lane registration exists for lane {lane}."),
            _ => throw new InvalidOperationException($"Unsupported verification lane selection status {result.Status}.")
        };
    }

    public ProcessVerificationLaneSelectionResult SelectExact(ProcessDriverVerificationGatewayLane lane)
    {
        if (!Enum.IsDefined(lane))
        {
            return ProcessVerificationLaneSelectionResult.Unsupported(lane);
        }

        return registry.TryGet(lane, out var registration)
            ? ProcessVerificationLaneSelectionResult.Selected(registration)
            : ProcessVerificationLaneSelectionResult.MissingRegistration(lane);
    }
}

internal sealed record ProcessVerificationLaneSelectionResult
{
    private ProcessVerificationLaneSelectionResult(
        ProcessDriverVerificationGatewayLane lane,
        ProcessVerificationLaneSelectionStatus status,
        ProcessVerificationLaneRegistration? registration)
    {
        if (status == ProcessVerificationLaneSelectionStatus.Selected && registration is null)
        {
            throw new ArgumentException("A selected verification lane result requires a registration.", nameof(registration));
        }

        Lane = lane;
        Status = status;
        Registration = registration;
    }

    public ProcessDriverVerificationGatewayLane Lane { get; }

    public ProcessVerificationLaneSelectionStatus Status { get; }

    public ProcessVerificationLaneRegistration? Registration { get; }

    public bool IsSelected => Status == ProcessVerificationLaneSelectionStatus.Selected;

    public static ProcessVerificationLaneSelectionResult Selected(ProcessVerificationLaneRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        return new ProcessVerificationLaneSelectionResult(
            registration.Lane,
            ProcessVerificationLaneSelectionStatus.Selected,
            registration);
    }

    public static ProcessVerificationLaneSelectionResult Unsupported(ProcessDriverVerificationGatewayLane lane)
    {
        return new ProcessVerificationLaneSelectionResult(
            lane,
            ProcessVerificationLaneSelectionStatus.UnsupportedLane,
            registration: null);
    }

    public static ProcessVerificationLaneSelectionResult MissingRegistration(ProcessDriverVerificationGatewayLane lane)
    {
        return new ProcessVerificationLaneSelectionResult(
            lane,
            ProcessVerificationLaneSelectionStatus.MissingRegistration,
            registration: null);
    }
}

internal enum ProcessVerificationLaneSelectionStatus
{
    Selected,
    UnsupportedLane,
    MissingRegistration
}
