using Bunit;
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
            var cleanup = Assert.Single(harness.Context.JSInterop.Invocations.Where(item =>
                item.Identifier == "CanDoItAll.browserState.unregisterDatabaseSwitchListener"));
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
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
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
