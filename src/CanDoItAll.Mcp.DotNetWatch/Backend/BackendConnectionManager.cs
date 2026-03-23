using CanDoItAll.Mcp.DotNetWatch.Configuration;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace CanDoItAll.Mcp.DotNetWatch.Backend;

internal sealed class BackendConnectionManager(
    RuntimeConfiguration configuration,
    BackendIdentityProvider identityProvider,
    BackendRegistrationStore registrationStore,
    BackendProcessLauncher processLauncher,
    ILogger<BackendConnectionManager> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly SemaphoreSlim _gate = new(1, 1);
    private BackendConnectionInfo? _currentConnection;

    public BackendConnectionInfo GetRequiredConnection()
        => _currentConnection ?? throw new InvalidOperationException("Backend connection has not been initialized.");

    public async Task<BackendConnectionInfo> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_currentConnection is not null && await IsRegistrationUsableAsync(_currentConnection.Registration, cancellationToken))
            {
                return _currentConnection;
            }

            await using var launchLock = await AcquireLaunchLockAsync(cancellationToken);

            var existing = await registrationStore.ReadAsync(cancellationToken);
            if (existing is not null && await IsRegistrationUsableAsync(existing, cancellationToken))
            {
                _currentConnection = new BackendConnectionInfo(existing);
                return _currentConnection;
            }

            if (existing is not null)
            {
                TryKillRegisteredProcess(existing.ProcessId);
                registrationStore.Delete();
            }

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            processLauncher.StartDetached(token);

            var deadline = DateTimeOffset.UtcNow.Add(configuration.BackendStartupTimeout);
            while (DateTimeOffset.UtcNow <= deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var registration = await registrationStore.ReadAsync(cancellationToken);
                if (registration is not null &&
                    string.Equals(registration.AuthToken, token, StringComparison.Ordinal) &&
                    await IsRegistrationUsableAsync(registration, cancellationToken))
                {
                    _currentConnection = new BackendConnectionInfo(registration);
                    return _currentConnection;
                }

                await Task.Delay(configuration.BackendStartupPollInterval, cancellationToken);
            }

            var observedRegistration = await registrationStore.ReadAsync(CancellationToken.None);
            throw new TimeoutException(CreateStartupTimeoutMessage(token, observedRegistration));
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<bool> IsRegistrationUsableAsync(BackendRegistrationRecord registration, CancellationToken cancellationToken)
    {
        if (!registrationStore.IsLiveProcess(registration))
        {
            return false;
        }

        if (!identityProvider.Matches(registration.Identity))
        {
            logger.LogInformation(
                "Ignoring backend registration because identity does not match. RegisteredSettings={RegisteredSettings}, CurrentSettings={CurrentSettings}",
                registration.Identity.SettingsPath,
                identityProvider.Current.SettingsPath);
            return false;
        }

        try
        {
            using var client = CreateClient(registration);
            var response = await client.GetAsync("/api/backend/ping", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var ping = await response.Content.ReadFromJsonAsync<BackendPingResponse>(JsonOptions, cancellationToken);
            return ping is not null && identityProvider.Matches(ping.Identity);
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Backend registration at {BaseUrl} did not answer ping.", registration.BaseUrl);
            return false;
        }
    }

    private HttpClient CreateClient(BackendRegistrationRecord registration)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(registration.BaseUrl, UriKind.Absolute),
            Timeout = TimeSpan.FromSeconds(5)
        };
        client.DefaultRequestHeaders.Add(BackendAuth.HeaderName, registration.AuthToken);
        return client;
    }

    private async Task<FileStream> AcquireLaunchLockAsync(CancellationToken cancellationToken)
    {
        var deadline = DateTimeOffset.UtcNow.Add(configuration.BackendStartupTimeout);
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    configuration.BackendLaunchLockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None);
            }
            catch (IOException) when (DateTimeOffset.UtcNow <= deadline)
            {
                await Task.Delay(configuration.BackendStartupPollInterval, cancellationToken);
            }
        }
    }

    private void TryKillRegisteredProcess(int processId)
    {
        try
        {
            using var process = Process.GetProcessById(processId);
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                process.WaitForExit((int)configuration.ForceKillAfter.TotalMilliseconds);
            }
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to kill incompatible backend PID {Pid}", processId);
        }
    }

    private string CreateStartupTimeoutMessage(string expectedToken, BackendRegistrationRecord? observedRegistration)
    {
        var details = new List<string>
        {
            $"registrationPath='{registrationStore.RegistrationPath}'",
            $"launchLockPath='{configuration.BackendLaunchLockPath}'",
            $"workspaceRoot='{configuration.WorkspaceRoot}'",
            $"serverAssemblyPath='{configuration.ServerAssemblyPath}'"
        };

        if (observedRegistration is null)
        {
            details.Add("observedRegistration=<none>");
        }
        else
        {
            details.Add($"observedBackendId='{observedRegistration.BackendId}'");
            details.Add($"observedProcessId={observedRegistration.ProcessId}");
            details.Add($"observedBaseUrl='{observedRegistration.BaseUrl}'");
            details.Add($"observedSettingsPath='{observedRegistration.Identity.SettingsPath}'");
            details.Add($"observedWorkspaceRoot='{observedRegistration.Identity.WorkspaceRoot}'");
            details.Add($"observedBinaryVersionMarker='{observedRegistration.Identity.BinaryVersionMarker}'");
            details.Add($"observedTokenMatchesExpected={string.Equals(observedRegistration.AuthToken, expectedToken, StringComparison.Ordinal)}");
            details.Add($"observedProcessLive={registrationStore.IsLiveProcess(observedRegistration)}");
        }

        return $"Timed out waiting for backend daemon registration. {string.Join("; ", details)}.";
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
