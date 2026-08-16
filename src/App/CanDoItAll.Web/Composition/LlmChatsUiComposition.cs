using CanDoItAll.Modules.LlmChats.Ui;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Composition;

public static class LlmChatsUiComposition
{
    public static IServiceCollection AddCanDoItAllLlmChatsUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddLlmChatsUi();
        services.AddCascadingAuthenticationState();
        services.TryAddScoped<ILlmChatUiPolicyEvaluator, WebLlmChatUiPolicyEvaluator>();
        return services;
    }
}

internal sealed class WebLlmChatUiPolicyEvaluator(
    IAuthorizationService authorization,
    AuthenticationStateProvider authenticationState,
    IOptions<ApiAccessOptions> apiOptions) : ILlmChatUiPolicyEvaluator
{
    public async ValueTask<bool> IsAllowedAsync(
        LlmChatUiPermission permission,
        CancellationToken cancellationToken = default)
    {
        if (!apiOptions.Value.Authorization.Enabled)
        {
            return true;
        }

        var policy = permission switch
        {
            LlmChatUiPermission.Read => ApiAuthorizationPolicies.ReadLlmChats,
            LlmChatUiPermission.Manage => ApiAuthorizationPolicies.ManageLlmChats,
            LlmChatUiPermission.Execute => ApiAuthorizationPolicies.ExecuteLlmChats,
            _ => throw new ArgumentOutOfRangeException(nameof(permission), permission, "Unknown permission.")
        };
        var state = await authenticationState.GetAuthenticationStateAsync().WaitAsync(cancellationToken);
        var result = await authorization.AuthorizeAsync(state.User, policy);
        return result.Succeeded;
    }
}
