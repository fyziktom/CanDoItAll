using Bunit;
using CanDoItAll.AppComponents;
using CanDoItAll.SharedKernel;
using CanDoItAll.Modules.Workbench;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using CanDoItAll.Modules.Projects;

namespace CanDoItAll.Tests.Components.Shell;

public sealed class MainLayoutRetirementTests {
    public enum CloseCommand { One, Others, Right, Background }

    [Theory]
    [InlineData(CloseCommand.One, false)]
    [InlineData(CloseCommand.Others, false)]
    [InlineData(CloseCommand.Right, false)]
    [InlineData(CloseCommand.Background, false)]
    [InlineData(CloseCommand.One, true)]
    public async Task A_pending_descriptor_cannot_reopen_a_closed_tab_but_an_explicit_reopen_can(CloseCommand command, bool legacyIdentity) {
        var gate = new ProjectReadGate();
        var store = new HeldStore(false);
        store.Release.TrySetResult();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IWorkbenchStateStore>(store));
            services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new HeldProjectsFactory(
                new PooledDbContextFactory<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>()), gate));
        });
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Closing Structure tab" })).Value;
        var cut = harness.Context.Render<ObservedLayout>();
        await cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(20));
        var navigation = services.GetRequiredService<NavigationManager>();
        var workbench = services.GetRequiredService<WorkbenchStateService>();
        var dashboard = workbench.GetActiveTab()!;
        if (legacyIdentity) {
            const string legacyTabId = "legacy-project-structure";
            const string legacyArtifactKey = "legacy-structure-key";
            await workbench.TrackTabAsync(new(legacyTabId, "Legacy Structure", $"/projects/{project:D}/structure?view=gantt",
                WorkbenchTabKinds.ProjectStructure, ProjectId: project, ArtifactKey: legacyArtifactKey));
        }
        await cut.InvokeAsync(() => navigation.NavigateTo($"/projects/{project:D}/structure?view=gantt"));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        var closing = workbench.GetActiveTab()!;
        gate.Armed = true;
        await cut.InvokeAsync(() => navigation.NavigateTo($"/projects/{project:D}/structure?view=canvas"));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var pendingNavigation = cut.Instance.NavigationCompletion;
        var shell = cut.FindComponent<AppShell>().Instance;
        store.HoldNextSave = true;
        var pendingClose = cut.InvokeAsync(() => command switch {
            CloseCommand.One => shell.CloseTab.InvokeAsync(closing.TabId),
            CloseCommand.Others => shell.CloseOtherTabs.InvokeAsync(dashboard.TabId),
            CloseCommand.Right => shell.CloseTabsToRight.InvokeAsync(dashboard.TabId),
            CloseCommand.Background => shell.CloseBackgroundTabs.InvokeAsync(dashboard.TabId),
            _ => throw new ArgumentOutOfRangeException(nameof(command))
        });
        try {
            await store.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.DoesNotContain(workbench.Tabs, tab => tab.TabId == closing.TabId);
            gate.Release.TrySetResult();
            await pendingNavigation.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.DoesNotContain(workbench.Tabs, tab => tab.TabId == closing.TabId);
            Assert.Contains(workbench.RecentTabs, tab => tab.TabId == closing.TabId);
        } finally {
            gate.Release.TrySetResult();
            store.SaveRelease.TrySetResult();
        }
        await pendingClose.WaitAsync(TimeSpan.FromSeconds(10));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(dashboard.TabId, workbench.ActiveTabId);
        Assert.Equal(dashboard.Route, new Uri(navigation.Uri).PathAndQuery);
        Assert.DoesNotContain(workbench.Tabs, tab => tab.TabId == closing.TabId);

        await cut.InvokeAsync(() => shell.ReopenRecentTab.InvokeAsync(closing.TabId));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(closing.TabId, workbench.ActiveTabId);
        Assert.Single(workbench.Tabs, tab => tab.TabId == closing.TabId);
        Assert.DoesNotContain(workbench.RecentTabs, tab => tab.TabId == closing.TabId);
        Assert.False(harness.Context.Renderer.UnhandledException.IsCompleted);
    }

    [Fact]
    public async Task Closing_background_tabs_does_not_discard_a_new_destination_read() {
        var gate = new ProjectReadGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new HeldProjectsFactory(
                new PooledDbContextFactory<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>()), gate)));
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "New Structure destination" })).Value;
        var cut = harness.Context.Render<ObservedLayout>();
        await cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(20));
        var navigation = services.GetRequiredService<NavigationManager>();
        var workbench = services.GetRequiredService<WorkbenchStateService>();
        var dashboard = workbench.GetActiveTab()!;
        await cut.InvokeAsync(() => navigation.NavigateTo("/prompt-gallery"));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        var background = workbench.GetActiveTab()!;
        var shell = cut.FindComponent<AppShell>().Instance;
        await cut.InvokeAsync(() => shell.SelectTab.InvokeAsync(dashboard.TabId));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        gate.Armed = true;
        await cut.InvokeAsync(() => navigation.NavigateTo($"/projects/{project:D}/structure"));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var pendingNavigation = cut.Instance.NavigationCompletion;
        try {
            await cut.InvokeAsync(() => shell.CloseBackgroundTabs.InvokeAsync(dashboard.TabId));
            Assert.DoesNotContain(workbench.Tabs, tab => tab.TabId == background.TabId);
        } finally {
            gate.Release.TrySetResult();
        }
        await pendingNavigation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(project, workbench.GetActiveTab()!.ProjectId);
        Assert.Equal(WorkbenchTabKinds.ProjectStructure, workbench.GetActiveTab()!.TabKind);
        Assert.DoesNotContain(workbench.Tabs, tab => tab.TabId == background.TabId);
    }

    [Fact]
    public async Task Explicit_reopen_to_the_same_uri_is_preserved_when_the_old_read_finishes_later() {
        var gate = new ProjectReadGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new HeldProjectsFactory(
                new PooledDbContextFactory<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>()), gate)));
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Explicitly reopened Structure" })).Value;
        var cut = harness.Context.Render<ObservedLayout>();
        await cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(20));
        var navigation = services.GetRequiredService<NavigationManager>();
        var workbench = services.GetRequiredService<WorkbenchStateService>();
        var route = $"/projects/{project:D}/structure";
        await cut.InvokeAsync(() => navigation.NavigateTo(route));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        var original = workbench.GetActiveTab()!;
        gate.Armed = true;
        await cut.InvokeAsync(() => navigation.NavigateTo(route));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var oldNavigation = cut.Instance.NavigationCompletion;
        var shell = cut.FindComponent<AppShell>().Instance;
        var pendingClose = cut.InvokeAsync(() => shell.CloseTab.InvokeAsync(original.TabId));
        try {
            cut.WaitForAssertion(() => Assert.DoesNotContain(workbench.Tabs, tab => tab.TabId == original.TabId));
            await cut.InvokeAsync(() => shell.ReopenRecentTab.InvokeAsync(original.TabId));
            await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(original.TabId, workbench.ActiveTabId);
        } finally {
            gate.Release.TrySetResult();
        }
        await oldNavigation.WaitAsync(TimeSpan.FromSeconds(10));
        await pendingClose.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(original.TabId, workbench.ActiveTabId);
        Assert.Equal(route, workbench.GetActiveTab()!.Route);
        Assert.Single(workbench.Tabs, tab => tab.TabId == original.TabId);
        Assert.DoesNotContain(workbench.RecentTabs, tab => tab.TabId == original.TabId);
    }

    [Fact]
    public async Task A_newer_same_uri_navigation_is_not_replaced_when_close_persistence_completes() {
        var gate = new ProjectReadGate();
        var store = new HeldStore(false);
        store.Release.TrySetResult();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IWorkbenchStateStore>(store));
            services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new HeldProjectsFactory(
                new PooledDbContextFactory<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>()), gate));
        });
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Newer Structure navigation" })).Value;
        var cut = harness.Context.Render<ObservedLayout>();
        await cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(20));
        var navigation = services.GetRequiredService<NavigationManager>();
        var workbench = services.GetRequiredService<WorkbenchStateService>();
        var route = $"/projects/{project:D}/structure";
        await cut.InvokeAsync(() => navigation.NavigateTo(route));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        var original = workbench.GetActiveTab()!;
        var shell = cut.FindComponent<AppShell>().Instance;
        store.HoldNextSave = true;
        var pendingClose = cut.InvokeAsync(() => shell.CloseTab.InvokeAsync(original.TabId));
        Task newerNavigation;
        try {
            await store.SaveEntered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            gate.Armed = true;
            await cut.InvokeAsync(() => navigation.NavigateTo(route));
            await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            newerNavigation = cut.Instance.NavigationCompletion;
            store.SaveRelease.TrySetResult();
            await pendingClose.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.Equal(route, new Uri(navigation.Uri).PathAndQuery);
            Assert.Same(newerNavigation, cut.Instance.NavigationCompletion);
        } finally {
            gate.Release.TrySetResult();
            store.SaveRelease.TrySetResult();
        }
        await newerNavigation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(original.TabId, workbench.ActiveTabId);
        Assert.Single(workbench.Tabs, tab => tab.TabId == original.TabId);
    }

    [Fact]
    public async Task Closing_the_previous_active_tab_waits_for_the_already_pending_destination() {
        var gate = new ProjectReadGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new HeldProjectsFactory(
                new PooledDbContextFactory<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>()), gate)));
        var services = harness.Context.Services;
        var project = (await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Already pending destination" })).Value;
        var cut = harness.Context.Render<ObservedLayout>();
        await cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(20));
        var navigation = services.GetRequiredService<NavigationManager>();
        var workbench = services.GetRequiredService<WorkbenchStateService>();
        await cut.InvokeAsync(() => navigation.NavigateTo("/prompt-gallery"));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        var previous = workbench.GetActiveTab()!;
        var route = $"/projects/{project:D}/structure";
        gate.Armed = true;
        await cut.InvokeAsync(() => navigation.NavigateTo(route));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var pendingNavigation = cut.Instance.NavigationCompletion;
        var shell = cut.FindComponent<AppShell>().Instance;
        var pendingClose = cut.InvokeAsync(() => shell.CloseTab.InvokeAsync(previous.TabId));
        try {
            cut.WaitForAssertion(() => Assert.DoesNotContain(workbench.Tabs, tab => tab.TabId == previous.TabId));
            Assert.False(pendingClose.IsCompleted);
            Assert.Equal(route, new Uri(navigation.Uri).PathAndQuery);
        } finally {
            gate.Release.TrySetResult();
        }
        await pendingNavigation.WaitAsync(TimeSpan.FromSeconds(10));
        await pendingClose.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(route, new Uri(navigation.Uri).PathAndQuery);
        Assert.Equal(project, workbench.GetActiveTab()!.ProjectId);
        Assert.Contains(workbench.RecentTabs, tab => tab.TabId == previous.TabId);
    }

    [Fact]
    public async Task Late_missing_project_cannot_redirect_the_newer_route() {
        var gate = new ProjectReadGate();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.AddSingleton<IDbContextFactory<ProjectsDbContext>>(provider => new HeldProjectsFactory(
                new PooledDbContextFactory<ProjectsDbContext>(provider.GetRequiredService<DbContextOptions<ProjectsDbContext>>()), gate)));
        var cut = harness.Context.Render<ObservedLayout>();
        await cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(20));
        var navigation = harness.Context.Services.GetRequiredService<NavigationManager>();
        gate.Armed = true;
        await cut.InvokeAsync(() => navigation.NavigateTo($"/projects/{Guid.NewGuid():D}/structure"));
        await gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var oldNavigation = cut.Instance.NavigationCompletion;
        await cut.InvokeAsync(() => navigation.NavigateTo("/settings"));
        await cut.Instance.NavigationCompletion.WaitAsync(TimeSpan.FromSeconds(10));
        gate.Release.TrySetResult();
        await oldNavigation.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.EndsWith("/settings", navigation.Uri, StringComparison.Ordinal);
        Assert.Equal("/settings", harness.Context.Services.GetRequiredService<WorkbenchStateService>().GetActiveTab()!.Route);
    }

    [Fact]
    public async Task Active_initialization_failure_is_visible_and_retry_persists_the_original_session() {
        var store = new HeldStore(false) { FailNextSave = true };
        store.Release.TrySetResult();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.Replace(ServiceDescriptor.Singleton<IWorkbenchStateStore>(store)));
        var cut = harness.Context.Render<ObservedLayout>();
        await cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(20));
        cut.Find("[data-testid='layout-initialization-failure']");
        Assert.DoesNotContain(harness.Context.JSInterop.Invocations, invocation =>
            invocation.Identifier == "CanDoItAll.browserState.registerDatabaseSwitchListener");
        await cut.Find("[data-testid='layout-initialization-retry']").ClickAsync(new());
        Assert.Empty(cut.FindAll("[data-testid='layout-initialization-failure']"));
        Assert.Equal(3, store.Saves);
        Assert.Single(harness.Context.JSInterop.Invocations, invocation =>
            invocation.Identifier == "CanDoItAll.browserState.registerDatabaseSwitchListener");
        Assert.Single(harness.Context.Services.GetRequiredService<WorkbenchStateService>().Tabs);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_first_render_does_not_continue_browser_effects(bool holdSave) {
        var store = new HeldStore(holdSave);
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.Replace(ServiceDescriptor.Singleton<IWorkbenchStateStore>(store)));
        var cut = harness.Context.Render<ObservedLayout>();
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await cut.InvokeAsync(cut.Instance.Dispose);
        store.Release.TrySetResult();
        var failure = await Record.ExceptionAsync(() => cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(failure is null or OperationCanceledException, failure?.ToString());
        Assert.Equal(holdSave ? 1 : 0, store.Saves);
        Assert.DoesNotContain(harness.Context.JSInterop.Invocations, invocation =>
            invocation.Identifier == "CanDoItAll.browserState.registerDatabaseSwitchListener");
        if (!holdSave) {
            Assert.Empty(harness.Context.Services.GetRequiredService<WorkbenchStateService>().Tabs);
        }
    }

    [Fact]
    public async Task Registration_finishing_after_retirement_unregisters_only_its_owned_listener() {
        await using var harness = await ComponentTestHarness.CreateAsync();
        var registration = harness.Context.JSInterop.SetupVoid("CanDoItAll.browserState.registerDatabaseSwitchListener", _ => true);
        var cut = harness.Context.Render<ObservedLayout>();
        cut.WaitForAssertion(() => Assert.Single(registration.Invocations));
        var invocation = registration.Invocations.Single();
        var callback = Assert.IsType<DotNetObjectReference<CanDoItAll.Web.Components.Layout.MainLayout>>(invocation.Arguments.Last());
        await cut.InvokeAsync(cut.Instance.Dispose);
        registration.SetVoidResult();
        var failure = await Record.ExceptionAsync(() => cut.Instance.FirstRender.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(failure is null or OperationCanceledException, failure?.ToString());
        cut.WaitForAssertion(() => {
            var cleanup = Assert.Single(harness.Context.JSInterop.Invocations, item =>
                item.Identifier == "CanDoItAll.browserState.unregisterDatabaseSwitchListener");
            Assert.Equal(invocation.Arguments[0], Assert.Single(cleanup.Arguments));
            Assert.Throws<ObjectDisposedException>(() => callback.Value);
        });
    }

    public sealed class ObservedLayout : CanDoItAll.Web.Components.Layout.MainLayout {
        public Task FirstRender { get; private set; } = Task.CompletedTask;
        protected override Task OnAfterRenderAsync(bool firstRender) {
            var rendering = base.OnAfterRenderAsync(firstRender);
            if (firstRender) {
                FirstRender = rendering;
            }
            return rendering;
        }
    }

    private sealed class HeldStore(bool holdSave) : IWorkbenchStateStore {
        public bool FailNextSave { get; set; }
        public bool HoldNextSave { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SaveEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource SaveRelease { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int Saves { get; private set; }
        public async ValueTask<WorkbenchSessionSnapshot?> LoadAsync(CancellationToken cancellationToken = default) {
            if (!holdSave) {
                Entered.TrySetResult();
                await Release.Task;
            }
            return null;
        }
        public async ValueTask SaveAsync(WorkbenchSessionSnapshot snapshot, CancellationToken cancellationToken = default) {
            Saves++;
            if (FailNextSave) {
                FailNextSave = false;
                throw new JSException("Synthetic browser storage failure");
            }
            if (holdSave) {
                Entered.TrySetResult();
                await Release.Task;
            }
            if (HoldNextSave) {
                HoldNextSave = false;
                SaveEntered.TrySetResult();
                await SaveRelease.Task;
            }
        }
    }

    private sealed class ProjectReadGate {
        public bool Armed { get; set; }
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class HeldProjectsFactory(IDbContextFactory<ProjectsDbContext> inner, ProjectReadGate gate) : IDbContextFactory<ProjectsDbContext> {
        public ProjectsDbContext CreateDbContext() => inner.CreateDbContext();
        public async Task<ProjectsDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) {
            if (gate.Armed) {
                gate.Armed = false;
                gate.Entered.TrySetResult();
                await gate.Release.Task;
            }
            return await inner.CreateDbContextAsync(CancellationToken.None);
        }
    }
}
