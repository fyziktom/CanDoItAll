using CanDoItAll.Memory.Application;

namespace CanDoItAll.Memory.Persistence;

public sealed class MemoryModuleOptions
{
    public bool EnableDeterministicMockProvider { get; set; }

    public MemoryAsyncWorkerOptions WorkerOptions { get; set; } = MemoryAsyncWorkerOptions.Default;

    public void Validate()
    {
        WorkerOptions.Validate();
    }
}
