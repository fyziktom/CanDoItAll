using System.Globalization;
using System.Text.Json;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.Tests.Playwright.StorageRecovery;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.Circuits;

internal static class RecoveryBrowserEntry {
    public static async Task Main(string[] args) {
        if (args.Length != 3) {
            throw new ArgumentException("Provide the Web content root, owned loopback port and private credential path.");
        }
        await using var app = new RecoveryBrowserApplication(Path.GetFullPath(args[0]));
        app.UseKestrel(int.Parse(args[1], CultureInfo.InvariantCulture));
        using var client = app.CreateClient();
        using var deadline = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        while (!(await client.GetStringAsync("/_dev/runtime", deadline.Token)).Contains("\"isReady\":true", StringComparison.Ordinal)) {
            await Task.Delay(100, deadline.Token);
        }
        var seeds = new[] { await RecoveryBrowserSeed.CreateAsync(app.Services, false), await RecoveryBrowserSeed.CreateAsync(app.Services, true) };
        await using (var credentialScope = app.Services.CreateAsyncScope()) {
            var issued = credentialScope.ServiceProvider.GetRequiredService<IApiTokenService>().IssueToken(new() {
                Subject = "recovery-browser-fixture",
                DisplayName = "Private Recovery proof",
                LifetimeMinutes = 10,
                Scopes = [ApiAccessScopeNames.Api, ApiAccessScopeNames.ReadStoragePlacementRecovery,
                    ApiAccessScopeNames.ReconcileStoragePlacement, ApiAccessScopeNames.WriteProjectStructure]
            });
            await File.WriteAllTextAsync(args[2], issued.Token);
            var readOnly = credentialScope.ServiceProvider.GetRequiredService<IApiTokenService>().IssueToken(new() {
                Subject = "recovery-read-browser-fixture",
                DisplayName = "Private Recovery read proof",
                LifetimeMinutes = 10,
                Scopes = [ApiAccessScopeNames.Api, ApiAccessScopeNames.ReadStoragePlacementRecovery]
            });
            await File.WriteAllTextAsync(args[2] + ".read", readOnly.Token);
        }
        var driver = app.Services.GetRequiredService<RecoveryDriverProbe>();
        driver.Armed = true;
        Console.WriteLine(RecoveryProbeProtocol.Ready + JsonSerializer.Serialize(seeds.Select(seed => seed.Description)));
        while (await Console.In.ReadLineAsync() is { } line) {
            var command = Enum.Parse<RecoveryProbeCommand>(line);
            switch (command) {
                case RecoveryProbeCommand.Verify:
                    var resolutions = driver.Attempts;
                    driver.Armed = false;
                    await using (var scope = app.Services.CreateAsyncScope()) {
                        var proof = new List<RecoveryBrowserProof>();
                        foreach (var seed in seeds) {
                            proof.Add(await RecoveryBrowserSeed.VerifyAsync(scope.ServiceProvider, seed, resolutions));
                        }
                        Console.WriteLine(RecoveryProbeProtocol.Verified + JsonSerializer.Serialize(proof));
                    }
                    break;
                case RecoveryProbeCommand.Stop:
                    return;
                default:
                    throw new ArgumentOutOfRangeException(nameof(command));
            }
        }
    }
}

internal sealed class RecoveryBrowserApplication(string webRoot) : WebApplicationFactory<Program> {
    protected override void ConfigureWebHost(IWebHostBuilder builder) {
        builder.UseEnvironment("Development");
        builder.UseContentRoot(webRoot);
        builder.ConfigureServices(services => {
            foreach (var descriptor in services.Where(descriptor => descriptor.ImplementationType == typeof(ProjectStructureWorkflowDeliveryWorker)).ToArray()) {
                services.Remove(descriptor);
            }
            services.AddSingleton<RecoveryDriverProbe>();
            services.AddSingleton<IStorageDriverRegistry>(provider => provider.GetRequiredService<RecoveryDriverProbe>());
            services.AddScoped<CircuitHandler, RecoveryCircuitProbe>();
        });
    }
}

internal sealed class RecoveryCircuitProbe(AuthenticationStateProvider authentication) : CircuitHandler {
    public override async Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken) {
        var principal = (await authentication.GetAuthenticationStateAsync()).User;
        Console.WriteLine("RECOVERY-CIRCUIT:" + JsonSerializer.Serialize(new {
            principal.Identity?.IsAuthenticated, principal.Identity?.AuthenticationType,
            Scopes = principal.FindAll("scope").Select(claim => claim.Value).ToArray()
        }));
    }
}
