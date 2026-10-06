using System.Data.Common;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;
using Bunit;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Security;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.Modules.Workspace;
using CanDoItAll.Security.Abstractions;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Operators.UI.Secrets;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureSecretReferenceTests {
    public enum OriginalChange { Parent, Kind, Metadata }
    [Fact]
    public async Task Existing_secret_uses_exact_metadata_identity_without_plaintext_reads_or_implicit_runtime_grants() {
        var gate = new OwnerGate();
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var value = Guid.NewGuid().ToString("N");
        var owner = harness.Context.Services.GetRequiredService<SecretService>();
        var first = await owner.SaveAsync(new() { Name = "Duplicate name", SecretValue = Guid.NewGuid().ToString("N") });
        var chosen = await owner.SaveAsync(new() { Name = "Duplicate name", SecretValue = value });
        Assert.True(first.IsSuccess && chosen.IsSuccess);
        var cut = Render(harness, target.ProjectId);
        await OpenAsync(cut, target.Node.Id);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-secret-select']").ChangeAsync(new() { Value = chosen.Value.ToString("D") }));
        await InputAsync(cut, "purpose", "Exact reference purpose");
        await UseAsync(cut);
        var reference = Assert.Single(await ReferencesAsync(harness, target.ProjectId));
        Assert.Equal(chosen.Value, ProjectObjectMetadataSerializer.Parse(reference.MetadataJson).SecretReference!.SecretId);
        Assert.Equal(target.Node.Id, reference.ParentId);
        Assert.Equal("Exact reference purpose", reference.Notes);
        Assert.Equal(0, gate.PlaintextReads);
        Assert.Equal(0, gate.Saves);
        var resolver = harness.Context.Services.GetRequiredService<ISecretRuntimeResolver>();
        var consumer = SecretRuntimeConsumerIds.AgentMcp(Guid.NewGuid(), "owned-tool", "header");
        await Assert.ThrowsAsync<InvalidOperationException>(() => resolver.ResolveValueAsync(new(chosen.Value,
            SecretRuntimePurposes.AgentMcpHeader, [], SecretRuntimeConsumerTypes.AgentMcp, consumer)));
        var resolved = await resolver.ResolveValueAsync(new(chosen.Value, SecretRuntimePurposes.AgentMcpHeader,
            [chosen.Value], SecretRuntimeConsumerTypes.AgentMcp, consumer));
        Assert.True(resolved == value);
        Assert.False(cut.Markup.Contains(value, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Edit_preserves_unknown_metadata_timing_and_refuses_a_competing_opening() {
        var gate = new OwnerGate();
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var secret = await harness.Context.Services.GetRequiredService<SecretService>().SaveAsync(new() { Name = "Reference identity", SecretValue = Guid.NewGuid().ToString("N") });
        var metadata = JsonNode.Parse(ProjectObjectMetadataSerializer.Serialize(new() { SecretReference = new() { SecretId = secret.Value, SecretNameSnapshot = "Old snapshot", Purpose = "Original" } }))!;
        metadata["unknownRoot"] = new JsonObject { ["exact"] = 73 };
        metadata["secretReference"]!["unknownChild"] = new JsonArray("keep", 42);
        var node = await Workbench(harness).CreateObjectAsync(target.ProjectId, new(ProjectObjectType.SecretReference, "Original", "Note", "Original",
            target.Node.Id, StartUtc: new DateTimeOffset(2026, 1, 1, 4, 5, 6, TimeSpan.Zero), EndUtc: new DateTimeOffset(2026, 1, 2, 4, 5, 6, TimeSpan.Zero),
            DurationSeconds: 86400, MetadataJson: metadata.ToJsonString()));
        var first = Render(harness, target.ProjectId);
        var second = Render(harness, target.ProjectId);
        await EditAsync(first, node.Id);
        await EditAsync(second, node.Id);
        await InputAsync(first, "purpose", "First committed purpose");
        await UseAsync(first);
        await InputAsync(second, "purpose", "Second stale purpose");
        await UseAsync(second);
        var saved = Assert.Single(await ReferencesAsync(harness, target.ProjectId));
        Assert.Equal("First committed purpose", saved.Notes);
        Assert.Equal((node.StartUtc, node.EndUtc, node.DurationSeconds, node.ParentId), (saved.StartUtc, saved.EndUtc, saved.DurationSeconds, saved.ParentId));
        var final = JsonNode.Parse(saved.MetadataJson)!;
        Assert.True(JsonNode.DeepEquals(metadata["unknownRoot"], final["unknownRoot"]));
        Assert.True(JsonNode.DeepEquals(metadata["secretReference"]!["unknownChild"], final["secretReference"]!["unknownChild"]));
        Assert.Equal(SecretReferencePhase.Refused, State(second).Receipt!.Phase);
        Assert.Equal(0, gate.PlaintextReads);
    }

    [Fact]
    public async Task Native_create_gate_freezes_input_and_rejects_direct_duplicate_submission() {
        var gate = new OwnerGate { HoldAfterSave = true };
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await OpenAsync(cut, target.Node.Id);
        var value = await FillAsync(cut);
        var state = State(cut);
        var create = cut.FindComponent<SecretReferenceEditor>().Instance.Create;
        var input = state.Draft.Capture();
        var model = state.Draft.Create.Copy();
        var pending = cut.InvokeAsync(() => create(input, model));
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            state.Draft.Create.Name = "Late unsubmitted name";
            state.Draft.Purpose = "Late unsubmitted purpose";
            await cut.InvokeAsync(() => create(state.Draft.Capture(), state.Draft.Create.Copy()));
            Assert.Equal(1, gate.Saves);
        } finally {
            gate.Release.TrySetResult();
        }
        await pending;
        Assert.Equal(SecretReferencePhase.Observed, state.Receipt!.Phase);
        Assert.Empty(state.Draft.Create.SecretValue);
        Assert.Empty(model.SecretValue);
        var items = await gate.Inner!.ListAsync(default);
        Assert.Equal("Owned new secret", Assert.Single(items, item => item.Id == state.Receipt.SecretId).Name);
        Assert.Equal("Original purpose", Assert.Single(await ReferencesAsync(harness, target.ProjectId)).Notes);
        var stored = await harness.Context.Services.GetRequiredService<ISecretRuntimeResolver>().ResolveValueAsync(new(state.Receipt.SecretId!.Value,
            SecretRuntimePurposes.AgentMcpHeader, [state.Receipt.SecretId.Value]));
        Assert.True(stored == value);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Accepted_vault_identity_survives_metadata_failure_and_finishes_only_original_reference(bool nativeActivityFailure) {
        var gate = new OwnerGate { FailReadAfterSave = true };
        var faults = new Faults { FailSecretActivity = nativeActivityFailure };
        await using var harness = await HarnessAsync(gate, faults);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await OpenAsync(cut, target.Node.Id);
        var value = await FillAsync(cut);
        await CreateAsync(cut);
        var state = State(cut);
        Assert.Equal(SecretReferencePhase.VaultCommitted, state.Receipt!.Phase);
        Assert.Equal(gate.Accepted, state.Receipt.SecretId);
        Assert.Equal(nativeActivityFailure ? SettingsWriteState.CommittedWarning : SettingsWriteState.Committed, gate.AcceptedState);
        Assert.Empty(await ReferencesAsync(harness, target.ProjectId));
        Assert.False(cut.Markup.Contains(value, StringComparison.Ordinal));
        Assert.Empty(state.Draft.Create.SecretValue);
        gate.FailReadAfterSave = false;
        await InputAsync(cut, "purpose", "Attempt to rebind original intent");
        await RetryAsync(cut);
        Assert.Empty(await ReferencesAsync(harness, target.ProjectId));
        Assert.True(state.CanFinishReference);
        await cut.InvokeAsync(() => cut.Find("[data-testid='project-structure-secret-finish']").ClickAsync(new MouseEventArgs()));
        var saved = Assert.Single(await ReferencesAsync(harness, target.ProjectId));
        Assert.Equal(gate.Accepted, ProjectObjectMetadataSerializer.Parse(saved.MetadataJson).SecretReference!.SecretId);
        Assert.Equal("Original purpose", saved.Notes);
        Assert.Equal(1, gate.Saves);
        Assert.Equal(0, gate.PlaintextReads);
    }

    [Theory]
    [InlineData(OriginalChange.Parent)]
    [InlineData(OriginalChange.Kind)]
    [InlineData(OriginalChange.Metadata)]
    public async Task Vault_commit_does_not_authorize_a_changed_original_node(OriginalChange change) {
        var gate = new OwnerGate { HoldAfterSave = true };
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await OpenAsync(cut, target.Node.Id);
        await FillAsync(cut);
        var pending = CreateAsync(cut);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await using var db = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
            var row = await db.Set<ProjectObjectRecord>().SingleAsync(row => row.Id == target.Node.RecordId);
            if (change == OriginalChange.Parent) {
                row.ParentNodeKey = target.Other.Id;
            } else if (change == OriginalChange.Kind) {
                row.ObjectType = ProjectObjectType.Meeting;
            } else {
                row.MetadataJson = "{\"changedByNativeOwner\":true}";
            }
            await db.SaveChangesAsync();
        } finally {
            gate.Release.TrySetResult();
        }
        await pending;
        Assert.Equal(SecretReferencePhase.VaultCommitted, State(cut).Receipt!.Phase);
        Assert.Empty(await ReferencesAsync(harness, target.ProjectId));
        await RetryAsync(cut);
        Assert.False(State(cut).CanFinishReference);
        Assert.Equal(1, gate.Saves);
    }

    [Fact]
    public async Task Committed_reference_retains_both_ids_when_surface_read_fails_and_retry_only_reads() {
        var gate = new OwnerGate();
        var faults = new Faults { FailAfterReferenceCommit = true };
        await using var harness = await HarnessAsync(gate, faults);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await OpenAsync(cut, target.Node.Id);
        await FillAsync(cut);
        await CreateAsync(cut);
        var state = State(cut);
        Assert.Equal(SecretReferencePhase.ReferenceCommitted, state.Receipt!.Phase);
        var receipt = state.Receipt;
        var saved = Assert.Single(await ReferencesAsync(harness, target.ProjectId));
        Assert.Equal(saved.Id, receipt.NodeId);
        await RetryAsync(cut);
        Assert.Equal(SecretReferencePhase.Observed, state.Receipt.Phase);
        Assert.Equal(receipt.SecretId, state.Receipt.SecretId);
        Assert.Equal(receipt.NodeId, state.Receipt.NodeId);
        Assert.Equal(1, gate.Saves);
        Assert.Single(await ReferencesAsync(harness, target.ProjectId));
    }

    [Fact]
    public async Task Native_unknown_vault_reply_retains_candidate_identity_without_blind_retry() {
        var gate = new OwnerGate();
        var faults = new Faults { LoseSecretCommitReply = true };
        await using var harness = await HarnessAsync(gate, faults);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        await OpenAsync(cut, target.Node.Id);
        await FillAsync(cut);
        await CreateAsync(cut);
        var state = State(cut);
        Assert.Equal(SecretReferencePhase.Unknown, state.Receipt!.Phase);
        Assert.NotNull(state.Receipt.SecretId);
        Assert.Contains(await gate.Inner!.ListAsync(default), item => item.Id == state.Receipt.SecretId);
        await RetryAsync(cut);
        Assert.Equal(SecretReferencePhase.Unknown, state.Receipt.Phase);
        Assert.Equal(1, gate.Saves);
        Assert.Empty(state.Draft.Create.SecretValue);
        Assert.Empty(await ReferencesAsync(harness, target.ProjectId));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Actor_or_runtime_retirement_keeps_accepted_vault_but_refuses_the_reference_phase(bool changeActor) {
        var gate = new OwnerGate { HoldAfterSave = true };
        RuntimeGeneration? runtime = null;
        await using var harness = await HarnessAsync(gate, configure: services => {
            services.AddSingleton<CanonicalRuntimeDatabase>();
            services.AddSingleton<ICanonicalRuntimeDatabase>(provider => runtime = new(provider.GetRequiredService<CanonicalRuntimeDatabase>()));
        });
        var target = await SeedAsync(harness);
        static Task<AuthenticationState> Actor(string name) => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
            new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Owned test actor"))));
        var actor = Actor("Original");
        var host = harness.Context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters.Add(component => component.Value, actor)
            .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, target.ProjectId)));
        var cut = host.FindComponent<ProjectStructurePage>();
        cut.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        await OpenAsync(cut, target.Node.Id);
        await FillAsync(cut);
        var original = State(cut);
        var pending = CreateAsync(cut);
        try {
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            if (changeActor) {
                actor = Actor("Successor");
            } else {
                runtime!.Generation++;
            }
            await host.InvokeAsync(() => host.Render(parameters => parameters.Add(component => component.Value, actor)
                .AddChildContent<ProjectStructurePage>(child => child.Add(component => component.ProjectId, target.ProjectId))));
            Assert.True(original.IsRetired);
            Assert.Empty(original.Draft.Create.SecretValue);
        } finally {
            gate.Release.TrySetResult();
        }
        await pending;
        Assert.Equal(SecretReferencePhase.VaultCommitted, original.Receipt!.Phase);
        Assert.Equal(gate.Accepted, original.Receipt.SecretId);
        Assert.Empty(await ReferencesAsync(harness, target.ProjectId));
        Assert.Equal(1, gate.Saves);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Late_A_B_A_metadata_success_or_cancellation_cannot_reset_the_newest_opening(bool cancelOld) {
        var gate = new OwnerGate { HoldNextRead = true, CancelHeldRead = cancelOld };
        await using var harness = await HarnessAsync(gate);
        var target = await SeedAsync(harness);
        var cut = Render(harness, target.ProjectId);
        var pending = cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(target.Node.Id, "add-secret-reference", 0, 0));
        SecretReferenceState newest;
        try {
            await gate.ReadEntered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await OpenAsync(cut, target.Other.Id);
            await OpenAsync(cut, target.Node.Id);
            newest = State(cut);
            await InputAsync(cut, "create-name", "Newest raw draft");
        } finally {
            gate.ReadRelease.TrySetResult();
        }
        await pending;
        Assert.Same(newest, State(cut));
        Assert.Equal("Newest raw draft", newest.Draft.Create.Name);
        Assert.False(newest.IsLoading);
        Assert.False(newest.IsUnavailable);
        Assert.Empty(newest.Message);
        Assert.Equal(0, gate.Saves);
    }

    private sealed record Target(Guid ProjectId, ProjectStructureNode Node, ProjectStructureNode Other);
    private static async Task<Target> SeedAsync(ComponentTestHarness harness) {
        var created = await harness.Context.Services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Owned WB5 protected reference" });
        Assert.True(created.IsSuccess);
        var first = await Workbench(harness).CreateObjectAsync(created.Value, new(ProjectObjectType.Note, "Original parent", "", "", $"project:{created.Value:D}"));
        var other = await Workbench(harness).CreateObjectAsync(created.Value, new(ProjectObjectType.Note, "Other parent", "", "", $"project:{created.Value:D}"));
        return new(created.Value, first, other);
    }
    private static ProjectWorkbenchService Workbench(ComponentTestHarness harness) => harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
    private static async Task<IReadOnlyList<ProjectStructureNode>> ReferencesAsync(ComponentTestHarness harness, Guid project)
        => (await Workbench(harness).GetStructureAsync(project)).Nodes.Where(node => node.ObjectType == ProjectObjectType.SecretReference).ToArray();
    private static IRenderedComponent<ProjectStructurePage> Render(ComponentTestHarness harness, Guid project) {
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(page => page.ProjectId, project));
        cut.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        return cut;
    }
    private static async Task OpenAsync(IRenderedComponent<ProjectStructurePage> cut, string parent) {
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(parent, "add-secret-reference", 0, 0));
        cut.WaitForAssertion(() => Assert.False(State(cut).IsLoading));
    }
    private static async Task EditAsync(IRenderedComponent<ProjectStructurePage> cut, string node) {
        await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(node, JsonSerializer.Serialize(new[] { node })));
        await cut.InvokeAsync(() => {
            var renderer = cut.FindComponent<ProjectStructureSelectionPanel>().Instance;
            return renderer.Dispatch.InvokeAsync(new(renderer.Origin, renderer.State.SelectedNodes.Select(item => item.Id).ToArray(),
                new CanDoItAll.Workbench.Insights.UI.InsightsSelectionCommand.Inspector("edit")));
        });
        cut.WaitForAssertion(() => Assert.False(State(cut).IsLoading));
    }
    private static SecretReferenceState State(IRenderedComponent<ProjectStructurePage> cut) => cut.FindComponent<SecretReferenceEditor>().Instance.State;
    private static Task InputAsync(IRenderedComponent<ProjectStructurePage> cut, string field, string value)
        => cut.InvokeAsync(() => cut.Find($"[data-testid='project-structure-secret-{field}']").InputAsync(new() { Value = value }));
    private static async Task<string> FillAsync(IRenderedComponent<ProjectStructurePage> cut) {
        var value = Guid.NewGuid().ToString("N");
        await InputAsync(cut, "create-name", "Owned new secret");
        await InputAsync(cut, "purpose", "Original purpose");
        await InputAsync(cut, "create-value", value);
        return value;
    }
    private static Task CreateAsync(IRenderedComponent<ProjectStructurePage> cut) => ClickAsync(cut, "create-use");
    private static Task UseAsync(IRenderedComponent<ProjectStructurePage> cut) => ClickAsync(cut, "use-selected");
    private static Task RetryAsync(IRenderedComponent<ProjectStructurePage> cut) => ClickAsync(cut, "retry");
    private static Task ClickAsync(IRenderedComponent<ProjectStructurePage> cut, string action)
        => cut.InvokeAsync(() => cut.Find($"[data-testid='project-structure-secret-{action}']").ClickAsync(new MouseEventArgs()));
    private static Task<ComponentTestHarness> HarnessAsync(OwnerGate gate, Faults? faults = null, Action<IServiceCollection>? configure = null)
        => ComponentTestHarness.CreateAsync(services => {
            configure?.Invoke(services);
            services.Replace(ServiceDescriptor.Singleton<ISecretVault, InMemorySecretVault>());
            services.AddScoped<IWorkspaceSecretsOwner>(provider => {
                gate.Inner = new WorkspaceSecretsOwner(provider.GetRequiredService<SecretService>(), provider.GetRequiredService<ICanonicalRuntimeDatabase>());
                return gate;
            });
            if (faults is not null) {
                services.Replace(ServiceDescriptor.Singleton<IActivityStream>(new ActivityFault(faults)));
                services.AddSingleton<IDbContextFactory<WorkbenchDbContext>>(provider => new PooledDbContextFactory<WorkbenchDbContext>(
                    new DbContextOptionsBuilder<WorkbenchDbContext>(provider.GetRequiredService<DbContextOptions<WorkbenchDbContext>>()).AddInterceptors(faults, new ReadFault(faults)).Options));
                services.AddSingleton<IDbContextFactory<SecurityDbContext>>(provider => new PooledDbContextFactory<SecurityDbContext>(
                    new DbContextOptionsBuilder<SecurityDbContext>(provider.GetRequiredService<DbContextOptions<SecurityDbContext>>()).AddInterceptors(faults).Options));
            }
        });

    private sealed class OwnerGate : IWorkspaceSecretsOwner {
        public IWorkspaceSecretsOwner? Inner { get; set; }
        public int PlaintextReads { get; private set; }
        public int Saves { get; private set; }
        public Guid? Accepted { get; private set; }
        public SettingsWriteState? AcceptedState { get; private set; }
        public bool FailReadAfterSave { get; set; }
        public bool HoldAfterSave { get; set; }
        public bool HoldNextRead { get; set; }
        public bool CancelHeldRead { get; set; }
        public TaskCompletionSource ReadEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReadRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsCurrent => Inner!.IsCurrent;
        public async Task<IReadOnlyList<SecretListItem>> ListAsync(CancellationToken cancellationToken) {
            if (FailReadAfterSave && Accepted.HasValue) {
                throw new IOException("Owned metadata read fault");
            }
            var hold = HoldNextRead;
            HoldNextRead = false;
            var items = await Inner!.ListAsync(cancellationToken);
            if (hold) {
                ReadEntered.TrySetResult();
                await ReadRelease.Task;
                if (CancelHeldRead) {
                    throw new OperationCanceledException("Owned retired metadata observation");
                }
            }
            return items;
        }
        public Task<SecretEditorModel?> GetAsync(Guid id, CancellationToken cancellationToken) {
            PlaintextReads++;
            throw new InvalidOperationException("Picker must not read stored plaintext.");
        }
        public async Task<SettingsWriteResult<Guid>> SaveAsync(SecretEditorModel command, CancellationToken cancellationToken) {
            Saves++;
            var result = await Inner!.SaveAsync(command, cancellationToken);
            if (result.State != SettingsWriteState.Refused) {
                Accepted = result.Value;
                AcceptedState = result.State;
            }
            if (HoldAfterSave) {
                Entered.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return result;
        }
        public Task<SettingsWriteResult<Guid>> DeleteAsync(Guid id, CancellationToken cancellationToken) => throw new InvalidOperationException("No automatic vault cleanup is authorized.");
    }

    private sealed class RuntimeGeneration(ICanonicalRuntimeDatabase inner) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile => inner.Profile;
        public long Generation { get; set; } = inner.Generation;
    }

    private sealed class Faults : SaveChangesInterceptor, IDbTransactionInterceptor {
        public bool FailSecretActivity { get; set; }
        public bool LoseSecretCommitReply { get; set; }
        public bool FailAfterReferenceCommit { get; set; }
        public bool FailNextRead { get; set; }
        private DbContext? referenceContext;
        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default) {
            if (FailAfterReferenceCommit && eventData.Context is WorkbenchDbContext db && db.ChangeTracker.Entries<ProjectObjectRecord>().Any(row => row.State == EntityState.Added && row.Entity.ObjectType == ProjectObjectType.SecretReference)) {
                referenceContext = db;
            }
            return ValueTask.FromResult(result);
        }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default) {
            if (LoseSecretCommitReply && eventData.Context is SecurityDbContext) {
                LoseSecretCommitReply = false;
                throw new IOException("Owned lost commit reply");
            }
            return ValueTask.FromResult(result);
        }
        public Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default) {
            if (ReferenceEquals(referenceContext, eventData.Context) && referenceContext is not null) {
                referenceContext = null;
                FailAfterReferenceCommit = false;
                FailNextRead = true;
            }
            return Task.CompletedTask;
        }
    }
    private sealed class ReadFault(Faults faults) : DbCommandInterceptor {
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default) {
            if (faults.FailNextRead) {
                faults.FailNextRead = false;
                throw new IOException("Owned read after reference commit failed");
            }
            return ValueTask.FromResult(result);
        }
    }
    private sealed class ActivityFault(Faults faults) : IActivityStream {
        public Task RecordAsync(ActivityWriteRequest request, CancellationToken cancellationToken = default) {
            if (request.ArtifactKind == "secret" && faults.FailSecretActivity) {
                faults.FailSecretActivity = false;
                throw new IOException("Owned native secret activity fault");
            }
            return Task.CompletedTask;
        }
    }
}
