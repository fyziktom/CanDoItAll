using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.Processes;

public sealed class ProcessRuntimeSession
{
    public ProcessRuntimeSession(IClock clock)
    {
        StartedAtUtc = clock.GetUtcNow();
    }

    public DateTimeOffset StartedAtUtc { get; }
}
