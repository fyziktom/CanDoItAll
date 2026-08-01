using CanDoItAll.AgentFramework.Core;
using CanDoItAll.AgentFramework.Models;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.SharedKernel.Streaming;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Modules.AgentFramework;

public enum AgentExecutionActivityAccessRejectionReason
{
    DatabaseProfileMismatch,
    DatabaseProfileGenerationMismatch,
    WorkspaceScopeMismatch
}

public sealed class AgentExecutionActivityAccessException(
    AgentExecutionActivityAccessRejectionReason reason,
    string message) : UnauthorizedAccessException(message)
{
    public AgentExecutionActivityAccessRejectionReason Reason { get; } = reason;
}

public interface ICurrentProfileAgentExecutionActivityReader
{
    ISequencedStreamReader<AgentExecutionActivity> OpenReader(
        AgentExecutionOperationId operationId,
        StreamSequence fromInclusive);
}

internal sealed partial class CurrentProfileAgentExecutionActivityReader(
    AgentExecutionActivityCoordinator reader,
    IDatabaseProfileRuntimeAccessor databaseProfileRuntimeAccessor,
    IAgentExecutionProfileGenerationSource executionProfileGenerationSource,
    IDatabaseSwitchNotificationService databaseSwitchNotificationService,
    ILogger<CurrentProfileAgentExecutionActivityReader> logger)
    : IAgentExecutionActivityReader,
      ICurrentProfileAgentExecutionActivityReader
{
    public ISequencedStreamReader<AgentExecutionActivity> OpenReader(
        AgentExecutionOperationId operationId,
        StreamSequence fromInclusive)
    {
        var currentProfile = databaseProfileRuntimeAccessor.ResolveCurrentProfile();
        var currentProfileGeneration = executionProfileGenerationSource.GetGeneration();
        var streamId = new AgentExecutionActivityStreamId(
            currentProfile.Profile.Id,
            WorkspaceScopeDescriptor.Organization(currentProfile.Profile.Id.ToString("N")),
            currentProfileGeneration,
            operationId);
        return OpenReader(streamId, fromInclusive);
    }

    public ISequencedStreamReader<AgentExecutionActivity> OpenReader(
        AgentExecutionActivityStreamId streamId,
        StreamSequence fromInclusive)
    {
        ArgumentNullException.ThrowIfNull(streamId);
        var currentProfile = databaseProfileRuntimeAccessor.ResolveCurrentProfile();
        var currentProfileGeneration =
            executionProfileGenerationSource.GetGeneration();
        AgentExecutionActivityAccessPolicy.EnsureAuthorized(
            streamId,
            currentProfile.Profile.Id,
            currentProfileGeneration);

        var profileLifetime = new ProfileReadLifetime(
            streamId.DatabaseProfileId,
            streamId.DatabaseProfileGeneration,
            databaseSwitchNotificationService,
            logger);
        try
        {
            AgentExecutionActivityAccessPolicy.EnsureAuthorized(
                streamId,
                databaseProfileRuntimeAccessor.ResolveCurrentProfile().Profile.Id,
                executionProfileGenerationSource.GetGeneration());
            return new ProfileBoundReader(
                reader.OpenReader(streamId, fromInclusive),
                profileLifetime);
        }
        catch
        {
            profileLifetime.Dispose();
            throw;
        }
    }
}

internal static class AgentExecutionActivityAccessPolicy
{
    public static void EnsureAuthorized(
        AgentExecutionActivityStreamId streamId,
        Guid currentProfileId,
        DatabaseProfileGeneration currentProfileGeneration)
    {
        if (streamId.DatabaseProfileId != currentProfileId)
        {
            throw new AgentExecutionActivityAccessException(
                AgentExecutionActivityAccessRejectionReason.DatabaseProfileMismatch,
                "The activity stream belongs to a different database profile.");
        }

        if (streamId.DatabaseProfileGeneration != currentProfileGeneration)
        {
            throw new AgentExecutionActivityAccessException(
                AgentExecutionActivityAccessRejectionReason.DatabaseProfileGenerationMismatch,
                "The activity stream belongs to a different database profile lifetime.");
        }

        var expectedScope = WorkspaceScopeDescriptor.Organization(
            currentProfileId.ToString("N"));
        if (streamId.WorkspaceScope != expectedScope)
        {
            throw new AgentExecutionActivityAccessException(
                AgentExecutionActivityAccessRejectionReason.WorkspaceScopeMismatch,
                "The activity stream is outside the current agent workspace scope.");
        }
    }
}

internal sealed partial class CurrentProfileAgentExecutionActivityReader
{
    private sealed class ProfileBoundReader :
        ISequencedStreamReader<AgentExecutionActivity>
    {
        private readonly ISequencedStreamReader<AgentExecutionActivity> innerReader;
        private readonly ProfileReadLifetime profileLifetime;
        private int disposed;

        public ProfileBoundReader(
            ISequencedStreamReader<AgentExecutionActivity> innerReader,
            ProfileReadLifetime profileLifetime)
        {
            this.innerReader = innerReader;
            this.profileLifetime = profileLifetime;
        }

        public StreamSequence NextSequence => innerReader.NextSequence;

        public async ValueTask<SequencedStreamReadResult<AgentExecutionActivity>> ReadAsync(
            CancellationToken cancellationToken = default)
        {
            ObjectDisposedException.ThrowIf(
                Volatile.Read(ref disposed) != 0,
                this);
            using var linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken,
                profileLifetime.CancellationToken);
            return await innerReader.ReadAsync(linkedCancellation.Token).ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            profileLifetime.Dispose();
            await innerReader.DisposeAsync().ConfigureAwait(false);
        }
    }

    private sealed class ProfileReadLifetime : IDisposable
    {
        private readonly Guid profileId;
        private readonly DatabaseProfileGeneration profileGeneration;
        private readonly IDatabaseSwitchNotificationService databaseSwitchNotificationService;
        private readonly ILogger logger;
        private readonly CancellationTokenSource cancellation = new();
        private int disposed;

        public ProfileReadLifetime(
            Guid profileId,
            DatabaseProfileGeneration profileGeneration,
            IDatabaseSwitchNotificationService databaseSwitchNotificationService,
            ILogger logger)
        {
            this.profileId = profileId;
            this.profileGeneration = profileGeneration;
            this.databaseSwitchNotificationService = databaseSwitchNotificationService;
            this.logger = logger;
            databaseSwitchNotificationService.Changed += HandleProfileChanged;
        }

        public CancellationToken CancellationToken => cancellation.Token;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            databaseSwitchNotificationService.Changed -= HandleProfileChanged;
            CancelForProfileChange();
        }

        private void HandleProfileChanged(
            object? sender,
            DatabaseProfileChangedNotification notification)
        {
            if (notification.CurrentProfileId != profileId ||
                notification.Generation != profileGeneration.Value)
            {
                Dispose();
            }
        }

        private void CancelForProfileChange()
        {
            try
            {
                cancellation.Cancel();
            }
            catch (Exception exception)
            {
                logger.LogError(
                    exception,
                    "Cancelling an agent activity reader for database profile {DatabaseProfileId} failed.",
                    profileId);
            }
        }
    }
}
