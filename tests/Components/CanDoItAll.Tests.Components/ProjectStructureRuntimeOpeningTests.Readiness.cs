using System.Net;
using Bunit;
using CanDoItAll.Components.BaseLib;
using CanDoItAll.Components.CanvasLib;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Modules.Workbench.Pages;
using CanDoItAll.SharedKernel;
using CanDoItAll.Workbench.Operators.UI.Runtime;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace CanDoItAll.Tests.Components.ProjectStructure;

public sealed partial class ProjectStructureRuntimeOpeningTests {
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Retired_readiness_success_or_error_cannot_replace_a_new_preview_on_the_same_page(bool fail) {
        var launcher = new Launcher();
        var http = new HeldReadiness();
        await using var harness = await ComponentTestHarness.CreateAsync(services => {
            services.Replace(ServiceDescriptor.Singleton<IProjectStructureRuntimeLauncher>(launcher));
            services.AddSingleton(new ProjectStructureRuntimeReadinessProbe(http));
        });
        var projectFile = Path.Combine(harness.RootPath, "ReadinessFixture.csproj");
        await File.WriteAllTextAsync(projectFile, "<Project Sdk=\"Microsoft.NET.Sdk.Web\"><PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup></Project>");
        launcher.Plan = launcher.Plan with { Targets = [new("Readiness fixture project", projectFile, IsDirectory: false)] };
        var project = await harness.Context.Services.GetRequiredService<ProjectsService>().SaveAsync(new() { Name = "Readiness origins" });
        Assert.True(project.IsSuccess);
        var workbench = harness.Context.Services.GetRequiredService<ProjectWorkbenchService>();
        var runtime = await workbench.CreateObjectAsync(project.Value, new(ProjectObjectType.Environment, "Original runtime", "", "",
            $"project:{project.Value}", ObjectSubtype: "dotnet-runtime", MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
                Environment = new() { EnvironmentKind = ProjectEnvironmentKind.DotNetRuntime, ProjectPath = ".", LocalhostUrl = "http://localhost:32147/" }
            })));
        var successor = await workbench.CreateObjectAsync(project.Value, new(ProjectObjectType.Link, "Successor preview", "", "",
            $"project:{project.Value}", MetadataJson: ProjectObjectMetadataSerializer.Serialize(new ProjectObjectMetadataEnvelope {
                Link = new() { Url = "https://example.invalid/successor" }
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
            await OpenAsync(cut, successor.Id);
            var opening = cut.FindComponent<OperatorWebPreview>().Instance.View;
            Assert.Equal("Successor preview", opening.Title);
            http.Fail = fail;
            http.Release.TrySetResult();
            await observation;
            await cut.InvokeAsync(() => {
                Assert.Equal(opening, cut.FindComponent<OperatorWebPreview>().Instance.View);
                Assert.Equal(launcher.Identity, Assert.Single(cut.Instance.RuntimeOutcomes).Identity);
                Assert.Single(launcher.Launched);
            });
        } finally {
            http.Release.TrySetResult();
            await observation;
            await dialogs.InvokeAsync(() => harness.Context.Services.GetRequiredService<DialogService>().CloseAsync(false));
        }
    }

    private sealed class HeldReadiness : HttpMessageHandler, IHttpClientFactory {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool Fail { get; set; }
        public HttpClient CreateClient(string name) => new(this, disposeHandler: false);
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) {
            Assert.Equal("http://localhost:32147/", request.RequestUri!.AbsoluteUri);
            Entered.TrySetResult();
            await Release.Task;
            if (Fail) {
                throw new InvalidOperationException("Held observation failed after the successor opened.");
            }
            return new(HttpStatusCode.OK);
        }
    }
}
