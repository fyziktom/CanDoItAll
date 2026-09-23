using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CanDoItAll.Modules.Workspace.ApiAccess;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;

namespace CanDoItAll.Tests.Playwright;

internal sealed class ApiAccessProductionHost : IAsyncDisposable {
    private readonly CanDoItAllTestEnvironment environment = CanDoItAllTestEnvironment.Create("api-access-production");
    private readonly ConcurrentQueue<string> logs = new();
    private readonly List<string> secrets = [];
    private readonly string signingKey = Secret();
    private readonly string certificatePath;
    private readonly TestDatabaseProfile primary;
    private Process? process;
    private string? previousPassword;
    private string? previousHash;
    private Task[] pumps = [];
    public string HttpUrl { get; } = $"http://127.0.0.1:{ReservePort()}";
    public string HttpsUrl { get; } = $"https://127.0.0.1:{ReservePort()}";
    public string RootPath => environment.RootPath;
    public string ControlPlaneRoot => environment.ControlPlaneRootPath;
    public string CertificatePath => certificatePath;
    public HttpClient Client { get; }

    public ApiAccessProductionHost() {
        primary = environment.CreatePostgreSqlProfile("primary");
        certificatePath = Path.Combine(environment.RootPath, "transport-and-data-protection.pfx");
        using var key = RSA.Create(2048);
        var request = new CertificateRequest("CN=localhost", key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("localhost");
        names.AddIpAddress(IPAddress.Loopback);
        request.CertificateExtensions.Add(names.Build());
        using var certificate = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(4));
        File.WriteAllBytes(certificatePath, certificate.Export(X509ContentType.Pfx));
        if (!OperatingSystem.IsWindows()) {
            File.SetUnixFileMode(certificatePath, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        var thumbprint = certificate.Thumbprint;
        Client = new(new HttpClientHandler {
            AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, presented, _, _) => presented?.Thumbprint == thumbprint
        }) { BaseAddress = new(HttpsUrl), Timeout = TimeSpan.FromSeconds(30) };
        secrets.Add(signingKey);
        secrets.Add(primary.ConnectionString);
    }

    public async Task StartAsync(string adminPassword, bool management = true, bool otherDatabase = false, bool loopbackHttp = true,
        IReadOnlyDictionary<string, string?>? overrides = null) {
        await StopAsync();
        var profile = otherDatabase ? environment.CreatePostgreSqlProfile("secondary") : primary;
        var hash = previousPassword == adminPassword ? previousHash! : await HashWithHelperAsync(adminPassword);
        previousPassword = adminPassword;
        previousHash = hash;
        secrets.Add(adminPassword);
        secrets.Add(hash);
        secrets.Add(profile.ConnectionString);
        var config = new Dictionary<string, string?> {
            ["Api:Enabled"] = "true", ["Api:Authorization:Enabled"] = "true",
            ["Api:Authorization:SigningKey"] = signingKey,
            ["Api:UserAuthentication:Enabled"] = "true",
            ["Api:UserAuthentication:AllowLoopbackHttp"] = loopbackHttp.ToString(),
            ["Api:AccessManagement:Enabled"] = management.ToString(),
            ["Api:BootstrapAdmin:PasswordHash"] = hash,
            ["Api:Authorization:DefaultTokenLifetimeMinutes"] = "480",
            ["Api:Authorization:MaxTokenLifetimeMinutes"] = "1440",
            ["WebHost:HttpsRedirectionEnabled"] = "false",
            ["WebHost:AllowedOrigins:0"] = "https://allowed.example",
            ["SecretVault:Provider"] = "Auto",
            ["SecretVault:AllowInsecureDevelopmentProviders"] = "false",
            ["DataProtection:KeyProtection:Provider"] = "Certificate",
            ["DataProtection:KeyProtection:CertificatePath"] = certificatePath,
            ["Kestrel:Certificates:Default:Path"] = certificatePath,
            ["DevelopmentManager:TuningModeEnabled"] = "false",
            ["Workbench:RuntimePresentation:EnableWindowsTerminal"] = "false",
            [LocalRuntimeHostedWorkerPolicy.LaneKindConfigurationKey] = LocalRuntimeHostedWorkerPolicy.McpToolHostLaneKind,
            ["ControlPlane:StateRootPath"] = Path.Combine(environment.RootPath, "state"),
            ["ControlPlane:LogsRootPath"] = Path.Combine(environment.RootPath, "logs"),
            ["ControlPlane:RuntimeTemporaryRootPath"] = Path.Combine(environment.RootPath, "runtime")
        };
        if (overrides is not null) {
            foreach (var entry in overrides) {
                config[entry.Key] = entry.Value;
            }
        }
        var webRoot = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "src", "App", "CanDoItAll.Web");
        var info = new ProcessStartInfo("dotnet") {
            WorkingDirectory = webRoot, UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true
        };
        info.ArgumentList.Add(Path.Combine(webRoot, "bin", PlaywrightTestHostPaths.BuildConfiguration, "net10.0", "CanDoItAll.Web.dll"));
        info.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        info.Environment["DOTNET_ENVIRONMENT"] = "Production";
        info.Environment["ASPNETCORE_URLS"] = HttpUrl + ";" + HttpsUrl;
        foreach (var pair in profile.CreateEnvironmentVariables(config)) {
            info.Environment[pair.Key] = pair.Value;
        }
        process = Process.Start(info) ?? throw new InvalidOperationException("Production acceptance host did not start.");
        pumps = [PumpAsync(process.StandardOutput), PumpAsync(process.StandardError)];
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        while (!timeout.IsCancellationRequested) {
            if (process.HasExited) {
                throw new InvalidOperationException("Production host exited: " + RedactedLogs());
            }
            try {
                using var response = await Client.GetAsync("/api/access/status", timeout.Token);
                if (response.IsSuccessStatusCode) {
                    return;
                }
            } catch (HttpRequestException) {
            }
            await Task.Delay(300, timeout.Token);
        }
        throw new TimeoutException("Production host was not ready: " + RedactedLogs());
    }

    public async Task StopAsync() {
        if (process is not null) {
            if (!process.HasExited) {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync();
            await Task.WhenAll(pumps);
            process.Dispose();
            process = null;
        }
    }

    public string RedactedLogs() {
        var text = string.Join(Environment.NewLine, logs.TakeLast(80));
        foreach (var secret in secrets) {
            text = text.Replace(secret, "[redacted]", StringComparison.Ordinal);
        }
        return text;
    }

    public void ProtectSecrets(params string[] values) => secrets.AddRange(values);

    public async ValueTask DisposeAsync() {
        await StopAsync();
        var evidence = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "output", "playwright", "api-access");
        Directory.CreateDirectory(evidence);
        await File.WriteAllTextAsync(Path.Combine(evidence, "production-host.log"), RedactedLogs());
        Client.Dispose();
        await environment.DisposeAsync();
    }

    public static string Secret() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(24));

    private async Task PumpAsync(StreamReader reader) {
        while (await reader.ReadLineAsync() is { } line) {
            logs.Enqueue(line);
            if (logs.Count > 500) {
                logs.TryDequeue(out _);
            }
        }
    }

    private static int ReservePort() {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        return ((IPEndPoint)listener.LocalEndpoint).Port;
    }

    private static async Task<string> HashWithHelperAsync(string password) {
        var helper = Path.Combine(PlaywrightTestHostPaths.RepositoryRoot, "tools", "ApiAccess", "ApiPasswordHash", "bin",
            PlaywrightTestHostPaths.BuildConfiguration, "net10.0", "ApiPasswordHash.dll");
        var info = new ProcessStartInfo("dotnet") { UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true };
        info.ArgumentList.Add(helper);
        using var helperProcess = Process.Start(info) ?? throw new InvalidOperationException("The password-hash helper did not start.");
        await helperProcess.StandardInput.WriteLineAsync(password);
        helperProcess.StandardInput.Close();
        var output = await helperProcess.StandardOutput.ReadToEndAsync();
        await helperProcess.WaitForExitAsync();
        var hash = output.Trim();
        if (helperProcess.ExitCode != 0 || !ApiPasswordService.IsSupportedHash(hash, requireCurrentWorkFactor: true)) {
            throw new InvalidOperationException("The password-hash helper failed.");
        }
        return hash;
    }
}
