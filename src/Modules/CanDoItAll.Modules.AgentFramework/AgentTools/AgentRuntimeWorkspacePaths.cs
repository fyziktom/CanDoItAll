using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Tooling;

namespace CanDoItAll.Modules.AgentFramework;

internal static class AgentRuntimeWorkspacePaths
{
    // A run's workspace tools map managed roots such as artifacts/ into the run's active scope, so a provider tool
    // that reads or writes the same files resolves them there as well. Without an active scope the host owner stays.
    public static IWorkspacePathResolutionService ForRun(
        IWorkspacePathResolutionService hostPaths,
        AgentRuntimeToolProviderContext context)
    {
        ArgumentNullException.ThrowIfNull(hostPaths);
        ArgumentNullException.ThrowIfNull(context);

        return context.ContextIntent.WorkspaceScope is { } activeScope
            ? hostPaths.ForScope(activeScope)
            : hostPaths;
    }
}
