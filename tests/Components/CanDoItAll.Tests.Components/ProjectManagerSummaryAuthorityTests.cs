using System.Data.Common;
using Bunit;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Insights.UI;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using NativeSummary = CanDoItAll.Modules.Workbench.Pages.Components.ProjectStructure.ProjectManagerSummaryPanel;
using ReportRenderer = CanDoItAll.Workbench.Insights.UI.ProjectManagerSummaryPanel;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed class ProjectManagerSummaryAuthorityTests {
    private const string PrivateName = "Private original Manager report";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retained_native_report_requires_current_read_without_requiring_structure_write(bool allowed) {
        var access = new ReportAccess();
        await using var harness = await HarnessAsync(access);
        var report = await PrepareAsync(harness, cached: true);
        access.Inner.Allowed = allowed;
        var cut = Render(harness, report);
        if (allowed) {
            cut.WaitForElement("[data-testid='manager-summary-metrics']");
            Assert.Equal(PrivateName, cut.FindComponent<ReportRenderer>().Instance.Session.Snapshot?.ProjectName);
        } else {
            cut.WaitForAssertion(() => {
                Assert.DoesNotContain(cut.FindComponents<ReportRenderer>(), child => child.Instance.Session.Snapshot is not null);
                Assert.Null(report.State.Snapshot);
            });
            Assert.DoesNotContain(PrivateName, cut.Markup, StringComparison.Ordinal);
            Assert.Null(report.State.Snapshot);
        }
        Assert.Equal(0, access.WriteChecks);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Original_read_grant_retirement_removes_report_and_any_open_activity(bool activity) {
        var access = new ReportAccess();
        await using var harness = await HarnessAsync(access);
        var report = await PrepareAsync(harness, cached: true);
        var cut = Render(harness, report);
        cut.WaitForElement("[data-testid='manager-summary-metrics']");
        var session = cut.FindComponent<ReportRenderer>().Instance.Session;
        if (activity) {
            await cut.InvokeAsync(() => cut.Find("[data-testid='manager-summary-open-activity']").ClickAsync(new()));
            Assert.NotNull(session.Activity?.Page);
        }

        await cut.InvokeAsync(access.Revoke);

        cut.WaitForAssertion(() => Assert.Empty(cut.FindAll("[data-testid='manager-summary-metrics']")));
        Assert.Empty(cut.FindAll("[data-testid='manager-summary-activity-dialog']"));
        Assert.Null(report.State.Snapshot);
        Assert.Equal(report.Options, report.State.Options);
        Assert.DoesNotContain(PrivateName, cut.Markup, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_scope_read_validates_original_access_after_SQL_before_report_publication(bool revoke) {
        var access = new ReportAccess();
        var probe = new ReportReadProbe(access, revoke);
        await using var harness = await HarnessAsync(access, probe);
        var report = await PrepareAsync(harness, cached: false);
        var cut = Render(harness, report);
        cut.WaitForElement("[data-testid='manager-summary-load']");
        var session = cut.FindComponent<ReportRenderer>().Instance.Session;
        probe.Armed = true;

        await cut.InvokeAsync(() => cut.Find("[data-testid='manager-summary-load']").ClickAsync(new()));

        Assert.True(probe.Reached);
        Assert.Equal(!revoke, session.Snapshot is not null);
        Assert.Equal(!revoke, report.State.Snapshot is not null);
        Assert.Equal(0, access.WriteChecks);
        if (revoke) {
            Assert.Empty(cut.FindAll("[data-testid='manager-summary-metrics']"));
            Assert.DoesNotContain(PrivateName, cut.Markup, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Native_activity_read_does_not_publish_a_page_after_original_access_loss(bool revoke) {
        var access = new ReportAccess();
        var probe = new ReportReadProbe(access, revoke);
        await using var harness = await HarnessAsync(access, probe);
        var report = await PrepareAsync(harness, cached: true);
        var cut = Render(harness, report);
        cut.WaitForElement("[data-testid='manager-summary-metrics']");
        var session = cut.FindComponent<ReportRenderer>().Instance.Session;
        probe.Armed = true;

        await cut.InvokeAsync(() => cut.Find("[data-testid='manager-summary-open-activity']").ClickAsync(new()));

        Assert.True(probe.Reached);
        Assert.Equal(!revoke, session.Activity?.Page is not null);
        Assert.Equal(0, access.WriteChecks);
        if (revoke) {
            Assert.Null(report.State.Snapshot);
            Assert.Empty(cut.FindAll("[data-testid='manager-summary-metrics']"));
            Assert.Empty(cut.FindAll("[data-testid='manager-summary-activity-dialog']"));
        }
    }

    [Fact]
    public async Task Retained_report_cannot_restore_a_recreated_descendant_under_the_original_scope() {
        var access = new ReportAccess();
        await using var harness = await HarnessAsync(access);
        var report = await PrepareAsync(harness, cached: true, descendant: true);
        var originalChild = Assert.Single(report.State.ScopeAdmissions, item => item.ProjectId != report.ProjectId);
        await using (var database = await harness.Context.Services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync()) {
            Assert.Equal(1, await database.Set<Project>().Where(item => item.Id == originalChild.ProjectId).ExecuteDeleteAsync());
            database.Set<Project>().Add(new() { Id = originalChild.ProjectId, Name = "Recreated report descendant", Slug = "recreated-report-descendant" });
            await database.SaveChangesAsync();
        }
        var current = await harness.Context.Services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(originalChild.ProjectId);
        Assert.NotNull(current);
        Assert.NotEqual(originalChild.LifetimeId, current.LifetimeId);

        var cut = Render(harness, report);

        cut.WaitForAssertion(() => {
            Assert.DoesNotContain(cut.FindComponents<ReportRenderer>(), child => child.Instance.Session.Snapshot is not null);
            Assert.Null(report.State.Snapshot);
        });
        Assert.Null(report.State.Snapshot);
        Assert.DoesNotContain(PrivateName, cut.Markup, StringComparison.Ordinal);
    }

    private static Task<ComponentTestHarness> HarnessAsync(ReportAccess access, ReportReadProbe? probe = null)
        => ComponentTestHarness.CreateAsync(services => {
            services.AddSingleton<IEditingUiAccess>(access);
            if (probe is not null) {
                services.ConfigureDbContext<ProjectsDbContext>(options => options.AddInterceptors(probe), ServiceLifetime.Singleton);
            }
        });

    private static async Task<ReportFixture> PrepareAsync(ComponentTestHarness harness, bool cached, bool descendant = false) {
        var services = harness.Context.Services;
        var saved = await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = PrivateName });
        Assert.True(saved.IsSuccess);
        if (descendant) {
            var child = await services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Original report descendant" });
            Assert.True(child.IsSuccess);
            await using var database = await services.GetRequiredService<IDbContextFactory<ProjectsDbContext>>().CreateDbContextAsync();
            database.Set<ProjectHierarchyLink>().Add(new() { ParentProjectId = saved.Value, ChildProjectId = child.Value });
            await database.SaveChangesAsync();
        }
        var options = new ProjectManagerSummaryOptions(Scope: descendant
            ? ProjectManagerSummaryScope.ProjectAndDescendants : ProjectManagerSummaryScope.CurrentProject);
        var admission = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureAsync(saved.Value);
        Assert.NotNull(admission);
        var authentication = services.GetRequiredService<AuthenticationStateProvider>().GetAuthenticationStateAsync();
        var state = services.GetRequiredService<ProjectManagerSummaryStateStore>().GetOrCreate(saved.Value);
        state.Options = options;
        state.ActorStamp = ProjectManagerSummarySource.ActorStamp((await authentication).User);
        state.LifetimeId = admission.LifetimeId;
        if (cached) {
            var scope = await services.GetRequiredService<ProjectManagerSummaryScopeResolver>().ResolveAsync(saved.Value, options.Scope, options.ContentMode);
            state.Snapshot = await services.GetRequiredService<ProjectManagerSummaryQueryService>().LoadAsync(scope, options);
            state.ScopeAdmissions = await services.GetRequiredService<ProjectWriteAdmissionService>().CaptureManyAsync(scope.ProjectIds);
        }
        return new(saved.Value, admission, state, options, authentication);
    }

    private static IRenderedComponent<NativeSummary> Render(ComponentTestHarness harness, ReportFixture report)
        => harness.Context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(component => component.Value, report.Authentication)
            .AddChildContent<NativeSummary>(child => child.Add(component => component.ProjectId, report.ProjectId)
                .Add(component => component.ExpectedProjectAdmission, report.Admission)))
            .FindComponent<NativeSummary>();

    private sealed record ReportFixture(Guid ProjectId, ProjectWriteAdmission Admission, ProjectManagerSummaryViewState State,
        ProjectManagerSummaryOptions Options, Task<AuthenticationState> Authentication);

    private sealed class ReportAccess : IEditingUiAccess {
        private readonly List<EditingUiGrant> grants = [];
        public EditingUiTestAccess Inner { get; } = new();
        public int WriteChecks { get; private set; }
        public async Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken) {
            var grant = await Inner.OpenAsync(topic, cancellationToken);
            grants.Add(grant);
            return grant;
        }
        public async ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) {
            await Inner.ValidateAsync(grant, write, cancellationToken);
            if (write) {
                WriteChecks++;
                throw new UnauthorizedAccessException("Report read has no Structure write authority.");
            }
        }
        public void Revoke() {
            foreach (var grant in grants) {
                grant.Revoke();
            }
        }
    }

    private sealed class ReportReadProbe(ReportAccess access, bool revoke) : DbCommandInterceptor {
        public bool Armed { get; set; }
        public bool Reached { get; private set; }
        public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
            DbDataReader result, CancellationToken cancellationToken = default) {
            if (Armed && command.CommandText.Contains("\"Projects_Projects\"", StringComparison.Ordinal)) {
                Armed = false;
                Reached = true;
                if (revoke) {
                    access.Inner.Allowed = false;
                }
            }
            return ValueTask.FromResult(result);
        }
    }
}
