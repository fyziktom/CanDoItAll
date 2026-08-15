using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Ports;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Modules.LlmChats.Persistence;

public sealed class DatabaseProfileLlmChatRuntimeLeaseFactory(
    ICanonicalRuntimeDatabase canonicalRuntimeDatabase,
    IDatabaseRuntimeState runtimeState,
    IDatabaseSwitchNotificationService notificationService) : ILlmChatRuntimeLeaseFactory
{
    public ValueTask<ILlmChatRuntimeLease> AcquireAsync(CancellationToken cancellationToken = default)
    {
        var canonicalProfile = canonicalRuntimeDatabase.Profile.Profile;
        var identity = new LlmChatRuntimeIdentity(
            canonicalProfile.Id,
            canonicalProfile.Runtime.Fingerprint,
            canonicalRuntimeDatabase.Generation);

        var lease = new DatabaseProfileLlmChatRuntimeLease(
            identity,
            runtimeState,
            notificationService,
            cancellationToken);
        if (lease.EnsureCurrent().IsFailure)
        {
            lease.Dispose();
            throw new LlmChatRuntimeProfileChangedException();
        }

        return ValueTask.FromResult<ILlmChatRuntimeLease>(lease);
    }
}

internal sealed class DatabaseProfileLlmChatRuntimeLease : ILlmChatRuntimeLease
{
    private readonly IDatabaseRuntimeState runtimeState;
    private readonly IDatabaseSwitchNotificationService notificationService;
    private readonly CancellationTokenSource cancellationSource;
    private readonly object lifecycleGate = new();
    private bool disposed;

    public DatabaseProfileLlmChatRuntimeLease(
        LlmChatRuntimeIdentity identity,
        IDatabaseRuntimeState runtimeState,
        IDatabaseSwitchNotificationService notificationService,
        CancellationToken cancellationToken)
    {
        Identity = identity;
        this.runtimeState = runtimeState;
        this.notificationService = notificationService;
        cancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        notificationService.Changed += OnProfileChanged;
    }

    public LlmChatRuntimeIdentity Identity { get; }

    public CancellationToken CancellationToken => cancellationSource.Token;

    public Result EnsureCurrent()
        => LlmChatRuntimeFence.IsCurrent(runtimeState.GetSnapshot(), Identity)
            ? Result.Success()
            : Result.Failure(Error.Failure(
                "The active database profile changed during LLM Chat execution.",
                LlmChatErrorCodes.RuntimeProfileChanged));

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        lock (lifecycleGate)
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            notificationService.Changed -= OnProfileChanged;
            cancellationSource.Dispose();
        }
    }

    private void OnProfileChanged(object? sender, DatabaseProfileChangedNotification notification)
    {
        lock (lifecycleGate)
        {
            if (disposed)
            {
                return;
            }

            cancellationSource.Cancel();
        }
    }
}

internal static class LlmChatRuntimeFence
{
    public static bool IsCurrent(DatabaseRuntimeSnapshot snapshot, LlmChatRuntimeIdentity identity)
        => snapshot.ActiveProfileId == identity.ProfileId &&
           string.Equals(snapshot.ActiveFingerprint, identity.Fingerprint, StringComparison.Ordinal) &&
           snapshot.Generation == identity.Generation;

    public static LlmChatRuntimeIdentity RequireCurrent(
        IDatabaseRuntimeState runtimeState,
        ILlmChatOperationScopeAccessor operationScope)
    {
        var identity = operationScope.Current?.RuntimeIdentity
            ?? throw new InvalidOperationException("An LLM Chat operation scope is required.");
        EnsureCurrent(runtimeState, identity);
        return identity;
    }

    public static void EnsureCurrent(
        IDatabaseRuntimeState runtimeState,
        LlmChatRuntimeIdentity identity)
    {
        if (!IsCurrent(runtimeState.GetSnapshot(), identity))
        {
            throw new LlmChatRuntimeProfileChangedException();
        }
    }
}
