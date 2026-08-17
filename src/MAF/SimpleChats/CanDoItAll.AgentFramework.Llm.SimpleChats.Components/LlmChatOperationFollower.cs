using Microsoft.Extensions.Logging;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Ports;
using Operations = CanDoItAll.AgentFramework.Llm.SimpleChats.Operations;

namespace CanDoItAll.AgentFramework.Llm.SimpleChats.Components;

internal enum LlmChatOperationFollowCompletion
{
    Stopped,
    Terminal,
    RecoveryRequired,
    ProfileChanged,
    Failed
}

internal sealed record LlmChatOperationFollowResult(
    LlmChatOperationFollowCompletion Completion,
    IReadOnlyList<LlmChatUiFailure> Failures)
{
    public static LlmChatOperationFollowResult Completed(LlmChatOperationFollowCompletion completion)
        => new(completion, []);
}

internal sealed class LlmChatOperationFollower(
    ILlmChatUiEventSessionGateway sessions,
    ILlmChatOperationProjectionReducer reducer,
    ILogger<LlmChatOperationFollower> logger)
{
    public const int PageSize = 50;

    private static readonly TimeSpan MaximumWait = TimeSpan.FromSeconds(15);

    public async Task<LlmChatOperationFollowResult> FollowAsync(
        Guid operationId,
        Func<LlmChatOperationProjectionState, Task> projectionChanged,
        Func<LlmChatOperationProjectionState, Task> authoritativeRefresh,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(projectionChanged);
        ArgumentNullException.ThrowIfNull(authoritativeRefresh);
        LlmChatUiResult<ILlmChatUiEventSession> opened;
        try
        {
            opened = await sessions.OpenAsync(operationId, cancellationToken);
        }
        catch (LlmChatRuntimeProfileChangedException) when (!cancellationToken.IsCancellationRequested)
        {
            return LlmChatOperationFollowResult.Completed(LlmChatOperationFollowCompletion.ProfileChanged);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return LlmChatOperationFollowResult.Completed(LlmChatOperationFollowCompletion.Stopped);
        }
        catch (Exception exception)
        {
            return UnexpectedFailure(exception, operationId);
        }

        if (!opened.IsSuccess)
        {
            return new(LlmChatOperationFollowCompletion.Failed, opened.Failures);
        }

        await using var session = opened.Value!;
        using var linkedLifetime = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            session.ProfileLifetime);
        var state = LlmChatOperationProjectionState.Initial(operationId);
        try
        {
            while (!linkedLifetime.IsCancellationRequested)
            {
                var page = await session.ReadAsync(
                    state.Cursor,
                    Math.Min(PageSize, session.MaximumPageSize),
                    MaximumWait,
                    linkedLifetime.Token);
                var next = reducer.Reduce(state, page);
                if (!next.RequiresAuthoritativeRefresh)
                {
                    state = next;
                    await projectionChanged(state);
                    continue;
                }

                state = next with
                {
                    ActiveAttemptOrdinal = null,
                    TransientAssistantText = string.Empty,
                    RequiresAuthoritativeRefresh = false
                };
                await projectionChanged(state);
                await authoritativeRefresh(state);
                if (next.IsTerminal)
                {
                    return LlmChatOperationFollowResult.Completed(LlmChatOperationFollowCompletion.Terminal);
                }

                if (next.Status == Operations.LlmChatOperationStatus.RecoveryRequired)
                {
                    return LlmChatOperationFollowResult.Completed(LlmChatOperationFollowCompletion.RecoveryRequired);
                }
            }

            return LlmChatOperationFollowResult.Completed(LlmChatOperationFollowCompletion.Stopped);
        }
        catch (OperationCanceledException) when (
            session.ProfileLifetime.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            return LlmChatOperationFollowResult.Completed(LlmChatOperationFollowCompletion.ProfileChanged);
        }
        catch (LlmChatRuntimeProfileChangedException) when (!cancellationToken.IsCancellationRequested)
        {
            return LlmChatOperationFollowResult.Completed(LlmChatOperationFollowCompletion.ProfileChanged);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return LlmChatOperationFollowResult.Completed(LlmChatOperationFollowCompletion.Stopped);
        }
        catch (Exception exception)
        {
            return UnexpectedFailure(exception, operationId);
        }
    }

    private LlmChatOperationFollowResult UnexpectedFailure(Exception exception, Guid operationId)
    {
        logger.LogError(
            exception,
            "Unable to follow Simple Chat operation events. OperationId={OperationId}.",
            operationId);
        return new(
            LlmChatOperationFollowCompletion.Failed,
            [new(LlmChatUiFailureCodes.RequestFailed, "The Simple Chat response could not be followed.")]);
    }
}
