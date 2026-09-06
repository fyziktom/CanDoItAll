using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public enum CapabilityCuratorLaunchStatus { Ready, Pending, Opened, Unconfirmed }

public sealed class CapabilityCuratorLaunch(IAgentChatLauncher launcher) {
    private readonly Lock gate = new();
    private CapabilityCuratorLaunchStatus status;
    public CapabilityCuratorLaunchStatus Status {
        get {
            lock (gate) {
                return status;
            }
        }
    }
    public ActiveAgentChat? OpenedChat { get; private set; }
    public event Action? Changed;

    public async Task<bool> OpenAsync(CancellationToken ownerToken = default) {
        lock (gate) {
            if (ownerToken.IsCancellationRequested || status is CapabilityCuratorLaunchStatus.Pending or CapabilityCuratorLaunchStatus.Unconfirmed) {
                return false;
            }
            status = CapabilityCuratorLaunchStatus.Pending;
        }
        try {
            var chat = await launcher.StartNewChatAsync(CapabilityCuratorAgentIdentity.AgentId, CancellationToken.None);
            lock (gate) {
                OpenedChat = chat;
                status = chat is null ? CapabilityCuratorLaunchStatus.Unconfirmed : CapabilityCuratorLaunchStatus.Opened;
            }
        } catch (Exception) {
            lock (gate) {
                status = CapabilityCuratorLaunchStatus.Unconfirmed;
            }
        }
        Changed?.Invoke();
        return true;
    }
}
