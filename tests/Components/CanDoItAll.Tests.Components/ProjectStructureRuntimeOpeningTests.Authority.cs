using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Editing;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Tests.Support;
using CanDoItAll.Workbench.Operators.UI.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructureRuntimeOpeningTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Runtime_readiness_retains_accepted_process_without_publishing_after_original_read_loss(bool denyAfterRead) {
        var launcher = new Launcher();
        var http = new HeldReadiness();
        var access = new RuntimeAccess();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IProjectStructureRuntimeLauncher>(launcher));
            services.Replace(ServiceDescriptor.Singleton<IEditingUiAccess>(access));
            services.AddSingleton(new ProjectStructureRuntimeReadinessProbe(http));
        });
        var projectFile = Path.Combine(harness.RootPath, "AuthorityReadiness.csproj");
        await File.WriteAllTextAsync(projectFile, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        launcher.Plan = launcher.Plan with { Targets = [new("Owned readiness project", projectFile, IsDirectory: false)] };
        var project = await harness.Context.Services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Readiness original authority" });
        Assert.True(project.IsSuccess);
        var runtime = await harness.Context.Services.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(project.Value,
            new(ProjectObjectType.Environment, "Owned original runtime", "", "", $"project:{project.Value}", ObjectSubtype: "dotnet-runtime",
                MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
                    Environment = new() { EnvironmentKind = ProjectEnvironmentKind.DotNetRuntime, ProjectPath = ".", LocalhostUrl = "http://localhost:32147/" }
                })));
        var cut = harness.Context.Render<ProjectStructurePage>(parameters => parameters.Add(page => page.ProjectId, project.Value));
        cut.WaitForElement("[data-testid='project-structure-canvas-loaded']");
        var dialogs = harness.Context.Render<DialogHost>();
        await SelectAsync(cut, runtime.Id);
        var launch = cut.InvokeAsync(() => cut.FindComponent<CanvasWorkbench>().Instance.OnContextAction(runtime.Id, "runtime:open", 0, 0));
        Task observation = Task.CompletedTask;
        try {
            dialogs.WaitForElement("[data-testid='project-structure-runtime-launch-approval-confirm']");
            await dialogs.InvokeAsync(() => dialogs.Find("[data-testid='project-structure-runtime-launch-approval-confirm']").ClickAsync(new()));
            await launch;
            await http.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            observation = cut.Instance.RuntimeObservation;
            Assert.False(observation.IsCompleted);
            access.Inner.Allowed = !denyAfterRead;
            access.WriteAllowed = false;
            http.Release.TrySetResult();
            await observation;
            Assert.Equal(launcher.Identity, Assert.Single(cut.Instance.RuntimeOutcomes).Identity);
            Assert.Single(launcher.Launched);
            Assert.Equal(denyAfterRead ? 0 : 1, cut.FindComponents<OperatorWebPreview>().Count);
        } finally {
            http.Release.TrySetResult();
            await observation;
            await dialogs.InvokeAsync(() => harness.Context.Services.GetRequiredService<DialogService>().CloseAsync(false));
        }
    }

    private sealed class RuntimeAccess : IEditingUiAccess {
        public EditingUiTestAccess Inner { get; } = new();
        public bool WriteAllowed { get; set; } = true;
        public Task<EditingUiGrant> OpenAsync(EditingTopic topic, CancellationToken cancellationToken)
            => Inner.OpenAsync(topic, cancellationToken);
        public async ValueTask ValidateAsync(EditingUiGrant grant, bool write, CancellationToken cancellationToken) {
            await Inner.ValidateAsync(grant, write, cancellationToken);
            if (write && !WriteAllowed) {
                throw new UnauthorizedAccessException("Structure write denied; original read remains allowed.");
            }
        }
    }
}
