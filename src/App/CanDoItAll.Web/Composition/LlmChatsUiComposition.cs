using System.Security.Claims;
using CanDoItAll.AgentFramework.Llm.SimpleChats.Components;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Web.Api;
using CanDoItAll.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Web.Composition;

public static class LlmChatsUiComposition
{
    public static IServiceCollection AddCanDoItAllLlmChatsUi(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSimpleChatsComponents();
        services.AddCascadingAuthenticationState();
        services.TryAddScoped<ILlmChatUiPolicyEvaluator, WebLlmChatUiPolicyEvaluator>();
        return services;
    }
}

internal sealed class WebLlmChatUiPolicyEvaluator(
    IAuthorizationService authorization,
    IInteractiveAccessPrincipalProvider interactiveAccessPrincipalProvider,
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
        ClaimsPrincipal principal = await interactiveAccessPrincipalProvider
            .GetCurrentAsync(cancellationToken);
        var result = await authorization.AuthorizeAsync(principal, policy);
        return result.Succeeded;
    }
}
