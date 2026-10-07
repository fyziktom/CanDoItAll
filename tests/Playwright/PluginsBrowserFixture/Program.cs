using System.Data.Common;
using System.Globalization;
using CanDoItAll.Modules.Plugins;
using CanDoItAll.Tests.Playwright.Plugins;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;

internal static class PluginsBrowserEntry {
    public static async Task Main(string[] args) {
        if (args.Length != 2) {
            throw new ArgumentException("Provide the Web content root and owned loopback port.");
        }
        var probe = new PluginsReadbackProbe();
        await using var app = new PluginsBrowserApplication(Path.GetFullPath(args[0]), probe);
        app.UseKestrel(int.Parse(args[1], CultureInfo.InvariantCulture));
        using var client = app.CreateClient();
        using var stopping = app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping
            .Register(() => Console.WriteLine(PluginsProbeProtocol.Stopping));
        Console.WriteLine(PluginsProbeProtocol.Ready);
        try {
            while (await Console.In.ReadLineAsync() is { } line) {
                var command = Enum.Parse<PluginsProbeCommand>(line);
                switch (command) {
                    case PluginsProbeCommand.HoldReadback:
                    case PluginsProbeCommand.FailReadback:
                        probe.Arm(command);
                        break;
                    case PluginsProbeCommand.Release:
                        probe.Release();
                        break;
                    case PluginsProbeCommand.Stop:
                        return;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(command));
                }
                Console.WriteLine(PluginsProbeProtocol.Ack(command));
            }
        } finally {
            probe.Release();
        }
    }
}

internal sealed class PluginsBrowserApplication(string webRoot, PluginsReadbackProbe probe) : WebApplicationFactory<Program> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(webRoot);
        builder.ConfigureServices(services => services.AddSingleton<IDbContextFactory<PluginsDbContext>>(provider =>
            new PooledDbContextFactory<PluginsDbContext>(new DbContextOptionsBuilder<PluginsDbContext>(
                provider.GetRequiredService<DbContextOptions<PluginsDbContext>>())
                .AddInterceptors(new PluginsReadbackProbe.SaveBoundary(probe), new PluginsReadbackProbe.ReadBoundary(probe)).Options)));
    }
}

internal sealed class PluginsReadbackProbe {
    private int armed;
    private int nextRead;
    private PluginsProbeCommand mode;
    private TaskCompletionSource released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public void Arm(PluginsProbeCommand command) {
        mode = command;
        released = new(TaskCreationOptions.RunContinuationsAsynchronously);
        Interlocked.Exchange(ref armed, 1);
    }
    public void Release() => released.TrySetResult();

    internal sealed class SaveBoundary(PluginsReadbackProbe probe) : SaveChangesInterceptor {
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (eventData.Context!.ChangeTracker.Entries<PluginConnectionRecord>().Any() && Interlocked.Exchange(ref probe.armed, 0) == 1) {
                Interlocked.Exchange(ref probe.nextRead, 1);
            }
            return ValueTask.FromResult(result);
        }
    }
    internal sealed class ReadBoundary(PluginsReadbackProbe probe) : DbCommandInterceptor {
        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (command.CommandText.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase) &&
                command.CommandText.Contains("\"Plugins_Connections\"", StringComparison.Ordinal) && Interlocked.Exchange(ref probe.nextRead, 0) == 1) {
                if (probe.mode == PluginsProbeCommand.FailReadback) {
                    throw new InvalidOperationException("Controlled Plugins browser read-back failure.");
                }
                Console.WriteLine(PluginsProbeProtocol.ReadHeld);
                await probe.released.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
    }
}
