using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace CanDoItAll.Modules.Security;

public interface IMacOsKeychainClient
{
    ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken);

    Task SetAsync(string service, string account, string value, CancellationToken cancellationToken);

    Task<string?> GetAsync(string service, string account, CancellationToken cancellationToken);

    Task DeleteAsync(string service, string account, CancellationToken cancellationToken);
}

public sealed class MacOsKeychainSecretVault : ISecretVault, ISecretVaultCapability
{
    private readonly string service;
    private readonly IMacOsKeychainClient client;

    public MacOsKeychainSecretVault(SecretVaultOptions options)
        : this(options, new MacOsKeychainClient())
    {
    }

    public MacOsKeychainSecretVault(SecretVaultOptions options, IMacOsKeychainClient client)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.UsageProfile != SecretVaultUsageProfile.Interactive)
        {
            throw new SecretVaultConfigurationException(
                "macOS Keychain requires an interactive unlocked-user profile. Configure ExternalWrappingKeyFile for headless use.");
        }

        service = string.IsNullOrWhiteSpace(options.ApplicationName)
            ? "CanDoItAll"
            : options.ApplicationName.Trim();
        this.client = client ?? throw new ArgumentNullException(nameof(client));
    }

    public SecretVaultProviderKind Provider => SecretVaultProviderKind.MacOsKeychain;

    public ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
        => client.ProbeAsync(cancellationToken);

    public Task SetAsync(string key, string value, CancellationToken ct = default)
        => client.SetAsync(service, NormalizeKey(key), value ?? throw new ArgumentNullException(nameof(value)), ct);

    public Task<string?> GetAsync(string key, CancellationToken ct = default)
        => client.GetAsync(service, NormalizeKey(key), ct);

    public Task DeleteAsync(string key, CancellationToken ct = default)
        => client.DeleteAsync(service, NormalizeKey(key), ct);

    private static string NormalizeKey(string key)
        => string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Secret key cannot be empty.", nameof(key))
            : key.Trim();
}

public sealed class MacOsKeychainClient : IMacOsKeychainClient
{
    private const int Success = 0;
    private const int DuplicateItem = -25299;
    private const int ItemNotFound = -25300;
    private const int AuthenticationFailed = -25293;
    private const int InteractionNotAllowed = -25308;
    private const uint UnlockStateStatus = 1;

    public ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!OperatingSystem.IsMacOS())
        {
            return ValueTask.FromResult(new SecretVaultProbeResult(
                SecretVaultProviderKind.MacOsKeychain,
                SecretVaultAvailability.UnsupportedPlatform,
                "Select macOS Keychain only on macOS."));
        }

        int status = NativeMethods.SecKeychainCopyDefault(out IntPtr keychain);
        if (status != Success)
        {
            return ValueTask.FromResult(MapProbeFailure(status));
        }

        try
        {
            status = NativeMethods.SecKeychainGetStatus(keychain, out uint keychainStatus);
            if (status == Success && (keychainStatus & UnlockStateStatus) == 0)
            {
                return ValueTask.FromResult(new SecretVaultProbeResult(
                    SecretVaultProviderKind.MacOsKeychain,
                    SecretVaultAvailability.Locked,
                    "Unlock the login Keychain before starting the interactive profile."));
            }

            return ValueTask.FromResult(status == Success
                ? SecretVaultProbeResult.Available(SecretVaultProviderKind.MacOsKeychain)
                : MapProbeFailure(status));
        }
        finally
        {
            NativeMethods.CFRelease(keychain);
        }
    }

    public Task SetAsync(
        string service,
        string account,
        string value,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMacOs();
        byte[] serviceBytes = Encoding.UTF8.GetBytes(service);
        byte[] accountBytes = Encoding.UTF8.GetBytes(account);
        byte[] valueBytes = Encoding.UTF8.GetBytes(value);
        try
        {
            int status = NativeMethods.SecKeychainAddGenericPassword(
                IntPtr.Zero,
                (uint)serviceBytes.Length,
                serviceBytes,
                (uint)accountBytes.Length,
                accountBytes,
                (uint)valueBytes.Length,
                valueBytes,
                out IntPtr item);
            if (item != IntPtr.Zero)
            {
                NativeMethods.CFRelease(item);
            }

            if (status == DuplicateItem)
            {
                ModifyExisting(serviceBytes, accountBytes, valueBytes);
            }
            else
            {
                ThrowIfFailed(status, "store");
            }

            return Task.CompletedTask;
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(valueBytes);
        }
    }

    public Task<string?> GetAsync(
        string service,
        string account,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMacOs();
        byte[] serviceBytes = Encoding.UTF8.GetBytes(service);
        byte[] accountBytes = Encoding.UTF8.GetBytes(account);
        int status = NativeMethods.SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)serviceBytes.Length,
            serviceBytes,
            (uint)accountBytes.Length,
            accountBytes,
            out uint passwordLength,
            out IntPtr passwordData,
            out IntPtr item);
        if (status == ItemNotFound)
        {
            return Task.FromResult<string?>(null);
        }

        ThrowIfFailed(status, "read");
        try
        {
            byte[] valueBytes = new byte[passwordLength];
            try
            {
                Marshal.Copy(passwordData, valueBytes, 0, valueBytes.Length);
                return Task.FromResult<string?>(Encoding.UTF8.GetString(valueBytes));
            }
            finally
            {
                System.Security.Cryptography.CryptographicOperations.ZeroMemory(valueBytes);
            }
        }
        finally
        {
            NativeMethods.SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            if (item != IntPtr.Zero)
            {
                NativeMethods.CFRelease(item);
            }
        }
    }

    public Task DeleteAsync(
        string service,
        string account,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        EnsureMacOs();
        byte[] serviceBytes = Encoding.UTF8.GetBytes(service);
        byte[] accountBytes = Encoding.UTF8.GetBytes(account);
        int status = NativeMethods.SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)serviceBytes.Length,
            serviceBytes,
            (uint)accountBytes.Length,
            accountBytes,
            out _,
            out IntPtr passwordData,
            out IntPtr item);
        if (status == ItemNotFound)
        {
            return Task.CompletedTask;
        }

        ThrowIfFailed(status, "delete");
        try
        {
            ThrowIfFailed(NativeMethods.SecKeychainItemDelete(item), "delete");
            return Task.CompletedTask;
        }
        finally
        {
            NativeMethods.SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            if (item != IntPtr.Zero)
            {
                NativeMethods.CFRelease(item);
            }
        }
    }

    private static void ModifyExisting(byte[] serviceBytes, byte[] accountBytes, byte[] valueBytes)
    {
        int status = NativeMethods.SecKeychainFindGenericPassword(
            IntPtr.Zero,
            (uint)serviceBytes.Length,
            serviceBytes,
            (uint)accountBytes.Length,
            accountBytes,
            out _,
            out IntPtr passwordData,
            out IntPtr item);
        ThrowIfFailed(status, "update");
        try
        {
            ThrowIfFailed(
                NativeMethods.SecKeychainItemModifyAttributesAndData(
                    item,
                    IntPtr.Zero,
                    (uint)valueBytes.Length,
                    valueBytes),
                "update");
        }
        finally
        {
            NativeMethods.SecKeychainItemFreeContent(IntPtr.Zero, passwordData);
            if (item != IntPtr.Zero)
            {
                NativeMethods.CFRelease(item);
            }
        }
    }

    private static SecretVaultProbeResult MapProbeFailure(int status)
    {
        return status switch
        {
            AuthenticationFailed or InteractionNotAllowed => new SecretVaultProbeResult(
                SecretVaultProviderKind.MacOsKeychain,
                SecretVaultAvailability.Locked,
                "Unlock the login Keychain and permit access before starting the interactive profile."),
            _ => new SecretVaultProbeResult(
                SecretVaultProviderKind.MacOsKeychain,
                SecretVaultAvailability.Unavailable,
                "Verify that a login Keychain is available for the current user session.")
        };
    }

    private static void ThrowIfFailed(int status, string operation)
    {
        if (status == Success)
        {
            return;
        }

        SecretVaultProbeResult result = MapProbeFailure(status);
        throw new InvalidOperationException(
            $"macOS Keychain could not {operation} the requested secret ({result.Availability}). {result.Remediation}");
    }

    private static void EnsureMacOs()
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("macOS Keychain is supported only on macOS.");
        }
    }

    private static class NativeMethods
    {
        private const string SecurityFramework = "/System/Library/Frameworks/Security.framework/Security";
        private const string CoreFoundationFramework = "/System/Library/Frameworks/CoreFoundation.framework/CoreFoundation";

        [DllImport(SecurityFramework)]
        internal static extern int SecKeychainCopyDefault(out IntPtr keychain);

        [DllImport(SecurityFramework)]
        internal static extern int SecKeychainGetStatus(IntPtr keychain, out uint status);

        [DllImport(SecurityFramework)]
        internal static extern int SecKeychainAddGenericPassword(
            IntPtr keychain,
            uint serviceNameLength,
            byte[] serviceName,
            uint accountNameLength,
            byte[] accountName,
            uint passwordLength,
            byte[] passwordData,
            out IntPtr itemRef);

        [DllImport(SecurityFramework)]
        internal static extern int SecKeychainFindGenericPassword(
            IntPtr keychainOrArray,
            uint serviceNameLength,
            byte[] serviceName,
            uint accountNameLength,
            byte[] accountName,
            out uint passwordLength,
            out IntPtr passwordData,
            out IntPtr itemRef);

        [DllImport(SecurityFramework)]
        internal static extern int SecKeychainItemModifyAttributesAndData(
            IntPtr itemRef,
            IntPtr attributes,
            uint length,
            byte[] data);

        [DllImport(SecurityFramework)]
        internal static extern int SecKeychainItemDelete(IntPtr itemRef);

        [DllImport(SecurityFramework)]
        internal static extern int SecKeychainItemFreeContent(IntPtr attributes, IntPtr data);

        [DllImport(CoreFoundationFramework)]
        internal static extern void CFRelease(IntPtr value);
    }
}

public interface ILinuxSecretServiceCommandRunner
{
    Task<LinuxSecretServiceCommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? standardInput,
        TimeSpan timeout,
        CancellationToken cancellationToken);
}

public sealed record LinuxSecretServiceCommandResult(int ExitCode, string StandardOutput, string StandardError);

public sealed class LinuxSecretServiceVault : ISecretVault, ISecretVaultCapability
{
    private const string ValueEnvelopePrefix = "candoitall:v1:";
    private readonly string applicationName;
    private readonly string executable;
    private readonly TimeSpan timeout;
    private readonly Func<string, string?> environmentVariableResolver;
    private readonly ILinuxSecretServiceCommandRunner commandRunner;

    public LinuxSecretServiceVault(SecretVaultOptions options)
        : this(options, new LinuxSecretServiceCommandRunner(), Environment.GetEnvironmentVariable)
    {
    }

    public LinuxSecretServiceVault(
        SecretVaultOptions options,
        ILinuxSecretServiceCommandRunner commandRunner,
        Func<string, string?> environmentVariableResolver)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.UsageProfile != SecretVaultUsageProfile.Interactive)
        {
            throw new SecretVaultConfigurationException(
                "Linux Secret Service requires an interactive D-Bus session. Configure ExternalWrappingKeyFile for headless use.");
        }

        applicationName = string.IsNullOrWhiteSpace(options.ApplicationName)
            ? "CanDoItAll"
            : options.ApplicationName.Trim();
        executable = string.IsNullOrWhiteSpace(options.LinuxSecretToolPath)
            ? "secret-tool"
            : options.LinuxSecretToolPath.Trim();
        timeout = options.ProbeTimeout is { } value && value > TimeSpan.Zero
            ? value
            : throw new SecretVaultConfigurationException("Configure a positive SecretVault probe timeout.");
        this.commandRunner = commandRunner ?? throw new ArgumentNullException(nameof(commandRunner));
        this.environmentVariableResolver = environmentVariableResolver
            ?? throw new ArgumentNullException(nameof(environmentVariableResolver));
    }

    public SecretVaultProviderKind Provider => SecretVaultProviderKind.LinuxSecretService;

    public async ValueTask<SecretVaultProbeResult> ProbeAsync(CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsLinux())
        {
            return new SecretVaultProbeResult(
                Provider,
                SecretVaultAvailability.UnsupportedPlatform,
                "Select Linux Secret Service only on Linux.");
        }

        if (string.IsNullOrWhiteSpace(environmentVariableResolver("DBUS_SESSION_BUS_ADDRESS")))
        {
            return new SecretVaultProbeResult(
                Provider,
                SecretVaultAvailability.SessionUnavailable,
                "Start an interactive D-Bus user session or select an explicit headless provider.");
        }

        LinuxSecretServiceCommandResult result;
        try
        {
            result = await commandRunner.RunAsync(
                executable,
                BuildLookupArguments("__capability_probe__"),
                standardInput: null,
                timeout,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Win32Exception)
        {
            return new SecretVaultProbeResult(
                Provider,
                SecretVaultAvailability.DependencyMissing,
                "Install the libsecret secret-tool utility or configure an explicit headless provider.");
        }
        catch (TimeoutException)
        {
            return new SecretVaultProbeResult(
                Provider,
                SecretVaultAvailability.Unavailable,
                "Verify that Secret Service responds within the configured probe timeout.");
        }

        return ClassifyResult(result, allowNotFound: true);
    }

    public async Task SetAsync(string key, string value, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(value);
        await EnsureAvailableAsync(ct).ConfigureAwait(false);
        var arguments = new List<string>
        {
            "store",
            $"--label={applicationName}",
            "application",
            applicationName,
            "key",
            NormalizeKey(key)
        };
        LinuxSecretServiceCommandResult result = await commandRunner.RunAsync(
            executable,
            arguments,
            EncodeValue(value),
            timeout,
            ct).ConfigureAwait(false);
        ThrowIfUnavailable(ClassifyResult(result, allowNotFound: false), "store");
    }

    public async Task<string?> GetAsync(string key, CancellationToken ct = default)
    {
        await EnsureAvailableAsync(ct).ConfigureAwait(false);
        LinuxSecretServiceCommandResult result = await commandRunner.RunAsync(
            executable,
            BuildLookupArguments(NormalizeKey(key)),
            standardInput: null,
            timeout,
            ct).ConfigureAwait(false);
        if (result.ExitCode == 1 && string.IsNullOrWhiteSpace(result.StandardError))
        {
            return null;
        }

        ThrowIfUnavailable(ClassifyResult(result, allowNotFound: false), "read");
        return DecodeValue(result.StandardOutput.TrimEnd('\r', '\n'));
    }

    public async Task DeleteAsync(string key, CancellationToken ct = default)
    {
        await EnsureAvailableAsync(ct).ConfigureAwait(false);
        LinuxSecretServiceCommandResult result = await commandRunner.RunAsync(
            executable,
            ["clear", "application", applicationName, "key", NormalizeKey(key)],
            standardInput: null,
            timeout,
            ct).ConfigureAwait(false);
        if (result.ExitCode == 1 && string.IsNullOrWhiteSpace(result.StandardError))
        {
            return;
        }

        ThrowIfUnavailable(ClassifyResult(result, allowNotFound: false), "delete");
    }

    private async Task EnsureAvailableAsync(CancellationToken cancellationToken)
    {
        SecretVaultProbeResult probe = await ProbeAsync(cancellationToken).ConfigureAwait(false);
        if (!probe.IsAvailable)
        {
            throw new SecretVaultUnavailableException(probe);
        }
    }

    private IReadOnlyList<string> BuildLookupArguments(string key)
        => ["lookup", "application", applicationName, "key", key];

    private SecretVaultProbeResult ClassifyResult(
        LinuxSecretServiceCommandResult result,
        bool allowNotFound)
    {
        if (result.ExitCode == 0 ||
            (allowNotFound && result.ExitCode == 1 && string.IsNullOrWhiteSpace(result.StandardError)))
        {
            return SecretVaultProbeResult.Available(Provider);
        }

        string error = result.StandardError;
        if (error.Contains("locked", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("prompt", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("interaction", StringComparison.OrdinalIgnoreCase))
        {
            return new SecretVaultProbeResult(
                Provider,
                SecretVaultAvailability.Locked,
                "Unlock the session keyring before starting the interactive profile.");
        }

        if (error.Contains("D-Bus", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("DBus", StringComparison.OrdinalIgnoreCase) ||
            error.Contains("service", StringComparison.OrdinalIgnoreCase))
        {
            return new SecretVaultProbeResult(
                Provider,
                SecretVaultAvailability.SessionUnavailable,
                "Start Secret Service in the interactive D-Bus session or select an explicit headless provider.");
        }

        return new SecretVaultProbeResult(
            Provider,
            SecretVaultAvailability.Unavailable,
            "Verify the Secret Service installation and current user-session access.");
    }

    private static void ThrowIfUnavailable(SecretVaultProbeResult result, string operation)
    {
        if (result.IsAvailable)
        {
            return;
        }

        throw new InvalidOperationException(
            $"Linux Secret Service could not {operation} the requested secret ({result.Availability}). {result.Remediation}");
    }

    private static string NormalizeKey(string key)
        => string.IsNullOrWhiteSpace(key)
            ? throw new ArgumentException("Secret key cannot be empty.", nameof(key))
            : key.Trim();

    private static string EncodeValue(string value)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(value);
        try
        {
            return ValueEnvelopePrefix + Convert.ToBase64String(bytes);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }

    private static string DecodeValue(string value)
    {
        if (!value.StartsWith(ValueEnvelopePrefix, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The Linux Secret Service payload has an unsupported envelope version.");
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(value[ValueEnvelopePrefix.Length..]);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException("The Linux Secret Service payload is invalid.", exception);
        }

        try
        {
            return Encoding.UTF8.GetString(bytes);
        }
        finally
        {
            System.Security.Cryptography.CryptographicOperations.ZeroMemory(bytes);
        }
    }
}

public sealed class LinuxSecretServiceCommandRunner : ILinuxSecretServiceCommandRunner
{
    public async Task<LinuxSecretServiceCommandResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        string? standardInput,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = false,
            RedirectStandardInput = standardInput is not null,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };
        process.Start();
        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput.AsMemory(), cancellationToken).ConfigureAwait(false);
            await process.StandardInput.WriteLineAsync().ConfigureAwait(false);
            process.StandardInput.Close();
        }

        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }

            throw new TimeoutException("Timed out while probing the Linux Secret Service provider.");
        }

        return new LinuxSecretServiceCommandResult(
            process.ExitCode,
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false));
    }
}
