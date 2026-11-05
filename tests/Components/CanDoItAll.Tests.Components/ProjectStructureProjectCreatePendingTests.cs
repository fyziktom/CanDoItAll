using Bunit;
using CanDoItAll.Editing;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Composition;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Infrastructure.Persistence;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Projects.Pages;
using CanDoItAll.Modules.Projects.Pages.Components;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectStructureProjectCreatePendingTests {
    public enum StorageFault { BeforePreparation, LostPreparationAcknowledgement, LostDispatchClaimAcknowledgement }
    public enum Retirement { Close, Route, Dispose, NewHierarchy }
    public enum SeedResult { Accepted, Refused, Unconfirmed }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Revoked_current_access_cannot_dispatch_or_seed_a_native_Workbench_creation(bool afterAcceptance) {
        var access = new EditingUiTestAccess();
        var store = new FaultStore();
        if (afterAcceptance) {
            store.Inner.BeforeAccept = () => {
                access.Allowed = false;
                return Task.CompletedTask;
            };
        } else {
            store.AfterDispatchClaim = () => access.Allowed = false;
        }
        var seed = new CountingSeed();
        await using var harness = await Harness(store, seed, access);
        var (page, projects, parent) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        var draft = modal.Draft;
        draft.Model.Name = "Preserve original creation input";
        draft.StarterObjects.Add(new() { ObjectType = ProjectObjectType.Note, Title = "Must remain unsent" });

        await page.InvokeAsync(() => modal.Save.InvokeAsync(draft.Capture()));

        Assert.Equal(0, seed.Calls);
        Assert.Equal("Preserve original creation input", draft.Model.Name);
        Assert.Null(draft.Acknowledgement);
        Assert.Null(draft.Model.Id);
        Assert.False(draft.CanMutate);
        Assert.Equal(1, store.Inner.DispatchClaims);
        Assert.Single(store.Inner.Records);
        if (afterAcceptance) {
            Assert.NotNull(draft.AcceptedOutcome);
            Assert.Equal(2, (await projects.ListAsync()).Count);
        } else {
            Assert.Null(draft.AcceptedOutcome);
            Assert.Equal(parent, Assert.Single(await projects.ListAsync()).Id);
        }
    }

    [Fact]
    public async Task Native_Workbench_seed_carries_the_held_current_write_check() {
        var store = new FaultStore();
        var seed = new CountingSeed();
        await using var harness = await Harness(store, seed);
        var (page, _, _) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        modal.Draft.Model.Name = "Admitted Workbench seed";
        modal.Draft.StarterObjects.Add(new() { ObjectType = ProjectObjectType.Note, Title = "One explicit starter" });

        await page.InvokeAsync(() => modal.Save.InvokeAsync(modal.Draft.Capture()));

        Assert.Equal(1, seed.Calls);
        Assert.True(seed.HadCurrentWriteCheck);
    }

    [Fact]
    public async Task Revocation_after_native_Project_SQL_rolls_back_Workbench_creation_and_retains_its_reference() {
        var access = new EditingUiTestAccess();
        var probe = new RevokeAfterSql(access);
        var store = new FaultStore();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IProjectPendingSubmissionStore>(store);
            services.AddSingleton<IProjectPendingReferenceStore>(store.Inner);
            services.AddSingleton<IEditingUiAccess>(access);
            services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
        });
        var (page, projects, parent) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        modal.Draft.Model.Name = RevokeAfterSql.Target;
        probe.Armed = true;

        await page.InvokeAsync(() => modal.Save.InvokeAsync(modal.Draft.Capture()));

        Assert.True(probe.Reached);
        Assert.Equal(parent, Assert.Single(await projects.ListAsync()).Id);
        Assert.Null(modal.Draft.AcceptedOutcome);
        Assert.Equal(RevokeAfterSql.Target, modal.Draft.Model.Name);
        Assert.Single(store.Inner.Records);
        Assert.False(modal.Draft.CanMutate);
    }

    [Fact]
    public async Task Opening_Projects_recovery_requires_an_explicit_leave_decision_for_the_current_creation() {
        var store = new FaultStore();
        await using var harness = await Harness(store);
        var (page, _, parent) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        var draft = modal.Draft;
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;
        Assert.True(modal.IsSameEditorRoute!($"/projects/{parent:D}/structure?tab=gantt"));
        Assert.False(modal.IsSameEditorRoute!($"/projects/{parent:D}/structure?foreignProject={Guid.NewGuid():D}"));
        Assert.False(modal.IsSameEditorRoute!($"/projects/{parent:D}/processes"));
        Assert.False(modal.IsSameEditorRoute!("https://external.invalid/projects"));
        await page.InvokeAsync(() => page.Find("[data-testid='project-name-input']").Input("Keep this raw creation draft"));
        var leaving = page.InvokeAsync(() => page.Find("[data-testid='project-create-recovery-open']").ClickAsync(new()));
        page.WaitForElement("[data-testid='project-dirty-close']");
        await page.InvokeAsync(() => page.Find("[data-testid='project-dirty-continue']").ClickAsync(new()));
        await leaving;
        Assert.Equal(originalUri, navigation.Uri);
        Assert.Same(draft, page.FindComponent<ProjectModalHost>().Instance.Draft);
        Assert.Equal("Keep this raw creation draft", draft.Model.Name);

        leaving = page.InvokeAsync(() => page.Find("[data-testid='project-create-recovery-open']").ClickAsync(new()));
        page.WaitForElement("[data-testid='project-dirty-close']");
        await page.InvokeAsync(() => page.Find("[data-testid='project-dirty-discard']").ClickAsync(new()));
        await leaving;
        Assert.EndsWith("/projects", navigation.Uri);
        Assert.Empty(store.Inner.Records);
        Assert.Equal(0, store.Inner.DispatchClaims);
    }

    [Theory]
    [InlineData(StorageFault.BeforePreparation)]
    [InlineData(StorageFault.LostPreparationAcknowledgement)]
    [InlineData(StorageFault.LostDispatchClaimAcknowledgement)]
    public async Task Browser_storage_failure_never_dispatches_a_new_creation_or_replaces_its_identity(StorageFault fault) {
        var store = new FaultStore { Fault = fault };
        await using var harness = await Harness(store);
        var (page, projects, parent) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        modal.Draft.Model.Name = "Must not reach owner";
        var submission = modal.Draft.Capture();
        await page.InvokeAsync(() => modal.Save.InvokeAsync(submission));
        var operation = modal.PendingRecovery!.Operation;
        Assert.Equal(ProjectEditorMutationState.PendingRecovery, modal.Draft.Mutation);
        await page.InvokeAsync(() => modal.Save.InvokeAsync(submission));
        Assert.Equal(operation, modal.PendingRecovery!.Operation);
        Assert.Equal(1, store.Preparations);
        Assert.Equal(parent, Assert.Single(await projects.ListAsync()).Id);
        Assert.Equal(fault == StorageFault.BeforePreparation ? 0 : 1, store.Inner.Records.Count);
        if (fault == StorageFault.LostDispatchClaimAcknowledgement) {
            Assert.Equal(ProjectPendingStage.Dispatching, Assert.Single(store.Inner.Records).Stage);
            await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.CancelPrepared)));
            Assert.Single(store.Inner.Records);
        }
    }

    [Fact]
    public async Task Known_creation_survives_browser_failure_and_explicit_recovery_never_seeds_or_creates_again() {
        var store = new FaultStore();
        store.Inner.AcceptFailure = new IOException("Accepted receipt browser write failed");
        var seed = new CountingSeed();
        await using var harness = await Harness(store, seed);
        var (page, projects, parent) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        var draft = modal.Draft;
        draft.Model.Name = "Historical created root";
        draft.StarterObjects.Add(new() { ObjectType = ProjectObjectType.Note, Title = "Unsent starter" });
        await page.InvokeAsync(() => modal.Save.InvokeAsync(draft.Capture()));
        var accepted = draft.AcceptedOutcome!.Accepted;
        var operation = Assert.Single(store.Inner.Records).Operation;
        Assert.Equal(ProjectEditorMutationState.SaveReconciliationFailed, draft.Mutation);
        Assert.Null(draft.Acknowledgement);
        Assert.Equal(accepted, Assert.Single(page.Instance.AuthoringOutcomes).Editor);
        Assert.Equal(0, seed.Calls);
        var later = await projects.GetAsync(accepted.Project.ProjectId);
        later.Name = "Later current definition";
        Assert.True((await projects.SaveAsync(later)).IsSuccess);
        draft.Model.Name = "Typing after acceptance";
        draft.EditContext.NotifyFieldChanged(new(draft.Model, nameof(draft.Model.Name)));
        store.Inner.AcceptFailure = null;
        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.Check)));
        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.ApplyAccepted)));
        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.ReadCurrent)));
        Assert.Equal("Typing after acceptance", draft.Model.Name);
        Assert.Equal(accepted.Definition, draft.Model.ExpectedDefinition);
        Assert.Equal("Later current definition", draft.InspectedModel!.Name);
        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.Retry)));
        Assert.Equal(1, store.Inner.DispatchClaims);
        Assert.Equal(0, seed.Calls);
        Assert.Equal(2, (await projects.ListAsync()).Count);
        Assert.Empty(await projects.ListHierarchyLinksAsync());
        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.ClearResolved)));
        Assert.Empty(store.Inner.Records);
        Assert.False(draft.CanMutate);
        Assert.Equal(ProjectEditorMutationState.AcceptedReadOnly, draft.Mutation);
        Assert.Equal(parent, page.Instance.ProjectId);
    }

    [Fact]
    public async Task Live_acceptance_reconciles_ids_once_and_preserves_post_dispatch_fields_and_starter_input() {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FaultStore();
        store.Inner.BeforeAccept = () => {
            entered.TrySetResult();
            return release.Task;
        };
        await using var harness = await Harness(store);
        var (page, projects, _) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        var draft = modal.Draft;
        var editContext = draft.EditContext;
        draft.Model.Name = "Frozen root";
        draft.Model.Phases.Add(new() { Name = "Frozen phase", Goal = "Original goal" });
        var phase = draft.Model.Phases[0];
        var save = page.InvokeAsync(() => modal.Save.InvokeAsync(draft.Capture()));
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            await page.InvokeAsync(() => {
                draft.Model.Name = "Later typed root";
                draft.EditContext.NotifyFieldChanged(new(draft.Model, nameof(draft.Model.Name)));
                phase.Goal = "Later typed goal";
                draft.EditContext.NotifyFieldChanged(new(phase, nameof(phase.Goal)));
                draft.StarterObjects.Add(new() { ObjectType = ProjectObjectType.Note, Title = "Later unsent starter" });
            });
        } finally {
            release.TrySetResult();
        }
        await save.WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Same(draft, page.FindComponent<ProjectModalHost>().Instance.Draft);
        Assert.Same(editContext, draft.EditContext);
        Assert.Equal("Later typed root", draft.Model.Name);
        Assert.Equal("Later typed goal", phase.Goal);
        Assert.NotNull(phase.Id);
        Assert.NotNull(draft.Model.ExpectedDefinition);
        Assert.False(Assert.Single(draft.StarterObjects).WasDispatched);
        Assert.Equal("Frozen root", (await projects.GetAsync(draft.Model.Id)).Name);
        Assert.Empty(store.Inner.Records);
        Assert.False(draft.CanMutate);
        Assert.Equal(ProjectEditorMutationState.AcceptedReadOnly, draft.Mutation);
    }

    [Theory]
    [InlineData(Retirement.Close)]
    [InlineData(Retirement.Route)]
    [InlineData(Retirement.Dispose)]
    [InlineData(Retirement.NewHierarchy)]
    public async Task Retired_creation_retains_exact_acceptance_without_acknowledgement_seed_or_navigation(Retirement retirement) {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var store = new FaultStore();
        store.Inner.BeforeAccept = () => {
            entered.TrySetResult();
            return release.Task;
        };
        var seed = new CountingSeed();
        var access = new CreationLifetimeAccess();
        await using var harness = await Harness(store, seed, access);
        var (page, projects, parent) = await OpenAsync(harness);
        var owner = page.Instance;
        Assert.True(access.CreationLifetime.CanBeCanceled);
        var stopped = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = access.CreationLifetime.Register(() => stopped.TrySetResult());
        var successor = (await projects.SaveAsync(new() { Name = "Route successor" })).Value;
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        var draft = modal.Draft;
        draft.Model.Name = "Retired accepted root";
        draft.StarterObjects.Add(new() { ObjectType = ProjectObjectType.Note, Title = "Must remain unsent" });
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        var originalUri = navigation.Uri;
        var save = page.InvokeAsync(() => modal.SaveAndOpenStructure.InvokeAsync(draft.Capture()));
        Task disposing = Task.CompletedTask;
        try {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(20));
            switch (retirement) {
                case Retirement.Close:
                    await page.InvokeAsync(() => modal.Close.InvokeAsync());
                    break;
                case Retirement.Route:
                    await page.InvokeAsync(() => page.Render(parameters => parameters.Add(component => component.ProjectId, successor)));
                    break;
                case Retirement.Dispose:
                    disposing = harness.Context.DisposeRenderedComponentsAsync();
                    await stopped.Task.WaitAsync(TimeSpan.FromSeconds(20));
                    break;
                case Retirement.NewHierarchy:
                    await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction(
                        $"project:{parent:D}", "project:add-subproject", 0, 0));
                    break;
            }
        } finally {
            release.TrySetResult();
        }
        await Task.WhenAll(save, disposing).WaitAsync(TimeSpan.FromSeconds(20));
        Assert.Equal(0, seed.Calls);
        Assert.Null(draft.Acknowledgement);
        Assert.Null(draft.Model.Id);
        Assert.NotNull(draft.AcceptedOutcome);
        Assert.Equal(draft.AcceptedOutcome.Accepted, Assert.Single(owner.AuthoringOutcomes).Editor);
        Assert.Equal(ProjectPendingStage.Accepted, Assert.Single(store.Inner.Records).Stage);
        Assert.Equal(originalUri, navigation.Uri);
        Assert.Empty(await projects.ListHierarchyLinksAsync());
        if (retirement == Retirement.Route) {
            Assert.Equal(successor, owner.ProjectId);
        } else {
            Assert.Equal(parent, owner.ProjectId);
        }
    }

    [Theory]
    [InlineData(SeedResult.Accepted)]
    [InlineData(SeedResult.Refused)]
    [InlineData(SeedResult.Unconfirmed)]
    public async Task Starter_effect_has_one_explicit_attempt_and_never_erases_definition_acceptance(SeedResult result) {
        var store = new FaultStore();
        var seed = new CountingSeed { Result = result };
        await using var harness = await Harness(store, seed);
        var (page, projects, _) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        var draft = modal.Draft;
        draft.Model.Name = "Definition before starter";
        var starter = new StarterObjectDraft { ObjectType = ProjectObjectType.Note, Title = "Starter attempt" };
        draft.StarterObjects.Add(starter);
        await page.InvokeAsync(() => modal.Save.InvokeAsync(draft.Capture()));
        Assert.Equal(1, seed.Calls);
        Assert.NotNull(draft.AcceptedOutcome);
        Assert.NotNull(draft.Acknowledgement);
        Assert.Equal("Definition before starter", (await projects.GetAsync(draft.Model.Id)).Name);
        Assert.True(starter.WasDispatched);
        Assert.Equal(result == SeedResult.Accepted, starter.IsSeeded);
        if (result == SeedResult.Accepted) {
            Assert.Empty(draft.StarterObjects);
        } else {
            Assert.Same(starter, Assert.Single(draft.StarterObjects));
        }
        if (result == SeedResult.Unconfirmed) {
            var operation = Assert.Single(store.Inner.Records).Operation;
            Assert.Equal(ProjectEditorMutationState.SeedOutcomeUnknown, draft.Mutation);
            await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.Check)));
            await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.Retry)));
            Assert.Equal(1, seed.Calls);
            Assert.Equal(1, store.Inner.DispatchClaims);
            await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(operation, ProjectPendingEditorAction.ClearResolved)));
            Assert.Contains("Starter-object completion remains unconfirmed", draft.Message);
        }
        await page.InvokeAsync(() => modal.Save.InvokeAsync(draft.Capture()));
        Assert.Equal(1, seed.Calls);
        Assert.Equal(1, store.Inner.DispatchClaims);
        Assert.NotNull(draft.AcceptedOutcome);
        Assert.Empty(store.Inner.Records);
    }

    [Fact]
    public async Task Unreadable_pending_inventory_keeps_creation_closed_and_exposes_explicit_recovery_navigation() {
        var store = new FaultStore();
        store.Inner.ScopedListFailure = new IOException("Inventory unavailable");
        await using var harness = await Harness(store);
        var (page, projects, parent) = await OpenAsync(harness, expectEditor: false);
        Assert.False(page.FindComponent<ProjectModalHost>().Instance.IsOpen);
        Assert.Contains("retained saves could not be read", page.Markup);
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-create-recovery-open']").ClickAsync(new()));
        Assert.EndsWith("/projects", harness.Context.Services.GetRequiredService<NavigationManager>().Uri);
        Assert.Equal(parent, Assert.Single(await projects.ListAsync()).Id);
        Assert.Equal(0, store.Inner.DispatchClaims);
    }

    [Fact]
    public async Task Retained_preparation_keeps_its_original_namespace_after_real_dataset_reactivation() {
        var store = new FaultStore { Fault = StorageFault.LostPreparationAcknowledgement };
        var seed = new CountingSeed();
        await using var harness = await Harness(store, seed);
        var (page, projects, parent) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        var draft = modal.Draft;
        draft.Model.Name = "Original namespace draft";
        await page.InvokeAsync(() => modal.Save.InvokeAsync(draft.Capture()));
        var original = Assert.Single(store.Inner.Records);
        var scope = draft.Scope;
        var services = harness.Context.Services;
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var changed = await services.GetRequiredService<DatasetActivationCoordinator>()
            .ReactivateAsync(profile, original.Operation.Namespace, Guid.NewGuid(), deadline.Token);

        Assert.NotEqual(original.Operation.Namespace, changed.Namespace);
        var definitions = services.GetRequiredService<ProjectNativeDefinitionOperations>();
        Assert.Equal(changed.Namespace, (await definitions.GetContextAsync(deadline.Token)).Namespace);
        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(original.Operation, ProjectPendingEditorAction.Check)));
        Assert.Contains("dataset, caller or epoch changed", modal.PendingRecovery!.Status);
        Assert.False(modal.PendingRecovery.CanRetry);
        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(original.Operation, ProjectPendingEditorAction.Retry)));
        await page.InvokeAsync(() => modal.Save.InvokeAsync(draft.Capture()));

        Assert.Equal(scope, draft.Scope);
        Assert.Equal(original, Assert.Single(store.Inner.Records));
        Assert.Equal(original.Operation, modal.PendingRecovery!.Operation);
        Assert.IsType<ProjectDefinitionLookup.DatasetChanged>(await definitions.LookupAsync(original.Operation, deadline.Token));
        Assert.Null(draft.AcceptedOutcome);
        Assert.False(draft.CanMutate);
        Assert.Equal(1, store.Preparations);
        Assert.Equal(0, store.Inner.DispatchClaims);
        Assert.Equal(0, seed.Calls);
        Assert.Equal(parent, Assert.Single(await projects.ListAsync()).Id);
    }

    [Fact]
    public async Task Retained_preparation_observes_draining_then_closed_absence_without_adopting_the_new_epoch() {
        var store = new FaultStore { Fault = StorageFault.LostPreparationAcknowledgement };
        await using var harness = await Harness(store);
        var (page, projects, parent) = await OpenAsync(harness);
        var modal = page.FindComponent<ProjectModalHost>().Instance;
        modal.Draft.Model.Name = "Original epoch draft";
        await page.InvokeAsync(() => modal.Save.InvokeAsync(modal.Draft.Capture()));
        var original = Assert.Single(store.Inner.Records);
        var services = harness.Context.Services;
        var definitions = services.GetRequiredService<ProjectNativeDefinitionOperations>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using (var context = new ProjectsDbContext(services.GetRequiredService<DbContextOptions<ProjectsDbContext>>())) {
            Assert.Equal(1, await context.Database.ExecuteSqlInterpolatedAsync($"""
                UPDATE "Infrastructure_FiniteOperationEpochs"
                SET "OpenedAtUtc" = clock_timestamp() - interval '1 hour',
                    "AdmissionClosesAtUtc" = clock_timestamp() - interval '1 second'
                WHERE "NamespaceId" = {original.Operation.Namespace.Value} AND "PoolId" = {ProjectFiniteOperationPool.Pool.Value}
                    AND "Epoch" = {original.Operation.Epoch.Value}
                """, deadline.Token));
        }

        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(original.Operation, ProjectPendingEditorAction.Check)));
        var draining = Assert.IsType<ProjectDefinitionLookup.NotObserved>(await definitions.LookupAsync(original.Operation, deadline.Token));
        Assert.Equal(ProjectOperationObservationState.Draining, draining.State);
        Assert.Equal(original, Assert.Single(store.Inner.Records));
        Assert.False(modal.PendingRecovery!.CanClear);
        Assert.False(modal.PendingRecovery.CanRetry);
        var profile = services.GetRequiredService<ICanonicalRuntimeDatabase>().Profile;
        var binding = await services.GetRequiredService<DatasetActivationService>().ReadActiveBindingAsync(profile.Profile.Id, deadline.Token);
        var pool = services.GetServices<FiniteOperationPoolRegistration>().Single(registration => registration.Pool == ProjectFiniteOperationPool.Pool);
        Assert.Equal(FiniteOperationRotationResult.Rotated,
            await services.GetRequiredService<FiniteOperationEpochStore>().RotateExpiredAsync(binding, pool, deadline.Token));
        var current = await definitions.GetContextAsync(deadline.Token);
        Assert.Equal(original.Operation.Namespace, current.Namespace);
        Assert.NotEqual(original.Operation.Epoch, current.Epoch);

        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(original.Operation, ProjectPendingEditorAction.Check)));
        var closed = Assert.Single(store.Inner.Records);
        Assert.Equal(ProjectPendingStage.ClosedAbsent, closed.Stage);
        Assert.Equal(original.Operation, closed.Operation);
        Assert.Equal(original.Frozen, closed.Frozen);
        Assert.Equal(original.Operation, closed.Absence!.Operation);
        Assert.True(modal.PendingRecovery!.CanClear);
        Assert.False(modal.PendingRecovery.CanRetry);
        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(original.Operation, ProjectPendingEditorAction.Retry)));
        await page.InvokeAsync(() => modal.Save.InvokeAsync(modal.Draft.Capture()));
        Assert.Equal(1, store.Preparations);
        Assert.Equal(0, store.Inner.DispatchClaims);
        Assert.Null(modal.Draft.AcceptedOutcome);
        Assert.Equal(parent, Assert.Single(await projects.ListAsync()).Id);
        await page.InvokeAsync(() => modal.PendingAction.InvokeAsync(new(original.Operation, ProjectPendingEditorAction.ClearResolved)));
        Assert.Empty(store.Inner.Records);
        Assert.False(modal.Draft.CanMutate);
    }

    private sealed class CreationLifetimeAccess : IEditingUiAccess {
        private readonly EditingUiTestAccess inner = new();
        public CancellationToken CreationLifetime { get; private set; }

        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) {
            if (topic.Owner == EditingOwner.Project && topic.ResourceId is null) {
                CreationLifetime = cancellationToken;
            }
            return inner.OpenAsync(topic, cancellationToken);
        }

        public ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken)
            => inner.ValidateAsync(grant, write, cancellationToken);
    }

    private static Task<ComponentTestHarness> Harness(FaultStore store, CountingSeed? seed = null, IEditingUiAccess? access = null)
        => ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IProjectPendingSubmissionStore>(store);
            services.AddSingleton<IProjectPendingReferenceStore>(store.Inner);
            if (access is not null) {
                services.AddSingleton<IEditingUiAccess>(access);
            }
            if (seed is not null) {
                services.AddSingleton<IProjectWorkbenchSeedService>(seed);
            }
        });

    private static async Task<(IRenderedComponent<ProjectStructurePage> Page, ProjectsService Projects, Guid Parent)> OpenAsync(
        ComponentTestHarness harness, bool expectEditor = true) {
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var parent = (await projects.SaveAsync(new() { Name = "Creation hierarchy parent" })).Value;
        harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo($"/projects/{parent:D}/structure");
        var page = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(component => component.ProjectId, parent));
        page.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        await page.InvokeAsync(() => page.FindComponent<CanvasWorkbench>().Instance.OnContextAction($"project:{parent:D}", "project:add-subproject", 0, 0));
        await page.InvokeAsync(() => page.Find("[data-testid='project-structure-hierarchy-create-project']").ClickAsync(new()));
        if (expectEditor) {
            Assert.True(page.FindComponent<ProjectModalHost>().Instance.SubmissionReady);
        }
        return (page, projects, parent);
    }

    private sealed class CountingSeed : IProjectWorkbenchSeedService, IAdmittedProjectWorkbenchSeedService {
        public SeedResult Result { get; init; }
        public int Calls { get; private set; }
        public bool HadCurrentWriteCheck { get; private set; }
        public Task SeedProjectObjectsAsync(Guid projectId, IReadOnlyCollection<ProjectObjectSeedDraft> seeds, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The test requires original project-lifetime admission.");
        public async Task SeedProjectObjectsAsync(ProjectWriteAdmission project, IReadOnlyCollection<ProjectObjectSeedDraft> seeds,
            CancellationToken cancellationToken = default, Func<CancellationToken, ValueTask>? requireCurrentWrite = null) {
            HadCurrentWriteCheck = requireCurrentWrite is not null;
            if (requireCurrentWrite is not null) {
                await requireCurrentWrite(cancellationToken);
            }
            Calls++;
            await (Result switch {
                SeedResult.Refused => Task.FromException(new ProjectWriteAdmissionRejectedException(project)),
                SeedResult.Unconfirmed => Task.FromException(new IOException("Starter completion unavailable")),
                _ => Task.CompletedTask
            });
        }
    }

    private sealed class FaultStore : IProjectPendingSubmissionStore {
        public ProjectPendingSubmissionTestStore Inner { get; } = new();
        public StorageFault? Fault { get; init; }
        public int Preparations { get; private set; }
        public Action? AfterDispatchClaim { get; set; }
        public ValueTask InitializeAsync(CancellationToken token = default) => Inner.InitializeAsync(token);
        public async ValueTask<ProjectPendingStoreChange> PrepareAsync(ProjectDefinitionFrozenSubmission frozen, bool starter, CancellationToken token = default) {
            Preparations++;
            if (Fault == StorageFault.BeforePreparation) {
                throw new IOException("Browser preparation failed before writing");
            }
            var result = await Inner.PrepareAsync(frozen, starter, token);
            if (Fault == StorageFault.LostPreparationAcknowledgement) {
                throw new IOException("Browser preparation acknowledgement lost");
            }
            return result;
        }
        public async ValueTask<ProjectPendingStoreChange> BeginDispatchAsync(ProjectPendingSubmission record, Guid attempt, bool retry, CancellationToken token = default) {
            var result = await Inner.BeginDispatchAsync(record, attempt, retry, token);
            AfterDispatchClaim?.Invoke();
            if (Fault == StorageFault.LostDispatchClaimAcknowledgement) {
                throw new IOException("Browser dispatch-claim acknowledgement lost");
            }
            return result;
        }
        public ValueTask<ProjectPendingStoreChange> ObserveAcceptedAsync(ProjectDefinitionFrozenSubmission frozen, ProjectDefinitionAcceptedReceipt receipt, CancellationToken token = default)
            => Inner.ObserveAcceptedAsync(frozen, receipt, token);
        public ValueTask<ProjectPendingStoreChange> ObserveAttemptAsync(ProjectDefinitionOperation operation, Guid attempt, ProjectPendingAttemptOutcome outcome, CancellationToken token = default)
            => Inner.ObserveAttemptAsync(operation, attempt, outcome, token);
        public ValueTask<ProjectPendingStoreChange> ObserveClosedAbsentAsync(ProjectDefinitionFrozenSubmission frozen, ProjectDefinitionLookup.ClosedAbsent absence, CancellationToken token = default)
            => Inner.ObserveClosedAbsentAsync(frozen, absence, token);
        public ValueTask<ProjectPendingSubmission?> ReadAsync(ProjectDefinitionOperation operation, CancellationToken token = default) => Inner.ReadAsync(operation, token);
        public ValueTask<ProjectPendingSubmissionPage> ListAsync(ProjectPendingScope scope, int take, ProjectDefinitionOperation? after = null, CancellationToken token = default)
            => Inner.ListAsync(scope, take, after, token);
        public ValueTask<ProjectPendingStoreChange> CancelPreparedAsync(ProjectPendingSubmission record, CancellationToken token = default) => Inner.CancelPreparedAsync(record, token);
        public ValueTask<ProjectPendingStoreChange> ClearResolvedAsync(ProjectPendingSubmission record, CancellationToken token = default) => Inner.ClearResolvedAsync(record, token);
    }

    private sealed class RevokeAfterSql(EditingUiTestAccess access) : SaveChangesInterceptor {
        public const string Target = "Workbench held SQL authority";
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default) {
            if (Armed && result > 0 && eventData.Context is ProjectsDbContext context &&
                context.ChangeTracker.Entries<Project>().Any(entry => entry.Entity.Name == Target)) {
                Assert.NotNull(context.Database.CurrentTransaction);
                Reached = true;
                Armed = false;
                access.Allowed = false;
            }
            return ValueTask.FromResult(result);
        }
    }
}
