using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace CanDoItAll.Tests.Integration;

internal static class OfflineMaintenanceCommandFixture {
    internal static async Task PersistProfileAsync(IServiceProvider services) {
        var original = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var connection = new NpgsqlConnectionStringBuilder(original.ConnectionString);
        var saved = await services.GetRequiredService<DatabaseProfileControlPlaneService>().SaveAsync(new() {
            Id = original.Profile.Id,
            DisplayName = "Offline recovery child",
            ProviderKind = DatabaseProviderKind.PostgreSql,
            SourceKind = DatabaseProfileSourceKind.PostgresConnection,
            PostgresHost = connection.Host!,
            PostgresPort = connection.Port,
            PostgresDatabaseName = connection.Database!,
            PostgresUsername = connection.Username!,
            PostgresPassword = connection.Password,
            WorkspaceRoot = original.Profile.Storage.WorkspaceRoot
        });
        Assert.True(saved.IsSuccess);
        Assert.Equal(original.Profile.Id, saved.Value);
    }

    internal static Task<JsonElement> RunAsync(TestApplication application,
        ProjectDeletionCompletionAddress address, string action, string? digest = null, int expectedExitCode = 0) {
        List<string> arguments = ["project-deletion-recovery", action, "--profile-id", address.DatabaseProfileId.ToString("D"),
            "--expected-namespace", address.Namespace.Value.ToString("D"), "--completion-id", address.Id.Value.ToString("D")];
        if (digest is not null) {
            arguments.AddRange(["--expected-plan-digest", digest, "--writers-stopped"]);
        }
        return RunAsync(application, address.DatabaseProfileId, arguments, expectedExitCode);
    }

    internal static async Task<JsonElement> RunAsync(TestApplication application,
        Guid profileId, IReadOnlyList<string> arguments, int expectedExitCode = 0) {
        string action = string.Join(' ', arguments.Take(2));
        using var blockedPort = new TcpListener(IPAddress.Loopback, 0);
        blockedPort.Start();
        string root = application.RootPath;
        string record = Path.Combine(root, "offline-maintenance-child.json");
        var start = new ProcessStartInfo("dotnet") {
            WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        string[] prefixes = ["Database__", "ControlPlane__", "Storage__", "SecretVault__", "DataProtection__", "Kestrel__"];
        foreach (string key in start.Environment.Keys.Where(key => prefixes.Any(prefix => key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))).ToArray()) {
            start.Environment.Remove(key);
        }
        foreach (var pair in application.ActiveProfile.CreateEnvironmentVariables()) {
            start.Environment[pair.Key] = pair.Value;
        }
        start.Environment["Database__ConnectionString"] = "Invalid ordinary runtime configuration must not select recovery's profile";
        start.Environment["Database__PasswordFile"] = string.Empty;
        start.Environment["DOTNET_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        start.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{((IPEndPoint)blockedPort.LocalEndpoint).Port}";
        start.Environment["ASPNETCORE_HTTP_PORTS"] = string.Empty;
        start.Environment["ASPNETCORE_HTTPS_PORTS"] = string.Empty;
        start.ArgumentList.Add(IntegrationTestPaths.ResolveProjectOutputAssembly(Path.Combine("App", "CanDoItAll.Web"), "CanDoItAll.Web.dll"));
        foreach (string argument in arguments) {
            start.ArgumentList.Add(argument);
        }
        await RecordAsync("prepared", null, null, null);
        using var child = new Process { StartInfo = start };
        Assert.True(child.Start(), "The owned offline recovery child could not start.");
        DateTime started = child.StartTime.ToUniversalTime();
        var output = DrainRecoveryOutputAsync(child.StandardOutput);
        var error = DrainRecoveryOutputAsync(child.StandardError);
        try {
            await RecordAsync("running", child.Id, started, null);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(90));
            await child.WaitForExitAsync(deadline.Token);
            var stdout = await output;
            var stderr = await error;
            Assert.Equal(0, stdout.Discarded);
            Assert.Equal(0, stderr.Discarded);
            Assert.True(child.ExitCode == expectedExitCode, $"Offline recovery {action} exited {child.ExitCode}; raw child output is suppressed.");
            Assert.DoesNotContain("Now listening on:", stdout.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("Now listening on:", stderr.Text, StringComparison.Ordinal);
            Assert.True(blockedPort.Server.IsBound);
            using var json = JsonDocument.Parse(stdout.Text);
            return json.RootElement.Clone();
        } finally {
            if (!child.HasExited) {
                child.Kill(entireProcessTree: true);
                using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(10));
                await child.WaitForExitAsync(cleanup.Token);
            }
            await Task.WhenAll(output, error);
            await RecordAsync("exited", child.Id, started, child.ExitCode);
            Assert.True(child.HasExited);
        }

        Task RecordAsync(string state, int? processId, DateTime? processStarted, int? exitCode)
            => File.WriteAllTextAsync(record, JsonSerializer.Serialize(new {
                Owner = nameof(OfflineMaintenanceCommandFixture), Action = action, State = state,
                ParentProcessId = Environment.ProcessId, ChildProcessId = processId, ChildStartedUtc = processStarted,
                ExitCode = exitCode, DatabaseProfileId = profileId, Root = root
            }));
    }

    private static async Task<(string Text, long Discarded)> DrainRecoveryOutputAsync(StreamReader stream) {
        const int limit = 64 * 1024;
        var retained = new StringBuilder(limit);
        var buffer = new char[4096];
        long discarded = 0;
        while (true) {
            int read = await stream.ReadAsync(buffer.AsMemory());
            if (read == 0) {
                return (retained.ToString(), discarded);
            }
            int keep = Math.Min(read, limit - retained.Length);
            retained.Append(buffer, 0, keep);
            discarded += read - keep;
        }
    }
}
