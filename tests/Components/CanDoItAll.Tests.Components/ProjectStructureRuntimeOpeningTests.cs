using System.Security.Claims;
using System.Text.Json;
using Bunit;
using CanDoItAll.AgentFramework.Core;
using CanDoItAll.Infrastructure.ControlPlane;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Operators.UI.Runtime;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

[Trait("Category", "HostPlatform")]
public sealed partial class ProjectStructureRuntimeOpeningTests {
    public enum ApprovalChange { None, Cancel, Metadata, Kind, Parent, ProjectLifetime, Navigation, SelectionABA, Actor, RuntimeGeneration }

    [Theory]
    [InlineData(ApprovalChange.None)]
    [InlineData(ApprovalChange.Cancel)]
    [InlineData(ApprovalChange.Metadata)]
    [InlineData(ApprovalChange.Kind)]
    [InlineData(ApprovalChange.Parent)]
    [InlineData(ApprovalChange.ProjectLifetime)]
    [InlineData(ApprovalChange.Navigation)]
    [InlineData(ApprovalChange.SelectionABA)]
    [InlineData(ApprovalChange.Actor)]
    [InlineData(ApprovalChange.RuntimeGeneration)]
    public async Task One_launch_approval_cannot_admit_a_changed_original_target(ApprovalChange change) {
        var launcher = new Launcher();
        RuntimeGeneration? runtime = null;
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IProjectStructureRuntimeLauncher>(launcher));
            services.AddSingleton<CanonicalRuntimeDatabase>();
            services.AddSingleton<ICanonicalRuntimeDatabase>(provider => runtime = new(provider.GetRequiredService<CanonicalRuntimeDatabase>()));
        });
        var target = await SeedAsync(harness);
        var actor = Actor("original");
        var host = harness.Context.Render<CascadingValue<Task<AuthenticationState>>>(parameters => parameters
            .Add(component => component.Value, actor).AddChildContent<ProjectStructurePage>(child => child.Add(page => page.ProjectId, target.ProjectId)));
        var cut = host.FindComponent<ProjectStructurePage>();
        cut.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var dialogs = harness.Context.Render<DialogHost>();
        await SelectAsync(cut, target.Node.Id);
        var pending = cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(target.Node.Id, "runtime:open", 0, 0));
        try {
            dialogs.WaitForElement("[data-testid='project-structure-runtime-launch-approval-confirm']");
            var originalDecision = dialogs.FindComponent<OperatorRuntimeApproval>().Instance.Decide;
            Assert.Contains(launcher.Plan.DisplayCommand, dialogs.Markup, StringComparison.Ordinal);
            await cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(target.Node.Id, "runtime:open", 0, 0));
            Assert.Empty(launcher.Launched);
            if (change is ApprovalChange.Metadata or ApprovalChange.Kind or ApprovalChange.Parent) {
                await using var database = await harness.Context.Services.GetRequiredService<IDbContextFactory<WorkbenchDbContext>>().CreateDbContextAsync();
                var record = await database.Set<ProjectObjectRecord>().SingleAsync(record => record.NodeKey == target.Node.Id);
                if (change == ApprovalChange.Metadata) {
                    record.MetadataJson = ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope { Script = new() {
                        ScriptKind = ProjectScriptKind.PowerShell, Command = "Write-Output changed", WorkingDirectory = "."
                    } });
                } else if (change == ApprovalChange.Kind) {
                    record.ObjectType = ProjectObjectType.Note;
                } else {
                    record.ParentNodeKey = target.Neighbor.Id;
                }
                await database.SaveChangesAsync();
            } else if (change == ApprovalChange.ProjectLifetime) {
                var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
                await projects.DeleteAsync(target.ProjectId, expectedProjectAdmission: target.Admission);
                Assert.True((await projects.CreateAsync(target.ProjectId, new() { Name = "Replacement runtime project" })).IsSuccess);
            } else if (change == ApprovalChange.Navigation) {
                harness.Context.Services.GetRequiredService<NavigationManager>().NavigateTo("/projects");
            } else if (change == ApprovalChange.SelectionABA) {
                await SelectAsync(cut, target.Neighbor.Id);
                await SelectAsync(cut, target.Node.Id);
            } else if (change is ApprovalChange.Actor or ApprovalChange.RuntimeGeneration) {
                if (change == ApprovalChange.RuntimeGeneration) {
                    runtime!.Generation++;
                }
                await host.InvokeAsync(() => host.Render(parameters => parameters.Add(component => component.Value,
                    change == ApprovalChange.Actor ? Actor("successor") : actor)
                    .AddChildContent<ProjectStructurePage>(child => child.Add(page => page.ProjectId, target.ProjectId))));
            }
            if (change == ApprovalChange.Navigation) {
                dialogs.WaitForAssertion(() => Assert.Empty(dialogs.FindAll("[data-testid='project-structure-runtime-launch-approval-confirm']")));
                await dialogs.InvokeAsync(() => originalDecision.InvokeAsync(true));
            } else {
                await dialogs.InvokeAsync(() => dialogs.Find(change == ApprovalChange.Cancel
                    ? "[data-testid='project-structure-runtime-launch-approval-cancel']"
                    : "[data-testid='project-structure-runtime-launch-approval-confirm']").ClickAsync(new()));
            }
        } finally {
            await dialogs.InvokeAsync(() => harness.Context.Services.GetRequiredService<DialogService>().CloseAsync(false));
        }
        await pending;
        Assert.Equal(change == ApprovalChange.None ? 1 : 0, launcher.Launched.Count);
        if (change == ApprovalChange.None) {
            var accepted = Assert.Single(cut.Instance.RuntimeOutcomes);
            Assert.Equal(launcher.Identity, accepted.Identity);
            Assert.Equal(target.Node.Id, Assert.Single(launcher.Launched));
            Assert.Equal(launcher.Plan.Arguments, launcher.Reviewed!.Arguments);
        } else {
            Assert.All(cut.Instance.RuntimeOutcomes, result => Assert.Null(result.Identity));
        }
    }

    [Fact]
    public async Task Retired_quick_action_and_close_callbacks_cannot_use_a_reopened_same_node() {
        var launcher = new Launcher();
        await using var harness = await ComponentTestHarness.CreateAsync(services =>
            services.Replace(ServiceDescriptor.Singleton<IProjectStructureRuntimeLauncher>(launcher)));
        var target = await SeedAsync(harness);
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(page => page.ProjectId, target.ProjectId));
        cut.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        await OpenAsync(cut, target.Node.Id);
        var old = cut.FindComponent<OperatorQuickActions>().Instance;
        var execute = old.Execute;
        var close = old.Close;
        var oldId = old.View.OpeningId;
        await cut.InvokeAsync(() => close.InvokeAsync());
        await OpenAsync(cut, target.Neighbor.Id);
        await cut.InvokeAsync(() => cut.FindComponent<OperatorQuickActions>().Instance.Close.InvokeAsync());
        await OpenAsync(cut, target.Node.Id);
        var latest = cut.FindComponent<OperatorQuickActions>().Instance.View.OpeningId;
        Assert.NotEqual(oldId, latest);
        await cut.InvokeAsync(() => execute.InvokeAsync(1));
        await cut.InvokeAsync(() => close.InvokeAsync());
        Assert.Empty(launcher.Launched);
        Assert.Equal(latest, cut.FindComponent<OperatorQuickActions>().Instance.View.OpeningId);
    }

    private static Task<AuthenticationState> Actor(string name) => Task.FromResult(new AuthenticationState(new ClaimsPrincipal(
        new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], "Owned test actor"))));
    private sealed class RuntimeGeneration(ICanonicalRuntimeDatabase inner) : ICanonicalRuntimeDatabase {
        public ResolvedDatabaseProfile Profile => inner.Profile;
        public long Generation { get; set; } = inner.Generation;
    }
    private static Task SelectAsync(IRenderedComponent<ProjectStructurePage> cut, string id)
        => cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnSelectionChanged(id, JsonSerializer.Serialize(new[] { id })));
    private static Task OpenAsync(IRenderedComponent<ProjectStructurePage> cut, string id)
        => cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.NodeOpened.InvokeAsync(id));
    private sealed record Target(Guid ProjectId, ProjectWriteAdmission Admission, ProjectStructureNode Node, ProjectStructureNode Neighbor);
    private static async Task<Target> SeedAsync(ComponentTestHarness harness) {
        var projects = harness.Context.Services.GetRequiredService<ProjectsService>();
        var saved = await projects.SaveAsync(new() { Name = "Runtime opening fixture" });
        Assert.True(saved.IsSuccess);
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var node = await workbench.CreateObjectAsync(saved.Value, new(ProjectObjectType.Script, "Original runtime", "Review", "Original notes",
            $"project:{saved.Value}", ObjectSubtype: "powershell", MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
                Script = new() { ScriptKind = ProjectScriptKind.PowerShell, Command = "Write-Output original", WorkingDirectory = "." }
            })));
        var neighbor = await workbench.CreateObjectAsync(saved.Value, new(ProjectObjectType.Note, "Neighbor", "", "", $"project:{saved.Value}"));
        var surface = await workbench.GetStructureAsync(saved.Value);
        return new(saved.Value, surface.ExpectedProjectAdmission!, node, neighbor);
    }

    private sealed class Launcher : IProjectStructureRuntimeLauncher {
        public bool IsAvailable => true;
        public WorkspaceOwnedProcessIdentity Identity { get; } = new(1234, DateTimeOffset.UnixEpoch, new string('a', 64),
            new(WorkspaceOwnedProcessBoundaryKind.UnixProcessGroup, 1234, Guid.NewGuid()));
        public ProjectStructureRuntimeLaunchPlan Plan { get; set; } = new(ProjectStructureRuntimePlanKind.PowerShellScript, ["pwsh"],
            ["-NoProfile", "-Command", "Write-Output original"], new Dictionary<string, string?>(), ".", "Write-Output original",
            "Original explicit script", [], true, false);
        public List<string> Launched { get; } = [];
        public bool IsRunning(string nodeId) => Launched.Contains(nodeId);
        public WorkspaceOwnedProcessIdentity? GetIdentity(string nodeId) => IsRunning(nodeId) ? Identity : null;
        public WorkspaceOwnedProcessIdentity? GetIdentity(string nodeId, ProjectStructureRuntimeSessionOwner owner) => GetIdentity(nodeId);
        public ProjectStructureRuntimeLaunchPlan? Reviewed { get; private set; }
        public ProjectStructureRuntimeLaunchResolution Resolve(ProjectStructureNode? node)
            => new(Plan, "Ready", new(ProjectStructureRuntimeCapability.Available("Direct"),
                ProjectStructureRuntimeCapability.Unavailable(ProjectStructureRuntimeCapabilityStatus.Headless, "No terminal"),
                ProjectStructureRuntimeCapability.Unavailable(ProjectStructureRuntimeCapabilityStatus.Unsupported, "No elevation")));
        public ProjectStructureRuntimeLaunchResolution Resolve(ProjectObjectType type, string? subtype, string? notes,
            string metadata, ProjectStructureRuntimePathAuthorityMode authority) => Resolve(null);
        public Task<ProjectStructureRuntimeLaunchResult> LaunchAsync(ProjectStructureNode node, ProjectStructureRuntimeLaunchMode mode,
            ProjectStructureRuntimeLaunchApproval approval, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The UI must submit its reviewed immutable plan.");
        public Task<ProjectStructureRuntimeLaunchResult> LaunchReviewedAsync(ProjectStructureRuntimeSessionOwner owner, ProjectStructureNode node, ProjectStructureRuntimeLaunchPlan reviewed,
            ProjectStructureRuntimeLaunchMode mode, ProjectStructureRuntimeLaunchApproval approval, CancellationToken cancellationToken = default) {
            Assert.Equal(ProjectStructureRuntimeLaunchApproval.OperatorConfirmed, approval);
            Reviewed = reviewed;
            Launched.Add(node.Id);
            return Task.FromResult(new ProjectStructureRuntimeLaunchResult(true, "Owned fixture session acquired") { Identity = Identity, ObservationCompleted = true });
        }
    }
}
