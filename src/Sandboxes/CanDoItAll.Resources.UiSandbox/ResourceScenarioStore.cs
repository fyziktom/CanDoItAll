using System.Globalization;
using System.Text;
using CanDoItAll.FileTools.FileBrowser;
using CanDoItAll.FileTools.FileInteraction;
using CanDoItAll.FileTools.Integration;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Resources;
using CanDoItAll.SharedKernel;
using CanDoItAll.SharedKernel.Configuration;

namespace CanDoItAll.Resources.UiSandbox;

public enum ResourceScenario { Representative, Empty, Large, UnavailableReferences, RetiredProject, MissingConnector, InvalidFields, FailedReads, RefusedWrite, UnknownWrite, CommittedWarning, DeniedActions, UnavailableActions }
public enum ResourceScenarioLane { RegistryRead, EditorRead, References, Parties, Catalog, Source, Preview, Save, Delete, Promotion, FileAction, Download, SourceRelease, PreviewRelease }

public sealed class ResourceScenarioGate {
    private readonly TaskCompletionSource release = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public async Task PauseAsync() {
        Entered.TrySetResult();
        await release.Task;
    }
    public void Release() => release.TrySetResult();
}

public sealed class ResourceScenarioStore : IResourceRegistryOwner, IResourceBrowseOwner {
    public const string SchemaVersion = "1.0";
    public const string FixturePlugin = "sandbox.fields";
    public const string MissingPlugin = "sandbox.unavailable";
    public const string JsonField = "fixtureJson";
    public const string NumberField = "fixtureNumber";
    public const string SecretField = "fixtureSecret";
    public const string FixtureText = "Owned synthetic Resources content. No production storage or transport is used.\n";
    private readonly Dictionary<ResourceScenarioLane, Queue<ResourceScenarioGate>> gates = [];
    private readonly List<ResourceScenarioGate> active = [];
    private readonly Dictionary<Guid, ResourceFileSelection> storedFiles = [];
    private readonly Dictionary<ResourceFileSourceKey, ResourceFixtureProvider> providers = [];
    private int nextId = 1000;
    private int revision;
    public ResourceScenario Scenario { get; }
    public Dictionary<Guid, ResourceEditorModel> Records { get; } = [];
    public List<ResourceProjectOption> Projects { get; } = [];
    public List<ResourceBrowseSource> Sources { get; } = [];
    public List<ResourceRegistryDispatch> Writes { get; } = [];
    public List<ResourcePromotionRequest> PromotionCommands { get; } = [];
    public List<(ResourceFileSelection Selection, FileToolsLocalFileAction Action)> Launches { get; } = [];
    public int ReleaseCount { get; private set; }
    public int ContentReleaseCount { get; private set; }
    public int DownloadReleaseCount { get; private set; }
    public int ReadCount { get; private set; }
    public int PendingCount => active.Count;
    public bool IsCurrent { get; set; } = true;
    public bool FailNextRead { get; set; }
    public bool FailNextPreview { get; set; }
    public bool FailCleanup { get; set; }
    public bool IsLocalLaunchAvailable => Scenario != ResourceScenario.UnavailableActions;
    public Func<ResourceEditorModel, Task>? BeforeSave { get; set; }
    public Func<Guid, Task>? AfterSave { get; set; }
    public Func<Guid, Task>? BeforeGet { get; set; }
    public ResourceProjectOption PrimaryProject => Projects[0];
    public static Guid FixtureId(int value) => new(value, 0, 0, new byte[8]);

    public ResourceScenarioStore(ResourceScenario scenario = ResourceScenario.Representative) {
        Scenario = scenario;
        Projects.Add(new(FixtureId(1), "Owned sample project", new(FixtureId(2), FixtureId(1), FixtureId(3))));
        Projects.Add(new(FixtureId(4), "Second sample project", new(FixtureId(2), FixtureId(4), FixtureId(5))));
        if (scenario == ResourceScenario.Empty) {
            return;
        }
        var count = scenario == ResourceScenario.Large ? 200 : 3;
        for (var i = 0; i < count; i++) {
            var record = CreateEditor($"Sample resource {i + 1}");
            record.Id = FixtureId(100 + i);
            Records.Add(record.Id.Value, record);
        }
        foreach (var sourceClass in Enum.GetValues<ResourceFileSourceClass>()) {
            var id = FixtureId(20 + (int)sourceClass);
            var key = sourceClass == ResourceFileSourceClass.Project ? ResourceFileSourceKey.ForProject(PrimaryProject.Id) : ResourceFileSourceKey.ForStorage(id);
            var name = $"Synthetic {sourceClass} files";
            var scope = new FileToolsSemanticScope(FileToolsSemanticScopeKind.ResourceSource, new FileToolsSemanticScopeId(key.Value), name);
            Sources.Add(new(key, sourceClass, name, "Bounded in-memory fixture; no live transport", scope,
                sourceClass == ResourceFileSourceClass.Project ? null : id, true, ResourceSourceHealth.Healthy));
            providers.Add(key, new(key.Value, scenario == ResourceScenario.Large ? 220 : 3));
        }
        var first = Records.Values.First();
        if (scenario == ResourceScenario.UnavailableReferences) {
            first.LinkedSecretId = FixtureId(300);
            first.OwnerPartyId = FixtureId(301);
            first.MaintainerPartyId = FixtureId(302);
        }
        if (scenario == ResourceScenario.RetiredProject) {
            first.ExpectedProjectAdmission = new(FixtureId(2), PrimaryProject.Id, FixtureId(999));
        }
        if (scenario == ResourceScenario.MissingConnector) {
            first.ConnectorPluginKey = MissingPlugin;
            first.Configuration.SetText(JsonField, "{\"retained\":true}");
            first.ConfigJson = first.Configuration.ToJson();
        }
        if (scenario == ResourceScenario.InvalidFields) {
            first.ConnectorPluginKey = FixturePlugin;
            first.Configuration.SetText(NumberField, "1e-");
            first.Configuration.SetText(JsonField, "{\"unfinished\":");
        }
    }

    public ResourceEditorModel CreateEditor(string name) {
        var editor = new ResourceEditorModel { Name = name, ProjectId = PrimaryProject.Id, ExpectedProjectAdmission = PrimaryProject.Admission,
            ConnectorPluginKey = ResourceConnectorPluginKeys.WebLink, ConfigSchemaVersion = SchemaVersion, Description = "Synthetic metadata", SupportsPreview = true };
        editor.Configuration.SetText(ResourceConnectorFieldKeys.WebUrl, "https://example.test/resource");
        editor.LocationOrIdentifier = BuildLocationPreview(editor);
        return editor;
    }

    public IReadOnlyList<ConnectorPluginManifest> ListManifests() => [
        Manifest(ResourceConnectorPluginKeys.WebLink, "Web link resource", [new(ResourceConnectorFieldKeys.WebUrl, "URL", ConfigurationFieldType.Url, true, "Absolute URL."), new(ResourceConnectorFieldKeys.UrlTitleHint, "Title hint", ConfigurationFieldType.Text, false, "Optional title.")]),
        Manifest(FixturePlugin, "Synthetic field examples", [new(NumberField, "Number", ConfigurationFieldType.Number, false, "Incomplete input remains visible."), new(JsonField, "JSON", ConfigurationFieldType.Json, false, "Raw JSON is validated on save."), new(SecretField, "Secret reference", ConfigurationFieldType.SecretReference, false, "Reference ID only.")]),
        Manifest(ResourceConnectorPluginKeys.StorageObject, "Governed storage object", [])
    ];
    private static ConnectorPluginManifest Manifest(string key, string name, IReadOnlyList<ConfigurationFieldDescriptor> fields) => new(key, name, SchemaVersion,
        ConnectorManifestCapability.ProjectResource, new(SchemaVersion, fields), [], new("fixture", "Synthetic schema; production connectors keep their own policy."), new("fixture", false, true, "No Agent execution"), null);
    public string BuildLocationPreview(ResourceEditorModel editor) => editor.Configuration.GetText(ResourceConnectorFieldKeys.WebUrl);
    public async Task<IReadOnlyList<ResourceSummary>> ListAsync(CancellationToken cancellationToken = default) {
        await ReadAsync(ResourceScenarioLane.RegistryRead);
        return Records.Values.Select(r => new ResourceSummary(r.Id!.Value, r.ProjectId!.Value, Projects.FirstOrDefault(p => p.Id == r.ProjectId)?.Name ?? "Unavailable project",
            null, r.ConnectorPluginKey, ListManifests().FirstOrDefault(m => m.PluginKey == r.ConnectorPluginKey)?.DisplayName ?? "Unavailable connector", r.Name,
            r.LocationOrIdentifier, r.ValidationStatus, r.Sensitivity) { ProjectLifetimeId = r.ExpectedProjectAdmission?.LifetimeId }).ToArray();
    }
    public async Task<ResourceEditorModel> GetAsync(Guid id, CancellationToken cancellationToken = default) {
        await ReadAsync(ResourceScenarioLane.EditorRead);
        if (BeforeGet is { } before) {
            await before(id);
        }
        return Records.TryGetValue(id, out var record) ? record.Capture() : new();
    }
    public async Task<IReadOnlyList<ResourceProjectOption>> ListProjectsAsync(CancellationToken cancellationToken = default) {
        await ReadAsync(ResourceScenarioLane.References);
        return Projects.ToArray();
    }
    public Task<IReadOnlyList<ResourceReferenceOption>> ListSecretsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ResourceReferenceOption>>([new(FixtureId(10), "Sample secret reference")]);
    public async Task<IReadOnlyList<ResourceReferenceOption>> ListPartiesAsync(Guid? projectId, IReadOnlyList<Guid> retainedIds, CancellationToken cancellationToken = default) {
        await ReadAsync(ResourceScenarioLane.Parties);
        return [new(FixtureId(11), "Sample owner")];
    }
    public async Task<Result<Guid>> SaveAsync(ResourceEditorModel command) {
        await PauseAsync(ResourceScenarioLane.Save);
        RequireAdmission(command.ExpectedProjectAdmission);
        if (BeforeSave is { } before) {
            await before(command);
        }
        var captured = command.Capture();
        if (captured.Id is { } existing && !Records.ContainsKey(existing)) {
            throw new ResourceActionRefusedException("The exact resource no longer exists.");
        }
        captured.Id ??= FixtureId(nextId++);
        captured.Name = captured.Name.Trim();
        captured.LocationOrIdentifier = BuildLocationPreview(captured);
        captured.ConfigJson = captured.Configuration.ToJson();
        Records[captured.Id.Value] = captured;
        Writes.Add(new(ResourceMutationKind.Save, command.Capture(), captured.Id.Value));
        if (AfterSave is { } after) {
            await after(captured.Id.Value);
        }
        ReportWrite(captured.Id.Value, ResourceMutationKind.Save);
        return Result<Guid>.Success(captured.Id.Value);
    }
    public async Task DeleteAsync(Guid id, ProjectWriteAdmission? admission) {
        await PauseAsync(ResourceScenarioLane.Delete);
        if (Scenario == ResourceScenario.RefusedWrite || !Records.TryGetValue(id, out var record) || record.ExpectedProjectAdmission != admission) {
            throw new ResourceActionRefusedException("The exact deletion admission was refused.");
        }
        Records.Remove(id);
        Writes.Add(new(ResourceMutationKind.Delete, record.Capture(), id));
        ReportWrite(id, ResourceMutationKind.Delete);
    }
    private void RequireAdmission(ProjectWriteAdmission? admission) {
        if (Scenario == ResourceScenario.RefusedWrite || !Projects.Any(p => p.Admission == admission)) {
            throw new ResourceActionRefusedException("The current project lifetime refused this command.");
        }
    }
    private void ReportWrite(Guid id, ResourceMutationKind kind) {
        if (Scenario == ResourceScenario.UnknownWrite) {
            throw new IOException("Synthetic acknowledgement loss after storage.");
        }
        if (Scenario == ResourceScenario.CommittedWarning) {
            throw new ResourceCommittedMutationException(id, kind, new IOException("Synthetic postcommit read failure."));
        }
    }
    public async Task<ResourceBrowseCatalog> LoadAsync(CancellationToken cancellationToken = default) {
        await ReadAsync(ResourceScenarioLane.Catalog);
        return new(Sources.ToArray(), Projects.ToArray(), revision.ToString(CultureInfo.InvariantCulture));
    }
    public async ValueTask<ResourceBrowseLease> OpenAsync(ResourceFileSourceKey key, CancellationToken cancellationToken = default) {
        await ReadAsync(ResourceScenarioLane.Source);
        var source = Sources.Single(s => s.Key == key);
        var provider = providers[key];
        var value = revision.ToString(CultureInfo.InvariantCulture);
        var browser = new FileBrowserSession(new FileBrowserSourceSet(value, [provider]), options: new FileBrowserSessionOptions(pageSize: 50,
            retentionMode: FileBrowserStateRetentionMode.Disabled, searchBudget: new(32, 2_000, TimeSpan.FromSeconds(5), 1, 200, 2L * 1024 * 1024)));
        return new(source, browser, new(IsLocalLaunchAvailable, true), value, async () => {
            ReleaseCount++;
            await PauseAsync(ResourceScenarioLane.SourceRelease);
            await browser.DisposeAsync();
            if (FailCleanup) {
                throw new IOException("Synthetic release failure.");
            }
        });
    }
    public ResourceFileSelection Selection(ResourceFileSourceKey key, int index = 0) => new(Sources.Single(s => s.Key == key), revision.ToString(CultureInfo.InvariantCulture), providers[key].Files[index]);
    public async ValueTask<ResourcePromotionObservation> PromoteAsync(ResourcePromotionRequest command) {
        await PauseAsync(ResourceScenarioLane.Promotion);
        RequireAdmission(command.Project);
        RequireSelection(command.Selection);
        PromotionCommands.Add(command);
        var existing = storedFiles.FirstOrDefault(p => p.Value.Source.Key == command.Selection.Source.Key && p.Value.Item.Key == command.Selection.Item.Key && Records[p.Key].ExpectedProjectAdmission == command.Project);
        var id = existing.Key;
        var created = id == Guid.Empty;
        if (created) {
            id = FixtureId(nextId++);
            var editor = CreateEditor(command.Name);
            editor.Id = id;
            editor.ProjectId = command.Project.ProjectId;
            editor.ExpectedProjectAdmission = command.Project;
            editor.ConnectorPluginKey = ResourceConnectorPluginKeys.StorageObject;
            editor.Sensitivity = command.Sensitivity;
            editor.LocationOrIdentifier = $"Synthetic stable object {command.Selection.Item.Name}";
            Records.Add(id, editor);
            storedFiles.Add(id, command.Selection);
        }
        revision++;
        if (Scenario == ResourceScenario.UnknownWrite) {
            throw new IOException("Synthetic promotion acknowledgement loss.");
        }
        return new(id, created, Scenario == ResourceScenario.CommittedWarning ? null : revision,
            Scenario == ResourceScenario.CommittedWarning ? "Stored object saved; synthetic revision publication failed." : null);
    }
    public async ValueTask<ResourcePreviewLease> OpenResourceAsync(Guid resourceId, CancellationToken cancellationToken = default) {
        await ReadAsync(ResourceScenarioLane.Preview);
        if (FailNextPreview) {
            FailNextPreview = false;
            throw new IOException("Synthetic preview failure.");
        }
        var selection = storedFiles[resourceId];
        if (!Sources.Any(s => s.Key == selection.Source.Key) || Scenario == ResourceScenario.DeniedActions) {
            throw new ResourceActionRefusedException("Current fixture authority denies reopen.");
        }
        var file = new FileReference("resources-fixture", resourceId.ToString("N"));
        return new(resourceId, new(file, selection.Item.Name, FileInteractionMode.View, "text/plain", Encoding.UTF8.GetByteCount(FixtureText)),
            new ResourceFixtureContent(file), async () => {
                ContentReleaseCount++;
                await PauseAsync(ResourceScenarioLane.PreviewRelease);
                if (FailCleanup) {
                    throw new IOException("Synthetic preview release failure.");
                }
            });
    }
    public async ValueTask<FileToolsBrowseItemActionResult> LaunchAsync(ResourceFileSelection selection, FileToolsLocalFileAction action, CancellationToken cancellationToken = default) {
        await PauseAsync(ResourceScenarioLane.FileAction);
        RequireSelection(selection);
        if (!IsLocalLaunchAvailable || Scenario == ResourceScenario.DeniedActions) {
            throw new ResourceActionRefusedException("Synthetic local launch is unavailable or denied.");
        }
        Launches.Add((selection, action));
        return FileToolsBrowseItemActionResult.Success("Recorded synthetic local-launch intent; no application was launched.");
    }
    public async ValueTask<IFileToolsDownloadLease> AuthorizeDownloadAsync(ResourceFileSelection selection, CancellationToken cancellationToken = default) {
        await PauseAsync(ResourceScenarioLane.Download);
        RequireSelection(selection);
        if (Scenario == ResourceScenario.DeniedActions) {
            throw new ResourceActionRefusedException("Synthetic download authorization denied.");
        }
        return new ResourceFixtureDownload(selection.Item.Name, () => DownloadReleaseCount++);
    }
    private void RequireSelection(ResourceFileSelection selection) {
        if (!IsCurrent || !Sources.Any(s => s == selection.Source) || selection.Revision != revision.ToString(CultureInfo.InvariantCulture) ||
            !providers[selection.Source.Key].Files.Any(f => f.Key == selection.Item.Key)) {
            throw new ResourceActionRefusedException("The exact source, item or revision is no longer current.");
        }
    }
    public ResourceScenarioGate HoldNext(ResourceScenarioLane lane) {
        var gate = new ResourceScenarioGate();
        if (!gates.TryGetValue(lane, out var queue)) {
            queue = new();
            gates.Add(lane, queue);
        }
        queue.Enqueue(gate);
        return gate;
    }
    private async Task PauseAsync(ResourceScenarioLane lane) {
        if (gates.TryGetValue(lane, out var queue) && queue.TryDequeue(out var gate)) {
            active.Add(gate);
            await gate.PauseAsync();
            active.Remove(gate);
        }
    }
    public void ReleaseAll() {
        foreach (var gate in active.Concat(gates.Values.SelectMany(q => q)).ToArray()) {
            gate.Release();
        }
    }
    private async Task ReadAsync(ResourceScenarioLane lane) {
        ReadCount++;
        await PauseAsync(lane);
        if (FailNextRead || Scenario == ResourceScenario.FailedReads) {
            FailNextRead = false;
            throw new IOException("Synthetic owner read failure.");
        }
    }
}

public sealed record ResourceRegistryDispatch(ResourceMutationKind Kind, ResourceEditorModel Command, Guid ResourceId);

public sealed class ResourceFixtureProvider : IFileBrowserProvider {
    private readonly FileBrowserItem root;
    public FileBrowserSourceDescriptor Descriptor { get; }
    public IReadOnlyList<FileBrowserItem> Files { get; }
    public ResourceFixtureProvider(string sourceId, int count) {
        Descriptor = new(new FileBrowserSourceId(sourceId), "Bounded fixture files");
        root = new(new(Descriptor.Id, "root", "fixture-1"), null, "Root", FileBrowserItemKind.Container, FileBrowserItemCategory.Folder,
            childState: FileBrowserChildState.HasChildren, capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Navigate);
        Files = Enumerable.Range(0, count).Select(i => new FileBrowserItem(new(Descriptor.Id, $"file-{i}", "fixture-1"), root.Key,
            i == 1 ? "unsupported.xlsx" : $"report-{i + 1:D3}.txt", FileBrowserItemKind.File, FileBrowserItemCategory.Document,
            childState: FileBrowserChildState.Empty, size: Encoding.UTF8.GetByteCount(ResourceScenarioStore.FixtureText),
            mediaType: i == 1 ? "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" : "text/plain",
            capabilities: FileBrowserItemCapabilities.Select | FileBrowserItemCapabilities.Preview)).ToArray();
    }
    public ValueTask<FileBrowserItem> GetRootAsync(FileBrowserMetadataRequest metadata, CancellationToken cancellationToken = default) => ValueTask.FromResult(root);
    public ValueTask<IReadOnlyList<FileBrowserItem>> GetPathAsync(FileBrowserItemKey itemKey, FileBrowserMetadataRequest metadata, CancellationToken cancellationToken = default) => ValueTask.FromResult<IReadOnlyList<FileBrowserItem>>([root]);
    public ValueTask<FileBrowserPage> BrowseAsync(FileBrowserBrowseRequest request, CancellationToken cancellationToken = default) {
        var offset = request.ContinuationToken is { } value ? int.Parse(value, CultureInfo.InvariantCulture) : 0;
        var items = Files.Skip(offset).Take(request.PageSize).ToArray();
        var next = offset + items.Length;
        return ValueTask.FromResult(new FileBrowserPage(items, next < Files.Count ? next.ToString(CultureInfo.InvariantCulture) : null, Files.Count, "fixture-1"));
    }
}

public sealed class ResourceFixtureContent(FileReference file) : IFileContentSource {
    public ValueTask<FileContentLease> OpenReadAsync(FileContentReadRequest request, CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        if (request.File != file) {
            throw new ResourceActionRefusedException("The fixture content identity does not match.");
        }
        var bytes = Encoding.UTF8.GetBytes(ResourceScenarioStore.FixtureText);
        var offset = (int)Math.Min(request.Offset, bytes.Length);
        var count = (int)Math.Min(request.Length ?? bytes.Length, bytes.Length - offset);
        return ValueTask.FromResult(new FileContentLease(new MemoryStream(bytes, offset, count, writable: false), "text/plain", count));
    }
}

internal sealed class ResourceFixtureDownload(string fileName, Action released) : IFileToolsDownloadLease {
    private int disposed;
    public string FileName => fileName;
    public ValueTask<FileContentLease> OpenReadAsync(CancellationToken cancellationToken = default) {
        cancellationToken.ThrowIfCancellationRequested();
        var bytes = Encoding.UTF8.GetBytes(ResourceScenarioStore.FixtureText);
        return ValueTask.FromResult(new FileContentLease(new MemoryStream(bytes, writable: false), "text/plain", bytes.Length));
    }
    public ValueTask DisposeAsync() {
        if (Interlocked.Exchange(ref disposed, 1) == 0) {
            released();
        }
        return ValueTask.CompletedTask;
    }
}
