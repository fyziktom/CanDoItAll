using CanDoItAll.Memory.Application;
using CanDoItAll.Memory.Persistence.Hosting;

namespace CanDoItAll.Memory.Persistence;

public sealed class MemoryModuleOptions
{
    public MemoryAsyncWorkerOptions WorkerOptions { get; set; } = MemoryAsyncWorkerOptions.Default;

    public MemoryWorkerHostingOptions WorkerHosting { get; set; } = MemoryWorkerHostingOptions.Disabled;

    public void Validate()
    {
        WorkerOptions.Validate();
        WorkerHosting.Validate();
    }
}
