using System.Globalization;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Playwright.StorageCatalog;
using Microsoft.AspNetCore.Mvc.Testing;

internal static class StorageCatalogBrowserEntry {
    public static async Task Main(string[] args) {
        if (args.Length != 2) {
            throw new ArgumentException("Provide the Web content root and owned loopback port.");
        }
        var access = new BrowserAccess();
        await using var app = new StorageCatalogBrowserApplication(Path.GetFullPath(args[0]), access);
        app.UseKestrel(int.Parse(args[1], CultureInfo.InvariantCulture));
        using var client = app.CreateClient();
        Console.WriteLine(StorageCatalogProbeProtocol.Ready);
        while (await Console.In.ReadLineAsync() is { } line) {
            var command = Enum.Parse<StorageCatalogProbeCommand>(line);
            switch (command) {
                case StorageCatalogProbeCommand.DenyApi:
                    access.Allowed = false;
                    break;
                case StorageCatalogProbeCommand.AllowApi:
                    access.Allowed = true;
                    break;
                case StorageCatalogProbeCommand.Stop:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command));
            }
            Console.WriteLine(StorageCatalogProbeProtocol.Ack(command));
        }
    }
}

internal sealed class BrowserAccess : IApiTokenAdministrationAccess {
    public volatile bool Allowed = true;
    public ValueTask<bool> CanManageAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Allowed);
}

internal sealed class StorageCatalogBrowserApplication(string webRoot, BrowserAccess access) : WebApplicationFactory<Program> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(webRoot);
        builder.ConfigureServices(services => services.AddSingleton<IApiTokenAdministrationAccess>(access));
    }
}
