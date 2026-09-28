using System.Text.Json;
using System.Text.Json.Serialization;
using CanDoItAll.Infrastructure.Readiness;
using CanDoItAll.Modules.Security;

namespace CanDoItAll.Composition;

/// <summary>
/// How the host is published, as a string token: <c>FrameworkDependent</c> (the host needs a matching installed .NET
/// runtime).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeDeploymentPublishMode
{
    FrameworkDependent
}

/// <summary>
/// Processor architecture of a publish target, as a string token: <c>X64</c> or <c>Arm64</c>.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeDeploymentArchitecture
{
    X64,
    Arm64
}

/// <summary>
/// Evidence behind a deployment support claim, as a string token: <c>ActualHostValidated</c> (validated on a real
/// host of that kind) or <c>ActualHostUnverified</c> (built and tested, but not yet validated on a real host).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeDeploymentEvidenceLevel
{
    ActualHostValidated,
    ActualHostUnverified
}

/// <summary>
/// Operational state of the host, as a string token: <c>Starting</c> (startup has not finished), <c>Ready</c> (startup
/// finished, including the preparation of the active database) or <c>Unavailable</c> (not ready for another reason,
/// for example a failed startup step).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RuntimeOperationalState
{
    Starting,
    Ready,
    Unavailable
}

/// <summary>
/// One supported publish target of the host in the deployment support manifest.
/// </summary>
/// <param name="RuntimeIdentifier">
/// .NET runtime identifier of the target: <c>win-x64</c>, <c>linux-x64</c>, <c>osx-x64</c> or <c>osx-arm64</c>.
/// </param>
/// <param name="OperatingSystem">
/// Operating system of the target, as a string token: <c>Windows</c>, <c>Linux</c> or <c>MacOs</c>.
/// </param>
/// <param name="Architecture">Processor architecture, as a string token: <c>X64</c> or <c>Arm64</c>.</param>
/// <param name="RuntimeEvidence">
/// Evidence for the target, as a string token: <c>ActualHostValidated</c> or <c>ActualHostUnverified</c>.
/// </param>
public sealed record RuntimeDeploymentSupportTarget(
    string RuntimeIdentifier,
    RuntimeHostOperatingSystem OperatingSystem,
    RuntimeDeploymentArchitecture Architecture,
    RuntimeDeploymentEvidenceLevel RuntimeEvidence);

/// <summary>
/// One supported headless host profile in the deployment support manifest, with the secret storage it uses by default.
/// </summary>
/// <param name="Profile">
/// The host profile, as a string token: <c>WindowsHeadless</c>, <c>LinuxHeadless</c> or <c>MacOsHeadless</c>.
/// </param>
/// <param name="BaselineSecretProvider">
/// Secret-vault provider the profile uses by default, as a string token, for example <c>Dpapi</c> or
/// <c>LocalUserFile</c>; the full token list is on the <c>SecretVaultProviderKind</c> schema.
/// </param>
/// <param name="BaselineProtectionLevel">
/// Protection of that secret storage, as a string token: <c>Unknown</c>, <c>DevelopmentOnly</c>, <c>BasicLocal</c> or
/// <c>Strong</c>.
/// </param>
/// <param name="RuntimeEvidence">
/// Evidence for the profile, as a string token: <c>ActualHostValidated</c> or <c>ActualHostUnverified</c>.
/// </param>
public sealed record RuntimeDeploymentSupportProfile(
    RuntimeHostProfileKind Profile,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SecretVaultProviderKind BaselineSecretProvider,
    [property: JsonConverter(typeof(JsonStringEnumConverter))]
    SecretVaultProtectionLevel BaselineProtectionLevel,
    RuntimeDeploymentEvidenceLevel RuntimeEvidence);

/// <summary>
/// Deployment support manifest embedded in this build of the host: how it is published, the supported publish
/// targets and headless host profiles, the prerequisites and the validations still pending. It describes the build,
/// not the current host.
/// </summary>
/// <param name="SchemaVersion">Version of the manifest format; currently 1.</param>
/// <param name="Product">Product the manifest describes: <c>CanDoItAll.Web</c>.</param>
/// <param name="PublishMode">How the host is published, as a string token: <c>FrameworkDependent</c>.</param>
/// <param name="HeadlessCoreRequiresDesktopCapabilities">
/// Whether the headless core needs desktop capabilities; always false in a valid manifest.
/// </param>
/// <param name="Targets">The supported publish targets.</param>
/// <param name="Profiles">The supported headless host profiles.</param>
/// <param name="Prerequisites">
/// Human-readable prerequisites for running the host, such as the required .NET runtime and PostgreSQL version.
/// </param>
/// <param name="DeferredValidationIds">
/// Identifiers of support validations that are still pending, for example <c>MACOS-KEYCHAIN-VALIDATION-001</c>.
/// </param>
public sealed record RuntimeDeploymentSupportManifest(
    int SchemaVersion,
    string Product,
    RuntimeDeploymentPublishMode PublishMode,
    bool HeadlessCoreRequiresDesktopCapabilities,
    IReadOnlyList<RuntimeDeploymentSupportTarget> Targets,
    IReadOnlyList<RuntimeDeploymentSupportProfile> Profiles,
    IReadOnlyList<string> Prerequisites,
    IReadOnlyList<string> DeferredValidationIds);

/// <summary>
/// Operational snapshot returned by <c>GET /api/runtime/operations</c>: whether the host finished starting, the
/// deployment support manifest of this build and the current host capability snapshot.
/// </summary>
/// <param name="State">
/// Operational state, as a string token: <c>Starting</c>, <c>Ready</c> or <c>Unavailable</c>. Only <c>Ready</c> means
/// the host finished starting.
/// </param>
/// <param name="DatabaseAndMigrationsReady">
/// True when the host reports itself ready, which happens only after its startup prepared the active database,
/// including the migrations. It is true exactly when <c>state</c> is <c>Ready</c>.
/// </param>
/// <param name="DeploymentSupport">The deployment support manifest embedded in this build.</param>
/// <param name="HostCapabilities">
/// The current host capability snapshot, the same as <c>GET /api/runtime/capabilities</c> returns.
/// </param>
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
