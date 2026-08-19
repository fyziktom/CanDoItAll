using System.Text.Json;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure;
using CanDoItAll.Infrastructure.Readiness;

namespace CanDoItAll.Tests.Unit.Infrastructure;

public sealed class RuntimeDeploymentSupportTests
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Embedded_manifest_declares_bounded_framework_dependent_support()
    {
        RuntimeDeploymentSupportManifest manifest = new EmbeddedRuntimeDeploymentSupportProvider().GetManifest();

        Assert.Equal(1, manifest.SchemaVersion);
        Assert.Equal("CanDoItAll.Web", manifest.Product);
        Assert.Equal(RuntimeDeploymentPublishMode.FrameworkDependent, manifest.PublishMode);
        Assert.False(manifest.HeadlessCoreRequiresDesktopCapabilities);
        Assert.Equal(
            ["win-x64", "linux-x64", "osx-x64", "osx-arm64"],
            manifest.Targets.Select(target => target.RuntimeIdentifier));
        Assert.All(
            manifest.Targets.Where(target => target.OperatingSystem == RuntimeHostOperatingSystem.MacOs),
            target => Assert.Equal(RuntimeDeploymentEvidenceLevel.ActualHostUnverified, target.RuntimeEvidence));
        Assert.Contains(
            manifest.Prerequisites,
            prerequisite => prerequisite.Contains("Data Protection", StringComparison.Ordinal));
        Assert.Contains("MACOS-KEYCHAIN-VALIDATION-001", manifest.DeferredValidationIds);
        Assert.Contains("A07-MACOS-HEADLESS-ACTUALHOST-001", manifest.DeferredValidationIds);
    }

    [Fact]
    public void Codec_rejects_unknown_schema_and_duplicate_target()
    {
        RuntimeDeploymentSupportManifest manifest = new EmbeddedRuntimeDeploymentSupportProvider().GetManifest();
        Assert.Throws<InvalidOperationException>(() => RuntimeDeploymentSupportManifestCodec.Read(
            JsonSerializer.Serialize(manifest with { SchemaVersion = 2 }, SerializerOptions)));

        RuntimeDeploymentSupportTarget[] duplicatedTargets =
        [
            manifest.Targets[0],
            manifest.Targets[0],
            manifest.Targets[2],
            manifest.Targets[3]
        ];
        Assert.Throws<InvalidOperationException>(() => RuntimeDeploymentSupportManifestCodec.Read(
            JsonSerializer.Serialize(manifest with { Targets = duplicatedTargets }, SerializerOptions)));
    }

    [Fact]
    public void Operations_projection_omits_runtime_summary_urls_and_physical_details()
    {
        const string sensitiveSummary = "Database failed for Password=do-not-copy at C:\\private\\db";
        const string sensitiveUrl = "http://private-host.internal:9000";
        RuntimeDeploymentSupportManifest manifest = new EmbeddedRuntimeDeploymentSupportProvider().GetManifest();
        var readiness = new RuntimeReadinessSnapshot(
            IsReady: false,
            EnvironmentName: "Production",
            Summary: sensitiveSummary,
            WatchIteration: null,
            StartedAtUtc: DateTimeOffset.UnixEpoch,
            LastChangedAtUtc: DateTimeOffset.UnixEpoch,
            ActiveUrls: [sensitiveUrl]);
        var capabilities = new HostCapabilitySnapshot(
            RuntimeHostProfileKind.LinuxHeadless,
            RuntimeHostOperatingSystem.Linux,
            IsInteractive: false,
            IsReady: false,
            DateTimeOffset.UnixEpoch,
            [
                new ApplicationPurposeRootReadiness(
                    ApplicationPurposeRootKind.Workspace,
                    ApplicationPurposeRootConfigurationSource.ExplicitConfiguration,
                    PathFoundationReadinessState.Ready,
                    PathFoundationReadinessReason.Ready)
            ],
            []);

        RuntimeOperationsSnapshot snapshot = RuntimeOperationsSnapshotProjector.Create(
            manifest,
            readiness,
            capabilities);
        string json = JsonSerializer.Serialize(snapshot, SerializerOptions);

        Assert.Equal(RuntimeOperationalState.Unavailable, snapshot.State);
        Assert.False(snapshot.DatabaseAndMigrationsReady);
        ApplicationPurposeRootReadiness purposeRoot = Assert.Single(
            snapshot.HostCapabilities.PurposeRoots);
        Assert.Equal(ApplicationPurposeRootKind.Workspace, purposeRoot.Purpose);
        Assert.Equal(
            ApplicationPurposeRootConfigurationSource.ExplicitConfiguration,
            purposeRoot.ConfigurationSource);
        Assert.Contains("\"purpose\":\"Workspace\"", json, StringComparison.Ordinal);
        Assert.Contains("\"configurationSource\":\"ExplicitConfiguration\"", json, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveSummary, json, StringComparison.Ordinal);
        Assert.DoesNotContain(sensitiveUrl, json, StringComparison.Ordinal);
        Assert.DoesNotContain("do-not-copy", json, StringComparison.Ordinal);
        Assert.DoesNotContain("C:\\private", json, StringComparison.Ordinal);
    }
}
