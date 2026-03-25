using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CanDoItAll.Mcp.DotNetWatch.Tray;

internal sealed class BackendTrayController : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly TrayOptions _options;
    private readonly HttpClient _httpClient;

    public BackendTrayController(TrayOptions options)
    {
        _options = options;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(5)
        };
    }

    public async Task<BackendTraySnapshot> GetSnapshotAsync()
    {
        var matchingRecords = await ReadMatchingRecordsAsync();
        if (matchingRecords.Count == 0)
        {
            return new BackendTraySnapshot(
                TrayStatusKind.Missing,
                "No backend registered for this workspace",
                "CanDoItAll MCP: no backend for workspace",
                "missing:none",
                "No backend is currently registered for this workspace.",
                ToolTipIcon.Warning,
                [],
                null,
                CanStartOrRecover: true,
                CanRestart: false);
        }

        var candidates = new List<BackendCandidate>(matchingRecords.Count);
        foreach (var record in matchingRecords)
        {
            candidates.Add(await EvaluateCandidateAsync(record));
        }

        var reachable = candidates.Where(static candidate => candidate.IsReachable).ToArray();
        var live = candidates.Where(static candidate => candidate.IsLive).ToArray();
        var primary = reachable.FirstOrDefault() ?? live.FirstOrDefault();

        if (live.Length > 1)
        {
            return new BackendTraySnapshot(
                TrayStatusKind.Duplicate,
                $"Duplicate backends detected ({live.Length})",
                BackendTraySnapshot.TrimNotifyText($"CanDoItAll MCP: duplicate backends ({live.Length})"),
                $"duplicate:{string.Join('|', live.Select(static candidate => candidate.Record.Registration.BackendId))}",
                $"Detected {live.Length} live backends for the same workspace. Use restart to recover a single owner.",
                ToolTipIcon.Warning,
                candidates,
                primary,
                CanStartOrRecover: true,
                CanRestart: true);
        }

        if (reachable.Length == 1)
        {
            var backend = reachable[0].Record.Registration;
            return new BackendTraySnapshot(
                TrayStatusKind.Healthy,
                $"Healthy | PID {backend.ProcessId}",
                BackendTraySnapshot.TrimNotifyText($"CanDoItAll MCP: healthy | PID {backend.ProcessId}"),
                $"healthy:{backend.BackendId}",
                "Backend is healthy again.",
                ToolTipIcon.Info,
                candidates,
                reachable[0],
                CanStartOrRecover: false,
                CanRestart: true);
        }

        if (live.Length == 1)
        {
            var backend = live[0].Record.Registration;
            return new BackendTraySnapshot(
                TrayStatusKind.Unreachable,
                $"Backend unreachable | PID {backend.ProcessId}",
                BackendTraySnapshot.TrimNotifyText($"CanDoItAll MCP: backend unreachable | PID {backend.ProcessId}"),
                $"unreachable:{backend.BackendId}",
                live[0].UnavailableReason ?? "The backend process is alive, but the manager endpoint did not respond.",
                ToolTipIcon.Error,
                candidates,
                live[0],
                CanStartOrRecover: true,
                CanRestart: true);
        }

        return new BackendTraySnapshot(
            TrayStatusKind.Missing,
            "No live backend for this workspace",
            "CanDoItAll MCP: no live backend",
            $"missing:{matchingRecords.Count}",
            "The workspace has catalog entries, but none of them are currently live.",
            ToolTipIcon.Warning,
            candidates,
            null,
            CanStartOrRecover: true,
            CanRestart: false);
    }

    public async Task<BackendTraySnapshot> RecoverAsync(BackendTraySnapshot currentSnapshot, bool forceRestart)
    {
        var requiresStop = forceRestart || currentSnapshot.MatchingBackends.Any(static candidate => candidate.IsLive);
        if (requiresStop)
        {
            await StopMatchingBackendsAsync(currentSnapshot);
        }

        WriteLog($"backend recover start | forceRestart={forceRestart}");
        var shadowDllPath = await PrepareShadowAsync();
        var backendToken = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));

        using var launcher = new Process
        {
            StartInfo = new ProcessStartInfo("dotnet")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _options.RepoRoot
            }
        };

        launcher.StartInfo.ArgumentList.Add(shadowDllPath);
        launcher.StartInfo.ArgumentList.Add("--backend-launcher");
        launcher.StartInfo.ArgumentList.Add("--settings");
        launcher.StartInfo.ArgumentList.Add(_options.SettingsPath);
        launcher.StartInfo.ArgumentList.Add("--backend-token");
        launcher.StartInfo.ArgumentList.Add(backendToken);

        if (!launcher.Start())
        {
            throw new InvalidOperationException("Backend launcher process did not start.");
        }

        await launcher.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));
        if (launcher.ExitCode != 0)
        {
            throw new InvalidOperationException($"Backend launcher exited with code {launcher.ExitCode}.");
        }

        await WaitForReachableBackendAsync();
        return await GetSnapshotAsync();
    }

    public void OpenManagerPage(BackendTraySnapshot snapshot)
    {
        var managerUrl = snapshot.PrimaryBackend?.Record.Registration.ManagerUrl;
        if (string.IsNullOrWhiteSpace(managerUrl))
        {
            return;
        }

        Process.Start(new ProcessStartInfo(managerUrl)
        {
            UseShellExecute = true
        });
    }

    public void OpenLogsFolder()
    {
        Process.Start(new ProcessStartInfo("explorer.exe", _options.WorkspaceLogDirectory)
        {
            UseShellExecute = true
        });
    }

    public void WriteLog(string message)
    {
        try
        {
            var line = $"{DateTimeOffset.UtcNow:O} {message}{Environment.NewLine}";
            File.AppendAllText(_options.TrayLogPath, line, Encoding.UTF8);
        }
        catch
        {
        }
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }

    private async Task<IReadOnlyList<CatalogRecord>> ReadMatchingRecordsAsync()
    {
        if (!Directory.Exists(_options.BackendCatalogDirectory))
        {
            return [];
        }

        var result = new List<CatalogRecord>();
        foreach (var path in Directory.GetFiles(_options.BackendCatalogDirectory, "*.json", SearchOption.TopDirectoryOnly))
        {
            try
            {
                await using var stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                var record = await JsonSerializer.DeserializeAsync<BackendRegistrationRecord>(stream, JsonOptions);
                if (record is null)
                {
                    continue;
                }

                if (!string.Equals(record.Identity.WorkspaceRoot, _options.RepoRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!string.Equals(Path.GetFullPath(record.Identity.SettingsPath), _options.SettingsPath, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!IsLiveProcess(record))
                {
                    TryDeleteCatalogRecord(path, record.BackendId);
                    continue;
                }

                result.Add(new CatalogRecord(path, record));
            }
            catch (Exception ex)
            {
                WriteLog($"catalog read skipped | file={path} | error={ex.Message}");
            }
        }

        return result
            .OrderByDescending(static record => record.Registration.RegisteredUtc)
            .ToArray();
    }

    private async Task<BackendCandidate> EvaluateCandidateAsync(CatalogRecord record)
    {
        var live = IsLiveProcess(record.Registration);
        if (!live)
        {
            return new BackendCandidate(record, IsLive: false, IsReachable: false, "The backend process is no longer running.");
        }

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(new Uri(record.Registration.BaseUrl), "/api/backend/status"));
            request.Headers.Add("X-CanDoItAll-Backend-Token", record.Registration.AuthToken);
            using var response = await _httpClient.SendAsync(request);
            return response.IsSuccessStatusCode
                ? new BackendCandidate(record, IsLive: true, IsReachable: true, null)
                : new BackendCandidate(record, IsLive: true, IsReachable: false, $"Manager endpoint returned HTTP {(int)response.StatusCode}.");
        }
        catch (Exception ex)
        {
            return new BackendCandidate(record, IsLive: true, IsReachable: false, $"Manager endpoint did not respond: {ex.Message}");
        }
    }

    private async Task StopMatchingBackendsAsync(BackendTraySnapshot snapshot)
    {
        foreach (var candidate in snapshot.MatchingBackends.Where(static candidate => candidate.IsLive))
        {
            try
            {
                using var process = Process.GetProcessById(candidate.Record.Registration.ProcessId);
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                    await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(10));
                    WriteLog($"backend killed | pid={candidate.Record.Registration.ProcessId} | backendId={candidate.Record.Registration.BackendId}");
                }
            }
            catch (Exception ex)
            {
                WriteLog($"backend kill skipped | pid={candidate.Record.Registration.ProcessId} | error={ex.Message}");
            }
        }
    }

    private async Task<string> PrepareShadowAsync()
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo("powershell")
            {
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = _options.RepoRoot
            }
        };

        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(_options.WrapperPath);
        process.StartInfo.ArgumentList.Add("-RepoRoot");
        process.StartInfo.ArgumentList.Add(_options.RepoRoot);
        process.StartInfo.ArgumentList.Add("-SettingsPath");
        process.StartInfo.ArgumentList.Add(_options.SettingsPath);
        process.StartInfo.ArgumentList.Add("-PrepareOnly");

        if (!process.Start())
        {
            throw new InvalidOperationException("Could not start the wrapper prepare process.");
        }

        var stdoutTask = process.StandardOutput.ReadToEndAsync();
        var stderrTask = process.StandardError.ReadToEndAsync();

        await process.WaitForExitAsync().WaitAsync(TimeSpan.FromMinutes(3));

        var stdout = await stdoutTask;
        var stderr = await stderrTask;
        WriteLog($"wrapper prepare exit | code={process.ExitCode}");

        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException($"Wrapper prepare failed with code {process.ExitCode}. {stderr}");
        }

        if (!File.Exists(_options.ShadowManifestPath))
        {
            throw new InvalidOperationException($"Shadow manifest '{_options.ShadowManifestPath}' was not created.");
        }

        var manifest = JsonSerializer.Deserialize<ShadowManifest>(await File.ReadAllTextAsync(_options.ShadowManifestPath), JsonOptions)
            ?? throw new InvalidOperationException("Could not read the shadow manifest.");
        if (string.IsNullOrWhiteSpace(manifest.ShadowDllPath) || !File.Exists(manifest.ShadowDllPath))
        {
            throw new InvalidOperationException($"Shadow manifest points to a missing dll. Stdout={stdout}");
        }

        return manifest.ShadowDllPath;
    }

    private async Task WaitForReachableBackendAsync()
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(45);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var snapshot = await GetSnapshotAsync();
            if (snapshot.PrimaryBackend?.IsReachable == true && snapshot.StatusKind == TrayStatusKind.Healthy)
            {
                return;
            }

            await Task.Delay(1000);
        }

        throw new TimeoutException("Timed out waiting for the backend to become reachable.");
    }

    private static bool IsLiveProcess(BackendRegistrationRecord registration)
    {
        try
        {
            using var process = Process.GetProcessById(registration.ProcessId);
            if (process.HasExited)
            {
                return false;
            }

            var startedUtc = process.StartTime.ToUniversalTime();
            return Math.Abs((startedUtc - registration.ProcessStartedUtc).TotalSeconds) <= 60;
        }
        catch
        {
            return false;
        }
    }

    private void TryDeleteCatalogRecord(string path, string backendId)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
                WriteLog($"catalog cleanup | removed stale backend record | backendId={backendId}");
            }
        }
        catch (Exception ex)
        {
            WriteLog($"catalog cleanup skipped | backendId={backendId} | error={ex.Message}");
        }
    }
}
