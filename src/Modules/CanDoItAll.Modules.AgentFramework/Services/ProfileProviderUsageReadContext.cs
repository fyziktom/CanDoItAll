using CanDoItAll.AgentFramework.Usage;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.AgentFramework;

public sealed class ProfileProviderUsageReadContext(IDatabaseRuntimeState runtime) : IProviderUsageReadContext {
    private readonly DatabaseRuntimeSnapshot origin = runtime.GetSnapshot();

    public void EnsureCurrent() {
        if (runtime.GetSnapshot() != origin) {
            throw new InvalidOperationException("The database profile changed. Reload usage in the current workspace.");
        }
    }
}
