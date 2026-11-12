using System.Text;
using System.Text.Json;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Infrastructure.Storage;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Insights.UI;
using CanDoItAll.Workbench.Content.UI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructurePageActionLifetimeTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_file_collection_withholds_private_sources_after_original_structure_access_retires(bool denyAfterNativeRead) {
        var access = new EditingUiTestAccess();
        BrowseAuthorityProbe? probe = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access));
            var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IFileToolsBrowseSessionFactory));
            Assert.NotNull(registration.ImplementationType);
            services.Replace(new ServiceDescriptor(typeof(IFileToolsBrowseSessionFactory), provider => {
                var native = (IFileToolsBrowseSessionFactory)ActivatorUtilities.CreateInstance(provider, registration.ImplementationType);
                return probe = new(native, access, denyAfterNativeRead);
            }, registration.Lifetime));
        });
        var target = await CreateTargetAsync(harness, "Original native collection");
        var file = await CreateAuthorityFileAsync(harness, target);
        var reference = Assert.IsType<StorageObjectReference>(StorageJson.ParseReference(file.StorageObjectReferenceJson));
        var storageId = Assert.IsType<Guid>(reference.StorageId);
        var collection = await Workbench(harness).CreateObjectAsync(target.ProjectId,
            new(ProjectObjectType.Infrastructure, "Owned storage collection", "", "", target.Node.Id,
                NodeReferences: new() { InfrastructureStorageCatalogId = storageId }) { ExpectedProjectAdmission = target.Admission });
        var cut = Render(harness, target.ProjectId);
        await ContextActionAsync(cut, collection.Id, ProjectStructureFileActions.BrowseFilesId);
        cut.WaitForAssertion(() => {
            Assert.NotNull(probe);
            Assert.True(probe.Calls > 0);
            var windows = cut.FindComponents<ContentFileCollectionWindow>();
            if (denyAfterNativeRead) {
                Assert.All(windows, window => {
                    Assert.False(window.Instance.State.IsLoading);
                    Assert.Null(window.Instance.State.Browse);
                    Assert.False(string.IsNullOrWhiteSpace(window.Instance.State.OpenError));
                });
            } else {
                var state = Assert.Single(windows).Instance.State;
                Assert.False(state.IsLoading);
                Assert.NotNull(state.Browse);
                Assert.Null(state.OpenError);
            }
        });
    }

    public enum FileAccessBoundary { Allowed, BeforeSave, ReadingSubmission, AfterStored }

    [Theory]
    [InlineData(FileAccessBoundary.Allowed)]
    [InlineData(FileAccessBoundary.BeforeSave)]
    [InlineData(FileAccessBoundary.ReadingSubmission)]
    [InlineData(FileAccessBoundary.AfterStored)]
    public async Task Native_known_file_save_preserves_original_structure_authority_and_exact_accepted_revision(FileAccessBoundary boundary) {
        var access = new EditingUiTestAccess();
        var receipt = new FileReceiptHook();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access));
            var registration = Assert.Single(services, descriptor => descriptor.ServiceType == typeof(IFileCatalogChangeSink));
            var factory = registration.ImplementationFactory;
            Assert.NotNull(factory);
            services.Replace(ServiceDescriptor.Singleton<IFileCatalogChangeSink>(provider => {
                receipt.Inner = (IFileCatalogChangeSink)factory(provider);
                return receipt;
            }));
        });
        var target = await CreateTargetAsync(harness, "Original known file authority");
        var file = await CreateAuthorityFileAsync(harness, target);
        var reference = Assert.IsType<StorageObjectReference>(StorageJson.ParseReference(file.StorageObjectReferenceJson));
        var storageId = Assert.IsType<Guid>(reference.StorageId);
        var storage = Assert.IsType<StorageDriverInput>(await harness.Context.Services.GetRequiredService<IStorageCatalogService>().GetDriverAsync(storageId));
        var driver = harness.Context.Services.GetRequiredService<IStorageDriverRegistry>().Resolve(storage.ProviderKind);
        var revisioned = Assert.IsAssignableFrom<IStorageRevisionedContentDriver>(driver);
        var originalRevision = await revisioned.GetRevisionAsync(storage, reference);
        var cut = Render(harness, target.ProjectId);
        await OpenAuthorityFileAsync(cut, file);
        cut.WaitForAssertion(() => Assert.Single(cut.FindComponents<ProjectStructureAttachmentPreviewDialog>()));
        var interaction = Assert.IsType<ProjectStructureKnownFileInteraction>(cut.FindComponent<ProjectStructureAttachmentPreviewDialog>().Instance.Interaction);
        Assert.True(interaction.CanEdit);
        Assert.NotNull(interaction.Request.ContentRevision);
        var content = new AuthorityFileContent("Owned submitted file body", () => {
            if (boundary == FileAccessBoundary.ReadingSubmission) {
                access.Allowed = false;
            }
        });
        var args = new FileInteractionSaveRequestedEventArgs(new FileSaveRequest(interaction.Session.File, 1,
            content, interaction.Request.ContentRevision, "text/plain", "utf-8"));
        access.Allowed = boundary != FileAccessBoundary.BeforeSave;
        if (boundary == FileAccessBoundary.AfterStored) {
            receipt.AfterStored = () => access.Allowed = false;
        }
        var committed = boundary is FileAccessBoundary.Allowed or FileAccessBoundary.AfterStored;
        if (committed) {
            await cut.InvokeAsync(() => interaction.SaveAsync(args));
            Assert.True(args.HasPersistedRevision);
            Assert.NotEqual(interaction.Request.ContentRevision, args.PersistedRevision);
        } else {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() => cut.InvokeAsync(() => interaction.SaveAsync(args)));
            Assert.False(args.HasPersistedRevision);
            Assert.Equal(originalRevision, await revisioned.GetRevisionAsync(storage, reference));
        }
        await using (var stored = await driver.OpenReadAsync(storage, reference)) {
            using var reader = new StreamReader(stored);
            Assert.Equal(committed ? "Owned submitted file body" : "Original owned file body", await reader.ReadToEndAsync());
        }
        if (boundary == FileAccessBoundary.AfterStored) {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(async () => {
                await using var denied = await interaction.Session.ContentSource.OpenReadAsync(new(interaction.Session.File));
            });
            Assert.True(args.HasPersistedRevision);
        }
        Assert.Equal(boundary != FileAccessBoundary.BeforeSave, content.Opened);
    }

    [Fact]
    public async Task Known_file_open_does_not_publish_a_private_interaction_after_structure_read_loss() {
        var access = new EditingUiTestAccess();
        await using var harness = await ComponentTestHarness.CreateAsync(services => services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access)));
        var target = await CreateTargetAsync(harness, "Denied known file opening");
        var file = await CreateAuthorityFileAsync(harness, target);
        var cut = Render(harness, target.ProjectId);
        access.Allowed = false;
        await OpenAuthorityFileAsync(cut, file);
        Assert.Empty(cut.FindComponents<ProjectStructureAttachmentPreviewDialog>());
    }

    private static Task<ProjectStructureNode> CreateAuthorityFileAsync(ComponentTestHarness harness, Target target)
        => Workbench(harness).CreateObjectAsync(target.ProjectId, new(ProjectObjectType.File, "Owned authority file", "", "Original file notes", target.Node.Id,
            ObjectSubtype: "text", Media: new("authority.txt", "text/plain", Convert.ToBase64String(Encoding.UTF8.GetBytes("Original owned file body")))) {
                ExpectedProjectAdmission = target.Admission
            });

    private static async Task OpenAuthorityFileAsync(IRenderedComponent<ProjectStructurePage> cut, ProjectStructureNode file) {
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(file.Id, JsonSerializer.Serialize(new[] { file.Id })));
        await cut.InvokeAsync(() => {
            var panel = cut.FindComponent<ProjectStructureSelectionPanel>().Instance;
            Assert.Equal(file.Id, panel.State.SelectedNode!.Id);
            Assert.True(panel.State.SelectedNode.AttachmentPreview!.CanRenderPreview);
            return panel.Dispatch.InvokeAsync(new(panel.Origin, panel.State.SelectedNodes.Select(node => node.Id).ToArray(), new InsightsSelectionCommand.Open(InsightsNativeView.AttachmentPreview)));
        });
    }

    private sealed class AuthorityFileContent(string text, Action opened) : IFileSaveContent {
        private readonly byte[] bytes = Encoding.UTF8.GetBytes(text);
        public long? Length => bytes.LongLength;
        public bool Opened { get; private set; }
        public ValueTask<Stream> OpenReadAsync(CancellationToken cancellationToken = default) {
            cancellationToken.ThrowIfCancellationRequested();
            Opened = true;
            opened();
            return ValueTask.FromResult<Stream>(new MemoryStream(bytes, writable: false));
        }
    }

    private sealed class FileReceiptHook : IFileCatalogChangeSink {
        public IFileCatalogChangeSink Inner { get; set; } = default!;
        public Action? AfterStored { get; set; }
        public FileCatalogRevision PublishStorageChanged(Guid storageId) => Inner.PublishStorageChanged(storageId);
        public FileCatalogRevision PublishScopeChanged(FileToolsSemanticScope scope, Guid storageId) {
            var result = Inner.PublishScopeChanged(scope, storageId);
            AfterStored?.Invoke();
            return result;
        }
    }

    private sealed class BrowseAuthorityProbe(IFileToolsBrowseSessionFactory native, EditingUiTestAccess access, bool denyAfterRead) : IFileToolsBrowseSessionFactory {
        public int Calls { get; private set; }
        public async ValueTask<FileToolsBrowseSession> CreateAsync(FileToolsSemanticScope scope, CancellationToken cancellationToken = default,
            Func<CancellationToken, ValueTask>? authorizeRead = null) {
            var result = await native.CreateAsync(scope, cancellationToken, authorizeRead);
            Calls++;
            if (denyAfterRead) {
                access.Allowed = false;
            }
            return result;
        }
    }
}
