using CanDoItAll.Memory.Abstractions;
using CanDoItAll.Modules.Memory.Components;
using CanDoItAll.Modules.Memory.Services;
using System.Text.Json;

namespace CanDoItAll.Memory.UiSandbox;

public enum MemoryScenario { Populated, InitialLoading, Empty, LargeCatalog, Unavailable, Partial, AcceptedQuery, UnknownQuery, UiVariants }
public enum MemoryScenarioLane { Read, Save, Query, Status }

public sealed class MemoryScenarioStore : IMemoryProviderManagementUiService {
    public const string ProviderA = "provider.a";
    public const string ProviderB = "provider.b";
    private readonly Dictionary<string, MemoryProviderProfileEditorModel> profiles = new(StringComparer.Ordinal);
    private readonly List<MemoryProviderOperationUiRecord> operations = [];
    private readonly HashSet<MemoryScenarioLane> heldLanes = [];
    private readonly List<TaskCompletionSource> pending = [];
    private readonly MemoryScenario scenario;
    private readonly MemoryProviderUiSurfaceProjector projector = new(new MemoryProviderUiSurfaceComponentRegistry([
        new(MemoryProviderUiSurfaceKeys.MockProviderPanelComponent, typeof(MemoryMockProviderPanel))]));
    public bool IsCurrent { get; set; } = true;
    public bool FailNextRead { get; set; }
    public bool FailNextSave { get; set; }
    public bool FailAfterSave { get; set; }
    public int WriteCount { get; private set; }
    public int QueryCount { get; private set; }
    public int StatusCount { get; private set; }
    public int ReadCount { get; private set; }
    public int PendingCount => pending.Count;
    public MemoryQueryEditorModel? LastQuery { get; private set; }
    public MemoryProviderProfileEditorModel? LastSave { get; private set; }
    public IReadOnlyDictionary<string, MemoryProviderProfileEditorModel> Profiles => profiles;

    public MemoryScenarioStore(MemoryScenario scenario = MemoryScenario.Populated, string fixtureUrl = "http://localhost/provider-fixture") {
        this.scenario = scenario;
        if (scenario == MemoryScenario.InitialLoading) {
            HoldNext(MemoryScenarioLane.Read);
        }
        if (scenario == MemoryScenario.Empty) {
            return;
        }
        var a = CreateEditor(ProviderA, "Business memory");
        a.SupportsRclUi = true;
        a.PreservedUiSurfaces = [new(MemoryProviderUiSurfaceKind.RazorComponentLibrary, "Provider panel", MemoryProviderUiSurfaceKeys.MockProviderPanelComponent, null, MemoryCapabilityIds.UiRcl)];
        if (scenario is MemoryScenario.AcceptedQuery or MemoryScenario.UnknownQuery) {
            a.DriverKind = MemoryProviderDriverKind.Mcp;
            a.SupportsContextQueryAsync = true;
            a.SupportsOperationStatus = true;
            a.Mcp.ContextQueryTool = "context_query";
            a.Mcp.OperationStatusTool = "operation_status";
        }
        if (scenario == MemoryScenario.UiVariants) {
            a.SupportsIframeUi = true;
            a.PreservedUiSurfaces = [.. a.PreservedUiSurfaces,
                new(MemoryProviderUiSurfaceKind.RazorComponentLibrary, "Missing registration", "unregistered.fixture", null, MemoryCapabilityIds.UiRcl),
                new(MemoryProviderUiSurfaceKind.Iframe, "Owned console", null, MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension, MemoryCapabilityIds.UiIframe),
                new(MemoryProviderUiSurfaceKind.ExternalUrl, "Owned external console", null, MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension, MemoryCapabilityIds.UiIframe),
                new(MemoryProviderUiSurfaceKind.Iframe, "Unsafe URL", null, "provider.vendor.fixture.invalid", MemoryCapabilityIds.UiIframe),
                new(MemoryProviderUiSurfaceKind.Iframe, "Missing URL", null, "provider.vendor.fixture.missing", MemoryCapabilityIds.UiIframe),
                new(MemoryProviderUiSurfaceKind.RazorComponentLibrary, "Missing capability", "unregistered.fixture", null, MemoryCapabilityIds.EventsProviderPush)];
            a.PreservedExtensions = new(new Dictionary<string, JsonElement> {
                [MemoryProviderUiSurfaceKeys.ProviderVendorUiUrlExtension] = JsonSerializer.SerializeToElement(fixtureUrl),
                ["provider.vendor.fixture.invalid"] = JsonSerializer.SerializeToElement("http://invalid.example/console?credential=redacted")
            });
        }
        profiles.Add(ProviderA, a);
        profiles.Add(ProviderB, CreateEditor(ProviderB, "Engineering memory"));
        var http = CreateEditor("provider.http", "HTTP transport draft");
        http.DriverKind = MemoryProviderDriverKind.Http;
        http.Http.BaseUrl = "https://example.invalid";
        http.Http.ApiKeyEnvironmentVariable = "MEMORY_FIXTURE_KEY";
        profiles.Add(http.InstanceId, http);
        if (scenario == MemoryScenario.LargeCatalog) {
            for (var i = 0; i < 100; i++) {
                var editor = CreateEditor($"provider.catalog-{i:D3}", $"Catalog provider {i:D3}");
                profiles.Add(editor.InstanceId, editor);
            }
        }
    }

    public void HoldNext(MemoryScenarioLane lane) => heldLanes.Add(lane);
    public void ReleaseAll() {
        foreach (var completion in pending.ToArray()) {
            completion.TrySetResult();
        }
        pending.Clear();
        heldLanes.Clear();
    }
    public void Remove(string id) => profiles.Remove(id);

    private async Task WaitAsync(MemoryScenarioLane lane, CancellationToken token = default) {
        if (!heldLanes.Remove(lane)) {
            return;
        }
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        pending.Add(completion);
        try {
            await completion.Task.WaitAsync(token);
        } finally {
            pending.Remove(completion);
        }
    }

    public async Task<MemoryProviderManagementSnapshot> GetSnapshotAsync(string? selectedProviderInstanceId = null, CancellationToken cancellationToken = default) {
        ReadCount++;
        await WaitAsync(MemoryScenarioLane.Read, cancellationToken);
        if (scenario == MemoryScenario.Unavailable || FailNextRead) {
            FailNextRead = false;
            throw new InvalidOperationException("Synthetic read failure.");
        }
        var views = profiles.Values.Select(ToProfile).Select(MemoryProviderManagementProfile.FromProfile).OrderBy(p => p.DisplayName, StringComparer.Ordinal).ToArray();
        var selected = selectedProviderInstanceId is null ? views.FirstOrDefault() : views.SingleOrDefault(p => p.InstanceId.Value == selectedProviderInstanceId);
        var now = new DateTimeOffset(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);
        MemoryProviderFeedbackUiRecord[] feedback = selected is null ? [] : [new(new(Guid.Parse("307994db-13c7-4d8e-9966-124e92a97a02")), selected.InstanceId,
            MemoryFeedbackStage.ContextUsed, MemoryFeedbackOutcome.Useful, MemoryFeedbackMatchState.Unmatched, MemoryLedgerStatus.Pending,
            "Synthetic unmatched feedback: no trusted context delivery", now, now)];
        MemoryProviderEventUiRecord[] events = selected is null ? [] : [new(new(Guid.Parse("efbf1581-3653-489f-a9e7-cf4801c6e02c")), selected.InstanceId,
            new(Guid.Parse("a80338bc-bf6e-411a-897e-7bebfef5d8bf")), MemoryProviderEventKind.VerificationRequest, MemoryEventPriority.Normal,
            MemoryLedgerStatus.Pending, "Synthetic pending inbox; acknowledgement remains unavailable", now, now)];
        return new(views, selected, operations.Where(o => o.ProviderInstanceId == selected?.InstanceId).Take(100).ToArray(), feedback, events, selected is null ? [] : projector.Project(selected)) {
            Editor = selected is null ? null : profiles[selected.InstanceId.Value].Capture(),
            SelectedRevision = selected is null ? null : MemoryProviderRevision.Capture(selected),
            FailedRegions = scenario == MemoryScenario.Partial ? [MemoryReadRegion.Feedback] : []
        };
    }

    public async Task<MemoryProviderProfile> SaveProviderAsync(MemoryProviderProfileEditorModel editor, CancellationToken cancellationToken = default) {
        var input = editor.Capture();
        LastSave = input;
        await WaitAsync(MemoryScenarioLane.Save);
        if (FailNextSave) {
            FailNextSave = false;
            throw new MemoryActionRefusedException("Synthetic validation refusal.");
        }
        profiles[input.InstanceId] = input;
        WriteCount++;
        if (FailAfterSave) {
            FailAfterSave = false;
            throw new InvalidOperationException("Synthetic lost response after store update.");
        }
        return ToProfile(input);
    }

    public async Task<IReadOnlyList<MemoryProviderProfile>> CreateDemoProvidersAsync(CancellationToken cancellationToken = default) {
        var saved = new List<MemoryProviderProfile>();
        foreach (var id in new[] { MemoryDemoProviderIds.Business, MemoryDemoProviderIds.Programming }) {
            if (!profiles.ContainsKey(id)) {
                saved.Add(await SaveProviderAsync(CreateEditor(id, id), cancellationToken));
            }
        }
        return saved;
    }

    public async Task<MemoryProviderQueryUiResult> RunQueryAsync(string? selectedProviderInstanceId, MemoryQueryEditorModel editor, CancellationToken cancellationToken = default) {
        var input = editor.Capture();
        var provider = Require(selectedProviderInstanceId, input.UseAsyncQuery ? MemoryCapabilityIds.ContextQueryAsync : MemoryCapabilityIds.ContextQuerySync);
        QueryCount++;
        LastQuery = input;
        await WaitAsync(MemoryScenarioLane.Query);
        if (scenario == MemoryScenario.UnknownQuery) {
            throw new InvalidOperationException("Synthetic lost response; operation identity unavailable.");
        }
        var id = new MemoryOperationId(Guid.NewGuid());
        var now = DateTimeOffset.Parse("2026-09-28T12:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        var accepted = input.UseAsyncQuery ? new MemoryOperationAccepted(id, "/fixture/status", now.AddHours(1), TimeSpan.FromSeconds(1), false) : null;
        var record = new MemoryProviderOperationUiRecord(id, provider.InstanceId, input.UseAsyncQuery ? MemoryCapabilityIds.ContextQueryAsync : MemoryCapabilityIds.ContextQuerySync,
            MemoryOperationKind.ContextQuery, input.UseAsyncQuery ? MemoryLedgerStatus.Accepted : MemoryLedgerStatus.Completed, "Synthetic owner fixture", now, now, input.UseAsyncQuery ? null : now, accepted, null);
        operations.Add(record);
        var pack = input.UseAsyncQuery ? null : new MemoryContextPack(new(Guid.NewGuid()), $"Synthetic context for {input.Query}",
            [new("Provenance", input.SourceModule, [new(input.SourceRecordId, input.Citation)], 1)], [], 1, null);
        return new(input.UseAsyncQuery ? MemoryProviderActionStatus.Accepted : MemoryProviderActionStatus.Completed, "Synthetic query result; no driver was invoked.", record, pack, accepted, null, false);
    }

    public async Task<MemoryProviderOperationUiResult> RefreshOperationAsync(string operationId, CancellationToken cancellationToken = default) {
        var id = new MemoryOperationId(Guid.Parse(operationId));
        var record = operations.Single(o => o.OperationId == id);
        Require(record.ProviderInstanceId.Value, MemoryCapabilityIds.OperationStatus);
        StatusCount++;
        await WaitAsync(MemoryScenarioLane.Status);
        var updated = record with { Status = MemoryLedgerStatus.Completed, StatusReason = "Synthetic status completed" };
        operations[operations.IndexOf(record)] = updated;
        return new(MemoryProviderActionStatus.Completed, updated.StatusReason, updated);
    }
    public Task<MemoryProviderOperationUiResult> CancelOperationAsync(string operationId, CancellationToken cancellationToken = default) => throw Refused();
    public Task<MemoryProviderFeedbackUiResult> SubmitFeedbackAsync(string? selectedProviderInstanceId, MemoryFeedbackEditorModel editor, CancellationToken cancellationToken = default) => throw Refused();
    public Task<MemoryProviderManualIngestionUiResult> EnqueueManualIngestionAsync(string? selectedProviderInstanceId, MemoryManualIngestionEditorModel editor, CancellationToken cancellationToken = default) => throw Refused();
    public Task<MemoryProviderEventAcknowledgeUiResult> AcknowledgeEventAsync(string? selectedProviderInstanceId, string providerEventId, bool accepted, CancellationToken cancellationToken = default) => throw Refused();
    private static MemoryActionRefusedException Refused() => new("This action is not executable by the shipped drivers. The sandbox preserves that refusal.");

    private MemoryProviderProfile Require(string? id, MemoryCapabilityId capability) {
        if (id is null || !profiles.TryGetValue(id, out var editor)) {
            throw new MemoryActionRefusedException("Select an existing provider.");
        }
        var provider = ToProfile(editor);
        if (!provider.IsEnabled || provider.HealthState != MemoryProviderHealthState.Healthy || !provider.Manifest.Capabilities.Any(c => c.Supported && c.Id == capability) || !MemoryProviderCapabilityPolicy.CanExecute(provider.DriverKind, capability)) {
            throw Refused();
        }
        return provider;
    }

    public static MemoryProviderProfileEditorModel CreateEditor(string id, string name) => new() { InstanceId = id, DisplayName = name, HealthState = MemoryProviderHealthState.Healthy, SelectionTags = ["sandbox"] };

    private static MemoryProviderProfile ToProfile(MemoryProviderProfileEditorModel editor) {
        var capabilities = new List<MemoryCapabilityDescriptor>();
        Add(editor.SupportsContextQuerySync, MemoryCapabilityIds.ContextQuerySync);
        Add(editor.SupportsContextQueryAsync, MemoryCapabilityIds.ContextQueryAsync);
        Add(editor.SupportsOperationStatus, MemoryCapabilityIds.OperationStatus);
        Add(editor.SupportsRclUi, MemoryCapabilityIds.UiRcl);
        Add(editor.SupportsIframeUi, MemoryCapabilityIds.UiIframe);
        return new(MemoryProviderInstanceId.Parse(editor.InstanceId), editor.DisplayName, editor.DriverKind, editor.IsEnabled, editor.HealthState,
            editor.WorkspaceScope, editor.SelectionTags.ToArray(), new(editor.FallbackBehavior), new(MemoryProviderKind.Parse(editor.ProviderKind), editor.PreservedProtocolVersion,
                capabilities, editor.PreservedInteractionSupport ?? MemoryProviderInteractionSupport.SyncQueryOnly, editor.PreservedUiSurfaces, editor.PreservedLimits, editor.PreservedExtensions));
        void Add(bool enabled, MemoryCapabilityId id) {
            if (enabled) {
                capabilities.Add(new(id, "1", true));
            }
        }
    }
}
