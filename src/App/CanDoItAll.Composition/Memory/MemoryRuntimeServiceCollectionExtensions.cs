using CanDoItAll.Memory.Http;
using CanDoItAll.Memory.Mcp;
using CanDoItAll.Memory.Mock;
using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Memory.Persistence;
using CanDoItAll.Memory.Persistence.Hosting;
using CanDoItAll.Modules.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Composition.Memory;

public static class MemoryRuntimeServiceCollectionExtensions
{
    private const string DeterministicMockSection = "Memory:Providers:DeterministicMock";
    private const string HttpSection = "Memory:Providers:Http";
    private const string McpSection = "Memory:Providers:Mcp";
    private const string NativeRemoteSection = "Memory:Providers:NativeRemote";
    private const string BackgroundWorkersSection = "Memory:BackgroundWorkers";
    private const string EnabledKey = "Enabled";
    private const string ClientNameKey = "ClientName";
    private const string DefaultTimeoutKey = "DefaultTimeout";
    private const string MaxRetryAttemptsKey = "MaxRetryAttempts";
    private const string MaximumResponseBytesKey = "MaximumResponseBytes";

    public static IServiceCollection AddCanDoItAllMemory(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddGenericMemoryModule(options =>
        {
            options.WorkerHosting = ReadWorkerHostingOptions(configuration);
        });

        if (IsEnabled(configuration, DeterministicMockSection))
        {
            services.AddDeterministicMockMemoryProviderDriver();
        }

        AddConfiguredProviderDrivers(services, configuration);
        services.AddMemoryUiModule();
        return services;
    }

    private static void AddConfiguredProviderDrivers(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var httpSection = configuration.GetSection(HttpSection);
        if (IsEnabled(configuration, HttpSection))
        {
            services.AddHttpMemoryProviderDriver(options =>
            {
                BindHttpOptions(options, httpSection);
            });
        }

        var nativeRemoteSection = configuration.GetSection(NativeRemoteSection);
        if (IsEnabled(configuration, NativeRemoteSection))
        {
            services.AddNativeRemoteMemoryProviderDriver(options =>
            {
                BindNativeRemoteOptions(options, nativeRemoteSection);
            });
        }

        var mcpSection = configuration.GetSection(McpSection);
        if (IsEnabled(configuration, McpSection))
        {
            services.AddMcpMemoryProviderDriver(options =>
            {
                var timeout = mcpSection.GetValue<TimeSpan?>(DefaultTimeoutKey);
                if (timeout.HasValue)
                {
                    options.DefaultTimeout = timeout.Value;
                }

                BindResponseSizeLimit(
                    mcpSection,
                    limit => options.ResponseSizeLimit = limit);
            });
        }
    }

    private static MemoryWorkerHostingOptions ReadWorkerHostingOptions(
        IConfiguration configuration)
    {
        var section = configuration.GetSection(BackgroundWorkersSection);
        var enabled = section.GetValue<bool?>(nameof(MemoryWorkerHostingOptions.Enabled)) is true;
        var cycleInterval = section.GetValue<TimeSpan?>(nameof(MemoryWorkerHostingOptions.CycleInterval))
            ?? MemoryWorkerHostingOptions.DefaultCycleInterval;
        var leaseDuration = section.GetValue<TimeSpan?>(nameof(MemoryWorkerHostingOptions.LeaseDuration))
            ?? MemoryWorkerHostingOptions.DefaultLeaseDuration;
        var leaseRenewalInterval = section.GetValue<TimeSpan?>(nameof(MemoryWorkerHostingOptions.LeaseRenewalInterval))
            ?? MemoryWorkerHostingOptions.DefaultLeaseRenewalInterval;
        return new MemoryWorkerHostingOptions(
            enabled,
            cycleInterval,
            leaseDuration,
            leaseRenewalInterval);
    }

    private static bool IsEnabled(
        IConfiguration configuration,
        string sectionName)
    {
        var section = configuration.GetSection(sectionName);
        return section.Exists() &&
               (section.GetValue<bool?>(EnabledKey) ?? false);
    }

    private static void BindHttpOptions(
        HttpMemoryProviderOptions options,
        IConfigurationSection section)
    {
        BindCommonHttpOptions(
            section,
            clientName => options.ClientName = clientName,
            timeout => options.DefaultTimeout = timeout,
            attempts => options.MaxRetryAttempts = attempts,
            limit => options.ResponseSizeLimit = limit);
    }

    private static void BindNativeRemoteOptions(
        NativeRemoteMemoryProviderOptions options,
        IConfigurationSection section)
    {
        BindCommonHttpOptions(
            section,
            clientName => options.ClientName = clientName,
            timeout => options.DefaultTimeout = timeout,
            attempts => options.MaxRetryAttempts = attempts,
            limit => options.ResponseSizeLimit = limit);
    }

    private static void BindCommonHttpOptions(
        IConfigurationSection section,
        Action<string> setClientName,
        Action<TimeSpan> setDefaultTimeout,
        Action<int> setMaxRetryAttempts,
        Action<MemoryProviderResponseSizeLimit> setResponseSizeLimit)
    {
        var clientName = section[ClientNameKey];
        if (!string.IsNullOrWhiteSpace(clientName))
        {
            setClientName(clientName.Trim());
        }

        var timeout = section.GetValue<TimeSpan?>(DefaultTimeoutKey);
        if (timeout.HasValue)
        {
            setDefaultTimeout(timeout.Value);
        }

        var retryAttempts = section.GetValue<int?>(MaxRetryAttemptsKey);
        if (retryAttempts.HasValue)
        {
            setMaxRetryAttempts(retryAttempts.Value);
        }

        BindResponseSizeLimit(section, setResponseSizeLimit);
    }

    private static void BindResponseSizeLimit(
        IConfigurationSection section,
        Action<MemoryProviderResponseSizeLimit> setResponseSizeLimit)
    {
        var maximumResponseBytes = section.GetValue<long?>(MaximumResponseBytesKey);
        if (maximumResponseBytes.HasValue)
        {
            setResponseSizeLimit(new MemoryProviderResponseSizeLimit(maximumResponseBytes.Value));
        }
    }
}
