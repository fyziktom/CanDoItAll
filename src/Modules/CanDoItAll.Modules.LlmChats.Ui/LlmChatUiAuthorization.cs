namespace CanDoItAll.Modules.LlmChats.Ui;

public enum LlmChatUiPermission
{
    Read,
    Manage,
    Execute
}

public sealed record LlmChatUiAuthorizationSnapshot(
    bool CanRead,
    bool CanManage,
    bool CanExecute);

public interface ILlmChatUiPolicyEvaluator
{
    ValueTask<bool> IsAllowedAsync(
        LlmChatUiPermission permission,
        CancellationToken cancellationToken = default);
}

public interface ILlmChatUiAuthorizationFacade
{
    ValueTask<LlmChatUiAuthorizationSnapshot> GetAsync(
        CancellationToken cancellationToken = default);

    ValueTask<bool> IsAllowedAsync(
        LlmChatUiPermission permission,
        CancellationToken cancellationToken = default);
}

public sealed class LlmChatUiAuthorizationFacade(ILlmChatUiPolicyEvaluator evaluator)
    : ILlmChatUiAuthorizationFacade
{
    public async ValueTask<LlmChatUiAuthorizationSnapshot> GetAsync(
        CancellationToken cancellationToken = default)
        => new(
            await IsAllowedAsync(LlmChatUiPermission.Read, cancellationToken),
            await IsAllowedAsync(LlmChatUiPermission.Manage, cancellationToken),
            await IsAllowedAsync(LlmChatUiPermission.Execute, cancellationToken));

    public ValueTask<bool> IsAllowedAsync(
        LlmChatUiPermission permission,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(permission))
        {
            throw new ArgumentOutOfRangeException(nameof(permission), permission, "Unknown permission.");
        }

        return evaluator.IsAllowedAsync(permission, cancellationToken);
    }
}
