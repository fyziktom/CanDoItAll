using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Modules.Security;

namespace CanDoItAll.Composition;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeDeploymentPublishMode
{
    FrameworkDependent
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeDeploymentArchitecture
{
    X64,
    Arm64
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeDeploymentEvidenceLevel
{
    ActualHostValidated,
    ActualHostUnverified
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeOperationalState
{
    Starting,
    Ready,
    Unavailable
}

public sealed record RuntimeDeploymentSupportTarget(
    string RuntimeIdentifier,
    RuntimeHostOperatingSystem OperatingSystem,
    RuntimeDeploymentArchitecture Architecture,
    RuntimeDeploymentEvidenceLevel RuntimeEvidence);

public sealed record RuntimeDeploymentSupportProfile(
    RuntimeHostProfileKind Profile,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SecretVaultProviderKind BaselineSecretProvider,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SecretVaultProtectionLevel BaselineProtectionLevel,
    RuntimeDeploymentEvidenceLevel RuntimeEvidence);

public sealed record RuntimeDeploymentSupportManifest(
    int SchemaVersion,
    string Product,
    RuntimeDeploymentPublishMode PublishMode,
    bool HeadlessCoreRequiresDesktopCapabilities,
    IReadOnlyList<RuntimeDeploymentSupportTarget> Targets,
    IReadOnlyList<RuntimeDeploymentSupportProfile> Profiles,
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<string> DeferredValidationIds);

public sealed record RuntimeOperationsSnapshot(
    RuntimeOperationalState State,
    bool DatabaseAndMigrationsReady,
    RuntimeDeploymentSupportManifest DeploymentSupport,
    HostCapabilitySnapshot HostCapabilities);

public interface IRuntimeDeploymentSupportProvider
{
    RuntimeDeploymentSupportManifest GetManifest();
}

public sealed class EmbeddedRuntimeDeploymentSupportProvider : IRuntimeDeploymentSupportProvider
{
    private const string ResourceName = "CanDoItAll.Composition.RuntimeDeploymentSupport.json";
    private readonly RuntimeDeploymentSupportManifest manifest;

    public EmbeddedRuntimeDeploymentSupportProvider()
    {
        using Stream stream = typeof(EmbeddedRuntimeDeploymentSupportProvider).Assembly
            .GetManifestResourceStream(ResourceName) ??
            throw new InvalidOperationException(
                $"Required deployment support resource '{ResourceName}' is missing.");
        manifest = RuntimeDeploymentSupportManifestCodec.Read(stream);
    }

    public RuntimeDeploymentSupportManifest GetManifest() => manifest;
}

public static class RuntimeDeploymentSupportManifestCodec
{
    private const int CurrentSchemaVersion = 1;
    private const string ProductName = "CanDoItAll.Web";
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    public static RuntimeDeploymentSupportManifest Read(Stream source)
    {
        ArgumentNullException.ThrowIfNull(source);
        RuntimeDeploymentSupportManifest manifest = JsonSerializer.Deserialize<RuntimeDeploymentSupportManifest>(
            source,
            SerializerOptions) ?? throw new InvalidOperationException(
                "The deployment support manifest is empty or malformed.");
        Validate(manifest);
        return manifest;
    }

    public static RuntimeDeploymentSupportManifest Read(string json)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        using var source = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(json));
        return Read(source);
    }

    private static void Validate(RuntimeDeploymentSupportManifest manifest)
    {
        if (manifest.SchemaVersion != CurrentSchemaVersion)
        {
            throw new InvalidOperationException(
                $"Deployment support schema '{manifest.SchemaVersion}' is not supported.");
        }

        if (!string.Equals(manifest.Product, ProductName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Deployment support product identity is invalid.");
        }

        if (manifest.PublishMode != RuntimeDeploymentPublishMode.FrameworkDependent ||
            manifest.HeadlessCoreRequiresDesktopCapabilities)
        {
            throw new InvalidOperationException(
                "Deployment support must describe the framework-dependent headless core profile.");
        }

        RuntimeDeploymentSupportTarget[] expectedTargets =
        [
            new("win-x64", RuntimeHostOperatingSystem.Windows, RuntimeDeploymentArchitecture.X64, RuntimeDeploymentEvidenceLevel.ActualHostValidated),
            new("linux-x64", RuntimeHostOperatingSystem.Linux, RuntimeDeploymentArchitecture.X64, RuntimeDeploymentEvidenceLevel.ActualHostValidated),
            new("osx-x64", RuntimeHostOperatingSystem.MacOs, RuntimeDeploymentArchitecture.X64, RuntimeDeploymentEvidenceLevel.ActualHostUnverified),
            new("osx-arm64", RuntimeHostOperatingSystem.MacOs, RuntimeDeploymentArchitecture.Arm64, RuntimeDeploymentEvidenceLevel.ActualHostUnverified)
        ];
        RequireExactSet(manifest.Targets, expectedTargets, static target => target.RuntimeIdentifier, "publish target");

        RuntimeDeploymentSupportProfile[] expectedProfiles =
        [
            new(RuntimeHostProfileKind.WindowsHeadless, SecretVaultProviderKind.Dpapi, SecretVaultProtectionLevel.Strong, RuntimeDeploymentEvidenceLevel.ActualHostValidated),
            new(RuntimeHostProfileKind.LinuxHeadless, SecretVaultProviderKind.LocalUserFile, SecretVaultProtectionLevel.BasicLocal, RuntimeDeploymentEvidenceLevel.ActualHostValidated),
            new(RuntimeHostProfileKind.MacOsHeadless, SecretVaultProviderKind.LocalUserFile, SecretVaultProtectionLevel.BasicLocal, RuntimeDeploymentEvidenceLevel.ActualHostUnverified)
        ];
        RequireExactSet(manifest.Profiles, expectedProfiles, static profile => profile.Profile.ToString(), "host profile");

        RequireNonEmptyValues(manifest.Prerequisites, "prerequisite");
        RequireNonEmptyValues(manifest.DeferredValidationIds, "deferred validation id");
        if (!manifest.DeferredValidationIds.Contains(
                "MACOS-KEYCHAIN-VALIDATION-001",
                StringComparer.Ordinal) ||
            !manifest.DeferredValidationIds.Contains(
                "A07-MACOS-HEADLESS-ACTUALHOST-001",
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "The deployment support manifest must preserve both macOS actual-host follow-ups.");
        }
    }

    private static void RequireExactSet<T>(
        IReadOnlyList<T>? actual,
        IReadOnlyList<T> expected,
        Func<T, string> identity,
        string description)
    {
        if (actual is null ||
            actual.Count != expected.Count ||
            actual.Select(identity).Distinct(StringComparer.Ordinal).Count() != actual.Count ||
            expected.Any(item => !actual.Contains(item)))
        {
            throw new InvalidOperationException(
                $"Deployment support {description} declarations are incomplete, duplicated, or inconsistent.");
        }
    }

    private static void RequireNonEmptyValues(IReadOnlyList<string>? values, string description)
    {
        if (values is null ||
            values.Count == 0 ||
            values.Any(string.IsNullOrWhiteSpace) ||
            values.Distinct(StringComparer.Ordinal).Count() != values.Count)
        {
            throw new InvalidOperationException(
                $"Deployment support {description} declarations are incomplete or duplicated.");
        }
    }
}

public static class RuntimeOperationsSnapshotProjector
{
    public static RuntimeOperationsSnapshot Create(
        RuntimeDeploymentSupportManifest deploymentSupport,
        RuntimeReadinessSnapshot readiness,
        HostCapabilitySnapshot hostCapabilities)
    {
        ArgumentNullException.ThrowIfNull(deploymentSupport);
        ArgumentNullException.ThrowIfNull(readiness);
        ArgumentNullException.ThrowIfNull(hostCapabilities);

        RuntimeOperationalState state = readiness.IsReady
            ? RuntimeOperationalState.Ready
            : string.Equals(readiness.Summary, "Starting", StringComparison.Ordinal)
                ? RuntimeOperationalState.Starting
                : RuntimeOperationalState.Unavailable;
        return new RuntimeOperationsSnapshot(
            state,
            readiness.IsReady,
            deploymentSupport,
            hostCapabilities);
    }
}
