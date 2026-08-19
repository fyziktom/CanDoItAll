using System.Text.Json.Serialization;
using CanDoItAll.Modules.Security;

namespace CanDoItAll.Composition;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeHostProfileKind
{
    Auto,
    WindowsInteractive,
    WindowsHeadless,
    LinuxInteractive,
    LinuxHeadless,
    MacOsInteractive,
    MacOsHeadless,
    Test
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeHostOperatingSystem
{
    Windows,
    Linux,
    MacOs
}

public sealed class RuntimeHostProfileOptions
{
    public const string SectionName = "RuntimeHost";

    public RuntimeHostProfileKind Profile { get; set; } = RuntimeHostProfileKind.Auto;
}

public sealed record RuntimeHostFacts(
    RuntimeHostOperatingSystem OperatingSystem,
    bool IsDevelopment)
{
    public static RuntimeHostFacts DetectCurrent(bool isDevelopment)
    {
        RuntimeHostOperatingSystem operatingSystem = System.OperatingSystem.IsWindows()
            ? RuntimeHostOperatingSystem.Windows
            : System.OperatingSystem.IsLinux()
                ? RuntimeHostOperatingSystem.Linux
                : System.OperatingSystem.IsMacOS()
                    ? RuntimeHostOperatingSystem.MacOs
                    : throw new PlatformNotSupportedException(
                        "The runtime host operating system is not supported. Use Windows, Linux, or macOS.");

        return new RuntimeHostFacts(operatingSystem, isDevelopment);
    }
}

public sealed record ResolvedRuntimeHostProfile(
    RuntimeHostProfileKind Kind,
    RuntimeHostOperatingSystem OperatingSystem,
    bool IsInteractive,
    bool IsTest,
    bool ActualHostSupportVerified);

public static class RuntimeHostProfileResolver
{
    public static ResolvedRuntimeHostProfile Resolve(
        RuntimeHostProfileOptions options,
        SecretVaultUsageProfile secretUsageProfile,
        RuntimeHostFacts facts)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(facts);

        if (!Enum.IsDefined(options.Profile))
        {
            throw new InvalidOperationException(
                $"Runtime host profile value '{options.Profile}' is invalid.");
        }

        if (options.Profile == RuntimeHostProfileKind.Test)
        {
            if (!facts.IsDevelopment)
            {
                throw new InvalidOperationException(
                    "The Test runtime host profile is available only in a Development environment.");
            }

            return new ResolvedRuntimeHostProfile(
                RuntimeHostProfileKind.Test,
                facts.OperatingSystem,
                secretUsageProfile == SecretVaultUsageProfile.Interactive,
                IsTest: true,
                ActualHostSupportVerified: false);
        }

        RuntimeHostProfileKind kind = options.Profile == RuntimeHostProfileKind.Auto
            ? ResolveAutomaticProfile(facts.OperatingSystem, secretUsageProfile)
            : options.Profile;
        (RuntimeHostOperatingSystem expectedOperatingSystem, bool isInteractive) = Describe(kind);

        if (expectedOperatingSystem != facts.OperatingSystem)
        {
            throw new InvalidOperationException(
                $"Runtime host profile '{kind}' does not match the current {facts.OperatingSystem} host.");
        }

        bool secretProfileIsInteractive = secretUsageProfile == SecretVaultUsageProfile.Interactive;
        if (secretProfileIsInteractive != isInteractive)
        {
            throw new InvalidOperationException(
                $"Runtime host profile '{kind}' does not match the configured secret-vault usage profile '{secretUsageProfile}'.");
        }

        return new ResolvedRuntimeHostProfile(
            kind,
            expectedOperatingSystem,
            isInteractive,
            IsTest: false,
            ActualHostSupportVerified: expectedOperatingSystem != RuntimeHostOperatingSystem.MacOs);
    }

    private static RuntimeHostProfileKind ResolveAutomaticProfile(
        RuntimeHostOperatingSystem operatingSystem,
        SecretVaultUsageProfile usageProfile)
        => (operatingSystem, usageProfile) switch
        {
            (RuntimeHostOperatingSystem.Windows, SecretVaultUsageProfile.Interactive) =>
                RuntimeHostProfileKind.WindowsInteractive,
            (RuntimeHostOperatingSystem.Windows, SecretVaultUsageProfile.Headless) =>
                RuntimeHostProfileKind.WindowsHeadless,
            (RuntimeHostOperatingSystem.Linux, SecretVaultUsageProfile.Interactive) =>
                RuntimeHostProfileKind.LinuxInteractive,
            (RuntimeHostOperatingSystem.Linux, SecretVaultUsageProfile.Headless) =>
                RuntimeHostProfileKind.LinuxHeadless,
            (RuntimeHostOperatingSystem.MacOs, SecretVaultUsageProfile.Interactive) =>
                RuntimeHostProfileKind.MacOsInteractive,
            (RuntimeHostOperatingSystem.MacOs, SecretVaultUsageProfile.Headless) =>
                RuntimeHostProfileKind.MacOsHeadless,
            _ => throw new InvalidOperationException(
                $"Runtime host profile could not be resolved for {operatingSystem} and {usageProfile}.")
        };

    private static (RuntimeHostOperatingSystem OperatingSystem, bool IsInteractive) Describe(
        RuntimeHostProfileKind profile)
        => profile switch
        {
            RuntimeHostProfileKind.WindowsInteractive => (RuntimeHostOperatingSystem.Windows, true),
            RuntimeHostProfileKind.WindowsHeadless => (RuntimeHostOperatingSystem.Windows, false),
            RuntimeHostProfileKind.LinuxInteractive => (RuntimeHostOperatingSystem.Linux, true),
            RuntimeHostProfileKind.LinuxHeadless => (RuntimeHostOperatingSystem.Linux, false),
            RuntimeHostProfileKind.MacOsInteractive => (RuntimeHostOperatingSystem.MacOs, true),
            RuntimeHostProfileKind.MacOsHeadless => (RuntimeHostOperatingSystem.MacOs, false),
            _ => throw new InvalidOperationException(
                $"Runtime host profile '{profile}' cannot be resolved as an explicit host profile.")
        };
}
