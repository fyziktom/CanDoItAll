using System.Diagnostics;
using System.Text.Json;

var repoRoot = ResolveRepoRoot();
var shadowServerPath = Path.Combine(repoRoot, ".artifacts", "mcp-server-shadow", "bin", "CanDoItAll.Mcp.DotNetWatch", "debug", "CanDoItAll.Mcp.DotNetWatch.dll");
var settingsPath = Path.Combine(repoRoot, "CanDoItAll.Mcp.DotNetWatch.settings.json");
var registrationPath = Path.Combine(repoRoot, ".mcp-state", "backend", "registration.json");

await CleanupExistingAsync(registrationPath);

using var proxy = new Process
{
    StartInfo = new ProcessStartInfo("dotnet")
    {
        WorkingDirectory = repoRoot,
        RedirectStandardInput = true,
        RedirectStandardOutput = true,
        RedirectStandardError = true,
        UseShellExecute = false
    }
};

proxy.StartInfo.ArgumentList.Add(shadowServerPath);
proxy.StartInfo.ArgumentList.Add("--settings");
proxy.StartInfo.ArgumentList.Add(settingsPath);

if (!proxy.Start())
{
    throw new InvalidOperationException("Failed to start the MCP stdio proxy.");
}

var registration = await WaitForRegistrationAsync(registrationPath, TimeSpan.FromSeconds(30))
    ?? throw new InvalidOperationException("Timed out waiting for backend registration.");

Console.WriteLine($"Proxy PID: {proxy.Id}");
Console.WriteLine($"Backend PID before kill: {registration.ProcessId}");
Console.WriteLine($"Backend alive before kill: {IsAlive(registration.ProcessId)}");

proxy.Kill(entireProcessTree: true);
proxy.WaitForExit();
await Task.Delay(TimeSpan.FromSeconds(3));

var aliveAfterKill = IsAlive(registration.ProcessId);
Console.WriteLine($"Backend alive after proxy kill: {aliveAfterKill}");
Console.WriteLine($"Registration still exists: {File.Exists(registrationPath)}");

return aliveAfterKill ? 0 : 1;

static async Task CleanupExistingAsync(string registrationPath)
{
    var existing = await WaitForRegistrationAsync(registrationPath, TimeSpan.FromMilliseconds(500));
    if (existing is not null)
    {
        TryKill(existing.ProcessId);
    }

    if (File.Exists(registrationPath))
    {
        File.Delete(registrationPath);
    }
}

static async Task<BackendRegistration?> WaitForRegistrationAsync(string registrationPath, TimeSpan timeout)
{
    var deadline = DateTimeOffset.UtcNow.Add(timeout);
    while (DateTimeOffset.UtcNow <= deadline)
    {
        if (File.Exists(registrationPath))
        {
            try
            {
                await using var stream = File.Open(registrationPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var value = await JsonSerializer.DeserializeAsync<BackendRegistration>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web));
                if (value is not null)
                {
                    return value;
                }
            }
            catch
            {
            }
        }

        await Task.Delay(250);
    }

    return null;
}

static bool IsAlive(int pid)
{
    try
    {
        using var process = Process.GetProcessById(pid);
        return !process.HasExited;
    }
    catch
    {
        return false;
    }
}

static void TryKill(int pid)
{
    try
    {
        using var process = Process.GetProcessById(pid);
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit(5000);
        }
    }
    catch
    {
    }
}

static string ResolveRepoRoot()
{
    var directory = new DirectoryInfo(AppContext.BaseDirectory);
    while (directory is not null)
    {
        if (File.Exists(Path.Combine(directory.FullName, "CanDoItAll.slnx")))
        {
            return directory.FullName;
        }

        directory = directory.Parent;
    }

    throw new InvalidOperationException("Could not resolve repository root.");
}

internal sealed record BackendRegistration(string BackendId, int ProcessId, string BaseUrl);
