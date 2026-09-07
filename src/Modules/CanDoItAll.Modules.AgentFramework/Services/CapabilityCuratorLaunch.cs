using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;

namespace CanDoItAll.Modules.AgentFramework;

public enum CapabilityCuratorLaunchStatus { Ready, Pending, Opened, Unconfirmed }

public sealed record CapabilityCuratorLaunchSnapshot(Guid AttemptId, CapabilityCuratorLaunchStatus Status, ActiveAgentChat? OpenedChat);

public sealed class CapabilityCuratorLaunch(IAgentChatLauncher launcher) {
    private readonly Lock gate = new();
    private CapabilityCuratorLaunchSnapshot snapshot = new(Guid.Empty, CapabilityCuratorLaunchStatus.Ready, null);
    public CapabilityCuratorLaunchSnapshot Snapshot {
        get {
            lock (gate) {
                return snapshot;
            }
        }
    }
    public CapabilityCuratorLaunchStatus Status => Snapshot.Status;
    public ActiveAgentChat? OpenedChat => Snapshot.OpenedChat;
    public event Action? Changed;

    public bool AcknowledgeUnconfirmed(Guid attemptId) {
        lock (gate) {
            if (snapshot.AttemptId != attemptId || snapshot.Status != CapabilityCuratorLaunchStatus.Unconfirmed) {
                return false;
            }
            snapshot = snapshot with {
                Status = CapabilityCuratorLaunchStatus.Ready
            };
        }
        Changed?.Invoke();
        return true;
    }

    public async Task<bool> OpenAsync(CancellationToken ownerToken = default) {
        Guid attemptId;
        lock (gate) {
            if (ownerToken.IsCancellationRequested || snapshot.Status is CapabilityCuratorLaunchStatus.Pending or CapabilityCuratorLaunchStatus.Unconfirmed) {
                return false;
            }
            attemptId = Guid.NewGuid();
            snapshot = snapshot with { AttemptId = attemptId, Status = CapabilityCuratorLaunchStatus.Pending };
        }
        ActiveAgentChat? chat = null;
        try {
            chat = await launcher.StartNewChatAsync(CapabilityCuratorAgentIdentity.AgentId, CancellationToken.None);
        } catch (Exception) {
        }
        lock (gate) {
            if (snapshot.AttemptId != attemptId) {
                return false;
            }
            snapshot = snapshot with {
                OpenedChat = chat ?? snapshot.OpenedChat,
                Status = chat is null ? CapabilityCuratorLaunchStatus.Unconfirmed : CapabilityCuratorLaunchStatus.Opened
            };
        }
        Changed?.Invoke();
        return true;
    }
}
