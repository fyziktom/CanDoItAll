namespace CanDoItAll.Web.Infrastructure;

public static class InteractiveServerServiceCollectionExtensions
{
    public const long MaximumReceiveMessageBytes = 40L * 1024L * 1024L;

    public static IServiceCollection AddCanDoItAllInteractiveServer(
        this IServiceCollection services,
        bool detailedErrors)
    {
        services.AddRazorComponents()
            .AddInteractiveServerComponents(options => options.DetailedErrors = detailedErrors)
            .AddHubOptions(options => options.MaximumReceiveMessageSize = MaximumReceiveMessageBytes);

        return services;
    }
}
