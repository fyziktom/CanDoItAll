using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace CanDoItAll.Tests.Integration;

public sealed class WebHostLoggingConfigurationTests
{
    [Fact]
    public void Default_logging_configuration_disables_only_event_log_provider()
    {
        var appSettingsPath = Path.Combine(
            IntegrationTestPaths.RepositoryRoot,
            "src",
            "App",
            "CanDoItAll.Web",
            "appsettings.json");
        var configuration = new ConfigurationBuilder()
            .AddJsonFile(appSettingsPath, optional: false, reloadOnChange: false)
            .Build();
        var eventLogProvider = new RecordingEventLogLoggerProvider();
        var otherProvider = new RecordingLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(logging =>
        {
            logging.AddConfiguration(configuration.GetSection("Logging"));
            logging.AddProvider(eventLogProvider);
            logging.AddProvider(otherProvider);
        });

        using var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<WebHostLoggingConfigurationTests>>();

        logger.LogWarning("Host logging policy probe.");

        Assert.Equal(0, eventLogProvider.EntryCount);
        Assert.Equal(1, otherProvider.EntryCount);
    }

    [ProviderAlias("EventLog")]
    private sealed class RecordingEventLogLoggerProvider : RecordingLoggerProvider
    {
    }

    private class RecordingLoggerProvider : ILoggerProvider
    {
        private int _entryCount;

        public int EntryCount => _entryCount;

        public ILogger CreateLogger(string categoryName)
        {
            return new RecordingLogger(() => Interlocked.Increment(ref _entryCount));
        }

        public void Dispose()
        {
        }
    }

    private sealed class RecordingLogger(Action recordEntry) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            recordEntry();
        }
    }
}
