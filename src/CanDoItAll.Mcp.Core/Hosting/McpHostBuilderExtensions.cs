using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace CanDoItAll.Mcp.Core.Hosting;

public static class McpHostBuilderExtensions
{
    public const string EnvironmentVariablePrefix = "CanDoItAllMcp_";

    public static IConfigurationBuilder AddCanDoItAllMcpSettings(this IConfigurationBuilder configuration, string settingsPath)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        if (string.IsNullOrWhiteSpace(settingsPath))
        {
            throw new ArgumentException("Settings path is required.", nameof(settingsPath));
        }

        return configuration
            .AddJsonFile(settingsPath, optional: false, reloadOnChange: false)
            .AddEnvironmentVariables(prefix: EnvironmentVariablePrefix);
    }

    public static ILoggingBuilder ConfigureCanDoItAllMcpStdioLogging(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.ClearProviders();
        logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);
        logging.SetMinimumLevel(LogLevel.Information);
        return logging;
    }

    public static ILoggingBuilder ConfigureCanDoItAllMcpBackendLogging(this ILoggingBuilder logging)
    {
        ArgumentNullException.ThrowIfNull(logging);

        logging.ClearProviders();
        logging.SetMinimumLevel(LogLevel.Information);
        return logging;
    }

    public static IServiceCollection AddValidatedCanDoItAllMcpOptions<TOptions, TValidator>(
        this IServiceCollection services,
        IConfiguration configuration,
        bool validateDataAnnotations = true)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var optionsBuilder = services
            .AddOptions<TOptions>()
            .Bind(configuration);

        if (validateDataAnnotations)
        {
            optionsBuilder.ValidateDataAnnotations();
        }

        optionsBuilder.ValidateOnStart();
        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();
        return services;
    }
}
