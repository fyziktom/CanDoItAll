using System.Collections.Immutable;
using CanDoItAll.Modules.Workspace.StorageCatalog.Contracts;

namespace CanDoItAll.Workspace.StorageCatalog.UiSandbox;

public enum CatalogScenario { Representative, Empty, Large, MissingReferences, MissingTarget, InvalidNumbers, ReadFailure, PartialReferences, Refused, UnknownAcknowledgement, PartialRouting, ActivityFailure, Degraded, Unavailable, ReadBackFailure }

public static class CatalogScenarioData {
    public static readonly Guid FileSystemId = Guid.Parse("12927020-4356-4a9d-b9d0-e4c81d53de19");
    public static readonly Guid IpfsId = Guid.Parse("9cb5d357-084c-4990-89d7-144fa2e30f48");
    public static readonly Guid FtpId = Guid.Parse("aba70186-a7d6-4070-ad86-19e2cf425f4b");
    public static readonly Guid SystemId = Guid.Parse("a9a83f4a-ccda-4ee1-bba0-ec39e2417087");
    public static readonly Guid SecretId = Guid.Parse("b22315bb-31e4-413a-b260-f11df3549e56");
    public static readonly DateTimeOffset TestedAt = new(2026, 9, 1, 12, 0, 0, TimeSpan.Zero);

    public static CatalogChoices Choices { get; } = new(
        [new(CatalogProvider.FileSystem, "File system", new() { UsePassiveMode = true },
            CatalogCapability.Read | CatalogCapability.Write | CatalogCapability.Delete | CatalogCapability.InlinePreview | CatalogCapability.Download | CatalogCapability.OpenLocally | CatalogCapability.MutableUpdate | CatalogCapability.BatchFolderUpload | CatalogCapability.BatchTransfer | CatalogCapability.ConnectionTest,
            CatalogCapability.Read | CatalogCapability.InlinePreview | CatalogCapability.Download | CatalogCapability.OpenLocally | CatalogCapability.BatchTransfer | CatalogCapability.ConnectionTest),
         new(CatalogProvider.Ipfs, "IPFS", new() { ProviderKind = CatalogProvider.Ipfs, ConnectionMode = CatalogConnection.Remote, PinOnUpload = true, UsePassiveMode = true },
            CatalogCapability.Read | CatalogCapability.Write | CatalogCapability.InlinePreview | CatalogCapability.Download | CatalogCapability.DirectUrl | CatalogCapability.BatchFolderUpload | CatalogCapability.BatchTransfer | CatalogCapability.ConnectionTest,
            CatalogCapability.Read | CatalogCapability.InlinePreview | CatalogCapability.Download | CatalogCapability.DirectUrl | CatalogCapability.BatchTransfer | CatalogCapability.ConnectionTest),
         new(CatalogProvider.Ftp, "FTP", new() { ProviderKind = CatalogProvider.Ftp, ConnectionMode = CatalogConnection.Remote, UseSsl = true, UsePassiveMode = true },
            CatalogCapability.Read | CatalogCapability.Write | CatalogCapability.Delete | CatalogCapability.Download | CatalogCapability.BatchFolderUpload | CatalogCapability.BatchTransfer | CatalogCapability.ConnectionTest,
            CatalogCapability.Read | CatalogCapability.Download | CatalogCapability.BatchTransfer | CatalogCapability.ConnectionTest)],
        [new(CatalogConnection.Local, "Local"), new(CatalogConnection.Remote, "Remote")],
        [new(CatalogPurpose.ProjectAsset, "Project assets"), new(CatalogPurpose.PromptAttachment, "Prompt attachments"),
         new(CatalogPurpose.PromptExport, "Prompt exports"), new(CatalogPurpose.Evidence, "Evidence"), new(CatalogPurpose.RecordingMedia, "Recording media"),
         new(CatalogPurpose.SnapshotPackage, "Snapshot packages"), new(CatalogPurpose.ReleasePackage, "Release packages"), new(CatalogPurpose.DeploymentMirror, "Deployment mirrors")],
        [.. Enum.GetValues<CatalogCapability>().Where(value => value != CatalogCapability.None).Select(value => new CatalogChoice<CatalogCapability>(value, value.ToString()))]);

    public static ImmutableArray<CatalogEdit> Entries => [
        Choices.Providers[0].Template with { Id = SystemId, Name = "Workspace file system", EndpointOrRoot = "/scenario/workspace", IsSystemDefault = true, Health = new(CatalogHealth.Healthy, Choices.Providers[0].WritableCapabilities, TestedAt, "Simulated healthy system target.") },
        Choices.Providers[0].Template with { Id = FileSystemId, Name = "Team artifacts", EndpointOrRoot = "/scenario/artifacts", DefaultPurposes = [CatalogPurpose.ProjectAsset] },
        Choices.Providers[1].Template with { Id = IpfsId, Name = "Evidence IPFS", EndpointOrRoot = "https://ipfs.example.test", GatewayBaseUrl = "https://gateway.example.test", BasePath = "evidence" },
        Choices.Providers[2].Template with { Id = FtpId, Name = "Release FTP", EndpointOrRoot = "ftp.example.test", Port = 21, BasePath = "releases", Username = "scenario-user", CredentialSecretId = SecretId }
    ];
}
