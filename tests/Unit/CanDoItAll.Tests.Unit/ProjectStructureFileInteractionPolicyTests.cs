using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.FileInteraction.Markdown;
using CanDoItAll.FileTools.FileInteraction.Spreadsheet;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;

namespace CanDoItAll.Tests.Unit.Projects;

public sealed class ProjectStructureFileInteractionPolicyTests
{
    [Theory]
    [InlineData("notes.txt", "application/octet-stream", FileInteractionBuiltInProfileIds.Text, true)]
    [InlineData("notes.md", "text/plain", FileInteractionMarkdownProfileIds.Markdown, true)]
    [InlineData("diagram.mmd", "text/plain", WorkbenchFileInteractionProfileIds.Mermaid, true)]
    [InlineData("photo.png", "application/octet-stream", FileInteractionBuiltInProfileIds.Image, false)]
    [InlineData("report.pdf", "application/octet-stream", FileInteractionBuiltInProfileIds.Pdf, false)]
    [InlineData("hostile.svg", "image/png", FileInteractionBuiltInProfileIds.Svg, false)]
    [InlineData("clip.mp4", "video/mp4", FileInteractionBuiltInProfileIds.Object, false)]
    [InlineData("archive.zip", "application/zip", FileInteractionBuiltInProfileIds.Object, false)]
    [InlineData("forecast.xlsx", "application/octet-stream", FileInteractionSpreadsheetProfileIds.Spreadsheet, false)]
    public void Explicit_composition_resolves_only_claimed_profiles(
        string fileName,
        string rawMediaType,
        string expectedProfileId,
        bool supportsEdit)
    {
        FileInteractionComponentComposition composition = BuildComposition();
        string? mediaType = ProjectStructureFileInteractionPolicy.NormalizeMediaType(fileName, rawMediaType);
        var file = new FileReference("test", "handle");
        var view = new FileInteractionRequest(file, fileName, FileInteractionMode.View, mediaType);

        FileInteractionResolution viewResolution = composition.Core.Profiles.Resolve(view);

        Assert.Equal(FileInteractionResolutionStatus.Resolved, viewResolution.Status);
        Assert.Equal(expectedProfileId, viewResolution.Profile?.Id);
        Assert.True(composition.Renderers.Resolve(expectedProfileId, FileInteractionMode.View).IsResolved);

        var edit = new FileInteractionRequest(file, fileName, FileInteractionMode.Edit, mediaType);
        FileInteractionResolution editResolution = composition.Core.Profiles.Resolve(edit);
        Assert.Equal(
            supportsEdit ? FileInteractionResolutionStatus.Resolved : FileInteractionResolutionStatus.Unsupported,
            editResolution.Status);
        if (supportsEdit)
        {
            Assert.Equal(expectedProfileId, editResolution.Profile?.Id);
            Assert.True(composition.Renderers.Resolve(expectedProfileId, FileInteractionMode.Edit).IsResolved);
        }
    }

    [Fact]
    public void Edit_intent_requires_text_revisioned_driver_and_writable_storage()
    {
        StorageCatalogRecord writable = CreateStorage(isReadOnly: false);
        StorageCatalogRecord readOnly = CreateStorage(isReadOnly: true);
        var revisioned = new PolicyDriver(
            StorageCapability.Read | StorageCapability.Write | StorageCapability.MutableUpdate);
        var readOnlyDriver = new PolicyDriver(StorageCapability.Read);

        Assert.Equal(
            FileToolsKnownFileIntent.Edit,
            ProjectStructureFileInteractionPolicy.ResolveIntent("notes.md", "text/plain", writable, revisioned));
        Assert.Equal(
            FileToolsKnownFileIntent.ReadOnly,
            ProjectStructureFileInteractionPolicy.ResolveIntent("notes.md", "text/plain", readOnly, revisioned));
        Assert.Equal(
            FileToolsKnownFileIntent.ReadOnly,
            ProjectStructureFileInteractionPolicy.ResolveIntent("notes.md", "text/plain", writable, readOnlyDriver));
        Assert.Equal(
            FileToolsKnownFileIntent.ReadOnly,
            ProjectStructureFileInteractionPolicy.ResolveIntent(
                "notes.md",
                "text/plain",
                writable,
                new NonRevisionedPolicyDriver()));
        Assert.Equal(
            FileToolsKnownFileIntent.ReadOnly,
            ProjectStructureFileInteractionPolicy.ResolveIntent("hostile.svg", "image/svg+xml", writable, revisioned));
        Assert.Equal(
            FileToolsKnownFileIntent.ReadOnly,
            ProjectStructureFileInteractionPolicy.ResolveIntent("report.pdf", "application/pdf", writable, revisioned));
    }

    [Theory]
    [InlineData("hostile.svg", "image/png", "image/svg+xml")]
    [InlineData("diagram.mmd", "text/plain", ProjectStructureFileInteractionPolicy.MermaidMediaType)]
    [InlineData("notes.md", "text/plain", "text/markdown")]
    [InlineData("report.pdf", "text/html", "application/pdf")]
    [InlineData("forecast.xlsx", "text/plain", ProjectStructureFileInteractionPolicy.XlsxMediaType)]
    public void Known_extension_normalizes_renderer_evidence_fail_closed(
        string fileName,
        string suppliedMediaType,
        string expectedMediaType)
    {
        Assert.Equal(
            expectedMediaType,
            ProjectStructureFileInteractionPolicy.NormalizeMediaType(fileName, suppliedMediaType));
    }

    [Fact]
    public void Xlsx_renderer_requires_bounded_full_content()
    {
        FileInteractionComponentComposition composition = BuildComposition();

        FileInteractionRendererResolution resolution = composition.Renderers.Resolve(
            FileInteractionSpreadsheetProfileIds.Spreadsheet,
            FileInteractionMode.View);

        Assert.True(resolution.IsResolved);
        Assert.Equal(
            FileInteractionContentRequirement.FullContent,
            resolution.Renderer?.ContentRequirement);
    }

    [Fact]
    public void Xlsx_host_notice_describes_the_bounded_cell_preview()
    {
        var request = new FileInteractionRequest(
            new FileReference("test", "forecast"),
            "forecast.xlsx",
            FileInteractionMode.View,
            ProjectStructureFileInteractionPolicy.XlsxMediaType);

        string notice = ProjectStructureFileInteractionPolicy.ResolveHostNotice(request, canEdit: false);

        Assert.Contains("bounded, read-only preview", notice, StringComparison.Ordinal);
        Assert.Contains("cells and formulas", notice, StringComparison.Ordinal);
        Assert.DoesNotContain("without loading", notice, StringComparison.OrdinalIgnoreCase);
    }

    private static FileInteractionComponentComposition BuildComposition()
        => new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .AddMarkdown()
            .AddWorkbenchMermaid()
            .AddSpreadsheet()
            .Build();

    private static StorageCatalogRecord CreateStorage(bool isReadOnly)
        => new()
        {
            Id = Guid.NewGuid(),
            Name = "Interaction storage",
            ProviderKind = StorageProviderKind.FileSystem,
            IsEnabled = true,
            IsReadOnly = isReadOnly,
            CapabilityMask = StorageCapability.Read | StorageCapability.Write | StorageCapability.MutableUpdate
        };

    private sealed class PolicyDriver(StorageCapability capabilities)
        : IStorageDriver, IStorageRevisionedContentDriver
    {
        public StorageProviderKind ProviderKind => StorageProviderKind.FileSystem;

        public StorageCapability SupportedCapabilities => capabilities;

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageContentRevision?> GetRevisionAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageRevisionedWriteResult> ReplaceAsync(
            StorageCatalogRecord storage,
            StorageRevisionedWriteRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class NonRevisionedPolicyDriver : IStorageDriver
    {
        public StorageProviderKind ProviderKind => StorageProviderKind.FileSystem;

        public StorageCapability SupportedCapabilities =>
            StorageCapability.Read | StorageCapability.Write | StorageCapability.MutableUpdate;

        public Task<StorageConnectionTestResult> TestConnectionAsync(
            StorageCatalogRecord storage,
            string? secretValue,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<StorageWriteResult> SaveAsync(
            StorageCatalogRecord storage,
            StorageWriteRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<Stream> OpenReadAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteAsync(
            StorageCatalogRecord storage,
            StorageObjectReference reference,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
