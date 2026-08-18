using System.Collections.Frozen;
using CanDoItAll.AppComponents.FileTools;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileBrowser.Components;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.FileInteraction.Components;
using CanDoItAll.FileTools.Integration;
using Microsoft.JSInterop;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class FileToolsHostActionsTests
{
    private const string AdditionalActionId = "test:preview";
    private const string ModulePath = "./_content/CanDoItAll.AppComponents/js/file-tools-host-actions.js";

    [Theory]
    [InlineData("README.md", "text/markdown", true)]
    [InlineData("README.markdown", "text/markdown", true)]
    [InlineData("notes.txt", "text/plain", true)]
    [InlineData("server.log", "text/plain", true)]
    [InlineData("data.json", "application/json", true)]
    [InlineData("data.xml", "application/xml", true)]
    [InlineData("settings.yaml", "application/yaml", true)]
    [InlineData("records.csv", "text/csv", true)]
    [InlineData("diagram.svg", "image/svg+xml", true)]
    [InlineData("photo.png", "image/png", true)]
    [InlineData("photo.jpg", "image/jpeg", true)]
    [InlineData("animation.gif", "image/gif", true)]
    [InlineData("photo.webp", "image/webp", true)]
    [InlineData("photo.bmp", "image/bmp", true)]
    [InlineData("manual.pdf", "application/pdf", true)]
    [InlineData("report.xlsx", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", false)]
    [InlineData("archive.zip", "application/zip", false)]
    public void Default_activation_prefers_specific_file_interaction_profiles(
        string fileName,
        string mediaType,
        bool expected)
    {
        FileInteractionComponentComposition composition = new FileInteractionComponentBuilder()
            .AddBuiltIns()
            .Build();

        bool result = FileInteractionDefaultActivationPolicy.ShouldOpenInternally(
            CreateFileInteractionItem(fileName, mediaType),
            composition.Core);

        Assert.Equal(expected, result);
    }

    [Fact]
    public async Task Catalog_orders_default_and_additional_leaf_actions()
    {
        var availability = new FileToolsBrowseSourceActionAvailability(
            SupportsLocalOpen: true,
            SupportsDownload: true);
        var catalog = new DefaultFileToolsHostActionCatalog(
            _ => availability,
            isLocalLaunchAvailable: true,
            GetAdditionalActionsAsync);

        IReadOnlyList<FileBrowserActionDescriptor> actions = await catalog.GetActionsAsync(
            CreateContext(FileBrowserItemKind.File, FileBrowserItemCapabilities.Preview));

        Assert.Equal(
        [
            FileBrowserActionIds.Open,
            AdditionalActionId,
            FileToolsBrowseHostActionIds.OpenContainingFolder,
            FileBrowserActionIds.Download
        ],
            actions.Select(action => action.Id));
        Assert.True(actions[0].IsPrimary);
    }

    [Fact]
    public async Task Catalog_gates_actions_by_item_and_host_capabilities()
    {
        var availability = new FileToolsBrowseSourceActionAvailability(
            SupportsLocalOpen: true,
            SupportsDownload: true);
        var locallyUnavailableCatalog = new DefaultFileToolsHostActionCatalog(
            _ => availability,
            isLocalLaunchAvailable: false,
            GetAdditionalActionsAsync);

        IReadOnlyList<FileBrowserActionDescriptor> availableActions =
            await locallyUnavailableCatalog.GetActionsAsync(
                CreateContext(FileBrowserItemKind.File, FileBrowserItemCapabilities.Preview));
        IReadOnlyList<FileBrowserActionDescriptor> nonPreviewActions =
            await locallyUnavailableCatalog.GetActionsAsync(
                CreateContext(FileBrowserItemKind.File, FileBrowserItemCapabilities.Select));
        IReadOnlyList<FileBrowserActionDescriptor> containerActions =
            await locallyUnavailableCatalog.GetActionsAsync(
                CreateContext(FileBrowserItemKind.Container, FileBrowserItemCapabilities.Preview));

        Assert.Equal(
            [AdditionalActionId, FileBrowserActionIds.Download],
            availableActions.Select(action => action.Id));
        Assert.Empty(nonPreviewActions);
        Assert.Empty(containerActions);
    }

    [Fact]
    public void Capability_capture_snapshots_declared_and_legacy_provider_availability()
    {
        var declaredSourceId = new FileBrowserSourceId("declared-actions");
        var legacySourceId = new FileBrowserSourceId("legacy-actions");
        var declaredAvailability = new FileToolsBrowseSourceActionAvailability(
            SupportsLocalOpen: true,
            SupportsDownload: false);

        IReadOnlyDictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability> captured =
            FileToolsHostActionCapabilityCapture.Capture(
            [
                new CapableProvider(declaredSourceId, declaredAvailability),
                new LegacyProvider(legacySourceId)
            ]);

        Assert.IsAssignableFrom<FrozenDictionary<FileBrowserSourceId, FileToolsBrowseSourceActionAvailability>>(
            captured);
        Assert.Equal(declaredAvailability, captured[declaredSourceId]);
        Assert.Equal(default, captured[legacySourceId]);
    }

    [Fact]
    public void Capability_capture_rejects_duplicate_source_ids()
    {
        var sourceId = new FileBrowserSourceId("duplicate-actions");

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            FileToolsHostActionCapabilityCapture.Capture(
            [
                new LegacyProvider(sourceId),
                new LegacyProvider(sourceId)
            ]));

        Assert.Contains(sourceId.Value, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Download_imports_uses_and_disposes_a_module_for_each_authorized_lease()
    {
        var runtime = new RecordingJsRuntime(() => new RecordingJsModule());
        var firstLease = new TrackingDownloadLease(@"C:\private\exports\quarterly.xlsx");
        var secondLease = new TrackingDownloadLease("/private/exports/summary.docx");
        var runner = new FileToolsHostActionRunner(runtime);

        FileToolsBrowseItemActionResult firstResult = await runner.ExecuteAsync(
            FileToolsHostAction.Download,
            UnexpectedLaunchAsync,
            _ => ValueTask.FromResult<IFileToolsDownloadLease>(firstLease));
        FileToolsBrowseItemActionResult secondResult = await runner.ExecuteAsync(
            FileToolsHostAction.Download,
            UnexpectedLaunchAsync,
            _ => ValueTask.FromResult<IFileToolsDownloadLease>(secondLease));

        Assert.True(firstResult.IsSuccess);
        Assert.Equal("Downloading quarterly.xlsx.", firstResult.Message);
        Assert.True(secondResult.IsSuccess);
        Assert.Equal("Downloading summary.docx.", secondResult.Message);
        Assert.Equal(2, runtime.ImportCount);
        Assert.Collection(
            runtime.Modules,
            module => AssertCompletedModule(module, "quarterly.xlsx"),
            module => AssertCompletedModule(module, "summary.docx"));
        AssertDisposed(firstLease);
        AssertDisposed(secondLease);
    }

    [Fact]
    public async Task Download_disposes_lease_content_and_module_when_javascript_invocation_fails()
    {
        var failure = new InvalidOperationException("JavaScript download failed.");
        var module = new RecordingJsModule((_, _, _) => ValueTask.FromException(failure));
        var runtime = new RecordingJsRuntime(() => module);
        var lease = new TrackingDownloadLease(@"C:\private\exports\failure.pptx");
        var runner = new FileToolsHostActionRunner(runtime);

        InvalidOperationException exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            runner.ExecuteAsync(
                FileToolsHostAction.Download,
                UnexpectedLaunchAsync,
                _ => ValueTask.FromResult<IFileToolsDownloadLease>(lease))
                .AsTask());

        Assert.Same(failure, exception);
        AssertCompletedModule(module, "failure.pptx");
        AssertDisposed(lease);
    }

    [Fact]
    public async Task Disposing_runner_does_not_interrupt_an_in_flight_javascript_invocation()
    {
        var invocationStarted = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCompletion = new TaskCompletionSource<object?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var module = new RecordingJsModule(async (_, _, cancellationToken) =>
        {
            invocationStarted.TrySetResult(null);
            await allowCompletion.Task.WaitAsync(cancellationToken);
        });
        var runtime = new RecordingJsRuntime(() => module);
        var lease = new TrackingDownloadLease("in-flight.xlsx");
        var runner = new FileToolsHostActionRunner(runtime);

        Task<FileToolsBrowseItemActionResult> execution = runner.ExecuteAsync(
            FileToolsHostAction.Download,
            UnexpectedLaunchAsync,
            _ => ValueTask.FromResult<IFileToolsDownloadLease>(lease))
            .AsTask();
        await invocationStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        await runner.DisposeAsync();
        allowCompletion.TrySetResult(null);
        FileToolsBrowseItemActionResult result = await execution.WaitAsync(TimeSpan.FromSeconds(5));

        Assert.True(result.IsSuccess);
        AssertCompletedModule(module, "in-flight.xlsx");
        AssertDisposed(lease);
        await Assert.ThrowsAsync<ObjectDisposedException>(() =>
            runner.ExecuteAsync(
                FileToolsHostAction.Download,
                UnexpectedLaunchAsync,
                UnexpectedAuthorizationAsync)
                .AsTask());
    }

    private static ValueTask<IReadOnlyList<FileBrowserActionDescriptor>> GetAdditionalActionsAsync(
        FileBrowserHostActionContext context,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult<IReadOnlyList<FileBrowserActionDescriptor>>(
        [
            new FileBrowserActionDescriptor(
                AdditionalActionId,
                "Preview",
                "visibility")
        ]);
    }

    private static FileBrowserHostActionContext CreateContext(
        FileBrowserItemKind kind,
        FileBrowserItemCapabilities capabilities)
    {
        var sourceId = new FileBrowserSourceId("catalog-actions");
        var source = new FileBrowserSourceDescriptor(sourceId, "Catalog files");
        FileBrowserItemCategory category = kind == FileBrowserItemKind.Container
            ? FileBrowserItemCategory.Folder
            : FileBrowserItemCategory.Document;
        FileBrowserChildState childState = kind == FileBrowserItemKind.Container
            ? FileBrowserChildState.HasChildren
            : FileBrowserChildState.Empty;
        var item = new FileBrowserItem(
            new FileBrowserItemKey(sourceId, "report.xlsx", "r1"),
            parentKey: null,
            "report.xlsx",
            kind,
            category,
            childState: childState,
            capabilities: capabilities);
        return new FileBrowserHostActionContext(item, source, snapshotRevision: 1);
    }

    private static FileBrowserItem CreateFileInteractionItem(string fileName, string mediaType)
    {
        var sourceId = new FileBrowserSourceId("interaction-routing");
        return new FileBrowserItem(
            new FileBrowserItemKey(sourceId, fileName, "r1"),
            parentKey: null,
            fileName,
            FileBrowserItemKind.File,
            FileBrowserItemCategory.Document,
            childState: FileBrowserChildState.Empty,
            mediaType: mediaType,
            capabilities: FileBrowserItemCapabilities.Open | FileBrowserItemCapabilities.Preview);
    }

    private static ValueTask<FileToolsBrowseItemActionResult> UnexpectedLaunchAsync(
        FileToolsLocalFileAction action,
        CancellationToken cancellationToken)
        => throw new InvalidOperationException($"Unexpected local launch '{action}'.");

    private static ValueTask<IFileToolsDownloadLease> UnexpectedAuthorizationAsync(
        CancellationToken cancellationToken)
        => throw new InvalidOperationException("Unexpected download authorization.");

    private static void AssertCompletedModule(RecordingJsModule module, string expectedFileName)
    {
        Assert.Equal(1, module.InvocationCount);
        Assert.Equal("downloadFileFromStream", module.Identifier);
        Assert.NotNull(module.Arguments);
        Assert.Equal(2, module.Arguments.Length);
        Assert.Equal(expectedFileName, module.Arguments[0]);
        Assert.IsType<DotNetStreamReference>(module.Arguments[1]);
        Assert.Equal(1, module.DisposeCount);
    }

    private static void AssertDisposed(TrackingDownloadLease lease)
    {
        Assert.Equal(1, lease.OpenCount);
        Assert.Equal(1, lease.DisposeCount);
        Assert.True(lease.Stream.IsDisposed);
    }

    private class LegacyProvider(FileBrowserSourceId sourceId) : IFileBrowserProvider
    {
        public FileBrowserSourceDescriptor Descriptor { get; } = new(sourceId, sourceId.Value);

        public ValueTask<FileBrowserItem> GetRootAsync(
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(
            FileBrowserItemKey itemKey,
            FileBrowserMetadataRequest metadata,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public ValueTask<FileBrowserPage> BrowseAsync(
            FileBrowserBrowseRequest request,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }

    private sealed class CapableProvider(
        FileBrowserSourceId sourceId,
        FileToolsBrowseSourceActionAvailability availability) : LegacyProvider(sourceId),
        IFileToolsBrowseSourceActionCapabilities
    {
        public FileToolsBrowseSourceActionAvailability ActionAvailability { get; } = availability;
    }

    private sealed class RecordingJsRuntime(Func<RecordingJsModule> createModule) : IJSRuntime
    {
        public List<RecordingJsModule> Modules { get; } = [];

        public int ImportCount => Modules.Count;

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Assert.Equal("import", identifier);
            Assert.NotNull(args);
            Assert.Single(args);
            Assert.Equal(ModulePath, args[0]);
            RecordingJsModule module = createModule();
            Modules.Add(module);
            return ValueTask.FromResult((TValue)(object)module);
        }
    }

    private sealed class RecordingJsModule(
        Func<string, object?[]?, CancellationToken, ValueTask>? invoke = null) : IJSObjectReference
    {
        public object?[]? Arguments { get; private set; }

        public int DisposeCount { get; private set; }

        public string? Identifier { get; private set; }

        public int InvocationCount { get; private set; }

        public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
            => InvokeAsync<TValue>(identifier, CancellationToken.None, args);

        public async ValueTask<TValue> InvokeAsync<TValue>(
            string identifier,
            CancellationToken cancellationToken,
            object?[]? args)
        {
            Identifier = identifier;
            Arguments = args?.ToArray();
            InvocationCount++;
            if (invoke is not null)
            {
                await invoke(identifier, args, cancellationToken);
            }

            return default!;
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingDownloadLease(string fileName) : IFileToolsDownloadLease
    {
        public int DisposeCount { get; private set; }

        public string FileName { get; } = fileName;

        public int OpenCount { get; private set; }

        public TrackingStream Stream { get; } = new([1, 2, 3]);

        public ValueTask<FileContentLease> OpenReadAsync(
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCount++;
            return ValueTask.FromResult(new FileContentLease(
                Stream,
                "application/octet-stream",
                Stream.Length));
        }

        public ValueTask DisposeAsync()
        {
            DisposeCount++;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class TrackingStream(byte[] buffer) : MemoryStream(buffer)
    {
        public bool IsDisposed { get; private set; }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
