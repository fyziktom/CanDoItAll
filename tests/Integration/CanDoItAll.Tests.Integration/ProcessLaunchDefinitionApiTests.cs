using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CanDoItAll.Modules.Projects;
using CanDoItAll.Modules.Workbench;
using CanDoItAll.Processes.Application;
using CanDoItAll.Processes.Builder;
using CanDoItAll.Processes.Projections;
using CanDoItAll.Processes.Runtime;
using CanDoItAll.Processes.Templates;
using CanDoItAll.SharedKernel;
using CanDoItAll.Web.Api;
using Microsoft.Extensions.DependencyInjection;

namespace CanDoItAll.Tests.Integration.Processes;

[Trait("Category", "HostPlatform")]
public sealed class ProcessLaunchDefinitionApiTests {
    private const string DefinitionKey = "dotnet-solution-setup";
    private const string DriverProbe = "Pc4ObservedDrivers";

    [Theory]
    [InlineData(LaunchScope.Global)]
    [InlineData(LaunchScope.Project)]
    [InlineData(LaunchScope.ProjectNode)]
    public async Task HTTP_selection_canonicalizes_supported_inputs_and_refuses_explicit_contradictions(LaunchScope target) {
        var observer = new DriverObserver();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, services =>
            services.AddSingleton<IProcessLaunchVariableContributor>(observer));
        var (project, node) = await CreateTargetAsync(host.App.Services);
        var projectId = target == LaunchScope.Global ? (Guid?)null : project;
        await PublishAsync(host.App.Services, projectId, "Canonical authored selection", content => content.Definition.LaunchDriverActivations.Clear());
        var templates = host.App.Services.GetRequiredService<ProcessTemplatePackLoader>();
        var profile = templates.LoadLiveRunProfiles().First(item => item.ProcessTemplateKey != DefinitionKey);
        var definitionId = ProcessDefinitionCatalogProjectionService.CreateDefinitionId(new(DefinitionKey)).Value;
        var original = new ProcessLaunchApiRequest(ProjectId: projectId, ProjectNodeId: target == LaunchScope.ProjectNode ? node : null,
            RunReadiness: false, Execute: false);
        (ProcessLaunchApiRequest Request, string? Expected)[] cases = [
            (original with { DefinitionKey = "  DOTNET-SOLUTION-SETUP  " }, DefinitionKey),
            (original with { ProcessDefinitionId = definitionId }, DefinitionKey),
            (original with { DefinitionKey = DefinitionKey, ProcessDefinitionId = definitionId }, DefinitionKey),
            (original with { DefinitionKey = DefinitionKey, ProcessDefinitionId = Guid.NewGuid() }, null),
            (original with { ProcessDefinitionId = Guid.Empty }, null),
            (original with { ProcessDefinitionId = Guid.NewGuid() }, null),
            (original with { DefinitionKey = "pc4-definition-does-not-exist" }, null),
            (original with { DefinitionKey = DefinitionKey, LiveRunProfileKey = profile.Key }, null),
            (original with { LiveRunProfileKey = "  " + profile.Key.ToUpperInvariant() + "  " }, profile.ProcessTemplateKey),
            (original, templates.LoadLiveRunProfiles().First().ProcessTemplateKey)
        ];
        foreach (var (input, expected) in cases) {
            var request = input with { CallerIntentId = Guid.NewGuid() };
            var before = observer.Contexts.Count;
            using var response = await host.Client.PostAsJsonAsync("/api/processes/launch/check", request);
            if (expected is null) {
                Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
                Assert.Equal(before, observer.Contexts.Count);
                continue;
            }
            using var body = await ReadAsync(response);
            Assert.Equal(expected, body.RootElement.GetProperty("launchPlan").GetProperty("definitionKey").GetString());
            if (expected == DefinitionKey) {
                Assert.Equal(JsonValueKind.Null, body.RootElement.GetProperty("launchPlan").GetProperty("liveRunProfileKey").ValueKind);
            }
            await using var scope = host.App.Services.CreateAsyncScope();
            var saved = (await scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>()
                .FindByIntentAsync(new(request.CallerIntentId!.Value)))!;
            Assert.Equal(expected, saved.Preparation.InitialCommit.InitialPlan!.ExecutableDefinitions!.DefinitionKey);
            Assert.Equal(expected, saved.Preparation.Request.DefinitionKey);
            if (target == LaunchScope.ProjectNode) {
                Assert.Equal(expected, observer.Contexts.Last().DefinitionKey);
                Assert.True(observer.Contexts.Last().DriverActivationsResolved);
            }
            using var replayResponse = await host.Client.PostAsJsonAsync("/api/processes/launch/check", request);
            using var replay = await ReadAsync(replayResponse);
            Assert.Equal(saved.Preparation.Review.PlanId.Value, replay.RootElement.GetProperty("launchPlanId").GetGuid());
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HTTP_project_preparation_preserves_published_empty_drivers_and_replays_without_enrichment(bool launch) {
        var observer = new DriverObserver();
        await using var host = await ApiTestHost.CreateAsync(jwtEnabled: false, services =>
            services.AddSingleton<IProcessLaunchVariableContributor>(observer));
        var (project, node) = await CreateTargetAsync(host.App.Services);
        await PublishAsync(host.App.Services, project, "Empty drivers", content => content.Definition.LaunchDriverActivations.Clear());
        var request = new ProcessLaunchApiRequest(DefinitionKey: DefinitionKey, ProjectId: project, ProjectNodeId: node,
            RunReadiness: false, Execute: false, CallerIntentId: Guid.NewGuid());
        var route = launch ? "/api/processes/launch" : "/api/processes/launch/check";
        using var response = await host.Client.PostAsJsonAsync(route, request);
        using var body = await ReadAsync(response);
        var admission = new ProcessLaunchAdmissionId(body.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid());
        var observed = Assert.Single(observer.Contexts);
        Assert.True(observed.DriverActivationsResolved);
        Assert.Empty(observed.DriverActivations);
        Assert.Equal(project, observed.Source.ProjectId);
        Assert.Equal(node, observed.Source.SelectedItem.Id);
        await using var scope = host.App.Services.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<IProcessPreparedLaunchStore>();
        var saved = (await store.GetAsync(admission))!;
        Assert.Equal("0", saved.Preparation.Request.Variables[DriverProbe]);
        var plan = saved.Preparation.InitialCommit.InitialPlan!;
        Assert.Equal("Empty drivers", ProcessExecutableDefinitionResolver.Decode(plan.ExecutableDefinitions!, DefinitionKey).Definition.DisplayName);
        Assert.Empty(ProcessExecutableDefinitionResolver.Decode(plan.ExecutableDefinitions!, DefinitionKey).Definition.LaunchDriverActivations);
        await PublishAsync(host.App.Services, project, "Later publication", content => content.Definition.LaunchDriverActivations.Clear());
        using var replayResponse = await host.Client.PostAsJsonAsync(route, request);
        using var replay = await ReadAsync(replayResponse);
        Assert.Equal(admission.Value, replay.RootElement.GetProperty("observation").GetProperty("admissionId").GetGuid());
        Assert.Equal(plan.Header.PlanId.Value, replay.RootElement.GetProperty("launchPlanId").GetGuid());
        Assert.Single(observer.Contexts);
        Assert.Equal(saved.PreparationFingerprint, (await store.GetAsync(admission))!.PreparationFingerprint);
        using var conflict = await host.Client.PostAsJsonAsync(route, request with { RequestedBy = "changed original input" });
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Single(observer.Contexts);
    }

    private static async Task<(Guid Project, string Node)> CreateTargetAsync(IServiceProvider services) {
        await using var scope = services.CreateAsyncScope();
        var project = Guid.NewGuid();
        Assert.True((await scope.ServiceProvider.GetRequiredService<ProjectsService>()
            .CreateAsync(project, new() { Name = "PC4 executable context" })).IsSuccess);
        var node = await scope.ServiceProvider.GetRequiredService<ProjectWorkbenchService>().CreateObjectAsync(project,
            new ProjectObjectCreateRequest(ProjectObjectType.ProjectBlock, "PC4 target", "Selected context", "Original notes",
                $"project:{project}", 420, 260, ObjectSubtype: "delivery"));
        return (project, node.Id);
    }

    private static async Task PublishAsync(IServiceProvider services, Guid? project, string name, Action<ProcessAuthoringContent> change) {
        await using var scope = services.CreateAsyncScope();
        var workspace = scope.ServiceProvider.GetRequiredService<ProcessAuthoringWorkspace>();
        var target = project is { } id ? ProcessWorkspaceShellScope.ForProject(id) : ProcessWorkspaceShellScope.Global;
        var baseline = await workspace.ReadAsync(target, new(DefinitionKey), default);
        Assert.NotEmpty(workspace.ReadTemplate(DefinitionKey).Definition.LaunchDriverActivations);
        var content = ProcessAuthoringCodec.Read(ProcessAuthoringCodec.Write(baseline.Content));
        content.Definition.DisplayName = name;
        content.Definition.Steps = [new() { Key = "work", Title = "Captured work", StepKind = "Work", OperationTargetScope = "ManagedProcessArtifactsOnly" }];
        content = content with { Guidance = new Dictionary<string, IReadOnlyList<ProcessTemplateExecutionGuidanceDocument>>() };
        change(content);
        content = await scope.ServiceProvider.GetRequiredService<ProcessExecutableDefinitionResolver>().PinPublicationAsync(baseline, content, default);
        var saved = await workspace.CommitAsync(baseline, Guid.NewGuid(), ProcessAuthoringCodec.Hash(name), content,
            ProcessAuthoringLifecycle.Published, true, null, default);
        Assert.Equal(ProcessAuthoringOutcome.Accepted, saved.Outcome);
    }

    private static async Task<JsonDocument> ReadAsync(HttpResponseMessage response) {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, $"{response.StatusCode}: {body}");
        return JsonDocument.Parse(body);
    }

    private sealed class DriverObserver : IProcessLaunchVariableContributor {
        public ConcurrentQueue<ProcessLaunchPreparationContext> Contexts { get; } = new();
        public void Enrich(ProcessLaunchPreparationContext context, IDictionary<string, string> variables) {
            Contexts.Enqueue(context);
            variables[DriverProbe] = context.DriverActivations.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    public enum LaunchScope { Global, Project, ProjectNode }
}
