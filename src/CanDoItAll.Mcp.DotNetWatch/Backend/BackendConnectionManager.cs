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

    public BackendConnectionInfo? TryGetCurrentConnection()
        => _currentConnection;

    public async Task<BackendConnectionInfo> EnsureReadyAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_currentConnection is not null)
            {
                var currentCompatibility = await GetRegistrationCompatibilityAsync(_currentConnection.Registration, cancellationToken);
                if (TryUseRegistration(_currentConnection.Registration, currentCompatibility))
                {
                    return _currentConnection!;
                }

                _currentConnection = null;
            }

            await using var launchLock = await AcquireLaunchLockAsync(cancellationToken);

            var existing = await registrationStore.ReadAsync(cancellationToken);
            if (existing is not null)
            {
                var existingCompatibility = await GetRegistrationCompatibilityAsync(existing, cancellationToken);
                if (TryUseRegistration(existing, existingCompatibility))
                {
                    return _currentConnection!;
                }

                switch (existingCompatibility)
                {
                    case RegistrationCompatibility.ConflictingOwner:
                        throw new InvalidOperationException(CreateOwnershipConflictMessage(existing));
                    case RegistrationCompatibility.UnreachableOwner:
                        TryKillRegisteredProcess(existing.ProcessId);
                        registrationStore.Delete();
                        break;
                    case RegistrationCompatibility.NotLive:
                    case RegistrationCompatibility.None:
                        registrationStore.Delete();
                        break;
                }
            }

            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            processLauncher.StartDetached(token);

            var deadline = DateTimeOffset.UtcNow.Add(configuration.BackendStartupTimeout);
            while (DateTimeOffset.UtcNow <= deadline)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var registration = await registrationStore.ReadAsync(cancellationToken);
                if (registration is not null)
                {
                    var registrationCompatibility = await GetRegistrationCompatibilityAsync(registration, cancellationToken);
                    if (TryUseRegistration(registration, registrationCompatibility))
                    {
                        return _currentConnection!;
                    }

                    switch (registrationCompatibility)
                    {
                        case RegistrationCompatibility.ConflictingOwner:
                            throw new InvalidOperationException(CreateOwnershipConflictMessage(registration));
                        case RegistrationCompatibility.NotLive:
                        case RegistrationCompatibility.None:
                            registrationStore.Delete();
                            break;
                    }
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

    public async Task<bool> TryRepairAsync(CancellationToken cancellationToken)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            _currentConnection = null;
        }
        finally
        {
            _gate.Release();
        }

        try
        {
            await EnsureReadyAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Backend connection repair failed.");
            return false;
        }
    }

    private bool TryUseRegistration(BackendRegistrationRecord registration, RegistrationCompatibility compatibility)
    {
        if (compatibility is RegistrationCompatibility.Exact)
        {
            _currentConnection = new BackendConnectionInfo(registration);
            return true;
        }

        if (compatibility is RegistrationCompatibility.Adoptable)
        {
            logger.LogInformation(
                "Adopting a live backend with matching workspace/settings but a different binary marker. BackendId={BackendId}, ProcessId={ProcessId}",
                registration.BackendId,
                registration.ProcessId);
            _currentConnection = new BackendConnectionInfo(registration);
            return true;
        }

        return false;
    }

    private async Task<RegistrationCompatibility> GetRegistrationCompatibilityAsync(BackendRegistrationRecord registration, CancellationToken cancellationToken)
    {
        if (!registrationStore.IsLiveProcess(registration))
        {
            return RegistrationCompatibility.NotLive;
        }

        try
        {
            using var client = CreateClient(registration);
            var response = await client.GetAsync("/api/backend/ping", cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return identityProvider.MatchesOwnerScope(registration.Identity)
                    ? RegistrationCompatibility.UnreachableOwner
                    : RegistrationCompatibility.None;
            }

            var ping = await response.Content.ReadFromJsonAsync<BackendPingResponse>(JsonOptions, cancellationToken);
            if (ping is null)
            {
                return identityProvider.MatchesOwnerScope(registration.Identity)
                    ? RegistrationCompatibility.UnreachableOwner
                    : RegistrationCompatibility.None;
            }

            if (identityProvider.Matches(ping.Identity))
            {
                return RegistrationCompatibility.Exact;
            }

            if (identityProvider.MatchesConfiguration(ping.Identity))
            {
                return RegistrationCompatibility.Adoptable;
            }

            if (identityProvider.MatchesOwnerScope(ping.Identity))
            {
                return RegistrationCompatibility.ConflictingOwner;
            }

            return RegistrationCompatibility.None;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Backend registration at {BaseUrl} did not answer ping.", registration.BaseUrl);
            return identityProvider.MatchesOwnerScope(registration.Identity)
                ? RegistrationCompatibility.UnreachableOwner
                : RegistrationCompatibility.None;
        }
    }

    private HttpClient CreateClient(BackendRegistrationRecord registration)
    {
        var client = new HttpClient
        {
            BaseAddress = new Uri(registration.BaseUrl, UriKind.Absolute),
            Timeout = configuration.BridgePingTimeout
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

    private string CreateOwnershipConflictMessage(BackendRegistrationRecord registration)
    {
        return string.Join(
            " ",
            "A different backend is already running for this workspace/settings path.",
            $"backendId='{registration.BackendId}'",
            $"processId={registration.ProcessId}",
            $"baseUrl='{registration.BaseUrl}'",
            $"managerUrl='{registration.ManagerUrl}'",
            $"registeredSettingsPath='{registration.Identity.SettingsPath}'",
            $"registeredBinaryVersionMarker='{registration.Identity.BinaryVersionMarker}'",
            $"currentBinaryVersionMarker='{identityProvider.Current.BinaryVersionMarker}'.",
            "Use the tray recover/restart action if you need to replace the current owner.");
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

    private enum RegistrationCompatibility
    {
        None,
        NotLive,
        Exact,
        Adoptable,
        ConflictingOwner,
        UnreachableOwner
    }
}
