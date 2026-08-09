using System.Security.Cryptography;
using System.Text;
using CanDoItAll.SharedKernel;

namespace CanDoItAll.Infrastructure;

public enum HostPlatformFamily
{
    Unknown,
    Windows,
    Linux,
    MacOS
}

public enum HostBoundPathState
{
    Active = 1,
    NeedsRebind = 2,
    Migrating = 3,
    Disabled = 4
}

public readonly record struct HostPathContext(HostPlatformFamily PlatformFamily, string HostBindingId)
{
    public const string HostBindingIdEnvironmentVariable = "CANDOITALL_HOST_BINDING_ID";

    public static HostPathContext CaptureCurrent()
    {
        HostPlatformFamily platform = OperatingSystem.IsWindows()
            ? HostPlatformFamily.Windows
            : OperatingSystem.IsMacOS()
                ? HostPlatformFamily.MacOS
                : OperatingSystem.IsLinux()
                    ? HostPlatformFamily.Linux
                    : HostPlatformFamily.Unknown;
        if (platform == HostPlatformFamily.Unknown)
        {
            throw new PlatformNotSupportedException("The current host platform does not have a supported path binding policy.");
        }

        string? configuredId = Environment.GetEnvironmentVariable(HostBindingIdEnvironmentVariable);
        string hostBindingId = string.IsNullOrWhiteSpace(configuredId)
            ? BuildOpaqueBindingId(platform, Environment.MachineName)
            : ValidateBindingId(configuredId);
        return new HostPathContext(platform, hostBindingId);
    }

    public static HostPathContext CreateForTest(HostPlatformFamily platformFamily, string hostBindingId)
    {
        if (platformFamily == HostPlatformFamily.Unknown)
        {
            throw new ArgumentOutOfRangeException(nameof(platformFamily));
        }

        return new HostPathContext(platformFamily, ValidateBindingId(hostBindingId));
    }

    private static string BuildOpaqueBindingId(HostPlatformFamily platform, string machineName)
    {
        byte[] bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{platform}:{machineName.Trim().ToUpperInvariant()}"));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string ValidateBindingId(string value)
    {
        string normalized = value.Trim();
        if (normalized.Length is < 8 or > 128 ||
            normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new InvalidOperationException(
                $"{HostBindingIdEnvironmentVariable} must contain 8-128 ASCII letters, digits, hyphens, or underscores.");
        }

        return normalized;
    }
}

public sealed class HostBoundPathRecord
{
    public const int CurrentFormatVersion = 1;

    public int FormatVersion { get; set; } = CurrentFormatVersion;

    public HostPlatformFamily PlatformFamily { get; set; }

    public PhysicalPathSyntax PathSyntax { get; set; }

    public string HostBindingId { get; set; } = string.Empty;

    public string Path { get; set; } = string.Empty;

    public HostBoundPathState State { get; set; } = HostBoundPathState.NeedsRebind;

    public DateTimeOffset? LastValidatedAtUtc { get; set; }
}

public static class HostBoundPathPolicy
{
    public static HostBoundPathRecord Bind(
        string path,
        HostPathContext hostContext,
        DateTimeOffset validatedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ValidateHostContext(hostContext);
        string normalizedPath = NormalizeAbsolutePath(path, hostContext.PlatformFamily);
        return new HostBoundPathRecord
        {
            PlatformFamily = hostContext.PlatformFamily,
            PathSyntax = PhysicalPathSyntaxClassifier.Classify(normalizedPath),
            HostBindingId = hostContext.HostBindingId,
            Path = normalizedPath,
            State = HostBoundPathState.Active,
            LastValidatedAtUtc = validatedAtUtc
        };
    }

    public static HostBoundPathRecord BindCurrent(string path, DateTimeOffset validatedAtUtc)
        => Bind(path, HostPathContext.CaptureCurrent(), validatedAtUtc);

    public static HostBoundPathRecord ImportLegacy(
        string path,
        HostPathContext hostContext)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ValidateHostContext(hostContext);
        string trimmedPath = path.Trim();
        PhysicalPathSyntax syntax = PhysicalPathSyntaxClassifier.Classify(trimmedPath);
        HostPlatformFamily sourcePlatform = InferPlatform(syntax, hostContext.PlatformFamily);

        return new HostBoundPathRecord
        {
            PlatformFamily = sourcePlatform,
            PathSyntax = syntax,
            Path = trimmedPath,
            State = HostBoundPathState.NeedsRebind
        };
    }

    public static bool TryResolve(
        HostBoundPathRecord? record,
        HostPathContext hostContext,
        out string path,
        out string diagnostic)
    {
        path = string.Empty;
        diagnostic = string.Empty;
        ValidateHostContext(hostContext);
        if (record is null || record.FormatVersion != HostBoundPathRecord.CurrentFormatVersion)
        {
            diagnostic = "The host-bound path record format is unsupported and requires migration.";
            return false;
        }

        if (record.State != HostBoundPathState.Active)
        {
            diagnostic = "The host-bound path is inactive and requires explicit rebind.";
            return false;
        }

        if (record.PlatformFamily != hostContext.PlatformFamily ||
            !string.Equals(record.HostBindingId, hostContext.HostBindingId, StringComparison.Ordinal))
        {
            diagnostic = "The host-bound path belongs to a different host and requires explicit rebind.";
            return false;
        }

        if (record.PathSyntax != PhysicalPathSyntaxClassifier.Classify(record.Path) ||
            !IsAbsoluteNativeSyntax(record.PathSyntax, hostContext.PlatformFamily))
        {
            diagnostic = "The host-bound path syntax is invalid for this host and requires explicit rebind.";
            return false;
        }

        try
        {
            path = NormalizeAbsolutePath(record.Path, hostContext.PlatformFamily);
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            path = string.Empty;
            diagnostic = "The host-bound path cannot be resolved safely and requires explicit rebind.";
            return false;
        }
    }

    public static string ResolveRequired(HostBoundPathRecord? record, string description)
    {
        if (TryResolve(record, HostPathContext.CaptureCurrent(), out string path, out string diagnostic))
        {
            return path;
        }

        throw new InvalidOperationException($"The {description} is unavailable. {diagnostic}");
    }

    public static HostBoundPathRecord RebindCurrent(string path, DateTimeOffset validatedAtUtc)
        => BindCurrent(path, validatedAtUtc);

    private static string NormalizeAbsolutePath(string path, HostPlatformFamily platform)
    {
        string trimmedPath = path.Trim();
        PhysicalPathSyntax syntax = PhysicalPathSyntaxClassifier.Classify(trimmedPath);
        if (!IsAbsoluteNativeSyntax(syntax, platform))
        {
            throw new InvalidOperationException("A host-bound path must use absolute native syntax for its owning platform.");
        }

        HostPlatformFamily currentPlatform = HostPathContext.CaptureCurrent().PlatformFamily;
        return currentPlatform == platform
            ? System.IO.Path.GetFullPath(trimmedPath)
            : trimmedPath;
    }

    private static bool IsAbsoluteNativeSyntax(PhysicalPathSyntax syntax, HostPlatformFamily platform)
    {
        return platform switch
        {
            HostPlatformFamily.Windows => syntax is PhysicalPathSyntax.WindowsDriveAbsolute or
                PhysicalPathSyntax.WindowsUnc or
                PhysicalPathSyntax.WindowsDevice,
            HostPlatformFamily.Linux or HostPlatformFamily.MacOS => syntax == PhysicalPathSyntax.UnixAbsolute,
            _ => false
        };
    }

    private static HostPlatformFamily InferPlatform(PhysicalPathSyntax syntax, HostPlatformFamily currentPlatform)
    {
        return syntax switch
        {
            PhysicalPathSyntax.WindowsDriveAbsolute or
                PhysicalPathSyntax.WindowsDriveRelative or
                PhysicalPathSyntax.WindowsUnc or
                PhysicalPathSyntax.WindowsDevice => HostPlatformFamily.Windows,
            PhysicalPathSyntax.UnixAbsolute when currentPlatform is HostPlatformFamily.Linux or HostPlatformFamily.MacOS => currentPlatform,
            PhysicalPathSyntax.UnixAbsolute => HostPlatformFamily.Unknown,
            _ => HostPlatformFamily.Unknown
        };
    }

    private static void ValidateHostContext(HostPathContext hostContext)
    {
        if (hostContext.PlatformFamily == HostPlatformFamily.Unknown || string.IsNullOrWhiteSpace(hostContext.HostBindingId))
        {
            throw new ArgumentException("A concrete platform and opaque host binding identifier are required.", nameof(hostContext));
        }
    }
}
