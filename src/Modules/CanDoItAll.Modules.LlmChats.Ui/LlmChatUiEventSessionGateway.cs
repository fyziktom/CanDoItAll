using CanDoItAll.Modules.LlmChats.Application;
using CanDoItAll.Modules.LlmChats.Common;
using CanDoItAll.Modules.LlmChats.Operations;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Modules.LlmChats.Ui;

public sealed record LlmChatUiOperationEventPage(
    Guid OperationId,
    LlmChatOperationStatus Status,
    bool IsTerminal,
    string FailureCode,
    IReadOnlyList<LlmChatOperationEvent> Events,
    long? EarliestRetainedSequence,
    long LatestSequence,
    int TextCharactersThroughCursor = 0);

public interface ILlmChatUiEventSession : IAsyncDisposable
{
    CancellationToken ProfileLifetime { get; }

    int MaximumPageSize { get; }

    ValueTask<LlmChatUiOperationEventPage> ReadAsync(
        long afterSequence,
        int take,
        TimeSpan maximumWait,
        CancellationToken cancellationToken = default);
}

public interface ILlmChatUiEventSessionGateway
{
    ValueTask<LlmChatUiResult<ILlmChatUiEventSession>> OpenAsync(
        Guid operationId,
        CancellationToken cancellationToken = default);
}

public sealed class LlmChatUiEventSessionGateway(
    IServiceScopeFactory scopeFactory,
    ILlmChatUiAuthorizationFacade authorization) : ILlmChatUiEventSessionGateway
{
    public async ValueTask<LlmChatUiResult<ILlmChatUiEventSession>> OpenAsync(
        Guid operationId,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.IsAllowedAsync(LlmChatUiPermission.Read, cancellationToken))
        {
            return LlmChatUiResultMapper.Forbidden<ILlmChatUiEventSession>(LlmChatUiPermission.Read);
        }

        if (operationId == Guid.Empty)
        {
            return LlmChatUiResultMapper.Invalid<ILlmChatUiEventSession>("Select a valid Simple Chat operation.");
        }

        var scope = scopeFactory.CreateAsyncScope();
        try
        {
            var sessions = scope.ServiceProvider.GetRequiredService<ILlmChatOperationEventSessionSource>();
            var result = await sessions.OpenAsync(new(operationId), cancellationToken).ConfigureAwait(false);
            var mapped = LlmChatUiResultMapper.Map(
                result,
                session => (ILlmChatUiEventSession)new LlmChatUiEventSession(session, scope));
            if (mapped.IsFailure)
            {
                await scope.DisposeAsync().ConfigureAwait(false);
            }

            return mapped;
        }
        catch
        {
            await scope.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private sealed class LlmChatUiEventSession(
        ILlmChatOperationEventSession session,
        AsyncServiceScope scope) : ILlmChatUiEventSession
    {
        public CancellationToken ProfileLifetime => session.ProfileLifetime;

        public int MaximumPageSize => session.MaximumPageSize;

        public async ValueTask<LlmChatUiOperationEventPage> ReadAsync(
            long afterSequence,
            int take,
            TimeSpan maximumWait,
            CancellationToken cancellationToken = default)
        {
            var page = await session.ReadAsync(afterSequence, take, maximumWait, cancellationToken)
                .ConfigureAwait(false);
            return new(
                page.Operation.Id.Value,
                page.Operation.Status,
                page.Operation.IsTerminal,
                page.Operation.FailureCode,
                page.Events.ToArray(),
                page.EarliestRetainedSequence,
                page.LatestSequence,
                page.TextCharactersThroughCursor);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await session.DisposeAsync().ConfigureAwait(false);
            }
            finally
            {
                await scope.DisposeAsync().ConfigureAwait(false);
            }
        }
    }
}
