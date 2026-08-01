using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.Persistence;

namespace CanDoItAll.Modules.AgentFramework;

internal sealed class DatabaseRuntimeAgentExecutionProfileGenerationSource :
    IAgentExecutionProfileGenerationSource,
    IDisposable
{
    private readonly IDatabaseRuntimeState runtimeState;
    private readonly IDatabaseSwitchNotificationService switchNotifications;
    private readonly IAgentExecutionPreparationCache preparationCache;
    private bool disposed;

    public DatabaseRuntimeAgentExecutionProfileGenerationSource(
        IDatabaseRuntimeState runtimeState,
        IDatabaseSwitchNotificationService switchNotifications,
        IAgentExecutionPreparationCache preparationCache)
    {
        this.runtimeState = runtimeState;
        this.switchNotifications = switchNotifications;
        this.preparationCache = preparationCache;
        switchNotifications.Changed += HandleDatabaseProfileChanged;
    }

    public DatabaseProfileGeneration GetGeneration()
    {
        return new DatabaseProfileGeneration(
            runtimeState.GetSnapshot().Generation);
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        switchNotifications.Changed -= HandleDatabaseProfileChanged;
    }

    private void HandleDatabaseProfileChanged(
        object? sender,
        DatabaseProfileChangedNotification notification)
    {
        preparationCache.InvalidateAll();
    }
}
