using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Conversations.Shell;

public static class ConversationShellServiceCollectionExtensions
{
    public static IServiceCollection AddConversationShell(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.TryAddScoped<ConversationShellCoordinator>();
        services.TryAddScoped<IConversationShellCoordinator>(serviceProvider =>
            serviceProvider.GetRequiredService<ConversationShellCoordinator>());
        services.TryAddScoped<IConversationShellLauncher>(serviceProvider =>
            serviceProvider.GetRequiredService<ConversationShellCoordinator>());
        return services;
    }
}
