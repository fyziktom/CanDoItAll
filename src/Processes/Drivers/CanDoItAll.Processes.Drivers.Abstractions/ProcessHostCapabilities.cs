namespace CanDoItAll.Processes.Drivers.Abstractions;

public readonly record struct ProcessHostCapabilityId
{
    public ProcessHostCapabilityId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 96 ||
            value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-')))
        {
            throw new ArgumentException(
                "A process host capability id must be a non-empty token containing only letters, digits, dots, or hyphens.",
                nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    public string Value { get; }

    public static bool TryParse(string? value, out ProcessHostCapabilityId id)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 96 ||
            value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-')))
        {
            id = default;
            return false;
        }

        id = new ProcessHostCapabilityId(value);
        return true;
    }

    public override string ToString() => Value;
}

public readonly record struct ProcessHostProfileId
{
    public ProcessHostProfileId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 64 ||
            value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-')))
        {
            throw new ArgumentException(
                "A process host profile id must be a non-empty token containing only letters, digits, dots, or hyphens.",
                nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public readonly record struct ProcessHostCapabilitySourceId
{
    public ProcessHostCapabilitySourceId(string value)
    {
        if (string.IsNullOrWhiteSpace(value) ||
            value.Length > 64 ||
            value.Any(character =>
                !(char.IsAsciiLetterOrDigit(character) || character is '.' or '-')))
        {
            throw new ArgumentException(
                "A process host capability source id must be a non-empty token containing only letters, digits, dots, or hyphens.",
                nameof(value));
        }

        Value = value.ToLowerInvariant();
    }

    public string Value { get; }

    public override string ToString() => Value;
}

public static class ProcessHostCapabilityIds
{
    public static ProcessHostCapabilityId DirectExecution { get; } = new("host.exec.direct");

    public static ProcessHostCapabilityId ManagedProcessAdapter { get; } = new("host.exec.managed-process-adapter");

    public static ProcessHostCapabilityId PowerShellScript { get; } = new("host.exec.pwsh-script");

    public static ProcessHostCapabilityId PosixScript { get; } = new("host.exec.posix-script");

    public static ProcessHostCapabilityId DotNetRuntime { get; } = new("host.runtime.dotnet");

    public static ProcessHostCapabilityId PythonRuntime { get; } = new("host.runtime.python");

    public static ProcessHostCapabilityId NodeRuntime { get; } = new("host.runtime.node");

    public static ProcessHostCapabilityId NodePackageManager { get; } = new("host.runtime.node-package-manager");

    public static ProcessHostCapabilityId Docker { get; } = new("host.container.docker");

    public static ProcessHostCapabilityId LocalStdioMcp { get; } = new("host.mcp.local-stdio");

    public static ProcessHostCapabilityId DesktopOpen { get; } = new("host.desktop.open");

    public static ProcessHostCapabilityId InteractiveTerminal { get; } = new("host.terminal.interactive");
}

public enum ProcessHostCapabilityAvailability
{
    Available,
    Unavailable,
    Unsupported,
    Unverified
}

public enum ProcessHostCapabilityReason
{
    Ready,
    DependencyMissing,
    NotRegistered,
    DisabledByProfile,
    UnsupportedByProfile,
    InvalidConfiguration,
    PermissionDenied,
    ProbePending,
    TimedOut,
    IoFailure,
    Unavailable,
    ActualHostValidationDeferred
}

public enum ProcessHostExecutionPort
{
    None,
    ManagedProcessHost,
    ManagedProcessAdapter,
    LocalStdioMcpClient,
    DockerHostTool,
    DesktopLauncher,
    InteractiveTerminal
}

public sealed record ProcessHostCapabilityFact(
    ProcessHostCapabilityId Id,
    ProcessHostCapabilityAvailability Availability,
    ProcessHostCapabilityReason Reason,
    ProcessHostExecutionPort ExecutionPort)
{
    public bool IsAvailable => Availability == ProcessHostCapabilityAvailability.Available;

    public bool IsStructurallyValid()
    {
        if (string.IsNullOrWhiteSpace(Id.Value) ||
            !Enum.IsDefined(Availability) ||
            !Enum.IsDefined(Reason) ||
            !Enum.IsDefined(ExecutionPort))
        {
            return false;
        }

        if (IsAvailable)
        {
            return Reason == ProcessHostCapabilityReason.Ready &&
                ExecutionPort != ProcessHostExecutionPort.None &&
                IsExpectedExecutionPort(Id, ExecutionPort);
        }

        return Reason != ProcessHostCapabilityReason.Ready &&
            ExecutionPort == ProcessHostExecutionPort.None;
    }

    private static bool IsExpectedExecutionPort(
        ProcessHostCapabilityId capabilityId,
        ProcessHostExecutionPort executionPort)
    {
        ProcessHostExecutionPort? expected = capabilityId switch
        {
            var id when id == ProcessHostCapabilityIds.DirectExecution ||
                id == ProcessHostCapabilityIds.PowerShellScript ||
                id == ProcessHostCapabilityIds.PosixScript ||
                id == ProcessHostCapabilityIds.DotNetRuntime ||
                id == ProcessHostCapabilityIds.PythonRuntime ||
                id == ProcessHostCapabilityIds.NodeRuntime ||
                id == ProcessHostCapabilityIds.NodePackageManager => ProcessHostExecutionPort.ManagedProcessHost,
            var id when id == ProcessHostCapabilityIds.ManagedProcessAdapter =>
                ProcessHostExecutionPort.ManagedProcessAdapter,
            var id when id == ProcessHostCapabilityIds.Docker => ProcessHostExecutionPort.DockerHostTool,
            var id when id == ProcessHostCapabilityIds.LocalStdioMcp =>
                ProcessHostExecutionPort.LocalStdioMcpClient,
            var id when id == ProcessHostCapabilityIds.DesktopOpen => ProcessHostExecutionPort.DesktopLauncher,
            var id when id == ProcessHostCapabilityIds.InteractiveTerminal =>
                ProcessHostExecutionPort.InteractiveTerminal,
            _ => null
        };
        return expected is null || expected == executionPort;
    }
}

public sealed record ProcessHostCapabilitySnapshot(
    ProcessHostProfileId ProfileId,
    IReadOnlyList<ProcessHostCapabilityFact> Capabilities)
{
    public const int MaximumCapabilities = 32;

    public static ProcessHostCapabilitySnapshot Unknown { get; } = new(new("unknown"), []);

    public bool IsStructurallyValid()
    {
        if (!ProcessStrategyReceiptValuePolicy.IsStableIdentifier(ProfileId.Value) ||
            Capabilities is null ||
            Capabilities.Count > MaximumCapabilities)
        {
            return false;
        }

        var ids = new HashSet<ProcessHostCapabilityId>();
        return Capabilities.All(capability =>
            capability is not null &&
            capability.IsStructurallyValid() &&
            ids.Add(capability.Id));
    }

    public bool TryGet(ProcessHostCapabilityId id, out ProcessHostCapabilityFact? capability)
    {
        capability = null;
        if (!IsStructurallyValid())
        {
            return false;
        }

        capability = Capabilities.FirstOrDefault(item => item.Id == id);
        return capability is not null;
    }

    public bool IsAvailable(ProcessHostCapabilityId id)
        => TryGet(id, out var capability) && capability!.IsAvailable;
}

public interface IProcessHostCapabilitySnapshotProvider
{
    ValueTask<ProcessHostCapabilitySnapshot> GetAsync(CancellationToken cancellationToken = default);
}

public interface IProcessHostCapabilitySource
{
    ProcessHostCapabilitySourceId SourceId { get; }

    IReadOnlySet<ProcessHostCapabilityId> DeclaredCapabilities { get; }

    ValueTask<IReadOnlyList<ProcessHostCapabilityFact>> ProbeAsync(
        CancellationToken cancellationToken = default);
}

public interface IProcessHostProfileSource
{
    ValueTask<ProcessHostProfileId> GetProfileIdAsync(
        CancellationToken cancellationToken = default);
}
